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

variable "ecr_api_url" {
  description = "URL repository ECR của image API"
  type        = string
}

variable "ecr_web_url" {
  description = "URL repository ECR của image web"
  type        = string
}

variable "ecr_migrator_url" {
  description = "URL repository ECR của image migrator"
  type        = string
}

variable "image_tag" {
  description = "Tag của cả 3 image — LUÔN là git SHA đầy đủ, không bao giờ dùng latest"
  type        = string

  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.image_tag))
    error_message = "image_tag phải là git SHA đầy đủ (40 ký tự hex thường) — không được là 'latest', tên nhánh, hay SHA rút gọn."
  }
}

variable "assets_bucket_name" {
  description = "Tên bucket ảnh sản phẩm — truyền vào container API qua AwsSettings__BucketName"
  type        = string
}

variable "allowed_origins" {
  description = "Origin được CORS cho phép, phân cách bằng dấu phẩy"
  type        = string
}

variable "api_memory_hard" {
  description = "Giới hạn cứng RAM (MiB) của container API"
  type        = number
  default     = 512
}

variable "api_memory_reservation" {
  description = "RAM (MiB) đặt trước cho container API. Dùng soft limit để không bị OOM-kill sớm trên t3.micro"
  type        = number
  default     = 384
}

variable "enable_alb" {
  description = "Bật serving stack. Service bị gate theo biến này vì ECS CreateService fail nếu target group chưa gắn vào load balancer"
  type        = bool
  default     = false
}

variable "tg_web_arn" {
  description = "ARN target group của Blazor client. Rỗng khi enable_alb = false — module alb trả về \"\" chứ không phải null"
  type        = string
  default     = ""
}

variable "tg_api_arn" {
  description = "ARN target group của API. Rỗng khi enable_alb = false — module alb trả về \"\" chứ không phải null"
  type        = string
  default     = ""
}

variable "service_desired_count" {
  description = "Số task mỗi service. Giữ 1 — max_size của ASG là 1 và host port là static"
  type        = number
  default     = 1

  validation {
    condition     = var.service_desired_count >= 0 && var.service_desired_count <= 1
    error_message = "service_desired_count chỉ được 0 hoặc 1 — static host port không cho phép 2 task cùng port trên 1 instance."
  }
}

variable "ssm_db_password_arn" {
  description = "ARN của SSM parameter chứa mật khẩu master của RDS. CHỈ task seeder dùng — sqlcmd không nhận connection string kiểu .NET nên phải truyền mật khẩu rời"
  type        = string
}

variable "ecr_seeder_url" {
  description = "URL repository ECR của image seeder"
  type        = string
}

variable "seeder_image_tag" {
  description = "Git SHA của image seeder. Tách riêng khỏi image_tag vì image seeder được thêm sau 3 image kia; Phase 2 sẽ build cả 4 ở cùng một SHA rồi bỏ biến này"
  type        = string

  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.seeder_image_tag))
    error_message = "seeder_image_tag phải là git SHA đầy đủ 40 ký tự hex — không dùng latest hay tag tự đặt."
  }
}

variable "rds_host" {
  description = "Hostname của RDS (không kèm port). Task seeder truyền vào sqlcmd -S"
  type        = string
}

variable "db_name" {
  description = "Tên database để seed"
  type        = string
  default     = "HushStoreDB"
}

variable "db_username" {
  description = "User đăng nhập SQL Server cho task seeder"
  type        = string
}
