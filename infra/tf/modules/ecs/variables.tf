variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "assets_bucket_arn" {
  description = "ARN bucket ảnh sản phẩm — task role của app chỉ được ghi/đọc object trong đây"
  type        = string
}

variable "artifacts_bucket_arn" {
  description = "ARN bucket artifacts — host đọc seed SQL và file ops từ đây"
  type        = string
}

variable "ssm_connection_string_arn" {
  description = "ARN parameter connection string — ECS agent inject vào container"
  type        = string
}

variable "ssm_jwt_secret_arn" {
  description = "ARN của parameter chứa JWT secret"
  type        = string
}
