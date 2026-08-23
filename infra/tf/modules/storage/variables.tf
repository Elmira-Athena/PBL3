variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "region" {
  description = "Vùng AWS"
  type        = string
}

variable "ecr_keep_images" {
  description = "Số image gần nhất giữ lại trong mỗi ECR repository"
  type        = number
  default     = 5
}

variable "alb_logs_retention" {
  description = "Số ngày giữ ALB access log"
  type        = number
  default     = 7
}

variable "artifacts_retention" {
  description = "Số ngày giữ file trong bucket artifacts. 365 vì migrations/migrate-<sha>.sql là bản ghi câu SQL đã chạy lên production và phải sống lâu hơn một học kỳ — xem comment ở aws_s3_bucket_lifecycle_configuration.artifacts"
  type        = number
  default     = 365
}
