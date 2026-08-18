output "alb_sg_id" {
  description = "ID của Security Group cho ALB"
  value       = aws_security_group.alb.id
}

output "web_sg_id" {
  description = "ID của Security Group cho ECS container instance"
  value       = aws_security_group.web.id
}

output "rds_sg_id" {
  description = "ID của Security Group cho RDS"
  value       = aws_security_group.rds.id
}
