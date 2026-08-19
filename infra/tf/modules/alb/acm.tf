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
#
# BỊ GATE theo enable_alb (khác với cert ở trên), vì đây là resource ẢO: nó
# không tạo gì trên AWS — Create là vòng poll DescribeCertificate, Delete là
# no-op. Nếu không gate, nó nằm trong graph của MỌI lần apply ở envs/prod (kể
# cả apply chỉ đổi instance_count), và khi CNAME chưa được thêm tay vào
# Cloudflare thì nó treo đúng 30 phút rồi fail — kỷ luật "chạy đúng thứ tự"
# không bảo vệ được một root state dùng chung về lâu dài.
#
# KHÔNG PHẢI SỢ: cert hiện đã ISSUED, nên khi enable_alb = false thì plan sẽ
# hiện resource này bị destroy. Đó là destroy một resource ảo — không gọi AWS,
# KHÔNG revoke và KHÔNG làm cert phải validate lại. Bật enable_alb = true lần
# sau, nó chỉ gọi DescribeCertificate một lần rồi trả về ngay vì cert đã ISSUED.
resource "aws_acm_certificate_validation" "this" {
  count = var.enable_alb ? 1 : 0

  certificate_arn = aws_acm_certificate.this.arn

  # distinct(): ACM trả một entry domain_validation_options cho mỗi tên trong
  # domain_name + SAN, và hai tên có thể dùng CHUNG một validation record (VD
  # thêm SAN wildcard trùng record với apex, hoặc lỡ liệt kê trùng tên). Truyền
  # list có phần tử trùng vào waiter dẫn tới chờ vô ích rồi timeout 30 phút.
  validation_record_fqdns = distinct([for o in aws_acm_certificate.this.domain_validation_options : o.resource_record_name])

  timeouts {
    create = "30m"
  }
}
