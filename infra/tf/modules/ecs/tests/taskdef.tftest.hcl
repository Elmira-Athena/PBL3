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
