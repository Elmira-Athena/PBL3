output "managed_rules_reminder" {
  description = "Operational check; the platform-managed Free ruleset is intentionally outside this root."
  value       = "Confirm the Cloudflare Free Managed Ruleset is active for the zone. Do not replace the managed-rules phase or deploy paid managed rulesets with this Free configuration."
}

output "protected_hostnames" {
  description = "Hostnames protected by custom blocks and the origin header. The Free login rate rule covers its path throughout the zone."
  value       = [var.web_domain, var.api_domain]
}
