# Cert KHÔNG bị enable_alb gate: ACM miễn phí, và DNS validation mất thời gian
# nên giữ cert tồn tại vĩnh viễn để bật/tắt ALB không phải validate lại.
resource "aws_acm_certificate" "this" {
  domain_name               = var.web_domain
  subject_alternative_names = [var.api_domain]
  validation_method         = "DNS"

  tags = { Name = "${var.project}-cert" }

  lifecycle {
    create_before_destroy = true
  }
}

# DNS record phải thêm TAY vào Cloudflare (DNS không do Terraform quản), và
# record phải để chế độ DNS only (mây xám) — bật proxy mây vàng thì ACM không
# đọc được record nên cert đứng mãi ở PENDING_VALIDATION.
# Resource này chờ tới khi ACM thấy record và chuyển cert sang ISSUED.
resource "aws_acm_certificate_validation" "this" {
  certificate_arn         = aws_acm_certificate.this.arn
  validation_record_fqdns = [for o in aws_acm_certificate.this.domain_validation_options : o.resource_record_name]

  timeouts {
    create = "30m"
  }
}
