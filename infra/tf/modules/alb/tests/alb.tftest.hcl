# mock_provider thay cho provider thật: module này không đọc data source nào,
# nên toàn bộ assert đều nhắm vào giá trị biết-được-ở-plan-time (literal trong
# config, giá trị variable). Nhờ đó test chạy offline, không cần AWS SSO session
# còn hiệu lực.
mock_provider "aws" {}

variables {
  project           = "hushstore-tftest"
  vpc_id            = "vpc-00000000000000000"
  public_subnet_ids = ["subnet-00000000000000001", "subnet-00000000000000002"]
  alb_sg_id         = "sg-00000000000000000"
  logs_bucket       = "hushstore-alb-logs"
  web_domain        = "hushstore.io.vn"
  api_domain        = "api.hushstore.io.vn"
  enable_alb        = true
}

run "alb_internet_facing_tren_2_az_va_khong_bat_deletion_protection" {
  command = plan

  assert {
    condition     = aws_lb.this[0].internal == false
    error_message = "ALB phải internet-facing để người dùng truy cập được."
  }

  assert {
    condition     = length(aws_lb.this[0].subnets) == 2
    error_message = "ALB bắt buộc phải nằm trên tối thiểu 2 subnet ở 2 AZ khác nhau."
  }

  assert {
    condition     = aws_lb.this[0].enable_deletion_protection == false
    error_message = "deletion_protection phải false, nếu không down.sh và nuke.sh không destroy được ALB."
  }

  assert {
    condition     = aws_lb.this[0].load_balancer_type == "application"
    error_message = "Phải là Application Load Balancer — NLB không route được theo Host header."
  }
}

run "alb_ghi_access_log_va_chan_header_x_forwarded_gia_mao" {
  command = plan

  assert {
    condition = alltrue([
      aws_lb.this[0].access_logs[0].enabled == true,
      aws_lb.this[0].access_logs[0].bucket == "hushstore-alb-logs",
    ])
    error_message = "Access log phải bật và ghi vào bucket alb-logs — đây là nguồn bằng chứng chính cho báo cáo bảo mật khi VPC Flow Logs đang tắt."
  }

  assert {
    condition     = aws_lb.this[0].drop_invalid_header_fields == true
    error_message = "drop_invalid_header_fields phải true, nếu không client tự gửi được X-Forwarded-For giả mạo."
  }
}

run "listener_80_redirect_301_sang_443" {
  command = plan

  assert {
    condition     = aws_lb_listener.http[0].default_action[0].type == "redirect"
    error_message = "Listener 80 phải redirect, không được forward — mọi traffic phải đi qua HTTPS."
  }

  assert {
    condition = alltrue([
      aws_lb_listener.http[0].default_action[0].redirect[0].protocol == "HTTPS",
      aws_lb_listener.http[0].default_action[0].redirect[0].port == "443",
      aws_lb_listener.http[0].default_action[0].redirect[0].status_code == "HTTP_301",
    ])
    error_message = "Redirect phải là 301 sang HTTPS port 443."
  }

  assert {
    condition = alltrue([
      tostring(aws_lb_listener.http[0].port) == "80",
      aws_lb_listener.http[0].protocol == "HTTP",
    ])
    error_message = "Listener redirect phải nghe trên port 80/HTTP."
  }
}

run "listener_443_dung_tls_policy_hien_dai" {
  command = plan

  assert {
    condition     = startswith(aws_lb_listener.https[0].ssl_policy, "ELBSecurityPolicy-TLS13")
    error_message = "Phải dùng SSL policy TLS 1.3, không dùng policy cũ cho phép TLS 1.0/1.1."
  }

  assert {
    condition = alltrue([
      tostring(aws_lb_listener.https[0].port) == "443",
      aws_lb_listener.https[0].protocol == "HTTPS",
    ])
    error_message = "Listener phục vụ ứng dụng phải nghe trên port 443/HTTPS."
  }
}

run "target_group_web_health_check_healthz_va_api_health_ready" {
  command = plan

  assert {
    condition = alltrue([
      aws_lb_target_group.web[0].port == 80,
      aws_lb_target_group.web[0].target_type == "instance",
      aws_lb_target_group.web[0].health_check[0].path == "/healthz",
    ])
    error_message = "tg-web phải trỏ port 80, target_type instance, health check /healthz (endpoint đã thêm vào nginx.conf)."
  }

  assert {
    condition = alltrue([
      aws_lb_target_group.api[0].port == 8080,
      aws_lb_target_group.api[0].target_type == "instance",
      aws_lb_target_group.api[0].health_check[0].path == "/health/ready",
    ])
    error_message = "tg-api PHẢI health check /health/ready (chạm DB), không phải /health — nếu không ALB giữ nguyên instance dù RDS chết."
  }

  assert {
    condition = alltrue([
      tostring(aws_lb_target_group.web[0].deregistration_delay) == "30",
      tostring(aws_lb_target_group.api[0].deregistration_delay) == "30",
    ])
    error_message = "deregistration_delay phải là 30s thay vì 300s mặc định để rút ngắn thời gian deploy."
  }
}

run "listener_rule_route_api_domain_sang_tg_api" {
  command = plan

  assert {
    condition = contains(flatten([
      for c in aws_lb_listener_rule.api[0].condition :
      [for h in c.host_header : tolist(h.values)]
    ]), "api.hushstore.io.vn")
    error_message = "Phải có listener rule route Host = api.hushstore.io.vn sang tg-api."
  }

  # KHÔNG so target_group_arn với aws_lb_target_group.web[0].arn — cả hai là
  # (known after apply). Assert type thay thế; việc route đúng tg-web được
  # kiểm chứng thật bằng curl ở Task 15 Step 10-11.
  assert {
    condition     = aws_lb_listener.https[0].default_action[0].type == "forward"
    error_message = "Default action của listener 443 phải là forward (sang tg-web), không phải redirect hay fixed-response."
  }

  assert {
    condition = alltrue([
      aws_lb_listener_rule.api[0].action[0].type == "forward",
      aws_lb_listener_rule.api[0].priority == 100,
    ])
    error_message = "Rule api phải forward và có priority cố định 100."
  }
}

run "acm_cert_bao_ca_hai_domain_va_dung_dns_validation" {
  command = plan

  assert {
    condition     = aws_acm_certificate.this.domain_name == "hushstore.io.vn"
    error_message = "Cert phải cấp cho domain gốc hushstore.io.vn."
  }

  assert {
    condition     = contains(aws_acm_certificate.this.subject_alternative_names, "api.hushstore.io.vn")
    error_message = "Cert phải có SAN api.hushstore.io.vn để listener phục vụ được cả 2 hostname."
  }

  assert {
    condition     = aws_acm_certificate.this.validation_method == "DNS"
    error_message = "Phải dùng DNS validation — email validation không tự động hoá được."
  }
}

run "tat_alb_thi_khong_tao_alb_va_target_group_nhung_van_giu_cert" {
  command = plan

  variables {
    enable_alb = false
  }

  assert {
    condition = alltrue([
      length(aws_lb.this) == 0,
      length(aws_lb_target_group.web) == 0,
      length(aws_lb_target_group.api) == 0,
      length(aws_lb_listener.https) == 0,
      length(aws_lb_listener.http) == 0,
      length(aws_lb_listener_rule.api) == 0,
    ])
    error_message = "Khi enable_alb = false, toàn bộ serving stack phải bị destroy để về $0."
  }

  assert {
    condition     = aws_acm_certificate.this.domain_name == "hushstore.io.vn"
    error_message = "ACM cert KHÔNG được gate theo enable_alb — cert miễn phí, và giữ lại để không phải validate lại mỗi lần bật ALB."
  }
}
