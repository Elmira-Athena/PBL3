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
}
