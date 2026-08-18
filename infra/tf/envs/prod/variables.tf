variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
  default     = "hushstore"
}

variable "region" {
  description = "Vùng AWS"
  type        = string
  default     = "ap-southeast-1"
}

variable "profile" {
  description = "AWS CLI profile dùng để authenticate"
  type        = string
  default     = "hushstore"
}

variable "azs" {
  description = "Hai Availability Zone dùng cho toàn bộ stack"
  type        = list(string)
  default     = ["ap-southeast-1a", "ap-southeast-1b"]

  validation {
    condition     = length(var.azs) == 2
    error_message = "Phải khai báo đúng 2 AZ — ALB cần tối thiểu 2 subnet ở 2 AZ khác nhau."
  }
}

variable "my_ip" {
  description = "IP công cộng của máy tấn công (laptop), dạng CIDR /32. Lấy bằng: curl -s https://checkip.amazonaws.com"
  type        = string

  validation {
    condition     = can(cidrhost(var.my_ip, 0)) && endswith(var.my_ip, "/32")
    error_message = "my_ip phải là CIDR /32, ví dụ 203.0.113.45/32."
  }
}

variable "vpc_cidr" {
  description = "CIDR của VPC — 10.20.0.0/16, KHÔNG trùng 10.0.0.0/16 của stack cũ"
  type        = string
  default     = "10.20.0.0/16"
}

variable "public_subnet_cidrs" {
  description = "CIDR của 2 public subnet"
  type        = list(string)
  default     = ["10.20.0.0/24", "10.20.1.0/24"]
}

variable "app_subnet_cidrs" {
  description = "CIDR của 2 app subnet"
  type        = list(string)
  default     = ["10.20.10.0/24", "10.20.11.0/24"]
}

variable "db_subnet_cidrs" {
  description = "CIDR của 2 db subnet"
  type        = list(string)
  default     = ["10.20.20.0/24", "10.20.21.0/24"]
}

variable "enable_nat" {
  description = "Tạo NAT Gateway — $0.045/giờ. Chỉ bật khi cần pull ECR hoặc dùng SSM"
  type        = bool
  default     = false
}

variable "enable_flow_logs" {
  description = "Bật VPC Flow Logs (chỉ REJECT) cho báo cáo bảo mật"
  type        = bool
  default     = false
}

variable "enable_deny_demo" {
  description = "Bật NACL rule 50 DENY my_ip — kịch bản kiểm thử số 8"
  type        = bool
  default     = false
}
