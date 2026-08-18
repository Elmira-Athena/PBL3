variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "db_subnet_ids" {
  description = "ID của 2 db subnet (isolated tier)"
  type        = list(string)
}

variable "rds_sg_id" {
  description = "ID Security Group của RDS — chỉ nhận 1433 từ sg-web"
  type        = string
}

variable "engine_version" {
  description = "Version của sqlserver-ex. Lấy bằng: aws rds describe-db-engine-versions --engine sqlserver-ex"
  type        = string
}

variable "instance_class" {
  description = "Instance class của RDS. db.t3.micro nằm trong free tier 750h/tháng"
  type        = string
  default     = "db.t3.micro"
}

variable "allocated_storage" {
  description = "Dung lượng GB. 20GB là mức tối thiểu cho SQL Server và nằm trong free tier"
  type        = number
  default     = 20
}

variable "db_username" {
  description = "Master username của RDS"
  type        = string
  default     = "dbadmin"
}

variable "db_name" {
  description = "Tên database ứng dụng. KHÔNG truyền vào aws_db_instance (SQL Server không hỗ trợ) — chỉ dùng để dựng connection string"
  type        = string
  default     = "HushStoreDB"
}

variable "backup_retention_days" {
  description = "Số ngày giữ backup tự động. Miễn phí tới mức bằng allocated_storage"
  type        = number
  default     = 7
}

variable "skip_final_snapshot" {
  description = "Bỏ qua final snapshot khi destroy. true để nuke.sh chạy nhanh"
  type        = bool
  default     = true
}
