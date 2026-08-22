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

variable "shared_notifications" {
  description = "Các ngưỡng cảnh báo KHÔNG thuộc dự án này, do người khác dùng chung account tự thêm. Khai ở đây để terraform apply không xoá chúng — xem comment trong main.tf. Để rỗng nếu account chỉ có mình dự án này dùng"

  type = list(object({
    threshold         = number
    notification_type = string
    emails            = list(string)
  }))

  default = []

  validation {
    condition = alltrue([
      for n in var.shared_notifications :
      contains(["ACTUAL", "FORECASTED"], n.notification_type)
    ])
    error_message = "notification_type chỉ nhận ACTUAL hoặc FORECASTED."
  }

  validation {
    condition = alltrue([
      for n in var.shared_notifications :
      n.threshold > 0 && n.threshold <= 1000
    ])
    error_message = "threshold là phần trăm của ngân sách, phải trong khoảng (0, 1000]."
  }

  validation {
    condition = alltrue([
      for n in var.shared_notifications : length(n.emails) > 0
    ])
    error_message = "Mỗi ngưỡng phải có ít nhất một email — một notification không có subscriber là vô nghĩa và AWS sẽ từ chối."
  }
}
