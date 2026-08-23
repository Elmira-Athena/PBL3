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

# ─── DANH TÍNH VÀ ARN DỰNG BẰNG CHUỖI ────────────────────────────
# Cùng lập luận đã dùng ở modules/cicd/main.tf, và ở module này nó còn quan
# trọng hơn: mọi assert bảo mật trong tests/costguard.tftest.hcl đọc NỘI DUNG
# của aws_iam_policy_document. Nếu một Resource trong policy trỏ tới thuộc tính
# của resource khác (aws_sns_topic.costguard.arn, aws_lambda_function.*.arn) thì
# giá trị đó là "(known after apply)", và Terraform làm CẢ document trở thành
# unknown — lúc đó không assert nào đọc được, và bộ test xanh mà không kiểm gì.
#
# ARN của SNS topic, Lambda function và log group đều tiền định: chúng chỉ phụ
# thuộc region, account id và tên ta tự đặt. Đánh đổi là mất quan hệ phụ thuộc
# ngầm, nên chỗ nào cần thứ tự tạo thì phải khai `depends_on` tường minh.
data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

locals {
  account_id = data.aws_caller_identity.current.account_id
  region     = data.aws_region.current.region

  sns_topic_name = "${var.project}-costguard-alerts"
  sns_topic_arn  = "arn:aws:sns:${local.region}:${local.account_id}:${local.sns_topic_name}"

  lambda_function_name = "${var.project}-cost-guard"
  lambda_arn           = "arn:aws:lambda:${local.region}:${local.account_id}:function:${local.lambda_function_name}"

  lambda_log_group_name = "/aws/lambda/${local.lambda_function_name}"
  lambda_log_group_arn  = "arn:aws:logs:${local.region}:${local.account_id}:log-group:${local.lambda_log_group_name}"

  # Service ARN dựng từ tên. Xem comment ở variable "service_names": service bị
  # enable_alb gate nên nó biến mất mỗi lần tắt stack, còn IAM policy phải tồn
  # tại liên tục.
  service_arns = [
    for name in var.service_names :
    "arn:aws:ecs:${local.region}:${local.account_id}:service/${var.cluster_name}/${name}"
  ]

  # ARN của ASG có dạng
  #   arn:aws:autoscaling:<region>:<account>:autoScalingGroup:<uuid>:autoScalingGroupName/<tên>
  # Đoạn <uuid> do AWS sinh lúc tạo group nên không đoán được, và nó là lý do
  # DUY NHẤT phải có dấu * ở đây. Phần định danh thật — tên group — vẫn bị so
  # khớp chính xác, nên policy không chạm được ASG nào khác.
  asg_arn = "arn:aws:autoscaling:${local.region}:${local.account_id}:autoScalingGroup:*:autoScalingGroupName/${var.asg_name}"

  rds_instance_arn = "arn:aws:rds:${local.region}:${local.account_id}:db:${var.rds_identifier}"
}

# ─── SNS TOPIC CẢNH BÁO TRẠNG THÁI HẠ TẦNG ───────────────────────
# Topic này KHÔNG được gắn vào `subscriber_sns_topic_arns` của aws_budgets_budget
# phía trên, và đó là quyết định có chủ ý: Budgets tiếp tục gửi email TRỰC TIẾP.
# Hai đường cảnh báo độc lập vì chúng nói hai chuyện khác nhau — Budgets báo về
# TIỀN ĐÃ TIÊU (dữ liệu trễ tới 8-24 giờ, ngưỡng theo phần trăm), Lambda báo về
# TRẠNG THÁI HẠ TẦNG ngay trong đêm. Gộp chúng vào một topic thì một cấu hình
# sai (policy topic, subscription chưa xác nhận, topic bị xoá tay) làm mất CẢ
# HAI, và mất im lặng.
resource "aws_sns_topic" "costguard" {
  name = local.sns_topic_name

  tags = { Name = local.sns_topic_name }
}

# ĐIỀU LÀM NGƯỜI TA TƯỞNG APPLY CHƯA XONG: một subscription `protocol = "email"`
# LUÔN nằm ở `pending_confirmation` trong state, vĩnh viễn, cho tới khi người
# nhận bấm link trong mail "AWS Notification - Subscription Confirmation". AWS
# không cho API xác nhận email thay người dùng (đó chính là mục đích của bước
# này), nên Terraform không bao giờ đọc lại được ARN subscription thật.
#
# Hệ quả cần biết: đây KHÔNG phải lỗi và KHÔNG phải drift — `terraform plan` sau
# đó vẫn báo "No changes". Nhưng nó cũng có nghĩa là Terraform KHÔNG THỂ cho biết
# email đã được xác nhận hay chưa. Cách duy nhất để biết:
#   aws sns list-subscriptions-by-topic --topic-arn <arn> --profile hushstore
# SubscriptionArn = "PendingConfirmation" nghĩa là chưa ai bấm, tức mọi cảnh báo
# của Lambda đang rơi vào hư không.
resource "aws_sns_topic_subscription" "alert_email" {
  topic_arn = aws_sns_topic.costguard.arn
  protocol  = "email"
  endpoint  = var.alert_email
}

# Topic policy khai TƯỜNG MINH thay vì để AWS áp policy mặc định. Lý do không
# phải là policy mặc định sai (nó cũng chỉ cho account chủ), mà là: policy mặc
# định không nằm trong config nên không có gì kiểm được nó, và một lần sửa qua
# console sẽ không hiện ra ở `terraform plan`.
#
# Tính chất phải giữ: KHÔNG có statement nào dùng Principal = "*". Đây là cách
# chặn cross-account publish MẠNH NHẤT vì nó không dựa vào một Condition nào cả
# — SNS mặc định từ chối, và ở đây không có Allow nào cho principal ngoài
# account để mà lách. Một statement `Principal = "*"` kèm Condition trông cũng
# an toàn, nhưng nó chỉ an toàn ĐÚNG BẰNG condition key đó: viết sai tên key,
# hay dùng một key vắng mặt với StringNotEquals, là mở topic cho cả thế giới
# publish. Xem assert trong tests/costguard.tftest.hcl.
data "aws_iam_policy_document" "sns_topic" {
  statement {
    sid    = "OnlyThisAccountMayPublishOrManage"
    effect = "Allow"

    # Principal là root của CHÍNH account này, không phải "*". Root ở đây nghĩa
    # là "mọi principal trong account này mà identity policy của nó cũng cho
    # phép" — không phải "root user". Cần đủ rộng để giữ được quyền quản lý:
    # thiếu GetTopicAttributes thì `terraform plan` không refresh được topic,
    # thiếu SetTopicAttributes thì không sửa được chính policy này nữa.
    principals {
      type        = "AWS"
      identifiers = ["arn:aws:iam::${local.account_id}:root"]
    }

    actions = [
      "SNS:Publish",
      "SNS:Subscribe",
      "SNS:GetTopicAttributes",
      "SNS:SetTopicAttributes",
      "SNS:ListSubscriptionsByTopic",
      "SNS:AddPermission",
      "SNS:RemovePermission",
      "SNS:DeleteTopic",
      "SNS:TagResource",
      "SNS:UntagResource",
    ]

    resources = [local.sns_topic_arn]
  }
}

resource "aws_sns_topic_policy" "costguard" {
  arn    = aws_sns_topic.costguard.arn
  policy = data.aws_iam_policy_document.sns_topic.json
}
