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

output "taskdef_api_arn" {
  description = "ARN revision hiện tại của task definition API"
  value       = aws_ecs_task_definition.api.arn
}

output "taskdef_web_arn" {
  description = "ARN revision hiện tại của task definition web"
  value       = aws_ecs_task_definition.web.arn
}

output "taskdef_migrator_arn" {
  description = "ARN revision hiện tại của task definition migrator"
  value       = aws_ecs_task_definition.migrator.arn
}

output "taskdef_api_family" {
  description = "Family của task definition API — dùng cho ecs update-service"
  value       = aws_ecs_task_definition.api.family
}

output "taskdef_web_family" {
  description = "Family của task definition web"
  value       = aws_ecs_task_definition.web.family
}

output "taskdef_migrator_family" {
  description = "Family của task definition migrator — dùng cho ecs run-task"
  value       = aws_ecs_task_definition.migrator.family
}

output "service_web_name" {
  description = "Tên ECS service của Blazor client. Rỗng khi enable_alb = false"
  value       = var.enable_alb ? aws_ecs_service.web[0].name : ""
}

output "service_api_name" {
  description = "Tên ECS service của API. Rỗng khi enable_alb = false"
  value       = var.enable_alb ? aws_ecs_service.api[0].name : ""
}

output "taskdef_seeder_arn" {
  description = "ARN revision hiện tại của task definition seeder"
  value       = aws_ecs_task_definition.seeder.arn
}

output "taskdef_seeder_family" {
  description = "Family của task definition seeder — dùng cho aws ecs run-task"
  value       = aws_ecs_task_definition.seeder.family
}

output "migrator_log_group_arn" {
  description = "ARN log group của migrator — module cicd giới hạn quyền đọc log của pipeline đúng vào group này"
  value       = aws_cloudwatch_log_group.migrator.arn
}
