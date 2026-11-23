# CloudWatch alarm an ninh — pentest 2026-11-23 §2.4#1 / F5 (trước đó describe-alarms = 0).
# Gate theo enable_alb: metric chỉ tồn tại khi ALB sống. alarm_actions nối tới SNS của costguard
# (truyền từ envs/prod); rỗng thì alarm vẫn hiện trên console nhưng không gửi thông báo.
# treat_missing_data = notBreaching: ALB tắt (down.sh) thì không có datapoint ⇒ không kêu nhầm.

locals {
  alarm_enabled = var.enable_alb ? 1 : 0
}

# Spike 4xx ở target = WAF trả 403 hàng loạt (SQLi/XSS/scan) hoặc brute-force. Tín hiệu an ninh.
resource "aws_cloudwatch_metric_alarm" "target_4xx_spike" {
  count = local.alarm_enabled

  alarm_name          = "${var.project}-alb-target-4xx-spike"
  alarm_description   = "Spike HTTPCode_Target_4XX_Count: nhiều request bị WAF/app chặn (tấn công/quét)."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "HTTPCode_Target_4XX_Count"
  dimensions          = { LoadBalancer = aws_lb.this[0].arn_suffix }
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 100
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = var.alarm_sns_topic_arns
  ok_actions          = var.alarm_sns_topic_arns
  tags                = { Name = "${var.project}-alb-target-4xx-spike" }
}

# 5xx ở target = app/WAF container lỗi (có thể do khai thác gây lỗi hoặc quá tải).
resource "aws_cloudwatch_metric_alarm" "target_5xx" {
  count = local.alarm_enabled

  alarm_name          = "${var.project}-alb-target-5xx"
  alarm_description   = "HTTPCode_Target_5XX_Count cao: container web/api/waf trả lỗi máy chủ."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "HTTPCode_Target_5XX_Count"
  dimensions          = { LoadBalancer = aws_lb.this[0].arn_suffix }
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 10
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = var.alarm_sns_topic_arns
  ok_actions          = var.alarm_sns_topic_arns
  tags                = { Name = "${var.project}-alb-target-5xx" }
}

# 5xx do CHÍNH ALB sinh (không có target lành, lỗi listener) — khác với target 5xx ở trên.
resource "aws_cloudwatch_metric_alarm" "elb_5xx" {
  count = local.alarm_enabled

  alarm_name          = "${var.project}-alb-elb-5xx"
  alarm_description   = "HTTPCode_ELB_5XX_Count: ALB không có target lành để chuyển request."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "HTTPCode_ELB_5XX_Count"
  dimensions          = { LoadBalancer = aws_lb.this[0].arn_suffix }
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 5
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = var.alarm_sns_topic_arns
  ok_actions          = var.alarm_sns_topic_arns
  tags                = { Name = "${var.project}-alb-elb-5xx" }
}
