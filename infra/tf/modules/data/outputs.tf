output "rds_endpoint" {
  description = "Hostname của RDS (không kèm port)"
  value       = aws_db_instance.this.address
}

output "rds_identifier" {
  description = "DB instance identifier — dùng cho aws rds start/stop-db-instance"
  value       = aws_db_instance.this.identifier
}

output "rds_arn" {
  description = "ARN của RDS instance"
  value       = aws_db_instance.this.arn
}

output "ssm_connection_string_arn" {
  description = "ARN parameter chứa connection string — dùng trong khối secrets của task definition"
  value       = aws_ssm_parameter.connection_string.arn
}

output "ssm_jwt_secret_arn" {
  description = "ARN parameter chứa JWT secret"
  value       = aws_ssm_parameter.jwt_secret.arn
}

output "ssm_db_password_arn" {
  description = "ARN parameter chứa master password của RDS"
  value       = aws_ssm_parameter.db_password.arn
}

output "ssm_path_prefix" {
  description = "Path prefix của mọi parameter — dùng cho IAM policy wildcard"
  value       = local.ssm_prefix
}

# Export để module ecs (task seeder) dùng đúng cùng giá trị, thay vì lặp lại
# literal ở envs/prod rồi lệch nhau khi ai đó đổi một bên. sqlcmd cần user và
# tên database rời vì nó không nhận connection string kiểu .NET.
output "db_username" {
  description = "User master của RDS — task seeder truyền vào sqlcmd -U"
  value       = var.db_username
}

output "db_name" {
  description = "Tên database — task seeder truyền vào sqlcmd -d"
  value       = var.db_name
}

# ─── READ REPLICA — rỗng khi tắt, không phải lỗi ─────────────────
# Ba output này là cách down.sh / status.sh và người đọc console biết replica có
# đang sống hay không. Trước đợt này replica được dựng mà KHÔNG có output nào
# chỉ tới nó: cách duy nhất để thấy là mở AWS console, mà đây đúng là resource
# không được phép sống qua đêm — thứ nguy hiểm nhất để làm cho khó thấy.
output "rds_replica_endpoint" {
  description = "Hostname của read replica (không kèm port). Rỗng khi enable_read_replica = false"
  value       = var.enable_read_replica ? aws_db_instance.replica[0].address : ""
}

output "rds_replica_identifier" {
  description = "Identifier của read replica — dùng cho aws rds delete-db-instance và cho status.sh. Rỗng khi tắt"
  value       = var.enable_read_replica ? aws_db_instance.replica[0].identifier : ""
}

output "ssm_replica_connection_string_arn" {
  description = "ARN parameter chứa connection string chỉ-đọc. Rỗng khi tắt. CHƯA có task definition nào tiêu thụ nó"
  value       = var.enable_read_replica ? aws_ssm_parameter.replica_connection_string[0].arn : ""
}
