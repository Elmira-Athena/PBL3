output "budget_name" {
  description = "Tên budget theo dõi chi phí tháng"
  value       = aws_budgets_budget.monthly.name
}

output "lambda_name" {
  description = "Tên Lambda cost guard — dùng cho `aws lambda invoke` khi kiểm chứng tay và cho CloudWatch Logs"
  value       = aws_lambda_function.cost_guard.function_name
}

output "lambda_role_arn" {
  description = "ARN role của Lambda — dùng cho `aws iam get-role-policy` khi kiểm chứng least privilege"
  value       = aws_iam_role.cost_guard.arn
}

output "sns_topic_arn" {
  description = "ARN topic cảnh báo trạng thái hạ tầng. Kiểm subscription đã xác nhận chưa bằng: aws sns list-subscriptions-by-topic --topic-arn <arn> — giá trị PendingConfirmation nghĩa là mọi cảnh báo đang rơi vào hư không"
  value       = aws_sns_topic.costguard.arn
}

output "schedule_name" {
  description = "Tên EventBridge Scheduler chạy Lambda hằng đêm. Rỗng khi enable_auto_stop = false — lúc đó KHÔNG còn lưới an toàn nào"
  value       = var.enable_auto_stop ? aws_scheduler_schedule.nightly_stop[0].name : ""
}
