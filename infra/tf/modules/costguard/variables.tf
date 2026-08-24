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

variable "enable_budget" {
  description = "Tạo AWS Budget cho dự án. Tắt khi 2 slot budget miễn phí của account đã bị dùng hết — cái thứ 3 tốn $0.02/ngày. Tắt = mất lớp backstop, chỉ còn Lambda cost guard"
  type        = bool
  default     = true
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

# ─── PHASE 3: LAMBDA COST GUARD ──────────────────────────────────
# Bốn biến dưới đây nhận TÊN resource, không nhận ARN, và cả bốn đi thẳng vào
# Resource của IAM policy. Vì thế mỗi biến có một validation ALLOWLIST
# `^[A-Za-z0-9_-]+$`, và cách viết đó có lý do:
#
#   • `*` là ca đã biết: một dấu * lọt vào đây không làm `apply` lỗi, không làm
#     `plan` khác đi, nó chỉ âm thầm nới quyền của Lambda ra mọi resource cùng
#     loại trong account.
#   • `:` và `/` là ca ngược lại và cũng im lặng y như vậy: truyền một ARN đầy
#     đủ vào chỗ mong đợi TÊN sẽ cho một ARN méo (arn:aws:rds:...:db:arn:aws:
#     rds:...) khớp không resource nào cả. Lambda mất quyền, `apply` xanh, và
#     triệu chứng duy nhất là một AccessDenied lúc 0 giờ sáng.
#   • Khoảng trắng cùng lớp với `:` và `/`, chỉ khó thấy hơn khi đọc diff.
#
# Allowlist chặn cả ba nhóm cùng lúc thay vì đuổi theo từng ký tự. Nó KHÔNG hẹp
# hơn thực tế: tên ECS cluster/service, tên ASG và DB identifier của AWS đều chỉ
# nhận chữ, số, gạch ngang và gạch dưới.

variable "cluster_name" {
  description = "Tên ECS cluster. Dùng để dựng ARN service (dạng .../service/<cluster>/<service>) — Lambda chỉ UpdateService được trong đúng cluster này"
  type        = string

  validation {
    condition     = can(regex("^[A-Za-z0-9_-]+$", var.cluster_name))
    error_message = "cluster_name chỉ được chứa chữ, số, `-` và `_`, và phải khác rỗng — giá trị này đi thẳng vào Resource của IAM policy. Truyền TÊN cluster, đừng truyền ARN: một ARN ở đây tạo ra một ARN méo không khớp resource nào và Lambda mất quyền mà apply vẫn xanh."
  }
}

variable "asg_name" {
  description = "Tên Auto Scaling Group mà Lambda được hạ desired về 0. Đúng một group"
  type        = string

  validation {
    condition     = can(regex("^[A-Za-z0-9_-]+$", var.asg_name))
    error_message = "asg_name chỉ được chứa chữ, số, `-` và `_`, và phải khác rỗng. Một dấu * ở đây cho Lambda hạ capacity của MỌI ASG trong account; một dấu `:` hay `/` (dấu hiệu của một ARN bị truyền vào chỗ mong đợi tên) thì ngược lại — Lambda mất quyền và chỉ lộ ra ở lần chạy đêm."
  }
}

variable "rds_identifier" {
  description = "DB instance identifier mà Lambda được StopDBInstance. Lambda KHÔNG có quyền start lại — xem lambda.tf"
  type        = string

  validation {
    condition     = can(regex("^[A-Za-z0-9_-]+$", var.rds_identifier))
    error_message = "rds_identifier chỉ được chứa chữ, số, `-` và `_`, và phải khác rỗng. Một dấu * ở đây cho Lambda stop MỌI database trong account; một ARN đầy đủ (có `:`) thì làm Lambda không stop được database nào và không có gì đỏ để báo."
  }
}

variable "service_names" {
  description = "Tên các ECS service được phép UpdateService về 0. Truyền TÊN chứ không truyền ARN của resource: service chỉ tồn tại khi enable_alb = true, nên tham chiếu aws_ecs_service[0].arn sẽ làm policy đổi nội dung mỗi lần bật/tắt stack"
  type        = list(string)

  validation {
    condition     = length(var.service_names) > 0
    error_message = "Phải truyền ít nhất một tên service — một cost guard không tắt được service nào thì không hạ được ASG an toàn."
  }

  validation {
    condition = alltrue([
      for name in var.service_names : can(regex("^[A-Za-z0-9_-]+$", name))
    ])
    error_message = "Tên service chỉ được chứa chữ, số, `-` và `_`, và phải khác rỗng — giá trị này nối vào Resource của IAM policy. Một dấu * cho Lambda tắt mọi service trong cluster; một `/` hay `:` (dấu hiệu của một ARN service bị truyền vào chỗ mong đợi tên) làm ARN méo và Lambda không tắt được service nào."
  }
}

variable "enable_auto_stop" {
  description = "Tạo EventBridge Scheduler chạy Lambda mỗi đêm. Đặt false khi CỐ TÌNH để stack chạy qua đêm — lúc đó KHÔNG còn lưới an toàn nào và rủi ro RDS tự khởi động lại sau 7 ngày quay về nguyên trạng. Lambda và IAM role vẫn tồn tại (đều $0), chỉ mất cái đồng hồ"
  type        = bool
  default     = true
}

variable "stop_cron" {
  description = "Biểu thức cron của EventBridge Scheduler, tính theo giờ Việt Nam (Asia/Ho_Chi_Minh được hardcode trong schedule.tf). Cú pháp 6 trường của Scheduler: phút giờ ngày tháng thứ năm"
  type        = string
  default     = "cron(0 0 * * ? *)"

  validation {
    condition     = can(regex("^cron\\(.+\\)$", var.stop_cron))
    error_message = "stop_cron phải là biểu thức cron(...). KHÔNG dùng rate(...): rate đếm từ lúc schedule được tạo nên mỗi lần apply lại đẩy giờ chạy đi một chỗ khác, và không ai biết đêm nay Lambda chạy lúc mấy giờ."
  }
}
