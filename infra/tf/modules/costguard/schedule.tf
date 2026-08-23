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

    # Chống confused deputy: không có condition này thì role tin BẤT KỲ schedule
    # nào của EventBridge Scheduler — kể cả schedule ở account của người khác —
    # miễn nó biết ARN của role.
    condition {
      test     = "StringEquals"
      variable = "aws:SourceAccount"
      values   = [local.account_id]
    }

    # ─── VÌ SAO KHÔNG CÓ aws:SourceArn Ở ĐÂY ────────────────────────────────
    # Chỗ này ĐÃ THỬ siết thêm bằng `ArnEquals` trên
    # arn:aws:scheduler:<region>:<account>:schedule/default/hushstore-nightly-stop
    # và AWS TỪ CHỐI, hai lần liên tiếp, không phải do eventual consistency:
    #
    #   ValidationException: The execution role you provide must allow AWS
    #   EventBridge Scheduler to assume the role.
    #
    # Nguyên nhân: `CreateSchedule` xác thực role bằng một phép assume-role thử,
    # và lúc đó schedule CHƯA TỒN TẠI — nên không có `aws:SourceArn` nào để so.
    # Một condition không thể thoả trong bước xác thực làm cả bước đó fail. Bỏ
    # đúng condition này ra thì apply thành công ngay ở lần chạy kế tiếp, cùng
    # mọi thứ khác giữ nguyên; đó là phép thử phân biệt, không phải phỏng đoán.
    #
    # Phần bị mất là nhỏ và đã được bù ở chỗ khác. SourceAccount đóng HOÀN TOÀN
    # đường cross-account — thứ mà confused deputy thật sự nói tới. Cái còn lại
    # là "một schedule KHÁC trong cùng account này assume được role", mà làm vậy
    # cũng chỉ được đúng một quyền: `lambda:InvokeFunction` trên đúng function
    # cost guard. Gọi thêm cost guard là vô hại — nó idempotent và chỉ TẮT được
    # thứ đang bật. Nói cách khác, policy quyền đã hẹp tới mức làm việc siết
    # trust policy thêm gần như không còn tác dụng.
    #
    # `locals` phía trên TỪNG có một `schedule_arn` dựng sẵn, dùng cho đúng
    # condition đã bị bỏ ở đây. Nó đã được xoá cùng lúc với dòng này được viết:
    # để một ARN schedule dựng sẵn nằm trong locals mà không ai dùng chính là
    # mời người sửa sau này nối nó vào một `ArnEquals` — tức tái phát đúng lỗi
    # mà cả đoạn comment trên tồn tại để cảnh báo. Cần lại ARN đó thì dựng lại
    # tại chỗ, và đọc đoạn trên trước.

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

    # Marker để LOG phân biệt được lần chạy theo hẹn với lần chạy do người gọi
    # tay. Mã Python in `event` ra dòng JSON summary; không có marker thì cả hai
    # loại đều là `{}` và không phân biệt được. Điều đó quan trọng vì lập luận
    # cho việc KHÔNG đặt reserved_concurrent_executions (xem lambda.tf) là "hai
    # lần chạy song song chỉ xảy ra nếu có người invoke tay lúc nửa đêm" — một
    # khẳng định về thực tế, và log phải xác nhận hay phủ định được nó chứ không
    # để nó mãi là suy đoán. Lambda KHÔNG đọc nội dung này để phân nhánh: nó chỉ
    # được in ra, nên một payload sai không đổi hành vi của lần chạy nào.
    input = jsonencode({
      invoked_by = "eventbridge-scheduler"
      schedule   = local.schedule_name
    })

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
