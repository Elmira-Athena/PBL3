locals {
  application_hosts = "http.host in {\"${var.web_domain}\" \"${var.api_domain}\"}"
  sensitive_paths = join(" or ", [
    "lower(http.request.uri.path) in {\"/.env\" \"/.git\" \"/.svn\" \"/wp-login.php\" \"/xmlrpc.php\"}",
    "starts_with(lower(http.request.uri.path), \"/.env.\")",
    "starts_with(lower(http.request.uri.path), \"/.git/\")",
    "starts_with(lower(http.request.uri.path), \"/.svn/\")",
  ])
}

# A zone phase has one entry-point ruleset. Reconcile existing rules before
# importing/adopting this resource; rules not declared here would be removed.
resource "cloudflare_ruleset" "custom" {
  zone_id     = var.zone_id
  name        = "HushStore custom WAF"
  description = "Free-plan sensitive-path and optional source-IP protection"
  kind        = "zone"
  phase       = "http_request_firewall_custom"

  rules = concat([
    {
      ref         = "sensitive_paths"
      description = "Block exposed environment, VCS, and unused WordPress endpoints"
      action      = "block"
      expression  = "(${local.application_hosts}) and (${local.sensitive_paths})"
      enabled     = true
    }
    ], length(var.blocked_ip_cidrs) == 0 ? [] : [
    {
      ref         = "blocked_ip_cidrs"
      description = "Block configured source CIDRs on application hosts"
      action      = "block"
      expression  = "(${local.application_hosts}) and (ip.src in {${join(" ", var.blocked_ip_cidrs)}})"
      enabled     = true
    }
  ])
}

resource "cloudflare_ruleset" "login" {
  zone_id     = var.zone_id
  name        = "HushStore login rate limit"
  description = "One Free-plan rule: login path across all proxied zone hosts and methods"
  kind        = "zone"
  phase       = "http_ratelimit"

  rules = [{
    ref         = "login_rate_limit"
    description = "Rate limit login requests per source IP and Cloudflare data center"
    action      = "block"
    # Free supports path filtering only: do not add Host or method fields.
    expression = "lower(http.request.uri.path) in {\"/api/auth/login\" \"/api/auth/login/\"}"
    enabled    = true
    ratelimit = {
      characteristics     = ["cf.colo.id", "ip.src"]
      period              = 10
      requests_per_period = var.login_requests_per_period
      mitigation_timeout  = 10
    }
  }]
}

resource "cloudflare_ruleset" "origin_header" {
  zone_id     = var.zone_id
  name        = "HushStore origin authentication header"
  description = "Set the origin secret on HTTPS and strip client-provided headers on HTTP"
  kind        = "zone"
  phase       = "http_request_late_transform"

  rules = [{
    ref         = "set_origin_secret"
    description = "Set shared origin secret on HTTPS web and API requests"
    action      = "rewrite"
    expression  = "(${local.application_hosts}) and (http.request.scheme eq \"https\")"
    enabled     = true
    action_parameters = {
      headers = {
        "x-hushstore-origin" = {
          operation = "set"
          value     = var.cloudflare_origin_secret
        }
      }
    }
    }, {
    ref         = "remove_http_origin_secret"
    description = "Strip origin header on HTTP before the ALB redirects to HTTPS"
    action      = "rewrite"
    expression  = "(${local.application_hosts}) and (http.request.scheme eq \"http\")"
    enabled     = true
    action_parameters = {
      headers = {
        "x-hushstore-origin" = {
          operation = "remove"
        }
      }
    }
  }]
}
