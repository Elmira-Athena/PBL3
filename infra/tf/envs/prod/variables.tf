variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
  default     = "hushstore"
}

variable "region" {
  description = "AWS region"
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
