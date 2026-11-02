# HushStore Cloudflare Free WAF

This standalone Terraform root manages three zone entry-point rulesets for the existing authoritative Cloudflare zone. It has its own state key and Cloudflare provider; it does not consume AWS-root outputs or declare an AWS provider. Its encrypted S3 backend reuses the existing state bucket and requires AWS backend credentials for live operations. No DNS records or SSL settings are created or adopted. The provider is pinned to `cloudflare/cloudflare = 5.28.1`, the latest stable v5 release returned by the Terraform Registry on 2026-10-28. The checked-in lock file includes verified macOS ARM64 and Linux AMD64 package hashes.

## Prerequisites and behavior

The existing apex (`hushstore.io.vn`) and API (`api.hushstore.io.vn`) records must already be **proxied** through Cloudflare and point to the intended origin. Keep **Full (strict)** enabled and the origin certificate valid for both hostnames. These are deployment prerequisites, not changes performed by this root. See [Cloudflare Full (strict)](https://developers.cloudflare.com/ssl/origin-configuration/ssl-modes/full-strict/) and [Transform Rules proxy requirements](https://developers.cloudflare.com/rules/transform/).

| Phase | Behavior | Scope |
| --- | --- | --- |
| `http_request_firewall_custom` | Block environment/VCS paths and unused WordPress probes; optionally block configured IPv4/IPv6 source CIDRs | Exactly the web and API hostnames |
| `http_ratelimit` | Block at the configured login threshold (default 20 requests / 10 seconds), for 10 seconds | Login path on **every proxied hostname in the zone**, all methods |
| `http_request_late_transform` | HTTPS: set `x-hushstore-origin` to the shared secret, overwriting any client value. HTTP: remove that header | Exactly the web and API hostnames |

The two header-transform rules separate HTTP from HTTPS. Full (strict) can still forward a viewer’s HTTP request to the origin over HTTP, so the secret is injected only when `http.request.scheme eq "https"`. HTTP requests have `x-hushstore-origin` removed, including forged client values; benign HTTP traffic reaches the ALB’s existing 301 redirect without exposing the secret on the plaintext connection. The HTTP rule rewrites headers and does not block requests. These two transforms fit within the Free plan’s 10-rule quota.

The case-insensitive sensitive-path rule blocks `/.env`, `/.env.*`, `/.git`, `/.git/*`, `/.svn`, `/.svn/*`, `/wp-login.php`, and `/xmlrpc.php`. It intentionally avoids broad API/admin/path blocks. An empty CIDR list creates no IP rule; `/0` is rejected. There are no allow or skip rules.

The single rate rule matches `/api/auth/login` and `/api/auth/login/`, case-insensitively, including query-string variants because only the path is inspected. **Free supports path filtering, not Host or method filtering**, so an identically named path on another proxied hostname shares this rule. Counters use source IP plus Cloudflare data center, with no custom counting expression, regex operator, response fields, or advanced features. Rate limiting has propagation delays and per-data-center counters; application-level login protection remains necessary. See the official [Free availability matrix](https://developers.cloudflare.com/waf/rate-limiting-rules/#availability) and [rate calculation](https://developers.cloudflare.com/waf/rate-limiting-rules/request-rate/).

The **Cloudflare Free Managed Ruleset is deployed by default on Free plans**. Confirm it is active in the dashboard; this root neither owns nor imports the `http_request_firewall_managed` phase. It does not deploy the paid Cloudflare Managed Ruleset or OWASP Core Ruleset. Free coverage is a limited subset, not a full OWASP ruleset or a promise to detect every SQLi/XSS payload. See [Cloudflare WAF setup](https://developers.cloudflare.com/waf/get-started/#before-you-begin) and [managed rules availability](https://developers.cloudflare.com/waf/managed-rules/#availability).

## Local checks and review

Use Terraform 1.10 or newer (S3 native state locking). No Cloudflare token or AWS credentials are needed for these mocked plan-only checks:

```sh
terraform -chdir=infra/cloudflare init -backend=false
terraform -chdir=infra/cloudflare fmt -check -recursive
terraform -chdir=infra/cloudflare validate
terraform -chdir=infra/cloudflare test
```

The tests inspect the real Terraform configuration through a mocked provider. They verify phase/rule scopes, Free rate parameters, HTTPS-only header overwrite and HTTP removal semantics, optional dual-stack CIDRs, and invalid inputs. **They cannot establish the zone's actual entitlements, existing rule quota, API permissions, DNS/proxy state, Full (strict), or live WAF behavior.** No apply or Cloudflare API writes are part of these checks.

For a separately authorized live review, copy `terraform.tfvars.example` to ignored `terraform.tfvars` and substitute the existing zone ID. Supply `CLOUDFLARE_API_TOKEN` and `TF_VAR_cloudflare_origin_secret` from your secret manager. The origin secret is required, with 32–128 ASCII letters, digits, `_`, or `-`, and must match the AWS ALB input exactly. Do not place secrets on command lines or commit them. The provider reads the token from the environment.

Scope the token to the existing zone. The official [rate-limit Terraform guide](https://developers.cloudflare.com/terraform/additional-configurations/rate-limiting-rules/#create-a-rate-limiting-rule-at-the-zone-level) requires `Zone WAF Write`; the [header-transform guide](https://developers.cloudflare.com/terraform/additional-configurations/transform-rules/#create-a-request-header-transform-rule) lists `Zone Transform Rules Write` and `Account Rulesets Read`. No DNS Edit or SSL settings write permission is needed for this root.

For the read-only preflight, grant Zone Read, DNS Read, Zone Settings Read, and the ruleset read permissions for this zone (Zone WAF / Zone Transform Rules; Account Rulesets Read where required). In the token editor these may be labelled “Read” / “Edit” rather than API “Read” / “Write”. Do not grant DNS Edit or use a Global API Key. The tool performs GET requests only, prints no secret/rule values, and reports managed enforcement as UNKNOWN pending dashboard verification:

```sh
python3 infra/cloudflare/scripts/preflight.py --zone-id "$TF_VAR_zone_id" --check-ip-ranges
```

Before deployment, add `--expected-origin "$ALB_DNS"` with the current reviewed ALB hostname. This verifies both CNAME chains, including a DNS-only `alb` intermediate; it does not require hiding the public ALB hostname. External aliases, non-CNAME records and cycles fail closed when this check is requested.

After the transform rules are deployed in an authorized future session, add `--verify-origin-header` with `TF_VAR_cloudflare_origin_secret` already set. The GET-only check compares a digest without printing the secret, checks exact host/scheme scope and HTTP removal, and rejects later header overrides. It accepts the repository's expression form; equivalent custom expressions require review. Do not use this post-provision flag to block a first plan when the ruleset does not exist yet. These checks do not replace a runtime bypass test.

See the [Vietnamese comparison and staged deployment guide](../../docs/waf-cloudflare.md) for the edge-first, origin-second rollout and runtime checks.

After reconciling any existing phase rulesets below, `terraform -chdir=infra/cloudflare plan` provides a review without applying changes. Do not enable the AWS origin lock until the transform rule is deployed and both hostnames have been checked through Cloudflare; use the staged rollout documented in the infrastructure runbook.

## Preserve existing rules before adoption

Cloudflare allows one zone entry-point ruleset per phase, and Terraform manages its **complete rules list**. Inspect the zone's existing custom, rate-limit, and request-header-transform phases before planning a first deployment. If any exists, export its complete configuration and reconcile its existing user rules and ordering into this root, respecting Free quotas. Preserve the HTTPS origin-secret overwrite and HTTP header removal as the final relevant header transforms so a later rule cannot replace or restore the header. Import the existing phase ruleset into its corresponding Terraform address; do not delete existing rulesets or import the managed phase.

The pinned [provider import syntax](https://github.com/cloudflare/terraform-provider-cloudflare/blob/v5.28.1/docs/resources/ruleset.md#import) is `zones/ZONE_ID/RULESET_ID`. Example commands, only when adoption is separately authorized:

```sh
terraform -chdir=infra/cloudflare import cloudflare_ruleset.custom 'zones/ZONE_ID/CUSTOM_RULESET_ID'
terraform -chdir=infra/cloudflare import cloudflare_ruleset.login 'zones/ZONE_ID/RATE_LIMIT_RULESET_ID'
terraform -chdir=infra/cloudflare import cloudflare_ruleset.origin_header 'zones/ZONE_ID/HEADER_TRANSFORM_RULESET_ID'
```

Import alone does not preserve undeclared rules: **reconcile first, then inspect the plan for removals or replacements**. If the Free zone already uses its one rate rule, agree on a consolidated policy before any deployment. Cloudflare documents [ruleset ownership and import](https://developers.cloudflare.com/terraform/additional-configurations/rate-limiting-rules/#before-you-start).

## State and secret handling

`backend.tf` uses the existing encrypted/versioned `hushstore-tfstate-551897327153` bucket with the separate key `cloudflare/terraform.tfstate`, encryption enabled and native S3 locking. It creates no AWS resources and never shares the prod/dev state key. Live init needs the `hushstore` AWS profile, bucket access and access to this state object and its `.tflock` object; Cloudflare authentication still comes from `CLOUDFLARE_API_TOKEN`. Offline CI keeps `init -backend=false` and receives neither credential.

The backend has **not been initialized or migrated in this change**. If no local state exists, initialize the reviewed backend in the authorized deployment session. If an earlier local state exists, protect its backup and review `init -migrate-state` as a separate state write before planning; do not use `-reconfigure` to abandon tracked rulesets. No migration command has been executed here.

Encryption at rest does not hide values from authorized readers. The origin secret remains readable in downloaded state, saved plans, Cloudflare transforms and ALB listener conditions (including by principals allowed `elasticloadbalancing:DescribeRules`). Restrict S3/IAM/Cloudflare read permissions accordingly; do not grant the CI plan role this state key. Never commit state, saved plans, JSON plans, credentials, or populated tfvars, and keep local plan exports in a private directory (`umask 077`).

To rotate, coordinate the same value across the Cloudflare transform and AWS ALB listener conditions. For uninterrupted rotation, temporarily accept old and new values at the origin, change the Cloudflare header, verify traffic, then remove the old value. Never print either secret during checks.
