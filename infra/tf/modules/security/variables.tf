variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "vpc_id" {
  description = "ID của VPC chứa các Security Group"
  type        = string
}

variable "enable_ecr_endpoints" {
  description = "Tạo rule HTTPS từ sg-web tới Security Group của ECR endpoint"
  type        = bool
  default     = false
}

variable "vpce_sg_id" {
  description = "ID Security Group của ECR endpoint; chỉ cần khi enable_ecr_endpoints = true"
  type        = string
  default     = null

  validation {
    condition     = !var.enable_ecr_endpoints || try(trimspace(var.vpce_sg_id) != "", false)
    error_message = "vpce_sg_id must be non-empty when enable_ecr_endpoints is true."
  }
}
