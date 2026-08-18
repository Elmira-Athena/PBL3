data "aws_caller_identity" "current" {}

locals {
  # Cả ba bucket gắn hậu tố account ID. Tên bucket S3 duy nhất toàn cầu và
  # KHÔNG có cách read-only nào kiểm tra được tên còn trống (head-bucket trả 404
  # cho cả bucket của account khác), nên đừng đoán — ghép account ID là xong.
  assets_bucket    = "${var.project}-public-assets-${data.aws_caller_identity.current.account_id}"
  artifacts_bucket = "${var.project}-artifacts-${data.aws_caller_identity.current.account_id}"
  alb_logs_bucket  = "${var.project}-alb-logs-${data.aws_caller_identity.current.account_id}"
}

# ─── BUCKET ẢNH SẢN PHẨM ─────────────────────────────────────────
resource "aws_s3_bucket" "assets" {
  bucket = local.assets_bucket

  tags = { Name = local.assets_bucket }

  # force_destroy = false (mặc định) là lưới an toàn đúng mức ở đây:
  # terraform destroy sẽ THẤT BẠI nếu bucket còn object, buộc phải xoá ảnh
  # một cách có ý thức trước. Không dùng prevent_destroy vì nó chặn cả
  # nuke.sh ngay cả khi bucket rỗng.
  force_destroy = false
}

# S3StorageService trả về URL public dạng
# https://<bucket>.s3.<region>.amazonaws.com/<key>, nên object phải đọc
# được công khai. Cho phép public policy nhưng vẫn chặn ACL.
resource "aws_s3_bucket_public_access_block" "assets" {
  bucket = aws_s3_bucket.assets.id

  block_public_acls       = true
  ignore_public_acls      = true
  block_public_policy     = false
  restrict_public_buckets = false
}

# CẢNH BÁO CÓ CHỦ Ý: policy này làm MỌI object trong bucket đọc được công khai,
# không chỉ ảnh sản phẩm. Đó là yêu cầu của S3StorageService (nó trả URL công
# khai và Blazor render trực tiếp), nhưng kéo theo hai điều phải nhớ:
#   1. TUYỆT ĐỐI không đặt dữ liệu không công khai vào bucket này. Thứ gì cần
#      riêng tư thì để ở bucket artifacts (đã chặn public hoàn toàn).
#   2. `ImageController.Upload` nhận `folder` từ query param với default
#      "general" và KHÔNG có allowlist, nên admin ghi được vào bất kỳ prefix
#      nào. Không siết được bằng bucket policy theo prefix cố định vì prefix do
#      caller quyết định. Cách sửa đúng là validate `folder` theo allowlist ở
#      tầng app — ghi nhận vào docs/security-validation-report.md (Phase 3).
data "aws_iam_policy_document" "assets_public_read" {
  statement {
    sid     = "PublicReadObjects"
    effect  = "Allow"
    actions = ["s3:GetObject"]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    resources = ["${aws_s3_bucket.assets.arn}/*"]
  }
}

resource "aws_s3_bucket_policy" "assets" {
  bucket = aws_s3_bucket.assets.id
  policy = data.aws_iam_policy_document.assets_public_read.json

  depends_on = [aws_s3_bucket_public_access_block.assets]
}

resource "aws_s3_bucket_cors_configuration" "assets" {
  bucket = aws_s3_bucket.assets.id

  cors_rule {
    allowed_methods = ["GET", "HEAD"]
    allowed_origins = ["*"]
    allowed_headers = ["*"]
    max_age_seconds = 3600
  }
}

# ─── BUCKET ARTIFACTS ────────────────────────────────────────────
resource "aws_s3_bucket" "artifacts" {
  bucket        = local.artifacts_bucket
  force_destroy = true

  tags = { Name = "${var.project}-artifacts" }
}

resource "aws_s3_bucket_public_access_block" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  rule {
    id     = "expire-artifacts"
    status = "Enabled"

    filter {}

    expiration {
      days = var.artifacts_retention
    }
  }
}

# ─── BUCKET ALB ACCESS LOGS ──────────────────────────────────────
resource "aws_s3_bucket" "alb_logs" {
  bucket        = local.alb_logs_bucket
  force_destroy = true

  tags = { Name = "${var.project}-alb-logs" }
}

resource "aws_s3_bucket_public_access_block" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id

  rule {
    id     = "expire-alb-logs"
    status = "Enabled"

    filter {}

    expiration {
      days = var.alb_logs_retention
    }
  }
}

# ap-southeast-1 là region ra đời trước 08/2022 nên ALB ghi log bằng ELB
# account ID của region. Region mới hơn dùng service principal
# logdelivery.elasticloadbalancing.amazonaws.com. Cấp cả hai để chắc chắn.
data "aws_elb_service_account" "current" {}

data "aws_iam_policy_document" "alb_logs" {
  statement {
    sid     = "ElbAccountWrite"
    effect  = "Allow"
    actions = ["s3:PutObject"]

    principals {
      type        = "AWS"
      identifiers = [data.aws_elb_service_account.current.arn]
    }

    resources = ["${aws_s3_bucket.alb_logs.arn}/*"]
  }

  # Service principal dùng CHUNG cho mọi khách hàng AWS, nên nếu không có
  # condition thì load balancer của account khác cũng có thể được trỏ vào bucket
  # này mà ghi log vào — đây đúng là lỗ hổng confused deputy. aws:SourceAccount
  # giới hạn chỉ ALB của chính account này.
  # Không dùng thêm aws:SourceArn được vì ARN của ALB chưa tồn tại ở Task 6
  # (ALB tạo ở Task 14 và còn bị enable_alb gate).
  statement {
    sid     = "LogDeliveryServiceWrite"
    effect  = "Allow"
    actions = ["s3:PutObject"]

    principals {
      type        = "Service"
      identifiers = ["logdelivery.elasticloadbalancing.amazonaws.com"]
    }

    resources = ["${aws_s3_bucket.alb_logs.arn}/*"]

    condition {
      test     = "StringEquals"
      variable = "aws:SourceAccount"
      values   = [data.aws_caller_identity.current.account_id]
    }
  }
}

resource "aws_s3_bucket_policy" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id
  policy = data.aws_iam_policy_document.alb_logs.json

  depends_on = [aws_s3_bucket_public_access_block.alb_logs]
}
