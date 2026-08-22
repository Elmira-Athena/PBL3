# AWS Budgets: 2 budget đầu tiên mỗi account là MIỄN PHÍ, từ cái thứ 3 mới tính
# $0.02/ngày. Ở đây chỉ dùng 1.
#
# Vì sao dựng bằng Terraform thay vì gọi `aws budgets create-budget`: resource tạo
# ngoài Terraform sẽ gây drift, và lần `apply` sau sẽ xử lý sai. Đây cũng là lý do
# Lambda cost-guard ở Phase 3 chỉ được gọi API stop/scale chứ không được xoá
# ALB hay NAT Gateway.
resource "aws_budgets_budget" "monthly" {
  name         = "${var.project}-monthly-spend"
  budget_type  = "COST"
  limit_amount = tostring(var.monthly_budget_usd)
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  # Ba mốc ACTUAL: bắn khi đã thực sự tiêu tới ngưỡng.
  # 25% và 50% là để phát hiện sớm việc quên tắt NAT Gateway ($0.045/giờ) hoặc
  # ALB ($0.0225/giờ) — hai khoản duy nhất trong thiết kế này không có free tier.
  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 25
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.alert_email]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 50
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.alert_email]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.alert_email]
  }

  # Mốc FORECASTED quan trọng nhất với rủi ro "quên tắt": nó bắn khi AWS DỰ BÁO
  # tháng này sẽ vượt ngưỡng, tức cảnh báo TRƯỚC khi tiền thật sự bị tiêu, chứ
  # không phải sau.
  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = [var.alert_email]
  }

  # ─── NGƯỠNG CỦA NGƯỜI DÙNG CHUNG ACCOUNT ────────────────────────────────────
  # Account này còn được một người khác dùng để làm lab học AWS, và người đó đã
  # tự thêm một ngưỡng cảnh báo qua console. Khối dynamic dưới đây tồn tại để
  # `terraform apply` KHÔNG xoá nó.
  #
  # Vì sao phải khai tường minh chứ không "cứ để yên": `scripts/up.sh` chạy
  # `terraform apply` đầy đủ mỗi lần bật hạ tầng. Terraform coi mọi notification
  # không có trong config là thứ cần xoá, nên "để yên" thật ra là "xoá ở lần bật
  # stack tới" — và người kia sẽ ngừng nhận cảnh báo chi phí mà không ai nói gì.
  # Thứ duy nhất giữ được nó qua các lần apply là có mặt trong config.
  #
  # Địa chỉ email nằm trong `terraform.tfvars` (bị .gitignore), KHÔNG nằm trong
  # code: đó là email của người khác, không phải cấu hình của dự án này.
  #
  # Đánh đổi phải biết: từ giờ Terraform SỞ HỮU ngưỡng này. Nếu người kia sửa nó
  # qua console thì lần apply sau sẽ kéo về giá trị trong tfvars. Đó vẫn tốt hơn
  # cách cũ — drift hiện ra trong `terraform plan` để có người thấy, thay vì bị
  # xoá âm thầm.
  dynamic "notification" {
    for_each = var.shared_notifications

    content {
      comparison_operator        = "GREATER_THAN"
      threshold                  = notification.value.threshold
      threshold_type             = "PERCENTAGE"
      notification_type          = notification.value.notification_type
      subscriber_email_addresses = notification.value.emails
    }
  }
}
