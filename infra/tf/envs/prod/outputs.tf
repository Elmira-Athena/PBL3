# Output được thêm dần theo từng task.

output "vpc_id" {
  description = "ID của VPC"
  value       = module.network.vpc_id
}

output "app_subnet_ids" {
  description = "ID của app subnet — nơi EC2 container instance chạy"
  value       = module.network.app_subnet_ids
}
