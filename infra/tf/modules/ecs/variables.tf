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

variable "app_subnet_ids" {
  description = "ID của 2 app subnet — nơi ASG đặt container instance"
  type        = list(string)
}

variable "web_sg_id" {
  description = "ID Security Group của container instance"
  type        = string
}

variable "instance_count" {
  description = "desired_capacity của ASG. 0 = tắt hoàn toàn (xoá cả EBS root)"
  type        = number
  default     = 0

  validation {
    condition     = var.instance_count >= 0 && var.instance_count <= 1
    error_message = "instance_count chỉ được 0 hoặc 1 — max_size của ASG cố định = 1."
  }
}

variable "instance_type" {
  description = "Instance type. t3.micro nằm trong free tier nhưng chỉ 1GB RAM (đã bù bằng 2GB swap)"
  type        = string
  default     = "t3.micro"
}

variable "root_volume_size" {
  description = "Dung lượng EBS root (GB). 30GB là mức free tier"
  type        = number
  default     = 30
}

variable "log_retention_days" {
  description = "Số ngày giữ log container trong CloudWatch"
  type        = number
  default     = 3
}
