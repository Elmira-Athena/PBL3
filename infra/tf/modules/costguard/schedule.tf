# ─── VÌ SAO EventBridge Scheduler, KHÔNG PHẢI EventBridge Rule ────
# aws_scheduler_schedule nhận `schedule_expression_timezone` trực tiếp, nên
# "0:00 giờ Việt Nam" viết đúng là 0:00. aws_cloudwatch_event_rule chỉ hiểu UTC,
# nên cùng ý định đó phải viết `cron(0 17 * * ? *)` — và con số 17 đó sai vào
# đúng ngày có người đọc lại mà không biết nó đã bị trừ 7 giờ. Cách viết nào
# cũng chạy được; cách viết này không đánh bẫy người bảo trì.
#
# Chi phí: cả hai đều $0 ở quy mô này (Scheduler miễn phí 14 triệu lượt invoke
# mỗi tháng; ở đây là 30).

locals {
  schedule_name = "${var.project}-nightly-stop"
  schedule_arn  = "arn:aws:scheduler:${local.region}:${local.account_id}:schedule/default/${local.schedule_name}"
}

# ─── ROLE RIÊNG CỦA SCHEDULER ────────────────────────────────────
# Đây KHÔNG phải role của Lambda. Trộn hai cái lại nghĩa là thêm
# lambda:InvokeFunction vào role của Lambda, tức cấp cho Lambda quyền tự gọi
# chính nó — một vòng đệ quy mà chỉ giới hạn concurrency của account mới chặn
# được, và concurrency của account này là 10.
#
# Role KHÔNG bị gate theo enable_auto_stop: IAM role và policy đều $0, nên giữ
# nó tồn tại liên tục tránh việc mỗi lần bật/tắt lưới an toàn lại tạo/xoá IAM
# resource (và tránh eventual consistency của IAM làm apply lỗi vì Scheduler
# tạo trước khi role kịp lan). Cùng lập luận đã dùng cho module cicd ở envs/prod.
data "aws_iam_policy_document" "scheduler_assume" {
  statement {
    sid     = "SchedulerServiceAssume"
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["scheduler.amazonaws.com"]
    }

    # Chống confused deputy. Không có hai condition này thì role tin bất kỳ
    # schedule nào của EventBridge Scheduler — kể cả schedule ở account khác —
    # miễn nó biết ARN của role. SourceAccount chặn ở mức account, SourceArn
    # ghim tiếp vào ĐÚNG schedule này.
    condition {
      test     = "StringEquals"
      variable = "aws:SourceAccount"
      values   = [local.account_id]
    }

    condition {
      test     = "ArnEquals"
      variable = "aws:SourceArn"
      values   = [local.schedule_arn]
    }
  }
}

# Đúng MỘT action, trên ĐÚNG MỘT function. Role này không đọc được gì, không
# sửa được gì, không gọi được Lambda nào khác.
#
# ARN của function dựng bằng chuỗi (local.lambda_arn) chứ không tham chiếu
# aws_lambda_function.cost_guard.arn: tham chiếu resource là (known after apply)
# và điều đó làm cả policy document unknown lúc plan, tức assert trong
# tests/costguard.tftest.hcl không đọc được nội dung. Xem comment ở locals trong
# main.tf.
data "aws_iam_policy_document" "scheduler_invoke" {
  statement {
    sid       = "InvokeOnlyTheCostGuardFunction"
    effect    = "Allow"
    actions   = ["lambda:InvokeFunction"]
    resources = [local.lambda_arn]
  }
}

resource "aws_iam_role" "scheduler" {
  name        = "${local.schedule_name}-scheduler-role"
  description = "EventBridge Scheduler: chi InvokeFunction dung cost guard lambda"

  assume_role_policy = data.aws_iam_policy_document.scheduler_assume.json

  tags = { Name = "${local.schedule_name}-scheduler-role" }
}

resource "aws_iam_role_policy" "scheduler" {
  name   = "${local.schedule_name}-scheduler-policy"
  role   = aws_iam_role.scheduler.id
  policy = data.aws_iam_policy_document.scheduler_invoke.json
}

# ─── SCHEDULE ────────────────────────────────────────────────────
# Gate bằng var.enable_auto_stop. Đặt false là quyết định CÓ CHỦ Ý để stack chạy
# qua đêm — và lúc đó KHÔNG còn lưới an toàn nào: rủi ro RDS tự khởi động lại
# sau 7 ngày ($16.5/tuần) quay về nguyên trạng, chỉ còn email Budgets ở mốc 25%
# của $20 phát hiện, tức chậm hơn hai ngày.
resource "aws_scheduler_schedule" "nightly_stop" {
  count = var.enable_auto_stop ? 1 : 0

  name       = local.schedule_name
  group_name = "default"

  schedule_expression          = var.stop_cron
  schedule_expression_timezone = "Asia/Ho_Chi_Minh"

  # mode = "OFF": chạy đúng giờ, không cho AWS dịch trong một cửa sổ. Đây là
  # ràng buộc thật chứ không phải khắt khe vô cớ — cửa sổ linh hoạt của Scheduler
  # có thể dịch tới 15 phút, và mục đích của lần chạy này là cắt chi phí ngay khi
  # sang ngày mới, không phải "quãng nào đó quanh nửa đêm".
  flexible_time_window {
    mode = "OFF"
  }

  target {
    arn      = aws_lambda_function.cost_guard.arn
    role_arn = aws_iam_role.scheduler.arn

    retry_policy {
      # 2 lần thử lại. Lỗi đáng retry ở đây là lỗi thoáng qua (throttle của một
      # API, timeout mạng). Lỗi cấu hình (sai tên ASG, thiếu quyền) thì retry
      # bao nhiêu lần cũng vậy, nên đặt cao hơn chỉ nhân số email báo lỗi lên.
      maximum_retry_attempts = 2

      # 1 giờ, không phải mặc định 24 giờ. Một lần chạy được giao thành công 20
      # giờ sau giờ hẹn sẽ hạ ECS và stop RDS vào giữa buổi chiều làm việc —
      # đúng lúc có người đang dùng. Quá một giờ thì bỏ hẳn lần chạy đó tốt hơn:
      # đêm sau nó chạy lại, và mất một đêm là tối đa $2.35.
      maximum_event_age_in_seconds = 3600
    }
  }

  depends_on = [aws_iam_role_policy.scheduler]
}
