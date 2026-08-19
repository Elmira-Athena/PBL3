locals {
  ssm_prefix = "/${var.project}/prod"
}

# ─── MẬT KHẨU SINH TỰ ĐỘNG ───────────────────────────────────────
# RDS SQL Server cấm các ký tự: / ' " @ và khoảng trắng trong master
# password. override_special dưới đây đã loại hết chúng.
resource "random_password" "db" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
  min_upper        = 2
  min_lower        = 2
  min_numeric      = 2
  min_special      = 2
}

resource "random_password" "jwt" {
  length  = 64
  special = false
}

# ─── DB SUBNET GROUP ─────────────────────────────────────────────
resource "aws_db_subnet_group" "this" {
  name        = "${var.project}-db-subnet-group"
  description = "HushStore RDS - db tier isolated, 2 AZ"
  subnet_ids  = var.db_subnet_ids

  tags = { Name = "${var.project}-db-subnet-group" }
}

# ─── RDS SQL SERVER EXPRESS ──────────────────────────────────────
resource "aws_db_instance" "this" {
  identifier = "${var.project}-db-tf"

  engine         = "sqlserver-ex"
  engine_version = var.engine_version
  license_model  = "license-included"
  instance_class = var.instance_class

  allocated_storage = var.allocated_storage
  storage_type      = "gp2"
  storage_encrypted = true

  username = var.db_username
  password = random_password.db.result

  # KHÔNG đặt db_name — aws_db_instance.db_name không được hỗ trợ cho engine
  # SQL Server. Database HushStoreDB do EF Core migration bundle tạo ở Task 13.

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [var.rds_sg_id]
  publicly_accessible    = false
  multi_az               = false

  backup_retention_period    = var.backup_retention_days
  auto_minor_version_upgrade = true

  # Phải false để nuke.sh / terraform destroy chạy được.
  deletion_protection = false
  skip_final_snapshot = var.skip_final_snapshot

  # Không bật Performance Insights / Enhanced Monitoring — tốn phí, không cần
  # cho quy mô đồ án.
  performance_insights_enabled = false

  tags = { Name = "${var.project}-db-tf" }
}

# ─── SSM PARAMETER STORE (SecureString, miễn phí) ────────────────
resource "aws_ssm_parameter" "db_password" {
  name        = "${local.ssm_prefix}/db-password"
  description = "Master password cua RDS SQL Server"
  type        = "SecureString"
  value       = random_password.db.result

  tags = { Name = "${var.project}-db-password" }
}

resource "aws_ssm_parameter" "connection_string" {
  name        = "${local.ssm_prefix}/connection-string"
  description = "Connection string day du, inject vao container qua khoi secrets cua ECS"
  type        = "SecureString"

  # Encrypt=True + TrustServerCertificate=False: bắt buộc TLS VÀ xác thực cert của
  # RDS thật, không tin mù. TrustServerCertificate=True (bản trước) vẫn mã hoá
  # nhưng bỏ qua kiểm cert, tức về nguyên tắc vẫn bị MITM ngay trong VPC.
  # ĐIỀU KIỆN: cert của RDS do Amazon RDS CA cấp, mà CA đó KHÔNG có trong trust
  # store mặc định — nên cả image API (Dockerfile) và image migrator
  # (Dockerfile.migrator) đều đã cài bundle CA của region ap-southeast-1. Thiếu
  # bước đó là app không kết nối được DB.
  # Chỉ chuỗi PRODUCTION này xác thực cert; chuỗi local dev vẫn dùng
  # TrustServerCertificate=True vì SQL Server trong container dùng cert tự ký.
  value = join("", [
    "Server=${aws_db_instance.this.address},1433;",
    "Database=${var.db_name};",
    "User Id=${var.db_username};",
    "Password=${random_password.db.result};",
    "Encrypt=True;",
    "TrustServerCertificate=False;",
    "MultipleActiveResultSets=True;",
  ])

  tags = { Name = "${var.project}-connection-string" }
}

resource "aws_ssm_parameter" "jwt_secret" {
  name        = "${local.ssm_prefix}/jwt-secret"
  description = "JwtSettings__SecretKey — 64 ky tu, tren 256-bit entropy"
  type        = "SecureString"
  value       = random_password.jwt.result

  tags = { Name = "${var.project}-jwt-secret" }
}
