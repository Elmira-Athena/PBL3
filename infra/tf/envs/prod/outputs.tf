# Output được thêm dần theo từng task.

output "vpc_id" {
  description = "ID của VPC"
  value       = module.network.vpc_id
}

output "app_subnet_ids" {
  description = "ID của app subnet — nơi EC2 container instance chạy"
  value       = module.network.app_subnet_ids
}

output "ecr_urls" {
  description = "URL 3 ECR repository để docker push"
  value = {
    api      = module.storage.ecr_api_url
    web      = module.storage.ecr_web_url
    migrator = module.storage.ecr_migrator_url
  }
}

output "artifacts_bucket" {
  description = "Bucket chứa migrate SQL và file ops"
  value       = module.storage.artifacts_bucket_name
}

output "assets_bucket" {
  description = "Bucket ảnh sản phẩm — tên này hiện trong URL ảnh công khai"
  value       = module.storage.assets_bucket_name
}

output "rds_endpoint" {
  description = "Hostname RDS — chỉ truy cập được từ trong app tier"
  value       = module.data.rds_endpoint
}

output "rds_identifier" {
  description = "DB identifier để start/stop tiết kiệm chi phí"
  value       = module.data.rds_identifier
}

output "budget_name" {
  description = "Tên budget theo dõi chi phí tháng"
  value       = module.costguard.budget_name
}

output "ecs_cluster_name" {
  description = "Tên ECS cluster"
  value       = module.ecs.cluster_name
}

output "asg_name" {
  description = "Tên ASG — dùng để bật/tắt instance tiết kiệm chi phí"
  value       = module.ecs.asg_name
}

output "capacity_provider_name" {
  description = "Tên ECS capacity provider — dùng cho aws ecs run-task"
  value       = module.ecs.capacity_provider_name
}

output "alb_dns_name" {
  description = "Hostname ALB — trỏ CNAME của Cloudflare vào đây"
  value       = module.alb.alb_dns_name
}

output "acm_certificate_arn" {
  description = "ARN của ACM certificate phục vụ cả hushstore.io.vn và api.hushstore.io.vn"
  value       = module.alb.certificate_arn
}

output "acm_validation_records" {
  description = "CNAME cần thêm vào Cloudflare để ACM cấp cert. Bắt buộc để Proxy status = DNS only (mây xám) — mây vàng làm ACM không đọc được record nên cert đứng mãi ở PENDING_VALIDATION"
  value       = module.alb.acm_validation_records
}

output "tg_arns" {
  description = "ARN 2 target group — Task 15 gắn vào ECS service"
  value = {
    web = module.alb.tg_web_arn
    api = module.alb.tg_api_arn
  }
}

output "taskdef_seeder_family" {
  description = "Family task definition seeder — dùng cho aws ecs run-task khi seed"
  value       = module.ecs.taskdef_seeder_family
}

output "github_deploy_role_arn" {
  description = "Đặt vào repository variable AWS_DEPLOY_ROLE_ARN trên GitHub. Không phải secret — chỉ là tên vai, vô dụng nếu không có OIDC token do GitHub ký cho đúng repo và đúng nhánh main"
  value       = module.cicd.deploy_role_arn
}

output "github_plan_role_arn" {
  description = "Đặt vào repository variable AWS_PLAN_ROLE_ARN trên GitHub"
  value       = module.cicd.plan_role_arn
}
