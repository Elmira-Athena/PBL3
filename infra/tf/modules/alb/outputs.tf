output "alb_dns_name" {
  description = "Hostname của ALB — dùng để test trước khi cắt DNS sang Cloudflare"
  value       = var.enable_alb ? aws_lb.this[0].dns_name : ""
}

output "alb_zone_id" {
  description = "Hosted zone ID của ALB"
  value       = var.enable_alb ? aws_lb.this[0].zone_id : ""
}

output "tg_web_arn" {
  description = "ARN target group của Blazor client"
  value       = var.enable_alb ? aws_lb_target_group.web[0].arn : ""
}

output "tg_api_arn" {
  description = "ARN target group của API"
  value       = var.enable_alb ? aws_lb_target_group.api[0].arn : ""
}

output "certificate_arn" {
  description = "ARN của ACM certificate"
  value       = aws_acm_certificate.this.arn
}

output "acm_validation_records" {
  description = "CNAME phải thêm TAY vào Cloudflare để ACM cấp cert — bắt buộc để chế độ DNS only (mây xám), bật proxy mây vàng thì ACM không validate được"
  # distinct(): hai tên trong cert có thể dùng chung một validation record, khi
  # đó ACM trả entry trùng — không bắt operator thêm hai lần cùng một CNAME.
  value = distinct([
    for o in aws_acm_certificate.this.domain_validation_options : {
      name  = o.resource_record_name
      type  = o.resource_record_type
      value = o.resource_record_value
    }
  ])
}
