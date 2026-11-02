#!/usr/bin/env python3
"""Read-only Cloudflare checks. Uses GET only; never prints API payloads or secrets.

Usage: CLOUDFLARE_API_TOKEN=<read-only-token> python3 preflight.py --zone-id <32hex>
Add --check-ip-ranges to compare the vendored IPv4 list with Cloudflare's live list.
An exit status of zero covers only the checks performed, not WAF enforcement.
"""
import argparse
import hashlib
import hmac
import ipaddress
import json
import os
from pathlib import Path
import re
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import HTTPRedirectHandler, Request, build_opener


class NoRedirect(HTTPRedirectHandler):
    """Do not forward an API bearer token to a redirect destination."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


HTTP = build_opener(NoRedirect())
API = "https://api.cloudflare.com/client/v4"
PHASES = ("http_request_firewall_custom", "http_ratelimit", "http_request_late_transform")
DEFAULT_IPV4_FILE = Path(__file__).resolve().parents[2] / "tf/modules/security/cloudflare-ipv4.txt"


class CheckFailed(Exception):
    pass


def zone_id(value):
    if not re.fullmatch(r"[0-9a-fA-F]{32}", value):
        raise argparse.ArgumentTypeError("zone ID must be 32 hexadecimal characters")
    return value.lower()


def hostname(value):
    value = value.rstrip(".").lower()
    if (len(value) > 253 or "." not in value or
            any(not re.fullmatch(r"[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?", part)
                for part in value.split("."))):
        raise argparse.ArgumentTypeError("domain must be a valid DNS hostname")
    return value


def get(url, token=None):
    headers = {"Accept": "application/json" if token else "text/plain"}
    if token:
        headers["Authorization"] = "Bearer " + token
    with HTTP.open(Request(url, headers=headers, method="GET"), timeout=20) as response:
        # Bound memory use and reject unexpectedly large provider responses.
        content = response.read(2_000_001)
    if len(content) > 2_000_000:
        raise CheckFailed("provider response exceeds the size limit")
    return content.decode("utf-8")


def api_get(path, token, params=None):
    query = "?" + urlencode(params) if params else ""
    body = json.loads(get(API + path + query, token))
    if not isinstance(body, dict) or body.get("success") is not True or "result" not in body:
        raise CheckFailed("provider returned an unsuccessful response")
    return body


def collection(path, token, params=None):
    items = []
    for page in range(1, 1001):
        body = api_get(path, token, {**(params or {}), "per_page": 100, "page": page})
        if not isinstance(body["result"], list):
            raise CheckFailed("provider returned an invalid collection")
        items.extend(body["result"])
        info = body.get("result_info", {})
        total_pages = info.get("total_pages")
        # Ruleset list responses have no pagination metadata and are complete.
        if total_pages is None or page >= int(total_pages):
            return items
    raise CheckFailed("provider pagination exceeded the page limit")


def networks(content):
    values = {ipaddress.ip_network(line.strip(), strict=True)
              for line in content.splitlines() if line.strip() and not line.lstrip().startswith("#")}
    if not values or any(network.version != 4 for network in values):
        raise CheckFailed("IPv4 range list is empty or invalid")
    return values


def check_origin(base, token, zone_name, host, records, expected):
    seen = set()
    for _ in range(32):
        if host in seen:
            raise CheckFailed("origin CNAME chain contains a cycle")
        seen.add(host)
        if len(records) != 1 or records[0].get("type") != "CNAME":
            raise CheckFailed("origin CNAME chain is missing or does not reach the expected origin")
        try:
            target = hostname(records[0].get("content", ""))
        except argparse.ArgumentTypeError:
            raise CheckFailed("origin CNAME chain has a missing or invalid target") from None
        if target == expected:
            return
        if target != zone_name and not target.endswith("." + zone_name):
            raise CheckFailed("origin CNAME chain does not reach the expected origin; external aliases are unverified")
        host = target
        records = collection(base + "/dns_records", token, {"name": host})
        records = [record for record in records if record.get("type") in ("A", "AAAA", "CNAME")
                   and record.get("name", "").rstrip(".").lower() == host]
    raise CheckFailed("origin CNAME chain exceeds the 32-link check limit")


def check_origin_header(base, token, existing, hosts, secret):
    ids = [item["id"] for item in existing
           if item.get("kind") == "zone" and item.get("phase") == "http_request_late_transform"]
    if len(ids) != 1:
        raise CheckFailed("origin header verification requires one deployed zone transform ruleset")
    ruleset = api_get(base + "/rulesets/" + ids[0], token)["result"]
    if (ruleset.get("id") != ids[0] or ruleset.get("kind") != "zone" or
            ruleset.get("phase") != "http_request_late_transform" or not isinstance(ruleset.get("rules"), list)):
        raise CheckFailed("provider returned an invalid origin header ruleset")
    # shortcut: recognize the repository's host/scheme form; extend if deployed rules need another form.
    scopes = {
        scheme: {'(http.host in {"' + web + '" "' + api + '"}) and '
                 '(http.request.scheme eq "' + scheme + '")' for web, api in (hosts, hosts[::-1])}
        for scheme in ("https", "http")
    }
    protected = {"https": False, "http": False}
    expected_digest = hashlib.sha256(secret.encode()).digest()
    for rule in ruleset["rules"]:
        if rule.get("enabled") is False:
            continue
        headers = rule.get("action_parameters", {}).get("headers", {})
        changes = [value for name, value in headers.items() if name.lower() == "x-hushstore-origin"]
        if not changes:
            continue
        expression = " ".join(rule.get("expression", "").split())
        scheme = next((scheme for scheme, expressions in scopes.items() if expression in expressions), None)
        if scheme is None or len(changes) != 1:
            # Unrecognized expressions may overlap either host/scheme; later valid rules must restore both.
            protected = {"https": False, "http": False}
            continue
        header = changes[0]
        valid = rule.get("enabled") is True and rule.get("action") == "rewrite" and header.get("expression") is None
        if scheme == "https":
            value = header.get("value")
            valid = (valid and header.get("operation") == "set" and isinstance(value, str) and
                     hmac.compare_digest(hashlib.sha256(value.encode()).digest(), expected_digest))
        else:
            valid = valid and header.get("operation") == "remove" and header.get("value") is None
        protected[scheme] = valid
    if not all(protected.values()):
        raise CheckFailed("origin header rules must end with enabled HTTPS secret set and HTTP removal "
                          "for exactly the configured hosts; secret mismatch or later overrides also fail")
    print("PASS: deployed origin header HTTPS secret, HTTP removal, exact host scope and ordering")


def check(args, token):
    base = "/zones/" + args.zone_id
    zone = api_get(base, token)["result"]
    if zone.get("status") != "active":
        raise CheckFailed("zone must be active")
    plan = zone.get("plan", {})
    if not (plan.get("legacy_id") == "free" or
            (plan.get("name") == "Free Website" and plan.get("price") == 0)):
        raise CheckFailed("zone Free plan could not be confirmed")
    name = hostname(zone.get("name", ""))
    hosts = (args.web_domain, args.api_domain)
    if hosts[0] == hosts[1] or any(host != name and not host.endswith("." + name) for host in hosts):
        raise CheckFailed("configured hosts must be distinct and belong to the selected zone")
    print("PASS: active zone, Free plan and domain membership")

    for label, host in zip(("web", "API"), hosts):
        records = collection(base + "/dns_records", token, {"name": host})
        records = [record for record in records if record.get("type") in ("A", "AAAA", "CNAME")
                   and record.get("name", "").rstrip(".").lower() == host]
        if not records or any(record.get("proxied") is not True for record in records):
            raise CheckFailed(label + " DNS record is missing or not proxied")
        print("PASS: " + label + " DNS records are proxied")
        if args.expected_origin:
            check_origin(base, token, name, host, records, args.expected_origin)
            print("PASS: " + label + " CNAME chain reaches the expected origin")

    if api_get(base + "/settings/ssl", token)["result"].get("value") != "strict":
        raise CheckFailed("SSL mode must be Full (strict)")
    print("PASS: SSL mode is Full (strict)")

    existing = collection(base + "/rulesets", token)
    for phase in PHASES:
        ids = [item.get("id", "") for item in existing
               if item.get("kind") == "zone" and item.get("phase") == phase]
        if any(not re.fullmatch(r"[0-9a-fA-F]{32}", item) for item in ids):
            raise CheckFailed("provider returned an invalid ruleset ID")
        print(phase + ": " + (", ".join(ids) if ids else "no existing entrypoint"))
    if args.verify_origin_header:
        check_origin_header(base, token, existing, hosts, os.environ["TF_VAR_cloudflare_origin_secret"])
    print("REVIEW: import and reconcile existing entrypoint rulesets before Terraform apply; "
          "each phase has one entrypoint. Existing rule contents are not displayed.")
    print("UNKNOWN: Free Managed Ruleset enforcement is not verified by these API checks. "
          "Confirm its active protection in the Cloudflare dashboard; Free normally enables it automatically.")

    if args.check_ip_ranges:
        vendored = networks(args.ipv4_file.read_text())
        live = networks(get("https://www.cloudflare.com/ips-v4/"))
        if live != vendored:
            added = sorted(str(item) for item in live - vendored)
            removed = sorted(str(item) for item in vendored - live)
            print("CIDR drift: additions=" + ",".join(added) + "; removals=" + ",".join(removed))
            raise CheckFailed("vendored Cloudflare IPv4 ranges have drifted; review before apply")
        print("PASS: vendored IPv4 ranges match the official live list")
    else:
        print("SKIP: live CIDR freshness; run again with --check-ip-ranges before deployment")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zone-id", required=True, type=zone_id)
    parser.add_argument("--web-domain", default="hushstore.io.vn", type=hostname)
    parser.add_argument("--api-domain", default="api.hushstore.io.vn", type=hostname)
    parser.add_argument("--expected-origin", type=hostname,
                        help="verify both zone CNAME chains reach this expected ALB DNS hostname")
    parser.add_argument("--verify-origin-header", action="store_true",
                        help="post-provision only: verify deployed transforms against TF_VAR_cloudflare_origin_secret")
    parser.add_argument("--check-ip-ranges", action="store_true")
    parser.add_argument("--ipv4-file", type=Path, default=DEFAULT_IPV4_FILE)
    args = parser.parse_args(argv)
    token = os.environ.get("CLOUDFLARE_API_TOKEN", "").strip()
    if not token:
        print("FAIL: set CLOUDFLARE_API_TOKEN to a read-only token for the selected zone")
        return 1
    if args.verify_origin_header and not re.fullmatch(r"[A-Za-z0-9_-]{32,128}",
                                                      os.environ.get("TF_VAR_cloudflare_origin_secret", "")):
        print("FAIL: set TF_VAR_cloudflare_origin_secret to the valid shared 32-128 character secret")
        return 1
    try:
        check(args, token)
    except CheckFailed as error:
        # Only locally constructed messages are ever emitted.
        print("FAIL: " + str(error))
        return 1
    except Exception as error:
        # Never expose provider payloads, token-bearing URLs, file contents or exception details.
        if isinstance(error, HTTPError):
            error.close()
        print("FAIL: read-only check unavailable or invalid provider response; "
              "verify connectivity, token read permissions and input files before proceeding")
        return 1
    print("PASS: performed read-only checks; review UNKNOWN and SKIP items before deployment")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
