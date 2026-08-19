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

run "asg_gioi_han_dung_1_instance" {
  command = plan

  assert {
    condition     = aws_autoscaling_group.this.max_size == 1
    error_message = "max_size phải = 1: rate limiter là in-memory nên 2 task API sẽ làm giới hạn 5 req/phút thành 10."
  }

  assert {
    condition     = aws_autoscaling_group.this.min_size == 0
    error_message = "min_size phải = 0 để down.sh hạ về 0 instance, xoá luôn EBS root và về $0 thật."
  }

  assert {
    condition     = length(aws_autoscaling_group.this.vpc_zone_identifier) == 2
    error_message = "ASG phải trải trên 2 app subnet ở 2 AZ."
  }
}

run "launch_template_khong_gan_ssh_key_va_bat_imdsv2" {
  command = plan

  assert {
    condition     = aws_launch_template.this.key_name == null || aws_launch_template.this.key_name == ""
    error_message = "Launch template TUYỆT ĐỐI không được gắn SSH key pair — admin access chỉ qua SSM Session Manager."
  }

  assert {
    condition     = aws_launch_template.this.metadata_options[0].http_tokens == "required"
    error_message = "Phải bắt buộc IMDSv2 (http_tokens = required) để chặn SSRF đọc credential từ metadata service."
  }

  assert {
    condition     = aws_launch_template.this.metadata_options[0].http_put_response_hop_limit == 1
    error_message = "hop_limit = 1 để container không tự gọi được metadata của host."
  }
}

run "launch_template_nam_trong_sg_web_va_dung_instance_profile" {
  command = plan

  assert {
    condition     = contains(aws_launch_template.this.vpc_security_group_ids, var.web_sg_id)
    error_message = "Container instance phải dùng sg-web (chỉ nhận traffic từ sg-alb)."
  }

  assert {
    condition     = aws_launch_template.this.iam_instance_profile[0].name == aws_iam_instance_profile.instance.name
    error_message = "Launch template phải gắn instance profile của container-instance-role."
  }
}

run "capacity_provider_tat_managed_scaling_va_termination_protection" {
  command = plan

  assert {
    condition     = aws_ecs_capacity_provider.this.auto_scaling_group_provider[0].managed_termination_protection == "DISABLED"
    error_message = "managed_termination_protection phải DISABLED, nếu không capacity provider không xoá được và nuke.sh sẽ treo."
  }

  assert {
    condition     = aws_ecs_capacity_provider.this.auto_scaling_group_provider[0].managed_scaling[0].status == "DISABLED"
    error_message = "managed_scaling phải DISABLED: max_size = 1 nên không có gì để scale, và bật lên sẽ tranh desired_capacity với Terraform."
  }
}

run "user_data_dung_thu_tu_va_khong_tu_khoi_dong_ecs_agent" {
  command = plan

  # Assertion 1 — CHỐNG DEADLOCK, là bug thật đã làm hai instance không đăng ký
  # được vào cluster. `ecs.service` có `After=cloud-final.service`, mà user_data
  # chạy trong cloud-final; gọi `systemctl start ecs` (hay tương đương như
  # `service ecs start`) từ đây thì bên chờ unit active, unit chờ cloud-final,
  # cloud-final chờ user_data → treo vô hạn.
  # Không thể assert bằng `!strcontains(user_data, "systemctl")` vì template có
  # một khối comment giải thích chính điều này (và comment đó PHẢI được giữ).
  # Nên bỏ comment ra trước rồi mới kiểm: chỉ dòng lệnh thật bị soi.
  # Bắt cả token "systemctl" lẫn các cách gọi tương đương thực sự có người sẽ
  # viết: "service ecs ..." và "... start ecs" (kể cả qua biến, vd `$CMD start
  # ecs`). Không cố bắt mọi khả năng lý thuyết (heredoc, drop-in unit) — chỉ
  # chặn những dạng một người sẽ thật sự gõ tay.
  assert {
    condition = length([
      for l in split("\n", base64decode(aws_launch_template.this.user_data)) :
      l if !startswith(trimspace(l), "#") && (
        strcontains(l, "systemctl") ||
        strcontains(l, "service ecs") ||
        strcontains(l, "start ecs")
      )
    ]) == 0
    error_message = "user_data KHÔNG được khởi động ecs agent thủ công (systemctl, service ecs, ... start ecs): ecs.service order sau cloud-final.service, nên start nó từ trong cloud-init tạo deadlock và instance sẽ không bao giờ đăng ký vào cluster. AMI ECS-optimized tự khởi động agent sau khi cloud-init xong."
  }

  # Assertion 2 — kiểm ĐÚNG đích ghi (/etc/ecs/ecs.config), không chỉ nội dung.
  # Reviewer đã chứng minh: đổi đích thành /etc/ecs/ecs.conf (thiếu 1 chữ "g")
  # thì agent không bao giờ đọc được ECS_CLUSTER, instance không đăng ký vào
  # cluster — mà assertion "ECS_CLUSTER=..." bên dưới vẫn pass, vì chuỗi đó vẫn
  # có mặt, chỉ là bị ghi sai file. Assertion này cũng là anchor bắt buộc phải
  # tồn tại trước khi assertion 3 dùng split() dựa vào nó.
  assert {
    condition = strcontains(
      base64decode(aws_launch_template.this.user_data),
      "} >> /etc/ecs/ecs.config"
    )
    error_message = "user_data phải ghi block config vào ĐÚNG /etc/ecs/ecs.config (agent chỉ đọc file này) — không phải /etc/ecs/ecs.conf hay đường dẫn tương tự."
  }

  # Assertion 3 — swap phải tạo và bật xong TRƯỚC khi cloud-init kết thúc: agent
  # chỉ khởi động sau TOÀN BỘ cloud-init (không phải ngay sau dòng ghi config),
  # nên chỉ cần swap sẵn sàng trước khi instance kết thúc boot. t3.micro có 1GB
  # RAM phải chạy ECS agent + nginx + .NET API, thiếu swap là OOM. Giữ thứ tự
  # "swap trước, ghi config sau" trong script cho dễ đọc: cắt chuỗi tại dòng ghi
  # config rồi đòi `swapon` nằm trong phần TRƯỚC đó.
  # split() "fail open": nếu anchor "} >> /etc/ecs/ecs.config" không còn trong
  # chuỗi, split() trả nguyên chuỗi ở phần tử [0] và assertion này sẽ pass giả.
  # Assertion 2 ở trên đã đảm bảo anchor tồn tại nên split() ở đây an toàn.
  assert {
    condition = strcontains(
      split("} >> /etc/ecs/ecs.config", base64decode(aws_launch_template.this.user_data))[0],
      "swapon /swapfile"
    )
    error_message = "user_data phải tạo và bật swap TRƯỚC khi ghi ECS_CLUSTER — t3.micro chỉ có 1GB RAM, agent lên sau cloud-final nên cần swap sẵn sàng trước khi instance kết thúc boot."
  }

  assert {
    condition = strcontains(
      base64decode(aws_launch_template.this.user_data),
      "ECS_CLUSTER=hushstore-tftest"
    )
    error_message = "user_data phải chứa ECS_CLUSTER=hushstore-tftest, nếu không instance không đăng ký được vào cluster."
  }
}

run "container_insights_tat_de_khong_ton_phi_cloudwatch" {
  command = plan

  assert {
    condition = anytrue([
      for s in aws_ecs_cluster.this.setting :
      s.name == "containerInsights" && s.value == "disabled"
    ])
    error_message = "Container Insights phải tắt — nó tính phí custom metric theo từng container."
  }
}
