variable "zone_id" {
  description = "Existing authoritative Cloudflare zone ID. This root never manages DNS."
  type        = string
  nullable    = false

  validation {
    condition     = can(regex("^[0-9a-fA-F]{32}$", var.zone_id))
    error_message = "zone_id must contain exactly 32 hexadecimal characters."
  }
}

variable "web_domain" {
  description = "Existing proxied web hostname, without scheme, path, port, or trailing dot."
  type        = string
  default     = "hushstore.io.vn"
  nullable    = false

  validation {
    condition     = length(var.web_domain) <= 253 && can(regex("^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z]([a-z0-9-]{0,61}[a-z0-9])?$", var.web_domain))
    error_message = "web_domain must be a lowercase DNS hostname with valid labels and no scheme, path, port, or trailing dot."
  }
}

variable "api_domain" {
  description = "Existing proxied API hostname, without scheme, path, port, or trailing dot."
  type        = string
  default     = "api.hushstore.io.vn"
  nullable    = false

  validation {
    condition     = length(var.api_domain) <= 253 && can(regex("^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z]([a-z0-9-]{0,61}[a-z0-9])?$", var.api_domain))
    error_message = "api_domain must be a lowercase DNS hostname with valid labels and no scheme, path, port, or trailing dot."
  }
}

variable "cloudflare_origin_secret" {
  description = "Required shared origin secret: supply the identical value to the AWS ALB root. Stored in Cloudflare and Terraform state."
  type        = string
  sensitive   = true
  nullable    = false

  validation {
    condition     = can(regex("^[A-Za-z0-9_-]{32,128}$", var.cloudflare_origin_secret))
    error_message = "cloudflare_origin_secret must contain 32–128 ASCII letters, digits, underscores, or hyphens."
  }
}

variable "blocked_ip_cidrs" {
  description = "Optional source IPv4/IPv6 CIDRs blocked only on the web and API hosts; default routes are forbidden."
  type        = list(string)
  default     = []
  nullable    = false

  validation {
    condition = alltrue([
      for cidr in var.blocked_ip_cidrs :
      can(cidrhost(cidr, 0)) && try(tonumber(split("/", cidr)[1]) > 0, false)
    ])
    error_message = "Every blocked_ip_cidrs entry must be a valid IPv4 or IPv6 CIDR with a prefix greater than zero."
  }
}

variable "login_requests_per_period" {
  description = "Maximum login-path requests per source IP and Cloudflare data center in a 10-second window (all methods and zone hosts)."
  type        = number
  default     = 20
  nullable    = false

  validation {
    condition     = var.login_requests_per_period == floor(var.login_requests_per_period) && var.login_requests_per_period >= 1 && var.login_requests_per_period <= 1000
    error_message = "login_requests_per_period must be an integer between 1 and 1000."
  }
}
