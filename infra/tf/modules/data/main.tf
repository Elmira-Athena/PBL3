locals {
  ssm_prefix = "/${var.project}/prod"
}

# ─── MẬT KHẨU SINH TỰ ĐỘNG ───────────────────────────────────────
# RDS PostgreSQL cấm / ' " @ và khoảng trắng trong master password.
# override_special dưới đây đã loại hết chúng.
#
# ⚠️ ĐÃ BỎ THÊM DẤU `=` so với bản SQL Server, và lý do không nằm ở phía AWS mà
# ở phía CLIENT: Npgsql đọc chuỗi kết nối theo cặp `key=value;`. Một mật khẩu
# chứa `=` không chắc chắn parse đúng ở mọi đường (chuỗi còn đi qua SSM, qua
# `secrets` của ECS, qua biến môi trường), và khi hỏng thì triệu chứng là
# "password authentication failed" — một câu chỉ thẳng vào sai mật khẩu, tức
# đánh lạc hướng hoàn toàn khỏi nguyên nhân thật. Bỏ một ký tự khỏi bảng chữ
# cái của mật khẩu 32 ký tự là cái giá không đáng kể để đổi lấy việc loại hẳn
# một giờ đi tìm nhầm chỗ.
resource "random_password" "db" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_+[]{}<>:?"
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

# ─── RDS POSTGRESQL ──────────────────────────────────────────────
# Vì sao đổi khỏi sqlserver-ex — lý do là EDITION, không phải kiến trúc:
#   · "Read replicas are only available on the SQL Server Enterprise Edition"
#   · "...only available for DB instance classes with four or more vCPUs"
# Stack cũ chạy sqlserver-ex trên db.t3.micro (2 vCPU) ⇒ hỏng CẢ HAI điều kiện.
# Không có cách cấu hình nào mở được read replica; EE thì gấp nhiều lần ngân sách.
#
# Lý do thứ hai, quan trọng ngang: "RDS for SQL Server doesn't support stopping a
# DB instance in a Multi-AZ deployment." PostgreSQL thì stop được. Nghĩa là bật
# Multi-AZ trên PostgreSQL KHÔNG phá cơ chế tắt tiền (up.sh/down.sh/cost guard)
# — trên SQL Server thì có.
resource "aws_db_instance" "this" {
  identifier = "${var.project}-db-tf"

  engine         = "postgres"
  engine_version = var.engine_version
  instance_class = var.instance_class
  # KHÔNG có license_model: PostgreSQL là engine open-source, không có license để
  # khai. Để lại "license-included" là apply đỏ ngay — hỏng ồn ào, không sao.

  allocated_storage = var.allocated_storage
  storage_type      = "gp3"
  storage_encrypted = true

  username = var.db_username
  password = random_password.db.result

  # ⚠️ CÓ đặt db_name — ngược hẳn bản SQL Server, nơi comment ở đúng chỗ này ghi
  # "KHÔNG đặt db_name vì aws_db_instance.db_name không hỗ trợ SQL Server".
  # PostgreSQL tạo database NGAY LÚC CREATE instance. Bỏ trống thì RDS tạo một
  # database tên `postgres` và ứng dụng nối vào một DB rỗng — mà migration bundle
  # sẽ chạy được ở đó, seed cũng chạy được, nên KHÔNG có gì đỏ; chỉ là toàn bộ hệ
  # thống sống trong một database mang tên sai.
  db_name = var.db_name

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [var.rds_sg_id]
  publicly_accessible    = false

  # Công tắc, mặc định false. Standby của Multi-AZ KHÔNG phục vụ đọc
  # ("You can't configure the secondary DB instance to accept database read
  # activity") — Multi-AZ là AVAILABILITY, replica mới là TẢI ĐỌC. Báo cáo phải
  # nói đúng hai chuyện đó, đừng gộp.
  multi_az = var.enable_multi_az

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

# ─── READ REPLICA — TÀI NGUYÊN PHÙ DU, MẶC ĐỊNH TẮT ──────────────
# 🔴 `enable_read_replica = false` là DEFAULT AN TOÀN, không phải default tiết
# kiệm. Đọc trước khi bật lần đầu:
#
#   1. AWS: "You can't stop a DB instance that has a read replica, or that is a
#      read replica." Replica PHÁ cơ chế tắt tiền của cả stack. Nó phải được
#      dựng và HUỶ trong cùng một cửa sổ đo, không để qua đêm.
#   2. Cộng với "If you don't manually start your DB instance after it is
#      stopped for seven consecutive days, RDS automatically starts your DB
#      instance for you" — một lần quên huỷ là hoá đơn chạy nhiều ngày.
#   3. Cost guard đã được vá để BÁO chuyện này (findings ⇒ có email) thay vì
#      nuốt nó thành notes. Vá đó phải có TRƯỚC lần bật đầu tiên — nếu không,
#      guard sẽ nói "đã tắt xong" trong khi tiền vẫn chạy. Xem
#      modules/costguard/src/cost_guard.py và tests/test_cost_guard_rds.py CA 2.
#
# THỨ TỰ BẮT BUỘC khi tắt: huỷ replica (enable_read_replica = false + apply)
# TRƯỚC, rồi mới stop primary. Đảo lại thì stop bị AWS từ chối.
#
# Không cần subnet/NACL mới: VPC đã có 2 AZ, db subnet group đã phủ cả hai, và
# NACL db gắn cả hai subnet — replica rơi vào AZ nào cũng đã có đường.
resource "aws_db_instance" "replica" {
  count = var.enable_read_replica ? 1 : 0

  identifier          = "${var.project}-db-tf-replica"
  replicate_source_db = aws_db_instance.this.identifier
  instance_class      = var.replica_instance_class

  # KHÔNG khai username/password/db_name/allocated_storage: replica thừa hưởng
  # tất cả từ primary, và khai lại là apply đỏ. Cũng KHÔNG khai
  # db_subnet_group_name — replica cùng region dùng lại subnet group của nguồn.
  vpc_security_group_ids = [var.rds_sg_id]
  publicly_accessible    = false
  storage_encrypted      = true

  # Replica không tự backup (backup_retention_period = 0 là mặc định của replica).
  skip_final_snapshot = true
  deletion_protection = false

  # Không Performance Insights — cùng lý do như primary.
  performance_insights_enabled = false

  tags = {
    Name = "${var.project}-db-tf-replica"
    # Tag này để status.sh và người đọc console nhìn phát là biết nó không được
    # phép sống qua đêm.
    Lifecycle = "ephemeral-demo-window-only"
  }
}

# ─── SSM PARAMETER STORE (SecureString, miễn phí) ────────────────
resource "aws_ssm_parameter" "db_password" {
  name        = "${local.ssm_prefix}/db-password"
  description = "Master password cua RDS PostgreSQL"
  type        = "SecureString"
  value       = random_password.db.result

  tags = { Name = "${var.project}-db-password" }
}

resource "aws_ssm_parameter" "connection_string" {
  name        = "${local.ssm_prefix}/connection-string"
  description = "Connection string day du, inject vao container qua khoi secrets cua ECS"
  type        = "SecureString"

  # 🔴 `SSL Mode=VerifyFull` LÀ THUỘC TÍNH BẢO MẬT DỄ MẤT NHẤT CỦA CẢ ĐỢT NÀY.
  # Npgsql mặc định `Prefer`: MÃ HOÁ NHƯNG KHÔNG XÁC THỰC CERT. Tụt về mặc định
  # là mất đúng thứ ba dòng comment này đang bảo vệ, và mất IM LẶNG — kết nối vẫn
  # thành công, log vẫn sạch, không có gì đỏ ở đâu cả. `Require` cũng không đủ:
  # ở Npgsql nó vẫn không xác thực chain. Chỉ `VerifyFull` mới vừa xác thực cert
  # vừa kiểm hostname.
  #
  # ĐIỀU KIỆN: cert của RDS do Amazon RDS CA cấp, CA đó KHÔNG có trong trust store
  # mặc định — nên image API (Dockerfile) và image migrator (Dockerfile.migrator)
  # đều cài bundle CA của ap-southeast-1 vào /usr/local/share/ca-certificates/.
  # `Root Certificate` trỏ thẳng vào file đó: khác sqlcmd, Npgsql/libpq KHÔNG đọc
  # trust store của hệ thống, nên `update-ca-certificates` một mình là chưa đủ.
  # Thiếu đường dẫn này thì kết nối GÃY chứ không tụt xuống chế độ kém an toàn —
  # đúng hướng hỏng ta muốn.
  #
  # Chuỗi local dev (devops/docker/docker-compose.multi.yml) dùng `SSL Mode=Disable`
  # vì container postgres không bật TLS. ĐỪNG chép chuỗi đó lên đây.
  #
  # Ba tham số của bản SQL Server KHÔNG TỒN TẠI ở Npgsql và đã biến mất cùng nhau:
  # Encrypt, TrustServerCertificate, MultipleActiveResultSets. Npgsql không cần
  # MARS — nó dùng nhiều kết nối trong pool thay vì nhiều result set trên một.
  value = join("", [
    "Host=${aws_db_instance.this.address};",
    "Port=5432;",
    "Database=${var.db_name};",
    "Username=${var.db_username};",
    "Password=${random_password.db.result};",
    "SSL Mode=VerifyFull;",
    "Root Certificate=/usr/local/share/ca-certificates/rds-ap-southeast-1.crt;",
    "Maximum Pool Size=30;",
    "Minimum Pool Size=2;",
    "Timeout=15;",
  ])

  tags = { Name = "${var.project}-connection-string" }
}

# ─── CONNECTION STRING CHỈ ĐỌC — sống/chết cùng replica ──────────
# ⚠️ HIỆN CHƯA CÓ DÒNG CODE NÀO ĐỌC PARAMETER NÀY, và phải nói thẳng: dựng
# replica lên là có thêm một instance TÍNH TIỀN mà primary KHÔNG được giảm tải
# chút nào. Read replica chỉ có ích khi ứng dụng chủ động lái truy vấn đọc sang
# nó; task definition không khai parameter này trong khối `secrets`, nên hôm nay
# replica là hạ tầng để ĐO và để trình bày trong báo cáo, không phải để tăng
# hiệu năng.
#
# Việc còn thiếu ở tầng app (chưa làm, cố ý): một DbContext thứ hai trỏ vào
# chuỗi này cho các truy vấn `AsNoTracking()`. Kèm hai cái bẫy phải biết trước:
#   1. Replication là BẤT ĐỒNG BỘ — đọc từ replica có thể thấy dữ liệu cũ. Mọi
#      đường "ghi rồi đọc lại để map DTO" PHẢI ở primary, nếu không người dùng
#      lưu xong bấm xem lại và thấy bản cũ.
#   2. `IsActive`/role/quyền TUYỆT ĐỐI không đọc từ replica — cùng lý lẽ với
#      luật "không cache trạng thái phân quyền" ở CLAUDE.md: hướng nguy hiểm là
#      hướng MỞ KHOÁ, và độ trễ replica làm nó xảy ra thật.
#
# Vì sao vẫn tạo parameter: không có nó, cách duy nhất để dùng endpoint replica
# là chép tay một chuỗi CÓ MẬT KHẨU từ `terraform output` vào chỗ khác — đúng
# thứ mà SecureString sinh ra để tránh. Parameter Standard: $0.
resource "aws_ssm_parameter" "replica_connection_string" {
  count = var.enable_read_replica ? 1 : 0

  name        = "${local.ssm_prefix}/connection-string-readonly"
  description = "Connection string tro vao read replica. CHI dung cho truy van doc, KHONG dung cho auth"
  type        = "SecureString"

  # Cùng `SSL Mode=VerifyFull` + Root Certificate như primary: replica dùng cert
  # do cùng RDS CA cấp, nên tụt xuống `Prefer` ở đây cũng mất đúng lớp xác thực
  # ấy — và mất im lặng y hệt. Pool nhỏ hơn primary vì replica chỉ nhận truy vấn
  # đọc, và mỗi kết nối vẫn ăn RAM của một db.t4g.micro.
  value = join("", [
    "Host=${aws_db_instance.replica[0].address};",
    "Port=5432;",
    "Database=${var.db_name};",
    "Username=${var.db_username};",
    "Password=${random_password.db.result};",
    "SSL Mode=VerifyFull;",
    "Root Certificate=/usr/local/share/ca-certificates/rds-ap-southeast-1.crt;",
    "Maximum Pool Size=10;",
    "Minimum Pool Size=0;",
    "Timeout=15;",
  ])

  tags = {
    Name      = "${var.project}-connection-string-readonly"
    Lifecycle = "ephemeral-demo-window-only"
  }
}

resource "aws_ssm_parameter" "jwt_secret" {
  name        = "${local.ssm_prefix}/jwt-secret"
  description = "JwtSettings__SecretKey — 64 ky tu, tren 256-bit entropy"
  type        = "SecureString"
  value       = random_password.jwt.result

  tags = { Name = "${var.project}-jwt-secret" }
}
