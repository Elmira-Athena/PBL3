output "oidc_provider_arn" {
  description = "ARN của OIDC provider GitHub. Chỉ có MỘT provider cho cả account — nếu apply báo EntityAlreadyExists thì đã có provider từ trước, dùng `import` block thay vì tạo mới"
  value       = aws_iam_openid_connect_provider.github.arn
}

output "deploy_role_arn" {
  description = "Đặt vào repository variable AWS_DEPLOY_ROLE_ARN. Đây KHÔNG phải secret — nó chỉ là tên vai; không assume được nếu không có OIDC token do GitHub ký cho đúng repo và đúng nhánh"
  value       = aws_iam_role.deploy.arn
}

output "plan_role_arn" {
  description = "Đặt vào repository variable AWS_PLAN_ROLE_ARN"
  value       = aws_iam_role.plan.arn
}

output "deploy_role_name" {
  description = "Tên role deploy — dùng cho aws iam get-role khi kiểm chứng trust policy"
  value       = aws_iam_role.deploy.name
}

output "plan_role_name" {
  description = "Tên role plan"
  value       = aws_iam_role.plan.name
}
