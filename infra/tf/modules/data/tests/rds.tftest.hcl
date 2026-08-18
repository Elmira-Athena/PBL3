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
