output "instance_profile_name" {
  description = "Tên instance profile gắn vào launch template"
  value       = aws_iam_instance_profile.instance.name
}

output "instance_role_name" {
  description = "Tên IAM role của EC2 container instance"
  value       = aws_iam_role.instance.name
}

output "task_execution_role_arn" {
  description = "ARN role ECS agent dùng lúc khởi task (pull ECR, đọc secret, ghi log)"
  value       = aws_iam_role.task_execution.arn
}

output "task_app_role_arn" {
  description = "ARN role container API dùng lúc runtime (chỉ S3 + ECS Exec)"
  value       = aws_iam_role.task_app.arn
}

output "task_migrator_role_arn" {
  description = "ARN role container migrator (không có quyền AWS API nào)"
  value       = aws_iam_role.task_migrator.arn
}

output "cluster_name" {
  description = "Tên ECS cluster"
  value       = aws_ecs_cluster.this.name
}

output "cluster_arn" {
  description = "ARN của ECS cluster"
  value       = aws_ecs_cluster.this.arn
}

output "asg_name" {
  description = "Tên ASG — dùng cho aws autoscaling set-desired-capacity khi bật/tắt"
  value       = aws_autoscaling_group.this.name
}

output "capacity_provider_name" {
  description = "Tên capacity provider — dùng trong capacity_provider_strategy của service"
  value       = aws_ecs_capacity_provider.this.name
}
