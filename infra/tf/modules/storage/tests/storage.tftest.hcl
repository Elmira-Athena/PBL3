provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project            = "hushstore"
  region             = "ap-southeast-1"
  alb_logs_retention = 7
  # artifacts_retention CỐ TÌNH không set: để assert dưới đây kiểm chính GIÁ TRỊ
  # MẶC ĐỊNH của module — đó là giá trị prod đang dùng (envs/prod không truyền
  # biến này). Set nó ở đây thì test chỉ còn kiểm lại chính đầu vào của mình.
}

run "co_dung_4_ecr_repository_va_deu_immutable" {
  command = plan

  # Bốn, không phải ba. `seeder` được thêm ở Task 16 và test này không được cập
  # nhật theo — nó đỏ từ lúc đó tới khi Phase 2 dựng CI và bắt gặp.
  #
  # Đếm bằng một con số cứng chứ không phải `>= 3` là cố ý: thêm một ECR
  # repository là thêm một chỗ pipeline được push vào, và policy của role deploy
  # liệt kê tường minh từng ARN. Test này đỏ khi có người thêm repo thứ năm buộc
  # họ nhìn lại cả hai chỗ.
  assert {
    condition     = length(aws_ecr_repository.this) == 4
    error_message = "Phải có đúng 4 ECR repository: api, web, migrator, seeder. Nếu vừa thêm repo mới thì phải cập nhật cả ecr_repository_arns của module cicd."
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
    condition     = aws_s3_bucket_lifecycle_configuration.artifacts.rule[0].expiration[0].days >= 365
    error_message = "Bucket artifacts phải giữ file ÍT NHẤT 365 ngày. Trong đó có migrations/migrate-<sha>.sql — bản ghi duy nhất còn lại về câu SQL nào đã chạy lên production ở commit nào (bản kia là artifact của Actions, hết hạn sau 14 ngày). Cả hai con số 14 và 30 ngày đều ngắn hơn một học kỳ. Và đừng \"sửa\" bằng cách thêm một rule riêng cho prefix migrations/: S3 áp mọi rule khớp object và không có luật rule cụ thể hơn thì thắng, nên rule 30 ngày bắt tất vẫn xoá trước."
  }

  assert {
    condition = alltrue([
      for p in aws_ecr_lifecycle_policy.this : can(jsondecode(p.policy).rules[0].selection.countNumber)
    ])
    error_message = "Mỗi ECR repo phải có lifecycle policy giới hạn số image giữ lại."
  }
}
