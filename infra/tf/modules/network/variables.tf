variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "vpc_cidr" {
  description = "CIDR của VPC"
  type        = string
}

variable "azs" {
  description = "Hai Availability Zone dùng cho stack"
  type        = list(string)
}

variable "public_subnet_cidrs" {
  description = "CIDR của 2 public subnet — chứa ALB và NAT Gateway"
  type        = list(string)
}

variable "app_subnet_cidrs" {
  description = "CIDR của 2 app subnet — chứa ECS container instance, không public IP"
  type        = list(string)
}

variable "db_subnet_cidrs" {
  description = "CIDR của 2 db subnet — chứa RDS, isolated"
  type        = list(string)
}

variable "my_ip" {
  description = "IP laptop dạng /32, dùng cho NACL deny rule demo"
  type        = string
}

variable "enable_nat" {
  description = "Tạo NAT Gateway. $0.045/giờ + $0.045/GB — chỉ bật khi cần egress (pull ECR, SSM)"
  type        = bool
  default     = false
}

variable "enable_flow_logs" {
  description = "Bật VPC Flow Logs (chỉ log REJECT) để thu bằng chứng cho báo cáo bảo mật"
  type        = bool
  default     = false
}

variable "flow_log_retention_days" {
  description = "Số ngày giữ Flow Logs trong CloudWatch"
  type        = number
  default     = 1
}

variable "enable_deny_demo" {
  description = "Bật NACL rule 50 DENY toàn bộ traffic từ my_ip — dùng cho kịch bản kiểm thử số 8"
  type        = bool
  default     = false
}
