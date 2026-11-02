"""Offline behavioral tests; all HTTP requests use fixtures."""
import contextlib
import copy
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError, URLError
from urllib.parse import parse_qs, urlsplit


SCRIPT = Path(__file__).with_name("preflight.py")
ZONE = "a" * 32
RULESET = "b" * 32
HEADER_RULESET = "c" * 32
ORIGIN = "hushstore-123.ap-southeast-1.elb.amazonaws.com"
SECRET = "do-not-display-this-token-or-header-value"


class PreflightTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location("preflight", SCRIPT)
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        self.zone = {"id": ZONE, "name": "hushstore.io.vn", "status": "active",
                     "plan": {"legacy_id": "free", "name": "Free Website", "price": 0}}
        self.dns = {
            "hushstore.io.vn": [{"type": "CNAME", "name": "hushstore.io.vn", "proxied": True,
                                 "content": "alb.hushstore.io.vn"}],
            "api.hushstore.io.vn": [{"type": "CNAME", "name": "api.hushstore.io.vn", "proxied": True,
                                     "content": "alb.hushstore.io.vn"}],
            "alb.hushstore.io.vn": [{"type": "CNAME", "name": "alb.hushstore.io.vn", "proxied": False,
                                     "content": ORIGIN}],
        }
        self.rulesets = [{"id": RULESET, "kind": "zone", "phase": "http_request_firewall_custom",
                          "name": SECRET, "rules": [{"action_parameters": {"value": SECRET}}]}]
        self.header_ruleset = {"id": HEADER_RULESET, "kind": "zone",
                               "phase": "http_request_late_transform", "rules": [
            {"ref": "set_origin_secret", "enabled": True, "action": "rewrite",
             "expression": '(http.host in {"hushstore.io.vn" "api.hushstore.io.vn"}) and '
                           '(http.request.scheme eq "https")',
             "action_parameters": {"headers": {"x-hushstore-origin": {"operation": "set", "value": SECRET}}}},
            {"ref": "remove_http_origin_secret", "enabled": True, "action": "rewrite",
             "expression": '(http.host in {"hushstore.io.vn" "api.hushstore.io.vn"}) and '
                           '(http.request.scheme eq "http")',
             "action_parameters": {"headers": {"x-hushstore-origin": {"operation": "remove"}}}},
        ]}
        self.ssl = "strict"
        self.pages = False
        self.failure = None
        self.range_failure = None
        self.requests = []
        self.ranges = "173.245.48.0/20\n103.21.244.0/22\n"

    def open(self, request, timeout):
        # A non-GET call must never reach any remote service.
        self.assertEqual(request.get_method(), "GET")
        self.assertIsNone(request.data)
        self.requests.append(request)
        if self.failure:
            raise self.failure
        url = urlsplit(request.full_url)
        if url.hostname == "www.cloudflare.com":
            self.assertEqual(url.path, "/ips-v4/")
            self.assertIsNone(request.get_header("Authorization"))
            if self.range_failure:
                raise self.range_failure
            payload = self.ranges.encode()
        else:
            self.assertEqual(request.get_header("Authorization"), "Bearer " + SECRET)
            query = parse_qs(url.query)
            info = None
            if url.path.endswith("/dns_records"):
                name = query["name"][0]
                page = int(query["page"][0])
                records = copy.deepcopy(self.dns.get(name, []))
                if self.pages and page == 1:
                    records = []
                result = records
                info = {"page": page, "per_page": 100, "total_pages": 2 if self.pages else 1}
            elif url.path.endswith("/settings/ssl"):
                result = {"id": "ssl", "value": self.ssl}
            elif url.path.endswith("/rulesets"):
                result = self.rulesets
            elif url.path.endswith("/rulesets/" + HEADER_RULESET):
                result = self.header_ruleset
            else:
                result = self.zone
            envelope = {"success": True, "errors": [], "messages": [], "result": result}
            if info:
                envelope["result_info"] = info
            payload = json.dumps(envelope).encode()
        return contextlib.closing(io.BytesIO(payload))

    def run_cli(self, extra=(), token=SECRET, origin_secret=None):
        output = io.StringIO()
        environment = {"CLOUDFLARE_API_TOKEN": token}
        if origin_secret is not None:
            environment["TF_VAR_cloudflare_origin_secret"] = origin_secret
        with patch.object(self.module.HTTP, "open", side_effect=self.open), \
             patch.dict("os.environ", environment, clear=True), \
             contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
            try:
                code = self.module.main(["--zone-id", ZONE, *extra])
            except SystemExit as error:
                code = error.code
        self.assertNotIn(SECRET, output.getvalue())
        return code, output.getvalue()

    def test_valid_configuration_reports_unknown_managed_rules_and_safe_import_guidance(self):
        code, output = self.run_cli()
        self.assertEqual(code, 0, output)
        self.assertIn(RULESET, output)
        self.assertIn("import", output.lower())
        self.assertIn("UNKNOWN", output)
        self.assertIn("dashboard", output)

    def test_zone_status_plan_and_domain_mismatch_fail(self):
        for field, value in [("status", "pending"), ("name", "unrelated.net"),
                             ("plan", {"legacy_id": "pro", "name": "Pro Website", "price": 20})]:
            with self.subTest(field=field):
                original = self.zone[field]
                self.zone[field] = value
                code, _ = self.run_cli()
                self.assertEqual(code, 1)
                self.zone[field] = original

    def test_missing_or_unproxied_dns_records_fail(self):
        for records in [[], [{"type": "CNAME", "name": "api.hushstore.io.vn", "proxied": False}]]:
            with self.subTest(records=records):
                self.dns["api.hushstore.io.vn"] = records
                code, _ = self.run_cli()
                self.assertEqual(code, 1)

    def test_dns_pagination_checks_later_pages(self):
        self.pages = True
        code, output = self.run_cli()
        self.assertEqual(code, 0, output)
        self.assertTrue(any("page=2" in item.full_url for item in self.requests))

    def test_non_strict_tls_fails(self):
        self.ssl = "full"
        self.assertEqual(self.run_cli()[0], 1)

    def test_provider_errors_and_network_exceptions_are_redacted(self):
        for error in [HTTPError("https://api.cloudflare.com/" + SECRET, 403, SECRET, {},
                                io.BytesIO(SECRET.encode())), URLError(SECRET)]:
            with self.subTest(error=type(error).__name__):
                self.failure = error
                code, output = self.run_cli()
                self.assertEqual(code, 1)
                self.assertIn("FAIL", output)

    def test_provider_error_envelope_and_malformed_json_are_redacted(self):
        for payload in [{"success": False, "errors": [{"message": SECRET}]}, SECRET]:
            with self.subTest(payload=type(payload).__name__):
                data = payload if isinstance(payload, str) else json.dumps(payload)
                with patch.object(self, "open", return_value=contextlib.closing(io.BytesIO(data.encode()))):
                    self.assertEqual(self.run_cli()[0], 1)

    def test_optional_range_check_detects_drift_without_leaking_token(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "ranges.txt"
            path.write_text(self.ranges)
            args = ["--check-ip-ranges", "--ipv4-file", str(path)]
            code, output = self.run_cli(args)
            self.assertEqual(code, 0, output)
            path.write_text("173.245.48.0/20\n")
            code, output = self.run_cli(args)
            self.assertEqual(code, 1)
            self.assertIn("drift", output.lower())

    def test_invalid_zone_and_missing_token_do_not_make_requests(self):
        code, _ = self.run_cli(token="")
        self.assertEqual(code, 1)
        self.assertEqual(self.requests, [])
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            self.module.main(["--zone-id", "invalid"])
        self.assertEqual(self.requests, [])

    def test_range_check_fails_closed_when_official_list_is_unavailable(self):
        self.range_failure = URLError(SECRET)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "ranges.txt"
            path.write_text(self.ranges)
            code, output = self.run_cli(["--check-ip-ranges", "--ipv4-file", str(path)])
            self.assertEqual(code, 1)
            self.assertIn("FAIL", output)

    def test_response_rules_and_invalid_identifiers_are_never_echoed(self):
        body = {"success": True, "result": [{"id": SECRET, "kind": "zone",
                 "phase": "http_request_firewall_custom", "rules": [{"value": SECRET}]}]}
        original = self.open

        def fixture(request, timeout):
            if urlsplit(request.full_url).path.endswith("/rulesets"):
                return contextlib.closing(io.BytesIO(json.dumps(body).encode()))
            return original(request, timeout)

        with patch.object(self, "open", side_effect=fixture):
            self.assertEqual(self.run_cli()[0], 1)

    def test_expected_origin_accepts_dns_only_intermediate_and_normalized_targets(self):
        self.dns["alb.hushstore.io.vn"][0]["content"] = ORIGIN.upper() + "."
        code, output = self.run_cli(["--expected-origin", ORIGIN])
        self.assertEqual(code, 0, output)
        self.assertIn("expected origin", output.lower())

    def test_expected_origin_rejects_wrong_missing_and_non_cname_targets(self):
        for record in [None, {"type": "CNAME", "content": "wrong.example.com"},
                       {"type": "CNAME"}, {"type": "A", "content": "192.0.2.1"}]:
            with self.subTest(record=record):
                self.dns["alb.hushstore.io.vn"] = [] if record is None else [
                    {"name": "alb.hushstore.io.vn", "proxied": False, **record}]
                code, output = self.run_cli(["--expected-origin", ORIGIN])
                self.assertEqual(code, 1, output)
                self.assertIn("origin", output.lower())

    def test_expected_origin_checks_api_chain_as_well_as_web_chain(self):
        self.dns["api.hushstore.io.vn"][0]["content"] = "wrong.example.com"
        self.assertEqual(self.run_cli(["--expected-origin", ORIGIN])[0], 1)

    def test_expected_origin_rejects_cname_cycles(self):
        self.dns["alb.hushstore.io.vn"][0]["content"] = "hushstore.io.vn"
        code, output = self.run_cli(["--expected-origin", ORIGIN])
        self.assertEqual(code, 1)
        self.assertIn("cycle", output.lower())

    def test_invalid_expected_origin_does_not_make_requests_or_echo_input(self):
        output = io.StringIO()
        with patch.object(self.module.HTTP, "open", side_effect=self.open), \
             contextlib.redirect_stderr(output), self.assertRaises(SystemExit):
            self.module.main(["--zone-id", ZONE, "--expected-origin", "https://" + SECRET])
        self.assertNotIn(SECRET, output.getvalue())
        self.assertEqual(self.requests, [])

    def enable_header_ruleset(self):
        self.rulesets.append({"id": HEADER_RULESET, "kind": "zone", "phase": "http_request_late_transform"})

    def test_verify_origin_header_requires_deployed_ruleset_but_default_allows_absence(self):
        self.assertEqual(self.run_cli()[0], 0)
        code, output = self.run_cli(["--verify-origin-header"], origin_secret=SECRET)
        self.assertEqual(code, 1)
        self.assertIn("origin", output.lower())
        self.assertIn("ruleset", output.lower())

    def test_verify_origin_header_accepts_live_https_set_and_http_remove(self):
        self.enable_header_ruleset()
        code, output = self.run_cli(["--verify-origin-header"], origin_secret=SECRET)
        self.assertEqual(code, 0, output)
        self.assertIn("origin header", output.lower())
        self.assertTrue(any(item.full_url.endswith("/rulesets/" + HEADER_RULESET) for item in self.requests))

    def test_verify_origin_header_requires_environment_secret_before_requests(self):
        for secret in [None, "", "too-short", SECRET + "\n"]:
            with self.subTest(secret=secret):
                code, output = self.run_cli(["--verify-origin-header"], origin_secret=secret)
                self.assertEqual(code, 1, output)
                self.assertIn("TF_VAR_cloudflare_origin_secret", output)
                self.assertEqual(self.requests, [])

    def test_verify_origin_header_rejects_mismatched_or_dynamic_secret_without_leaking(self):
        self.enable_header_ruleset()
        header = self.header_ruleset["rules"][0]["action_parameters"]["headers"]["x-hushstore-origin"]
        for change in [{"value": "different-deployed-value"}, {"expression": '"' + SECRET + '"'}]:
            with self.subTest(change=next(iter(change))):
                original = copy.deepcopy(header)
                header.update(change)
                code, output = self.run_cli(["--verify-origin-header"], origin_secret=SECRET)
                self.assertEqual(code, 1, output)
                self.assertNotIn("different-deployed-value", output)
                header.clear()
                header.update(original)

    def test_verify_origin_header_rejects_missing_disabled_or_wrong_scope_rules(self):
        self.enable_header_ruleset()
        original = copy.deepcopy(self.header_ruleset["rules"])
        for index in (0, 1):
            for change in ("missing", "disabled", "wrong-host", "extra-host", "extra-condition"):
                with self.subTest(index=index, change=change):
                    self.header_ruleset["rules"] = copy.deepcopy(original)
                    rule = self.header_ruleset["rules"][index]
                    if change == "missing":
                        del self.header_ruleset["rules"][index]
                    elif change == "disabled":
                        rule["enabled"] = False
                    elif change == "wrong-host":
                        rule["expression"] = rule["expression"].replace("api.hushstore.io.vn", "other.hushstore.io.vn")
                    elif change == "extra-host":
                        rule["expression"] = rule["expression"].replace("}", ' "other.hushstore.io.vn"}')
                    else:
                        rule["expression"] += ' and (http.request.uri.path eq "/only")'
                    self.assertEqual(self.run_cli(["--verify-origin-header"], origin_secret=SECRET)[0], 1)

    def test_verify_origin_header_rejects_secret_header_on_http(self):
        self.enable_header_ruleset()
        self.header_ruleset["rules"][1]["action_parameters"]["headers"]["x-hushstore-origin"] = {
            "operation": "set", "value": SECRET}
        self.assertEqual(self.run_cli(["--verify-origin-header"], origin_secret=SECRET)[0], 1)

    def test_verify_origin_header_rejects_later_overrides_but_accepts_earlier_overwrites(self):
        self.enable_header_ruleset()
        override = {"enabled": True, "action": "rewrite", "expression": "true",
                    "action_parameters": {"headers": {"X-HushStore-Origin": {"operation": "set", "value": SECRET}}}}
        self.header_ruleset["rules"].append(override)
        self.assertEqual(self.run_cli(["--verify-origin-header"], origin_secret=SECRET)[0], 1)
        self.header_ruleset["rules"].remove(override)
        self.header_ruleset["rules"].insert(0, override)
        code, output = self.run_cli(["--verify-origin-header"], origin_secret=SECRET)
        self.assertEqual(code, 0, output)

    def test_verify_origin_header_ignores_disabled_or_unrelated_header_overrides(self):
        self.enable_header_ruleset()
        self.header_ruleset["rules"].extend([
            {"enabled": False, "action": "rewrite", "expression": "true",
             "action_parameters": {"headers": {"x-hushstore-origin": {"operation": "set", "value": SECRET}}}},
            {"enabled": True, "action": "rewrite", "expression": "true",
             "action_parameters": {"headers": {"x-other": {"operation": "set", "value": "unrelated"}}}},
        ])
        code, output = self.run_cli(["--verify-origin-header"], origin_secret=SECRET)
        self.assertEqual(code, 0, output)


if __name__ == "__main__":
    unittest.main()
