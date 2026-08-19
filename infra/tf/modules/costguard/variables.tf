variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "alert_email" {
  description = "Email nhận cảnh báo chi phí. AWS gửi mail xác nhận đăng ký, phải bấm xác nhận mới nhận được"
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.alert_email))
    error_message = "alert_email phải là một địa chỉ email hợp lệ."
  }
}

variable "monthly_budget_usd" {
  description = "Ngưỡng ngân sách tháng (USD). Cảnh báo bắn ở 25%, 50% và 100% của mức này"
  type        = number
  default     = 20
}
