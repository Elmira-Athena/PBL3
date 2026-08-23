provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project        = "hushstore-tftest"
  db_subnet_ids  = ["subnet-00000000000000001", "subnet-00000000000000002"]
  rds_sg_id      = "sg-00000000000000000"
  engine_version = "16.00.4210.1.v1"
  db_username    = "dbadmin"
  db_name        = "HushStoreDB"
}

run "rds_khong_bao_gio_public_accessible" {
  command = plan

  assert {
    condition     = aws_db_instance.this.publicly_accessible == false
    error_message = "RDS TUYỆT ĐỐI không được publicly_accessible — đây là lớp phòng thủ đầu tiên cho kịch bản kiểm thử số 3."
  }

  assert {
    condition     = aws_db_instance.this.multi_az == false
    error_message = "SQL Server Express không hỗ trợ Multi-AZ, và Multi-AZ nằm ngoài free tier."
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
    error_message = "RDS phải dùng đúng sg-rds (chỉ nhận 1433 từ sg-web)."
  }
}

# KHÔNG assert `aws_db_instance.this.db_name == null`: attribute này là
# Optional+Computed trong AWS provider, nên ở plan-time nó là (known after apply)
# NGAY CẢ KHI config không đặt nó — Terraform báo `Unknown condition value` và bỏ
# luôn các run còn lại. Việc "không đặt db_name" được bảo đảm bằng chính việc
# main.tf không có argument đó (SQL Server không hỗ trợ), kiểm bằng grep; còn
# database HushStoreDB do EF Core migration bundle tạo ở Task 13.
# Thay bằng các thuộc tính đặc thù SQL Server mà plan-time biết được.
run "rds_dung_cau_hinh_sql_server_express" {
  command = plan

  assert {
    condition     = aws_db_instance.this.engine == "sqlserver-ex"
    error_message = "Phải dùng engine sqlserver-ex (Express) — đây là edition nằm trong free tier."
  }

  assert {
    condition     = aws_db_instance.this.license_model == "license-included"
    error_message = "SQL Server trên RDS bắt buộc license_model = license-included."
  }

  assert {
    condition = alltrue([
      aws_db_instance.this.storage_encrypted == true,
      aws_db_instance.this.allocated_storage == 20,
      aws_db_instance.this.storage_type == "gp2",
    ])
    error_message = "Storage phải mã hoá, 20GB, gp2 — mức tối thiểu của SQL Server Express và nằm trong free tier."
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
