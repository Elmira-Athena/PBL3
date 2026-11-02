variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string

  # AWS cap tên của cả aws_lb và aws_lb_target_group ở 32 ký tự. Hậu tố dài
  # nhất module này thêm vào là "-tg-web"/"-tg-api" (7 ký tự), nên trần thực tế
  # của project là 32 - 7 = 25.
  # Không có block này thì plan vẫn fail, nhưng bằng lỗi của provider
  # (`"name" cannot be longer than 32 characters`) — lỗi đó không nói biến nào
  # sai và cũng không nói trần là 25. Block này fail sớm hơn, đúng tên biến.
  validation {
    condition     = length(var.project) <= 25
    error_message = "Tên target group là \"${var.project}-tg-web\" mà AWS giới hạn 32 ký tự, nên project không được dài quá 25 ký tự."
  }
}

variable "vpc_id" {
  description = "ID VPC chứa target group"
  type        = string
}

variable "public_subnet_ids" {
  description = "ID của 2 public subnet — ALB bắt buộc nằm trên 2 AZ"
  type        = list(string)

  validation {
    condition     = length(var.public_subnet_ids) >= 2
    error_message = "ALB cần tối thiểu 2 subnet ở 2 AZ khác nhau, nếu không AWS từ chối tạo."
  }
}

variable "alb_sg_id" {
  description = "ID Security Group của ALB"
  type        = string
}

variable "logs_bucket" {
  description = "Tên bucket nhận ALB access log"
  type        = string
}

variable "web_domain" {
  description = "Domain của Blazor client — là domain chính của cert"
  type        = string
}

variable "api_domain" {
  description = "Domain của API — là SAN của cert, route bằng listener rule"
  type        = string
}

variable "enable_alb" {
  description = "Bật serving stack (ALB + target group + listener). $0.0225/giờ"
  type        = bool
  default     = false
}

variable "enable_cloudflare_origin_lockdown" {
  description = "Require the allowed Host and Cloudflare-injected origin secret on both HTTPS routes"
  type        = bool
  default     = false
}

variable "cloudflare_origin_secret" {
  description = "Shared x-hushstore-origin header value; supply securely, because Terraform state contains this secret"
  type        = string
  sensitive   = true
  default     = null

  validation {
    condition     = !var.enable_cloudflare_origin_lockdown || can(regex("^[A-Za-z0-9_-]{32,128}$", var.cloudflare_origin_secret))
    error_message = "cloudflare_origin_secret must contain 32-128 letters, digits, underscores or hyphens when origin lockdown is enabled."
  }
}
