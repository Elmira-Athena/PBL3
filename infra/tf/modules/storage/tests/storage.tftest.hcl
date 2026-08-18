provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project             = "hushstore"
  region              = "ap-southeast-1"
  alb_logs_retention  = 7
  artifacts_retention = 30
}

run "co_dung_3_ecr_repository_va_deu_immutable" {
  command = plan

  assert {
    condition     = length(aws_ecr_repository.this) == 3
    error_message = "Phải có đúng 3 ECR repository: api, web, migrator."
  }

  assert {
    condition = alltrue([
      for r in aws_ecr_repository.this : r.image_tag_mutability == "IMMUTABLE"
    ])
    error_message = "ECR phải IMMUTABLE — tag là git SHA, không được ghi đè. Đây là điều kiện để rollback đáng tin."
  }

  assert {
    condition = alltrue([
      for r in aws_ecr_repository.this : r.image_scanning_configuration[0].scan_on_push == true
    ])
    error_message = "ECR phải bật scan_on_push để phát hiện CVE trong image."
  }
}

run "artifacts_va_alb_logs_bucket_chan_public_hoan_toan" {
  command = plan

  assert {
    condition = alltrue([
      aws_s3_bucket_public_access_block.artifacts.block_public_acls,
      aws_s3_bucket_public_access_block.artifacts.block_public_policy,
      aws_s3_bucket_public_access_block.artifacts.ignore_public_acls,
      aws_s3_bucket_public_access_block.artifacts.restrict_public_buckets,
    ])
    error_message = "Bucket artifacts phải chặn public hoàn toàn — nó chứa migrate SQL và file ops."
  }

  assert {
    condition = alltrue([
      aws_s3_bucket_public_access_block.alb_logs.block_public_acls,
      aws_s3_bucket_public_access_block.alb_logs.block_public_policy,
      aws_s3_bucket_public_access_block.alb_logs.ignore_public_acls,
      aws_s3_bucket_public_access_block.alb_logs.restrict_public_buckets,
    ])
    error_message = "Bucket alb-logs phải chặn public hoàn toàn — access log lộ IP và path của người dùng."
  }
}

run "co_lifecycle_don_du_lieu_cu" {
  command = plan

  assert {
    condition     = aws_s3_bucket_lifecycle_configuration.alb_logs.rule[0].expiration[0].days == 7
    error_message = "ALB access log phải hết hạn sau 7 ngày để không phình phí lưu trữ."
  }

  assert {
    condition     = aws_s3_bucket_lifecycle_configuration.artifacts.rule[0].expiration[0].days == 30
    error_message = "Artifacts phải hết hạn sau 30 ngày."
  }

  assert {
    condition = alltrue([
      for p in aws_ecr_lifecycle_policy.this : can(jsondecode(p.policy).rules[0].selection.countNumber)
    ])
    error_message = "Mỗi ECR repo phải có lifecycle policy giới hạn số image giữ lại."
  }
}
