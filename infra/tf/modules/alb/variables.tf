variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
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
