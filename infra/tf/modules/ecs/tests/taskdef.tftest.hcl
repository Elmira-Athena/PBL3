provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

mock_provider "aws" {
  alias = "taskdef_baseline"

  mock_data "aws_iam_policy_document" {
    defaults = {
      json = "{\"Version\":\"2012-10-17\",\"Statement\":[]}"
    }
  }
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
  ecr_waf_url               = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-waf"
  vpc_cidr                  = "10.0.0.0/16"
  image_tag                 = "abc123def456abc123def456abc123def456ab12"
  assets_bucket_name        = "hushstore-public-assets"
  allowed_origins           = "https://hushstore.io.vn"
}

run "api_keeps_platform_proxy_defaults" {
  command = plan
  providers = {
    aws = aws.taskdef_baseline
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) :
      alltrue([for e in c.environment : !startswith(e.name, "ProxyHeaders__")])
    ])
    error_message = "The API must retain its default ALB forwarding behavior without proxy-header overrides."
  }
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

# Cổng host 80/8080 nay thuộc container `waf` (sidecar), không thuộc app. Lọc theo
# tên `waf` chứ không anytrue trên mọi container: anytrue sẽ vẫn xanh nếu app giữ
# lại cổng host còn waf thì không — đúng tình huống ALB đi vòng qua WAF.
run "static_host_port_dung_80_va_8080_de_sg_web_giu_dung_2_rule" {
  command = plan

  assert {
    condition = length([
      for c in jsondecode(aws_ecs_task_definition.web.container_definitions) : c
      if c.name == "waf" && anytrue([for p in try(c.portMappings, []) : p.hostPort == 80 && p.containerPort == 8080])
    ]) == 1
    error_message = "Container waf của task web phải map static hostPort 80 → containerPort 8080 — dynamic port mapping sẽ buộc mở dải 32768-65535 trên sg-web."
  }

  assert {
    condition = length([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) : c
      if c.name == "waf" && anytrue([for p in try(c.portMappings, []) : p.hostPort == 8080 && p.containerPort == 8080])
    ]) == 1
    error_message = "Container waf của task api phải map static hostPort 8080 → containerPort 8080."
  }
}

# Bridge mode: `containerPort` mà thiếu `hostPort` KHÔNG có nghĩa là "không mở
# cổng" — ECS cấp một cổng host ĐỘNG (32768-65535). Cổng đó vẫn là cửa sau: ai tới
# được host (container khác, instance khác trong VPC nếu SG nới) gọi thẳng app mà
# không qua WAF. Nên app không được có portMappings nào, kể cả rỗng-hostPort.
run "app_khong_mo_cong_host_de_khong_di_vong_waf" {
  command = plan

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) :
      length(try(c.portMappings, [])) == 0 if c.name == "api"
    ])
    error_message = "Container api KHÔNG được có portMappings. containerPort thiếu hostPort ở bridge mode là cổng host ĐỘNG — vẫn là cửa sau đi vòng WAF."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.web.container_definitions) :
      length(try(c.portMappings, [])) == 0 if c.name == "web"
    ])
    error_message = "Container web KHÔNG được có portMappings. containerPort thiếu hostPort ở bridge mode là cổng host ĐỘNG — vẫn là cửa sau đi vòng WAF."
  }

  # Chốt chống pass rỗng: hai alltrue trên xanh nếu không còn container nào tên
  # api/web (đổi tên). Đòi mỗi task có đúng một container app.
  assert {
    condition = alltrue([
      length([for c in jsondecode(aws_ecs_task_definition.api.container_definitions) : c if c.name == "api"]) == 1,
      length([for c in jsondecode(aws_ecs_task_definition.web.container_definitions) : c if c.name == "web"]) == 1,
    ])
    error_message = "Mỗi task phải có đúng một container app (api / web) — thiếu thì hai assertion trên pass rỗng."
  }
}

run "waf_dung_truoc_app" {
  command = plan

  assert {
    condition = alltrue([
      for pair in [
        { app = "api", backend = "http://api:8080", defs = jsondecode(aws_ecs_task_definition.api.container_definitions) },
        { app = "web", backend = "http://web:80", defs = jsondecode(aws_ecs_task_definition.web.container_definitions) },
        ] : length([
          for c in pair.defs : c if c.name == "waf"
          && contains(try(c.links, []), pair.app)
          && try(c.essential, false) == true
          && length([for d in try(c.dependsOn, []) : d if d.containerName == pair.app && d.condition == "START"]) == 1
          && length([for e in try(c.environment, []) : e if e.name == "BACKEND" && e.value == pair.backend]) == 1
          && length([for e in try(c.environment, []) : e if e.name == "MODSEC_RULE_ENGINE" && e.value == var.waf_rule_engine]) == 1
          && endswith(c.image, ":${var.image_tag}")
          && try(c.logConfiguration.options["awslogs-group"], "") == "/ecs/${var.project}-waf"
      ]) == 1
    ])
    error_message = "Mỗi task api/web phải có ĐÚNG MỘT container `waf`: links tới app, essential, dependsOn app (START), BACKEND trỏ đúng app, MODSEC_RULE_ENGINE = var.waf_rule_engine, image :<image_tag>, log vào /ecs/<project>-waf."
  }
}

run "waf_api_nhan_upload_8mb" {
  command = plan

  assert {
    condition = length([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) : c
      if c.name == "waf"
      && length([for e in try(c.environment, []) : e if e.name == "MODSEC_REQ_BODY_LIMIT" && e.value == "8388608"]) == 1
      && length([for e in try(c.environment, []) : e if e.name == "CLIENT_MAX_BODY_SIZE" && e.value == "8m"]) == 1
    ]) == 1
    error_message = "WAF của api phải cho body tới 8 MB (MODSEC_REQ_BODY_LIMIT=8388608, CLIENT_MAX_BODY_SIZE=8m) — upload ảnh thật tới 5 MB, thấp hơn là chặn nhầm upload hợp lệ."
  }
}

run "waf_rule_engine_sai_gia_tri_bi_tu_choi" {
  command = plan

  variables {
    waf_rule_engine = "Off"
  }

  expect_failures = [var.waf_rule_engine]
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

  # Lọc đúng container `api` (task còn có sidecar `waf`); length == 1 để không
  # pass rỗng nếu container bị đổi tên.
  assert {
    condition = length([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) : c
      if c.name == "api" && try(c.linuxParameters.initProcessEnabled, false) == true
    ]) == 1
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

# ════════════════════════════════════════════════════════════════════════════
# BỘ BA SỐ TẮT ÊM — KHOÁ CHÚNG LẠI VỚI NHAU, KHÔNG KHOÁ TỪNG SỐ
# ════════════════════════════════════════════════════════════════════════════
# Lỗi mà test này tồn tại để chặn ĐÃ XẢY RA THẬT: đợt 4 nâng ShutdownTimeout
# 30 → 45 ở tầng app và KHÔNG nâng hai số Terraform. Kết quả 5 + 45 = 50 > 30,
# tức ECS SIGKILL container giữa lúc .NET còn đang trả nốt request. Không test
# nào bắt được, vì mỗi số riêng lẻ vẫn "hợp lệ".
#
# Nên test này KHÔNG khẳng định `stopTimeout == 90`. Nó khẳng định BẤT ĐẲNG THỨC,
# và đọc cả ba số từ ba nguồn thật — kể cả nguồn nằm ngoài module này và ngoài
# Terraform. Đổi bất kỳ số nào ở bất kỳ đâu mà quên hai số kia thì test đỏ.
run "bo_ba_so_tat_em_phai_thoa_bat_dang_thuc" {
  command = plan

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) :
      c.stopTimeout > (
        tonumber(regex("deregistration_delay = (\\d+)", file("${path.module}/../alb/alb.tf"))[0])
        +
        tonumber(regex("ShutdownTimeout = TimeSpan\\.FromSeconds\\((\\d+)\\)", file("${path.module}/../../../../src/API/Program.cs"))[0])
      )
    ])
    error_message = "VI PHẠM: deregistration_delay + ShutdownTimeout >= stopTimeout của container api. Ba số này là MỘT thay đổi: modules/alb/alb.tf, src/API/Program.cs, và modules/ecs/taskdef.tf. Sửa một số mà bỏ hai số kia thì ECS SIGKILL container giữa lúc .NET đang tắt êm — request đang bay bị cắt, không log nào báo."
  }

  # Trần mức AGENT là trần THỨ HAI và nó thắng: taskdef khai 90 mà
  # ECS_CONTAINER_STOP_TIMEOUT còn 30s thì agent cắt ở 30. Mất đúng thứ vừa sửa,
  # im lặng. Nên nó phải >= số lớn nhất mà taskdef khai.
  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) :
      tonumber(regex("ECS_CONTAINER_STOP_TIMEOUT=(\\d+)s", file("${path.module}/user_data.sh.tftpl"))[0]) >= c.stopTimeout
    ])
    error_message = "ECS_CONTAINER_STOP_TIMEOUT trong user_data.sh.tftpl nhỏ hơn stopTimeout của taskdef. Agent thắng, nên trần thật là số nhỏ hơn và dòng stopTimeout ở taskdef trở thành trang trí."
  }

  # web cũng phải có, không được để mặc định 30.
  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.web.container_definitions) :
      can(c.stopTimeout) && c.stopTimeout >= 90
    ])
    error_message = "Container web không khai stopTimeout ⇒ ECS lấy mặc định 30 giây, và nginx bị giết giữa lúc đang trả file .wasm lớn cho client chậm."
  }
}
