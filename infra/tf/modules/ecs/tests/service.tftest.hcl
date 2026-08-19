provider "aws" {
  region = "ap-southeast-1"
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
  image_tag                 = "0123456789abcdef0123456789abcdef01234567"
  assets_bucket_name        = "hushstore-public-assets"
  allowed_origins           = "https://hushstore.io.vn"
  enable_alb                = true
  tg_web_arn                = "arn:aws:elasticloadbalancing:ap-southeast-1:000000000000:targetgroup/hushstore-tg-web/aaaaaaaaaaaaaaaa"
  tg_api_arn                = "arn:aws:elasticloadbalancing:ap-southeast-1:000000000000:targetgroup/hushstore-tg-api/bbbbbbbbbbbbbbbb"
}

# LƯU Ý CHO NGƯỜI SỬA FILE NÀY
# `load_balancer` và `capacity_provider_strategy` của aws_ecs_service có
# nesting_mode = "set" (kiểm bằng `terraform providers schema -json`), KHÔNG phải
# list. Nên `aws_ecs_service.api[0].load_balancer[0]` là LỖI, không phải chỉ là
# kém đẹp. Dùng `one(...)` — nó trả về phần tử duy nhất của collection và báo lỗi
# nếu số phần tử khác 1, tức nó vừa truy cập được vừa kiêm luôn assertion
# "đúng một load balancer".
#
# Cũng KHÔNG assert `launch_type == null`: attribute đó là Optional+Computed nên
# unknown ở plan-time NGAY CẢ KHI config không đặt nó. Muốn chứng minh module
# không dùng launch_type thì grep service.tf, đừng assert.

run "deployment_percent_phu_hop_voi_static_host_port_1_instance" {
  command = plan

  assert {
    condition = alltrue([
      aws_ecs_service.api[0].deployment_minimum_healthy_percent == 0,
      aws_ecs_service.api[0].deployment_maximum_percent == 100,
    ])
    error_message = "Static host port 8080 + 1 instance nên không chạy 2 bản song song được. min 0 / max 100 là bắt buộc, nếu không deploy sẽ treo vì xung đột port."
  }

  assert {
    condition = alltrue([
      aws_ecs_service.web[0].deployment_minimum_healthy_percent == 0,
      aws_ecs_service.web[0].deployment_maximum_percent == 100,
    ])
    error_message = "Service web cũng phải min 0 / max 100 vì static host port 80."
  }
}

run "service_gan_dung_target_group_va_container" {
  command = plan

  assert {
    condition = alltrue([
      one(aws_ecs_service.api[0].load_balancer).target_group_arn == var.tg_api_arn,
      one(aws_ecs_service.api[0].load_balancer).container_name == "api",
      one(aws_ecs_service.api[0].load_balancer).container_port == 8080,
    ])
    error_message = "Service api phải gắn ĐÚNG MỘT target group: tg-api, container tên 'api', port 8080."
  }

  assert {
    condition = alltrue([
      one(aws_ecs_service.web[0].load_balancer).target_group_arn == var.tg_web_arn,
      one(aws_ecs_service.web[0].load_balancer).container_name == "web",
      one(aws_ecs_service.web[0].load_balancer).container_port == 80,
    ])
    error_message = "Service web phải gắn ĐÚNG MỘT target group: tg-web, container tên 'web', port 80."
  }

  # Bắt đúng lỗi tráo target group: nếu ai đó gán tg_web cho service api thì
  # hai assertion trên vẫn có thể pass nếu cả hai biến trỏ cùng ARN. Đòi hai ARN
  # khác nhau để tình huống đó không lọt.
  assert {
    condition     = one(aws_ecs_service.api[0].load_balancer).target_group_arn != one(aws_ecs_service.web[0].load_balancer).target_group_arn
    error_message = "api và web phải gắn hai target group KHÁC nhau — gắn trùng thì một trong hai service không bao giờ nhận traffic đúng."
  }
}

run "service_api_bat_ecs_exec" {
  command = plan

  assert {
    condition     = aws_ecs_service.api[0].enable_execute_command == true
    error_message = "Phải bật ECS Exec Ở MỨC SERVICE trên service api — thiếu nó thì execute-command lỗi dù task definition đã có initProcessEnabled. Đây là điều kiện của kịch bản kiểm thử số 10."
  }
}

run "grace_period_lon_hon_cua_so_unhealthy_cua_target_group" {
  command = plan

  # Target group đặt interval = 15, unhealthy_threshold = 3 → ALB kết luận
  # unhealthy sau 45 giây. Grace period PHẢI lớn hơn 45, nếu không task đang
  # khởi động bình thường vẫn bị giết và service không bao giờ stable.
  # API cần nhiều hơn web vì /health/ready mở kết nối tới RDS.
  assert {
    condition = alltrue([
      aws_ecs_service.web[0].health_check_grace_period_seconds > 45,
      aws_ecs_service.api[0].health_check_grace_period_seconds > 45,
      aws_ecs_service.api[0].health_check_grace_period_seconds >= aws_ecs_service.web[0].health_check_grace_period_seconds,
    ])
    error_message = "grace period phải > 45s (= interval 15 × unhealthy_threshold 3 của target group), và API phải >= web vì /health/ready có chạm DbContext tới RDS."
  }
}

run "service_dung_capacity_provider_khong_dung_launch_type" {
  command = plan

  assert {
    condition = alltrue([
      one(aws_ecs_service.api[0].capacity_provider_strategy).capacity_provider == aws_ecs_capacity_provider.this.name,
      one(aws_ecs_service.web[0].capacity_provider_strategy).capacity_provider == aws_ecs_capacity_provider.this.name,
    ])
    error_message = "Service phải dùng capacity_provider_strategy trỏ vào capacity provider của module — launch_type và capacity_provider_strategy loại trừ nhau."
  }
}

run "tat_alb_thi_khong_tao_service_nao" {
  command = plan

  variables {
    enable_alb = false
    tg_web_arn = ""
    tg_api_arn = ""
  }

  assert {
    condition = alltrue([
      length(aws_ecs_service.api) == 0,
      length(aws_ecs_service.web) == 0,
    ])
    error_message = "Khi enable_alb = false phải destroy cả service — ECS CreateService fail nếu target group chưa gắn vào load balancer."
  }

  assert {
    condition = alltrue([
      aws_ecs_task_definition.api.network_mode == "bridge",
      aws_ecs_cluster.this.name == "hushstore-tftest",
      aws_ecs_capacity_provider.this.name == "hushstore-tftest-cp",
    ])
    error_message = "Cluster, capacity provider và task definition KHÔNG được gate theo enable_alb — chúng miễn phí và cần tồn tại để run-task migrator khi ALB đang tắt."
  }
}
