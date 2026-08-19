provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project                   = "hushstore-tftest"
  assets_bucket_arn         = "arn:aws:s3:::hushstore-public-assets"
  artifacts_bucket_arn      = "arn:aws:s3:::hushstore-artifacts"
  ssm_connection_string_arn = "arn:aws:ssm:ap-southeast-1:000000000000:parameter/hushstore/prod/connection-string"
  ssm_jwt_secret_arn        = "arn:aws:ssm:ap-southeast-1:000000000000:parameter/hushstore/prod/jwt-secret"
  ssm_db_password_arn       = "arn:aws:ssm:ap-southeast-1:000000000000:parameter/hushstore/prod/db-password"
  ecr_seeder_url            = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-seeder"
  seeder_image_tag          = "0123456789abcdef0123456789abcdef01234567"
  rds_host                  = "hushstore-db-tf.abcdefghijkl.ap-southeast-1.rds.amazonaws.com"
  db_username               = "hushadmin"
  app_subnet_ids            = ["subnet-00000000000000001", "subnet-00000000000000002"]
  web_sg_id                 = "sg-00000000000000000"
  instance_count            = 1
  instance_type             = "t3.micro"
  ecr_api_url               = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-api"
  ecr_web_url               = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-web"
  ecr_migrator_url          = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-migrator"
  image_tag                 = "abc123def456abc123def456abc123def456ab12"
  assets_bucket_name        = "hushstore-public-assets"
  allowed_origins           = "https://hushstore.io.vn"
}

run "tat_ca_task_def_dung_bridge_va_ec2_launch_type" {
  command = plan

  assert {
    condition = alltrue([
      aws_ecs_task_definition.api.network_mode == "bridge",
      aws_ecs_task_definition.web.network_mode == "bridge",
      aws_ecs_task_definition.migrator.network_mode == "bridge",
    ])
    error_message = "Phải dùng bridge network mode: awsvpc cấp 1 ENI cho mỗi task, t3.micro chỉ có 2 ENI nên không đủ cho 2 service."
  }

  assert {
    condition = alltrue([
      contains(aws_ecs_task_definition.api.requires_compatibilities, "EC2"),
      contains(aws_ecs_task_definition.web.requires_compatibilities, "EC2"),
      contains(aws_ecs_task_definition.migrator.requires_compatibilities, "EC2"),
    ])
    error_message = "Phải là EC2 launch type — đề bài yêu cầu triển khai website thông qua EC2 Instance, không phải Fargate."
  }
}

run "static_host_port_dung_80_va_8080_de_sg_web_giu_dung_2_rule" {
  command = plan

  assert {
    condition = anytrue([
      for c in jsondecode(aws_ecs_task_definition.web.container_definitions) :
      anytrue([for p in c.portMappings : p.hostPort == 80 && p.containerPort == 80])
    ])
    error_message = "Container web phải map static hostPort 80 — dynamic port mapping sẽ buộc mở dải 32768-65535 trên sg-web."
  }

  assert {
    condition = anytrue([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) :
      anytrue([for p in c.portMappings : p.hostPort == 8080 && p.containerPort == 8080])
    ])
    error_message = "Container API phải map static hostPort 8080."
  }
}

run "khong_container_nao_nhan_secret_qua_environment" {
  command = plan

  assert {
    condition = alltrue([
      for c in concat(
        jsondecode(aws_ecs_task_definition.api.container_definitions),
        jsondecode(aws_ecs_task_definition.web.container_definitions),
        jsondecode(aws_ecs_task_definition.migrator.container_definitions),
        ) : alltrue([
          for e in try(c.environment, []) :
          !anytrue([
            # So sánh không phân biệt hoa thường (lower()) và mở rộng danh sách
            # keyword: PascalCase thuần trước đây bỏ lọt AWS_SECRET_ACCESS_KEY
            # hay SA_PASSWORD_HASH — chính dạng credential tĩnh mà task role
            # (iam.tf) được dựng ra để thay thế.
            for kw in ["password", "pwd", "secret", "key", "token", "connectionstring"] :
            strcontains(lower(e.name), kw)
          ])
      ])
    ])
    error_message = "Secret KHÔNG được truyền qua environment (hiện trong describe-task-definition) — phải dùng khối secrets với valueFrom."
  }
}

run "migrator_nhan_ca_connection_string_va_jwt_secret" {
  command = plan

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.migrator.container_definitions) :
      length([for s in c.secrets : s.name]) == 2
    ])
    error_message = "Migrator phải nhận ĐÚNG 2 secret."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.migrator.container_definitions) :
      contains([for s in c.secrets : s.name], "JwtSettings__SecretKey")
    ])
    error_message = "Migrator PHẢI có JwtSettings__SecretKey: efbundle chạy lại entry point của API, và Program.cs throw nếu thiếu nó (dòng đó nằm trước builder.Build())."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.migrator.container_definitions) :
      contains([for s in c.secrets : s.name], "ConnectionStrings__DefaultConnection")
    ])
    error_message = "Migrator phải có ConnectionStrings__DefaultConnection."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.migrator.container_definitions) :
      length(try(c.portMappings, [])) == 0
    ])
    error_message = "Migrator không được map port nào — nó là one-off task, không phục vụ request."
  }
}

run "image_tag_khong_bao_gio_la_latest" {
  command = plan

  assert {
    condition = alltrue([
      for c in concat(
        jsondecode(aws_ecs_task_definition.api.container_definitions),
        jsondecode(aws_ecs_task_definition.web.container_definitions),
        jsondecode(aws_ecs_task_definition.migrator.container_definitions),
      ) : !endswith(c.image, ":latest")
    ])
    error_message = "Image tag phải là git SHA, không được dùng :latest — nếu không thì rollback về revision cũ sẽ không đáng tin."
  }
}

run "api_bat_ecs_exec_va_co_task_role_rieng" {
  command = plan

  # KHÔNG so task_role_arn với aws_iam_role.x.arn trực tiếp — cả hai là (known
  # after apply), so sánh hai giá trị unknown làm Terraform báo "Unknown
  # condition value" và bỏ luôn các run còn lại trong file.
  # override_resource với override_during = plan làm arn của role BIẾT ĐƯỢC ở
  # plan-time (giá trị giả cố định), nhờ đó so sánh task_role_arn của API
  # không còn là unknown nữa — assert được đúng cái tên run block đã hứa.
  override_resource {
    target          = aws_iam_role.task_app
    override_during = plan
    values = {
      arn = "arn:aws:iam::000000000000:role/hushstore-tftest-task-app-role"
    }
  }

  assert {
    condition     = aws_ecs_task_definition.api.task_role_arn == "arn:aws:iam::000000000000:role/hushstore-tftest-task-app-role"
    error_message = "Task definition API PHẢI gắn task-app-role: không có nó thì SDK trong container không lấy được credential tạm thời, upload S3 và ECS Exec đều chết."
  }

  # Web vẫn giữ tính chất biết-ở-plan-time không cần override: container web
  # KHÔNG được gán task role nào cả.
  assert {
    condition     = aws_ecs_task_definition.web.task_role_arn == null || aws_ecs_task_definition.web.task_role_arn == ""
    error_message = "Task definition web KHÔNG được có task_role_arn — nginx serve static file, không gọi AWS API nào."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) :
      try(c.linuxParameters.initProcessEnabled, false) == true
    ])
    error_message = "Phải bật initProcessEnabled trên container API để ECS Exec hoạt động (kịch bản kiểm thử số 10)."
  }
}

run "seeder_khong_co_task_role_va_mat_khau_di_qua_secrets" {
  command = plan

  # Seeder chỉ mở TCP tới RDS, không gọi API AWS nào — nên KHÔNG cấp task role.
  # Không có role thì blast radius bằng 0 nếu image bị chiếm.
  # aws_ecs_task_definition.task_role_arn là Optional+Computed nên KHÔNG assert
  # được `== null` ở plan-time (nó unknown ngay cả khi config không đặt). Kiểm
  # bằng cách khác: cắt đúng block resource seeder ra khỏi file .tf rồi đòi block
  # đó không chứa task_role_arn.
  #
  # LẦN ĐẦU TÔI VIẾT ASSERTION NÀY BẰNG `[^}]*` VÀ NÓ RỖNG: `"${var.project}-seeder"`
  # có dấu } ngay dòng đầu của block, nên `[^}]*` dừng ở đó và không bao giờ chạm
  # tới task_role_arn — thêm task_role_arn vào vẫn pass. Phải dùng (?s) với .*?
  # để cắt tới dấu } ở đầu dòng.
  #
  # Assertion đầu bắt buộc phải có: nếu regex không tìm thấy block nào thì
  # assertion thứ hai pass RỖNG. Đòi đúng 1 block trước, rồi mới kiểm nội dung.
  assert {
    condition = length(regexall(
      "(?s)resource \"aws_ecs_task_definition\" \"seeder\" \\{.*?\n\\}",
      file("${path.module}/taskdef.tf")
    )) == 1
    error_message = "Không cắt được đúng 1 block resource aws_ecs_task_definition.seeder từ taskdef.tf — assertion sau sẽ pass rỗng nên phải sửa regex này trước."
  }

  # Phải lọc bỏ dòng COMMENT trước khi kiểm: chính block seeder có comment
  # "KHÔNG có task_role_arn: ..." giải thích lý do, nên strcontains trên cả block
  # sẽ khớp vào comment đó và assertion đỏ oan. (Đúng cùng bài học với assertion
  # chống deadlock trong tests/cluster.tftest.hcl.)
  assert {
    condition = length([
      for l in flatten([
        for blk in regexall(
          "(?s)resource \"aws_ecs_task_definition\" \"seeder\" \\{.*?\n\\}",
          file("${path.module}/taskdef.tf")
        ) : split("\n", blk)
      ]) : l if !startswith(trimspace(l), "#") && strcontains(l, "task_role_arn")
    ]) == 0
    error_message = "Task definition seeder KHÔNG được có task_role_arn — nó chỉ nói chuyện TCP với RDS, không gọi API AWS nào."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.seeder.container_definitions) :
      length([for s in c.secrets : s if s.name == "DB_PASSWORD" && s.valueFrom == var.ssm_db_password_arn]) == 1
    ])
    error_message = "DB_PASSWORD phải đi qua khối `secrets` trỏ vào SSM db-password — không được đặt trong `environment`."
  }

  # Ba giá trị không bí mật đi qua environment. Assert đủ cả ba, và assert
  # environment KHÔNG chứa gì tên giống mật khẩu.
  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.seeder.container_definitions) :
      toset([for e in c.environment : e.name]) == toset(["DB_HOST", "DB_NAME", "DB_USER"])
    ])
    error_message = "environment của seeder phải đúng 3 biến DB_HOST/DB_NAME/DB_USER — thêm biến thứ tư vào đây là lối để mật khẩu lọt ra ngoài `secrets`."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.seeder.container_definitions) :
      length(c.portMappings) == 0
    ])
    error_message = "Seeder là one-off task, không được map port nào."
  }
}
