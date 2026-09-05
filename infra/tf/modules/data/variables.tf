variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "db_subnet_ids" {
  description = "ID của 2 db subnet (isolated tier)"
  type        = list(string)
}

variable "rds_sg_id" {
  description = "ID Security Group của RDS — chỉ nhận 5432 từ sg-web"
  type        = string
}

variable "engine_version" {
  description = "Major version của PostgreSQL. Dùng prefix ('17') chứ ĐỪNG ghim minor: 17.5/17.6 hết hỗ trợ 31/10/2026, ghim là tự tạo việc — và prefix match cũng khiến auto minor upgrade không sinh drift trong plan."
  type        = string
}

variable "instance_class" {
  description = "Instance class của RDS. db.t4g.micro (Graviton) rẻ hơn t3 và PostgreSQL chạy được trên arm64 — SQL Server thì không, đó là lý do bản cũ kẹt ở t3."
  type        = string
  default     = "db.t4g.micro"
}

variable "allocated_storage" {
  description = "Dung lượng GB. 20GB là mức tối thiểu cho gp3 và nằm trong free tier"
  type        = number
  default     = 20
}

variable "db_username" {
  description = "Master username của RDS"
  type        = string
  default     = "dbadmin"
}

variable "db_name" {
  description = "Tên database ứng dụng. Từ đợt 7 nó ĐƯỢC truyền vào aws_db_instance.db_name — PostgreSQL tạo database ngay lúc create instance, khác hẳn SQL Server."
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

# ─── HAI CÔNG TẮC CỦA ĐỢT 7 ──────────────────────────────────────
variable "enable_multi_az" {
  description = "Bật Multi-AZ cho RDS. Standby KHÔNG phục vụ đọc — đây là availability, không phải read scaling."
  type        = bool
  default     = false
}

variable "enable_read_replica" {
  description = "Dựng read replica. MẶC ĐỊNH FALSE VÌ AN TOÀN: có replica thì AWS từ chối stop primary, tức cơ chế tắt tiền mất tác dụng. Chỉ bật trong cửa sổ đo, huỷ ngay sau đó."
  type        = bool
  default     = false
}

variable "replica_instance_class" {
  description = "Instance class của read replica. Để bằng primary cho số liệu đo có nghĩa."
  type        = string
  default     = "db.t4g.micro"
}
