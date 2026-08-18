output "ecr_api_url" {
  description = "URL repository ECR của image API"
  value       = aws_ecr_repository.this["api"].repository_url
}

output "ecr_web_url" {
  description = "URL repository ECR của image web (nginx + Blazor WASM)"
  value       = aws_ecr_repository.this["web"].repository_url
}

output "ecr_migrator_url" {
  description = "URL repository ECR của image migrator (EF Core bundle)"
  value       = aws_ecr_repository.this["migrator"].repository_url
}

output "ecr_api_arn" {
  description = "ARN repository ECR của image API"
  value       = aws_ecr_repository.this["api"].arn
}

output "ecr_web_arn" {
  description = "ARN repository ECR của image web"
  value       = aws_ecr_repository.this["web"].arn
}

output "ecr_migrator_arn" {
  description = "ARN repository ECR của image migrator"
  value       = aws_ecr_repository.this["migrator"].arn
}

output "assets_bucket_name" {
  description = "Tên bucket ảnh sản phẩm"
  value       = aws_s3_bucket.assets.id
}

output "assets_bucket_arn" {
  description = "ARN bucket ảnh sản phẩm — dùng cho IAM policy của ECS task role"
  value       = aws_s3_bucket.assets.arn
}

output "artifacts_bucket_name" {
  description = "Tên bucket artifacts"
  value       = aws_s3_bucket.artifacts.id
}

output "artifacts_bucket_arn" {
  description = "ARN của bucket artifacts"
  value       = aws_s3_bucket.artifacts.arn
}

output "alb_logs_bucket_name" {
  description = "Tên bucket chứa ALB access log"
  value       = aws_s3_bucket.alb_logs.id
}
