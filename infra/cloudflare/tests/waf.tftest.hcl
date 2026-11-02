# Mock plans exercise the real configuration without Cloudflare credentials or writes.
mock_provider "cloudflare" {}

variables {
  zone_id                  = "0123456789abcdef0123456789abcdef"
  cloudflare_origin_secret = "test-only-origin-secret-0123456789abcdef"
}

run "free_baseline" {
  command = plan

  assert {
    condition     = cloudflare_ruleset.custom.phase == "http_request_firewall_custom" && cloudflare_ruleset.custom.kind == "zone" && length(cloudflare_ruleset.custom.rules) == 1
    error_message = "Default custom rules must contain only the sensitive-path block; no empty IP set or allow/skip rule."
  }

  assert {
    condition     = cloudflare_ruleset.custom.rules[0].action == "block" && cloudflare_ruleset.custom.rules[0].enabled && cloudflare_ruleset.custom.rules[0].ref == "sensitive_paths" && cloudflare_ruleset.custom.rules[0].expression == "(http.host in {\"hushstore.io.vn\" \"api.hushstore.io.vn\"}) and (lower(http.request.uri.path) in {\"/.env\" \"/.git\" \"/.svn\" \"/wp-login.php\" \"/xmlrpc.php\"} or starts_with(lower(http.request.uri.path), \"/.env.\") or starts_with(lower(http.request.uri.path), \"/.git/\") or starts_with(lower(http.request.uri.path), \"/.svn/\"))"
    error_message = "Sensitive-path protection must be a tight, case-insensitive block on both application hosts."
  }

  assert {
    condition     = cloudflare_ruleset.login.phase == "http_ratelimit" && cloudflare_ruleset.login.kind == "zone" && length(cloudflare_ruleset.login.rules) == 1 && cloudflare_ruleset.login.rules[0].action == "block" && cloudflare_ruleset.login.rules[0].enabled && cloudflare_ruleset.login.rules[0].expression == "lower(http.request.uri.path) in {\"/api/auth/login\" \"/api/auth/login/\"}"
    error_message = "Free login limiting must use one path-only block rule without paid Host or method expressions."
  }

  assert {
    condition     = cloudflare_ruleset.login.rules[0].ratelimit.period == 10 && cloudflare_ruleset.login.rules[0].ratelimit.mitigation_timeout == 10 && cloudflare_ruleset.login.rules[0].ratelimit.requests_per_period == 20 && toset(cloudflare_ruleset.login.rules[0].ratelimit.characteristics) == toset(["cf.colo.id", "ip.src"]) && cloudflare_ruleset.login.rules[0].ratelimit.counting_expression == null && cloudflare_ruleset.login.rules[0].ratelimit.score_per_period == null
    error_message = "Login limiting must retain Free parameters, mandatory colo plus source-IP counters, and no advanced counting."
  }

  assert {
    condition     = cloudflare_ruleset.origin_header.phase == "http_request_late_transform" && cloudflare_ruleset.origin_header.kind == "zone" && length(cloudflare_ruleset.origin_header.rules) == 2 && cloudflare_ruleset.origin_header.rules[0].action == "rewrite" && cloudflare_ruleset.origin_header.rules[0].enabled && cloudflare_ruleset.origin_header.rules[0].expression == "(http.host in {\"hushstore.io.vn\" \"api.hushstore.io.vn\"}) and (http.request.scheme eq \"https\")"
    error_message = "The secret-set rewrite must be scoped to HTTPS on both application hosts."
  }

  assert {
    condition     = cloudflare_ruleset.origin_header.rules[0].action_parameters.headers["x-hushstore-origin"].operation == "set" && cloudflare_ruleset.origin_header.rules[0].action_parameters.headers["x-hushstore-origin"].value == var.cloudflare_origin_secret && cloudflare_ruleset.origin_header.rules[0].action_parameters.headers["x-hushstore-origin"].expression == null
    error_message = "The literal secret must overwrite client-supplied origin headers."
  }

  assert {
    condition     = cloudflare_ruleset.origin_header.rules[1].ref == "remove_http_origin_secret" && cloudflare_ruleset.origin_header.rules[1].action == "rewrite" && cloudflare_ruleset.origin_header.rules[1].enabled && cloudflare_ruleset.origin_header.rules[1].expression == "(http.host in {\"hushstore.io.vn\" \"api.hushstore.io.vn\"}) and (http.request.scheme eq \"http\")"
    error_message = "HTTP must remove the origin header on both application hosts, allowing the ALB's HTTP redirect without sending a secret."
  }

  assert {
    condition     = cloudflare_ruleset.origin_header.rules[1].action_parameters.headers["x-hushstore-origin"].operation == "remove" && cloudflare_ruleset.origin_header.rules[1].action_parameters.headers["x-hushstore-origin"].value == null && cloudflare_ruleset.origin_header.rules[1].action_parameters.headers["x-hushstore-origin"].expression == null
    error_message = "HTTP must strip forged client headers and contain no literal or computed secret value."
  }

  assert {
    condition     = length([for phase in [cloudflare_ruleset.custom.phase, cloudflare_ruleset.login.phase, cloudflare_ruleset.origin_header.phase] : phase if phase == "http_request_firewall_managed"]) == 0
    error_message = "This Free configuration must not own or override the managed-rules phase."
  }
}

run "optional_dual_stack_blocklist" {
  command = plan
  variables {
    blocked_ip_cidrs          = ["192.0.2.0/24", "2001:db8::/32"]
    login_requests_per_period = 30
    web_domain                = "shop.example.com"
    api_domain                = "api.example.com"
  }
  assert {
    condition     = length(cloudflare_ruleset.custom.rules) == 2 && cloudflare_ruleset.custom.rules[1].action == "block" && cloudflare_ruleset.custom.rules[1].enabled && cloudflare_ruleset.custom.rules[1].expression == "(http.host in {\"shop.example.com\" \"api.example.com\"}) and (ip.src in {192.0.2.0/24 2001:db8::/32})"
    error_message = "The optional IP block must accept IPv4 and IPv6 CIDRs and match only configured hosts."
  }
  assert {
    condition     = cloudflare_ruleset.login.rules[0].ratelimit.requests_per_period == 30 && cloudflare_ruleset.origin_header.rules[0].expression == "(http.host in {\"shop.example.com\" \"api.example.com\"}) and (http.request.scheme eq \"https\")" && cloudflare_ruleset.origin_header.rules[1].expression == "(http.host in {\"shop.example.com\" \"api.example.com\"}) and (http.request.scheme eq \"http\")"
    error_message = "Custom rate threshold and hostnames must propagate."
  }
}

run "reject_invalid_zone" {
  command = plan
  variables { zone_id = "not-a-zone-id" }
  expect_failures = [var.zone_id]
}
run "reject_unsafe_web_hostname" {
  command = plan
  variables { web_domain = "example.com\" or true" }
  expect_failures = [var.web_domain]
}
run "reject_url_api_hostname" {
  command = plan
  variables { api_domain = "https://api.example.com" }
  expect_failures = [var.api_domain]
}
run "reject_short_secret" {
  command = plan
  variables { cloudflare_origin_secret = "short" }
  expect_failures = [var.cloudflare_origin_secret]
}
run "reject_unsafe_secret" {
  command = plan
  variables { cloudflare_origin_secret = "0123456789abcdef0123456789abcdef\"" }
  expect_failures = [var.cloudflare_origin_secret]
}
run "reject_invalid_cidr" {
  command = plan
  variables { blocked_ip_cidrs = ["not-a-cidr"] }
  expect_failures = [var.blocked_ip_cidrs]
}
run "reject_bare_ip" {
  command = plan
  variables { blocked_ip_cidrs = ["192.0.2.1"] }
  expect_failures = [var.blocked_ip_cidrs]
}
run "reject_ipv4_default_route" {
  command = plan
  variables { blocked_ip_cidrs = ["0.0.0.0/0"] }
  expect_failures = [var.blocked_ip_cidrs]
}
run "reject_ipv6_default_route" {
  command = plan
  variables { blocked_ip_cidrs = ["::/0"] }
  expect_failures = [var.blocked_ip_cidrs]
}
run "reject_fractional_rate" {
  command = plan
  variables { login_requests_per_period = 1.5 }
  expect_failures = [var.login_requests_per_period]
}
run "reject_zero_rate" {
  command = plan
  variables { login_requests_per_period = 0 }
  expect_failures = [var.login_requests_per_period]
}
run "reject_excessive_rate" {
  command = plan
  variables { login_requests_per_period = 1001 }
  expect_failures = [var.login_requests_per_period]
}
