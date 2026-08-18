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
