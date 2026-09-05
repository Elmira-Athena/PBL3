provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project        = "hushstore-tftest"
  db_subnet_ids  = ["subnet-00000000000000001", "subnet-00000000000000002"]
  rds_sg_id      = "sg-00000000000000000"
  engine_version = "17"
  db_username    = "dbadmin"
  db_name        = "HushStoreDB"
}

run "rds_khong_bao_gio_public_accessible" {
  command = plan

  assert {
    condition     = aws_db_instance.this.publicly_accessible == false
    error_message = "RDS TUYỆT ĐỐI không được publicly_accessible — đây là lớp phòng thủ đầu tiên cho kịch bản kiểm thử số 3."
  }

  # ⚠️ ASSERT NÀY ĐÃ ĐỔI HỢP ĐỒNG, không chỉ đổi giá trị. Bản cũ đòi
  # `multi_az == false` với lý do "SQL Server Express không hỗ trợ Multi-AZ" —
  # TIỀN ĐỀ ĐÓ BIẾN MẤT cùng engine. Giữ nguyên câu cũ là để lại một assert xanh
  # vì lý do sai, thứ nguy hiểm hơn một assert đỏ.
  # Hợp đồng mới: Multi-AZ phải do người ta CỐ Ý bật, không bao giờ tự bật.
  assert {
    condition     = aws_db_instance.this.multi_az == var.enable_multi_az
    error_message = "multi_az phải bám đúng var.enable_multi_az — hard-code lại là tước mất công tắc."
  }

  # ⚠️ ASSERT NÀY NÓI VỀ DEFAULT CỦA MODULE, KHÔNG VỀ HẠ TẦNG ĐANG CHẠY.
  # envs/prod GHIM enable_multi_az = true (xem envs/prod/variables.tf) — đó là
  # kiến trúc đã chốt. Module thì phải giữ default false, vì nó là thư viện: một
  # env khác dùng lại nó không được tự nhiên bị Multi-AZ. Hợp đồng hai tầng:
  # module an toàn theo mặc định, env quyết định kiến trúc.
  #
  # Câu lỗi cũ ghi "chỉ bật trong cửa sổ demo" — nay SAI, vì prod bật vĩnh viễn.
  # Sửa câu chữ chứ không sửa điều kiện: điều kiện vẫn là bất biến đúng.
  assert {
    condition     = var.enable_multi_az == false
    error_message = "MODULE phải mặc định false để env khác dùng lại không tự bị Multi-AZ. Muốn bật thì ghim ở env (envs/prod đã ghim true), đừng đổi default ở đây."
  }
}

run "read_replica_mac_dinh_tat_va_khong_duoc_dung_len" {
  command = plan

  # 🔴 Đây là DEFAULT AN TOÀN, không phải default tiết kiệm. Có replica thì AWS
  # từ chối StopDBInstance ⇒ toàn bộ cơ chế tắt tiền (down.sh + cost guard) mất
  # tác dụng, và RDS còn tự khởi động lại sau 7 ngày stopped. Một lần quên huỷ
  # là hoá đơn chạy im lặng nhiều ngày.
  assert {
    condition     = var.enable_read_replica == false
    error_message = "enable_read_replica phải mặc định false — bật nó là vô hiệu hoá cơ chế tắt tiền của cả stack."
  }

  assert {
    condition     = length(aws_db_instance.replica) == 0
    error_message = "Khi enable_read_replica = false thì KHÔNG được dựng replica nào."
  }
}

run "rds_nam_trong_db_subnet_va_dung_sg_rds" {
  command = plan

  assert {
    condition     = length(aws_db_subnet_group.this.subnet_ids) == 2
    error_message = "DB subnet group phải có 2 subnet ở 2 AZ — yêu cầu của RDS."
  }

  assert {
    condition     = contains(aws_db_instance.this.vpc_security_group_ids, var.rds_sg_id)
    error_message = "RDS phải dùng đúng sg-rds (chỉ nhận 5432 từ sg-web)."
  }
}

# ⚠️ ĐẢO NGƯỢC SO VỚI BẢN SQL SERVER. Chỗ này trước đây là một đoạn dài giải
# thích vì sao KHÔNG assert `db_name` — vì SQL Server không nhận argument đó.
# PostgreSQL thì NGƯỢC LẠI: nó tạo database ngay lúc create instance, nên db_name
# BẮT BUỘC phải được đặt. Bỏ trống thì RDS tạo một DB tên `postgres`, migration
# và seed đều chạy được ở đó, KHÔNG có gì đỏ — hệ thống chỉ đơn giản sống trong
# một database mang tên sai.
#
# Vẫn không assert được `db_name` ở plan-time (Optional+Computed ⇒ unknown, và
# `Unknown condition value` sẽ bỏ luôn các run còn lại). Nên kiểm bằng cách khác:
# đòi chính dòng `db_name` có mặt trong main.tf, lọc bỏ dòng comment.
run "rds_dung_cau_hinh_postgres" {
  command = plan

  assert {
    condition     = aws_db_instance.this.engine == "postgres"
    error_message = "Phải dùng engine postgres — sqlserver-ex không mở được read replica (đòi Enterprise Edition + >= 4 vCPU)."
  }

  # 🚨 PHẢI dùng startswith, KHÔNG dùng strcontains. Lần đầu tôi viết assert này
  # bằng `strcontains(l, "db_name")` VÀ NÓ XANH CẢ KHI ĐÃ XOÁ DÒNG db_name — vì
  # chuỗi kết nối có dòng `"Database=${var.db_name};",` cũng chứa "db_name".
  # Assert xanh vì lý do sai nguy hiểm hơn không có assert: nó bảo chuyện đã được
  # canh. Đo bằng cách xoá thật dòng đó rồi chạy lại, không bằng cách đọc.
  assert {
    condition = length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if startswith(trimspace(l), "db_name")
    ]) == 1
    error_message = "main.tf phải đặt argument db_name: PostgreSQL tạo database lúc create instance, bỏ trống thì RDS tạo DB tên `postgres`, migration và seed vẫn chạy được ở đó — hệ thống sống trong một database mang tên sai mà KHÔNG có gì đỏ."
  }

  assert {
    condition = length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "license_model")
    ]) == 0
    error_message = "PostgreSQL là engine open-source — KHÔNG được khai license_model."
  }

  assert {
    condition = alltrue([
      aws_db_instance.this.storage_encrypted == true,
      aws_db_instance.this.allocated_storage == 20,
      aws_db_instance.this.storage_type == "gp3",
    ])
    error_message = "Storage phải mã hoá, 20GB, gp3."
  }
}

# Chốt cho thuộc tính bảo mật DỄ MẤT NHẤT của đợt 7. Npgsql mặc định `Prefer` —
# mã hoá nhưng KHÔNG xác thực cert — nên tụt về mặc định là mất im lặng đúng thứ
# đang được bảo vệ: kết nối vẫn thành công, log vẫn sạch. `Require` cũng không đủ.
run "chuoi_ket_noi_phai_giu_ssl_mode_verifyfull" {
  command = plan

  # ⚠️ KHÔNG assert được trên `aws_ssm_parameter.connection_string.value`: nó phụ
  # thuộc `aws_db_instance.this.address` (known after apply) nên ở plan-time là
  # unknown, và Terraform sẽ đỏ với "Condition expression could not be evaluated
  # at this time" — một cái đỏ nói về THỜI ĐIỂM chứ không nói về nội dung.
  # Kiểm trên MÃ NGUỒN thay vì trên giá trị. Phải lọc bỏ dòng comment, vì ngay
  # phía trên chuỗi trong main.tf có một khối comment dài nhắc tên các tham số
  # này — không lọc thì assert xanh nhờ chính lời giải thích của nó.
  # 🚨 BA ASSERT DƯỚI ĐÂY TỪNG VIẾT `== 1` VÀ ĐÃ ĐỎ ĐÚNG LÚC CẦN ĐỎ.
  # Chúng đếm số dòng trong main.tf, nên `== 1` ngầm khẳng định "cả module chỉ có
  # MỘT chuỗi kết nối". Điều đó đúng cho tới khi read replica có chuỗi chỉ-đọc
  # riêng — lúc đó `== 1` đỏ, và cái đỏ ấy KHÔNG nói "bảo mật hỏng", nó nói "giả
  # định về số lượng đã cũ". Nếu sửa bằng cách đổi thành `== 2` thì đúng hôm nay
  # và rỗ lại ở chuỗi thứ ba.
  #
  # Bất biến THẬT không phụ thuộc số lượng: MỌI chuỗi kết nối đều phải có
  # VerifyFull, có Root Certificate, và dùng 5432. Diễn đạt bằng phép SO SÁNH
  # GIỮA CÁC SỐ ĐẾM thay vì bằng một hằng số.
  assert {
    condition = length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "SSL Mode=VerifyFull")
    ]) >= 1
    error_message = "Phải có ít nhất một chuỗi kết nối mang SSL Mode=VerifyFull. Npgsql mặc định Prefer — mã hoá nhưng KHÔNG xác thực cert, tức mất im lặng đúng thứ đang được bảo vệ. Require cũng không đủ."
  }

  # Mỗi chuỗi có VerifyFull phải có đúng một Root Certificate đi kèm, và mỗi
  # chuỗi phải dùng 5432. Hai phép so dưới đây tự đúng với 1, 2 hay N chuỗi.
  assert {
    condition = length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "Root Certificate=")
      ]) == length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "SSL Mode=VerifyFull")
    ])
    error_message = "Số dòng `Root Certificate=` phải BẰNG số chuỗi VerifyFull — một chuỗi có VerifyFull mà thiếu Root Certificate là chuỗi sẽ GÃY lúc kết nối (Npgsql/libpq KHÔNG đọc trust store hệ thống)."
  }

  assert {
    condition = length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "Port=5432")
      ]) == length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "SSL Mode=VerifyFull")
    ])
    error_message = "Mỗi chuỗi kết nối phải dùng cổng 5432 của PostgreSQL — sót một chuỗi còn 1433 là tàn dư SQL Server."
  }

  assert {
    condition = length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "SSL Mode=") && !strcontains(l, "SSL Mode=VerifyFull")
    ]) == 0
    error_message = "Có một `SSL Mode=` khác VerifyFull trong main.tf — đó là đường tụt xuống mức không xác thực cert."
  }
}

# ─────────────────────────────────────────────────────────────────
# Đây là run block canh một RÀNG BUỘC LIÊN MODULE mà Terraform không nối được:
# cost guard (modules/costguard) chạy 00:00 ICT = 17:00 UTC và chỉ stop được RDS
# khi status là `available`. Nếu backup hay maintenance window chồng giờ đó thì
# instance ở `backing-up`/`modifying`, AWS từ chối StopDBInstance, và guard trả
# về THÀNH CÔNG trong lúc DB tính $2,35/ngày — mỗi đêm, không phải ngẫu nhiên
# một đêm. Không có gì khác trong dự án canh chỗ này: fmt, validate và plan đều
# xanh với một cửa sổ 17:00 UTC.
# ─────────────────────────────────────────────────────────────────
run "cua_so_backup_va_maintenance_khong_cham_gio_chay_cua_cost_guard" {
  command = plan

  # Để trống hai field này KHÔNG phải là "mặc định an toàn": AWS tự bốc ngẫu
  # nhiên trong khối 14:00-22:00 UTC của ap-southeast-1, và bốc lại mỗi lần
  # instance được tạo lại.
  assert {
    condition = alltrue([
      length(aws_db_instance.this.backup_window) > 0,
      length(aws_db_instance.this.maintenance_window) > 0,
    ])
    error_message = "backup_window và maintenance_window phải khai TƯỜNG MINH. Để trống thì AWS tự gán ngẫu nhiên trong khối 14:00-22:00 UTC của ap-southeast-1 — khối đó chứa 17:00 UTC, tức giờ cost guard chạy, và mỗi lần instance được tạo lại là một lần bốc thăm mới."
  }

  # Dải chặn là 16, 17, 18 giờ UTC chứ không chỉ đúng 17: cửa sổ backup tối
  # thiểu 30 phút nên một cửa sổ bắt đầu 16:45 vẫn trùm qua 17:00, và Scheduler
  # được phép giao muộn tới 3600 giây (maximum_event_age_in_seconds).
  assert {
    condition     = !can(regex("^1[678]:", aws_db_instance.this.backup_window))
    error_message = "backup_window KHÔNG được bắt đầu trong khoảng 16:00-18:59 UTC — đó là dải bao quanh 17:00 UTC, giờ cost guard chạy (00:00 ICT). Trong lúc RDS ở 'backing-up' thì StopDBInstance trả InvalidDBInstanceState nhưng instance vẫn tính đủ $0,098/giờ, nên một cửa sổ trùng giờ làm guard mất tác dụng mỗi đêm. Nếu bạn vừa đổi stop_cron của module costguard: đổi cả giá trị này và cả maintenance_window."
  }

  assert {
    condition     = !can(regex("^[a-z]{3}:1[678]:", aws_db_instance.this.maintenance_window))
    error_message = "maintenance_window KHÔNG được bắt đầu trong khoảng 16:00-18:59 UTC — cùng lý do như backup_window. Maintenance đưa instance vào 'modifying'/'upgrading', và ở hai trạng thái đó guard cũng không stop được trong khi tiền vẫn chạy."
  }
}

run "rds_co_the_destroy_duoc_de_phuc_vu_nuke_sh" {
  command = plan

  assert {
    condition     = aws_db_instance.this.deletion_protection == false
    error_message = "deletion_protection phải false, nếu không nuke.sh sẽ treo."
  }
}

run "secret_dung_ssm_securestring_khong_dung_secrets_manager" {
  command = plan

  assert {
    condition = alltrue([
      aws_ssm_parameter.db_password.type == "SecureString",
      aws_ssm_parameter.connection_string.type == "SecureString",
      aws_ssm_parameter.jwt_secret.type == "SecureString",
    ])
    error_message = "Cả 3 parameter phải là SecureString — Parameter Store miễn phí, Secrets Manager tốn $0.40/secret/tháng."
  }

  assert {
    condition = alltrue([
      startswith(aws_ssm_parameter.db_password.name, "/hushstore-tftest/prod/"),
      startswith(aws_ssm_parameter.connection_string.name, "/hushstore-tftest/prod/"),
      startswith(aws_ssm_parameter.jwt_secret.name, "/hushstore-tftest/prod/"),
    ])
    error_message = "Parameter phải nằm dưới cùng một path prefix để IAM policy giới hạn được bằng wildcard."
  }
}

run "mat_khau_du_dai_va_khong_chua_ky_tu_rds_cam" {
  command = plan

  assert {
    condition     = random_password.db.length >= 24
    error_message = "Mật khẩu DB phải tối thiểu 24 ký tự."
  }

  assert {
    condition     = random_password.jwt.length >= 48
    error_message = "JWT secret phải tối thiểu 48 ký tự để đủ 256-bit entropy."
  }
}

run "bat_replica_thi_dung_len_dung_mot_cai_va_co_chuoi_chi_doc" {
  command = plan

  variables {
    enable_read_replica = true
  }

  assert {
    condition     = length(aws_db_instance.replica) == 1
    error_message = "enable_read_replica = true phải dựng đúng 1 replica."
  }

  assert {
    condition     = aws_db_instance.replica[0].replicate_source_db == aws_db_instance.this.identifier
    error_message = "Replica phải nhân bản từ primary của chính stack này, không phải identifier viết cứng."
  }

  # Replica KHÔNG thừa hưởng publicly_accessible và storage_encrypted theo cách
  # hiển nhiên — phải khai. Bỏ sót là dựng một bản sao TOÀN BỘ dữ liệu ra
  # internet, và đó là bản sao mà kịch bản kiểm thử số 3 không hề nhắm tới.
  assert {
    condition     = aws_db_instance.replica[0].publicly_accessible == false
    error_message = "Replica TUYỆT ĐỐI không được publicly_accessible — nó chứa đúng dữ liệu như primary."
  }

  assert {
    condition     = aws_db_instance.replica[0].storage_encrypted == true
    error_message = "Replica phải mã hoá at-rest như primary."
  }

  # ĐIỀU KIỆN TIÊN QUYẾT DỄ MẤT NHẤT: read replica đòi primary có backup tự động
  # (backup_retention_period > 0). Hạ nó về 0 để "tiết kiệm" là hợp lý ở mọi góc
  # nhìn khác — backup miễn phí tới mức bằng allocated_storage nên chẳng tiết
  # kiệm được gì — nhưng nó làm việc DỰNG replica đỏ ở giữa apply, với một câu
  # lỗi của AWS không nhắc gì tới replica. Assert này bắt trước lúc plan.
  assert {
    condition     = aws_db_instance.this.backup_retention_period > 0
    error_message = "Read replica ĐÒI primary bật backup tự động: backup_retention_period phải > 0. Đặt 0 thì replica không dựng được, và thông báo lỗi của AWS không nói vì sao."
  }

  # Chuỗi chỉ-đọc chỉ tồn tại khi có replica, và phải giữ nguyên lớp xác thực
  # cert. Tụt về `Prefer` ở đây mất đúng thứ mà chuỗi primary đang bảo vệ.
  assert {
    condition     = length(aws_ssm_parameter.replica_connection_string) == 1
    error_message = "Bật replica phải sinh kèm connection string chỉ-đọc, nếu không endpoint chỉ nằm trong terraform output."
  }

  assert {
    condition     = aws_ssm_parameter.replica_connection_string[0].type == "SecureString"
    error_message = "Chuỗi chỉ-đọc chứa mật khẩu master nên phải là SecureString."
  }

  # ⚠️ KHÔNG assert được nội dung `.value`: nó nội suy
  # `aws_db_instance.replica[0].address` (known after apply) nên ở plan-time là
  # unknown, và Terraform đỏ với "Unknown condition value" — một cái đỏ nói về
  # THỜI ĐIỂM, không nói về nội dung. Cùng lý do như chuỗi primary ở run block
  # `chuoi_ket_noi_phai_giu_ssl_mode_verifyfull`, nên dùng cùng cách: kiểm trên
  # MÃ NGUỒN. Bất biến VerifyFull/Root Certificate/5432 của chuỗi này đã được
  # run block đó phủ, vì nó đếm trên toàn bộ main.tf.
  #
  # Còn lại đúng một điều nó không phủ: chuỗi chỉ-đọc phải trỏ vào REPLICA. Chép
  # nhầm host sang primary là lỗi IM LẶNG TUYỆT ĐỐI — mọi truy vấn vẫn đúng, chỉ
  # là replica không nhận tải nào trong khi vẫn tính tiền đủ.
  assert {
    condition = length([
      for l in split("\n", file("${path.module}/main.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "aws_db_instance.replica[0].address")
    ]) == 1
    error_message = "Chuỗi chỉ-đọc phải nội suy aws_db_instance.replica[0].address. Không thấy dòng nào ⇒ nó đang trỏ vào primary và replica là tiền bỏ đi."
  }
}
