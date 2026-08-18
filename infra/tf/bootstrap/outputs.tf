output "state_bucket" {
  description = "Tên bucket chứa Terraform state — dán vào envs/prod/backend.tf"
  value       = aws_s3_bucket.tfstate.id
}

output "account_id" {
  description = "AWS account ID"
  value       = data.aws_caller_identity.current.account_id
}
