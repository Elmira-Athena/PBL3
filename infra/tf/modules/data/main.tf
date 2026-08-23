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

  # ─── HAI CỬA SỔ NÀY PHẢI TRÁNH GIỜ CHẠY CỦA COST GUARD ───────────
  # Cost guard (modules/costguard) chạy 00:00 giờ Việt Nam = 17:00 UTC và chỉ
  # gọi StopDBInstance khi status là `available`. Trong lúc RDS đang
  # `backing-up`, `modifying` hay `upgrading` thì AWS TỪ CHỐI lệnh stop bằng
  # InvalidDBInstanceState — nhưng instance vẫn tính đủ $0,098/giờ, tức
  # $2,35/ngày. Nghĩa là một cửa sổ trùng 17:00 UTC làm guard mất tác dụng
  # ĐÚNG mỗi đêm, không phải ngẫu nhiên một đêm.
  #
  # Vì sao phải khai tường minh: khi hai field này để trống, AWS tự gán ngẫu
  # nhiên trong khối mặc định 8 giờ của ap-southeast-1 (14:00-22:00 UTC) và
  # gán LẠI mỗi lần instance được tạo lại. 17:00 UTC nằm trong khối đó, nên
  # xác suất bốc phải một cửa sổ chồng giờ guard là cỡ 1/16 mỗi lần dựng —
  # một cái bẫy không ai chọn và không ai thấy, vì triệu chứng duy nhất là
  # hoá đơn.
  #
  # 06:00-06:30 UTC = 13:00-13:30 ICT (giờ nghỉ trưa, cách giờ guard 11 tiếng
  # theo cả hai chiều) và maintenance Chủ nhật 07:00-08:00 UTC = 14:00-15:00
  # ICT. Biên đó lớn hơn tổng của mọi thứ có thể trượt: timeout của Lambda là
  # 120 giây và maximum_event_age_in_seconds của Scheduler là 3600.
  #
  # RÀNG BUỘC CỦA AWS khi sửa hai giá trị này: định dạng bắt buộc là
  # hh24:mi-hh24:mi (UTC) cho backup và ddd:hh24:mi-ddd:hh24:mi cho
  # maintenance, tối thiểu 30 phút mỗi cửa sổ, và HAI CỬA SỔ KHÔNG ĐƯỢC CHỒNG
  # NHAU — chồng nhau thì apply chết ngay, không âm thầm.
  #
  # NẾU ĐỔI stop_cron của costguard: phải đổi cả hai giá trị dưới đây. Terraform
  # không nối được hai module này (data không biết gì về costguard), nên thứ canh
  # ràng buộc đó là assert trong tests/rds.tftest.hcl — nó chặn mọi cửa sổ bắt
  # đầu trong khoảng 16:00-18:59 UTC.
  backup_window      = "06:00-06:30"
  maintenance_window = "sun:07:00-sun:08:00"

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
