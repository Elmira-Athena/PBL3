# mock_provider thay cho provider thật: module này không đọc data source nào
# (`grep -n '^data ' *.tf` → không match), nên toàn bộ assert đều nhắm vào giá
# trị biết-được-ở-plan-time (literal trong config, giá trị variable). Nhờ đó
# test chạy offline, không cần AWS SSO session còn hiệu lực → chạy được trong CI
# không có credential.
#
# ĐÁNH ĐỔI phải biết: mock_provider mock phần CRUD/API, nên mọi thứ chỉ biết
# được khi AWS thật trả lời đều KHÔNG được kiểm ở đây:
#   - tên ssl_policy có tồn tại thật hay không — ĐÃ ĐO: đổi thành
#     "ELBSecurityPolicy-KHONG-TON-TAI-2021-06" thì provider im lặng, chỉ mỗi
#     assert so-sánh-đúng-bằng-tên ở run listener_443_* bắt được. Vì vậy assert
#     đó phải giữ dạng == (đừng đổi lại thành startswith).
#   - quyền ghi của access-log bucket (ALB PutObject bằng service principal),
#   - subnet/SG/VPC có tồn tại và cùng VPC hay không, cert ARN có ISSUED hay chưa.
# NGƯỢC LẠI (đã đo, đừng ghi sai như bản trước): validation phía schema của
# provider VẪN chạy dưới mock_provider — cho project 26 ký tự, plan báo ngay
# `"name" cannot be longer than 32 characters`. Nên guard độ dài var.project là
# để có lỗi SỚM và ĐÚNG TÊN BIẾN, không phải để bù một lỗ hổng plan-time.
#
# Đây là module DUY NHẤT trong dự án dùng mock_provider; 6 module còn lại dùng
# provider thật vì có data source. Đừng "thống nhất" bằng cách đổi hết sang
# mock_provider: modules/ecs có data.aws_ssm_parameter.ecs_ami và
# data.aws_region.current — chuyển sang mock_provider mà không thêm
# override_data sẽ vô hiệu hoá chính những assert đang dựa vào các read đó.
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

run "alb_ghi_access_log_va_bo_header_ten_khong_hop_le" {
  command = plan

  assert {
    condition = alltrue([
      aws_lb.this[0].access_logs[0].enabled == true,
      aws_lb.this[0].access_logs[0].bucket == "hushstore-alb-logs",
    ])
    error_message = "Access log phải bật và ghi vào bucket alb-logs — đây là nguồn bằng chứng chính cho báo cáo bảo mật khi VPC Flow Logs đang tắt."
  }

  # drop_invalid_header_fields chỉ bỏ header có TÊN không hợp lệ theo RFC 7230.
  # Nó KHÔNG chống giả mạo X-Forwarded-For (ALB append IP client vào cuối XFF
  # chứ không thay chuỗi client gửi) — việc đó do ForwardLimit phía ASP.NET lo.
  assert {
    condition     = aws_lb.this[0].drop_invalid_header_fields == true
    error_message = "drop_invalid_header_fields phải true để ALB không forward header có tên vi phạm RFC 7230 xuống target."
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

  # Ghi tường minh host/path/query để 301 giữ nguyên request; assert luôn để
  # không ai sửa thành path = "/" (sẽ phá deep link của Blazor mà test vẫn xanh).
  assert {
    condition = alltrue([
      aws_lb_listener.http[0].default_action[0].redirect[0].host == "#{host}",
      aws_lb_listener.http[0].default_action[0].redirect[0].path == "/#{path}",
      aws_lb_listener.http[0].default_action[0].redirect[0].query == "#{query}",
    ])
    error_message = "Redirect phải giữ nguyên host/path/query (#{host}, /#{path}, #{query}), không được ép về trang gốc."
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

  # So sánh ĐÚNG BẰNG tên policy, KHÔNG dùng startswith("ELBSecurityPolicy-TLS13"):
  # predicate đó pass cả với ELBSecurityPolicy-TLS13-1-0-2021-06 và -TLS13-1-1-,
  # mà hai policy này vẫn negotiate TLS 1.0/1.1 (kiểm bằng
  # `aws elbv2 describe-ssl-policies`: TLS13-1-0 có SslProtocols = TLSv1,
  # TLSv1.1, TLSv1.2, TLSv1.3). Tên đúng bằng còn bắt được cả typo — vì
  # mock_provider không validate tên policy, typo chỉ lộ ra ở apply.
  assert {
    condition     = aws_lb_listener.https[0].ssl_policy == "ELBSecurityPolicy-TLS13-1-2-2021-06"
    error_message = "SSL policy phải đúng ELBSecurityPolicy-TLS13-1-2-2021-06 (sàn TLS 1.2, hỗ trợ 1.3). Các policy TLS13-1-0/1-1 vẫn cho phép TLS 1.0/1.1."
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

  # 5s, không phải 30s: deploy là stop-rồi-start (minimum_healthy_percent = 0)
  # nên không có connection nào để drain, mỗi giây delay là downtime cộng thêm.
  assert {
    condition = alltrue([
      tostring(aws_lb_target_group.web[0].deregistration_delay) == "5",
      tostring(aws_lb_target_group.api[0].deregistration_delay) == "5",
    ])
    error_message = "deregistration_delay phải là 5s: topology static host port + max_size 1 buộc stop-rồi-start nên draining chỉ kéo dài downtime."
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

  # KHÔNG so target_group_arn với aws_lb_target_group.*[0].arn — cả hai là
  # (known after apply). Assert type + priority thay thế; việc route đúng target
  # group được kiểm chứng thật bằng curl ở Task 15 Step 10-11.
  assert {
    condition = alltrue([
      aws_lb_listener_rule.api[0].action[0].type == "forward",
      aws_lb_listener_rule.api[0].priority == 100,
    ])
    error_message = "Rule api phải forward và có priority cố định 100."
  }

  assert {
    condition = contains(flatten([
      for c in aws_lb_listener_rule.web[0].condition :
      [for h in c.host_header : tolist(h.values)]
    ]), "hushstore.io.vn")
    error_message = "Phải có listener rule route Host = hushstore.io.vn sang tg-web (không dùng default action nữa)."
  }

  assert {
    condition = alltrue([
      aws_lb_listener_rule.web[0].action[0].type == "forward",
      aws_lb_listener_rule.web[0].priority == 200,
    ])
    error_message = "Rule web phải forward và có priority 200 (sau rule api)."
  }
}

run "default_action_443_tra_403_va_chi_allowlist_dung_2_rule_host" {
  command = plan

  # Ai biết tên DNS thô của ALB cũng KHÔNG vào được origin: default action là
  # 403, không phải forward sang tg-web. Đây là phần "rule mở theo nguyên tắc
  # tối thiểu" của đề bài. Health check không đi qua listener nên không ảnh hưởng.
  assert {
    condition     = aws_lb_listener.https[0].default_action[0].type == "fixed-response"
    error_message = "Default action của listener 443 phải là fixed-response, KHÔNG được forward — nếu forward thì mọi Host (kể cả DNS thô của ALB) đều vào được origin, bỏ qua Cloudflare."
  }

  # Dùng list comprehension chứ KHÔNG index [0] vào fixed_response: khi ai đó
  # đổi default action sang forward thì fixed_response là list rỗng, index [0]
  # sẽ ném "Invalid index" (một Error làm Terraform BỎ các run còn lại) thay vì
  # fail sạch. Dạng dưới đây trả 0 và fail đúng assert này.
  assert {
    condition = length(flatten([
      for a in aws_lb_listener.https[0].default_action :
      [for fr in a.fixed_response : fr if fr.status_code == "403" && fr.content_type == "text/plain"]
    ])) == 1
    error_message = "Default action phải trả fixed-response 403 với content_type text/plain."
  }

  # Đúng 2 rule host-based, và tập Host được cho qua đúng bằng 3 tên miền của
  # dự án — thêm "*" hay một hostname lạ vào values là ĐỎ ngay.
  assert {
    condition = setunion(
      toset(flatten([for c in aws_lb_listener_rule.api[0].condition : [for h in c.host_header : tolist(h.values)]])),
      toset(flatten([for c in aws_lb_listener_rule.web[0].condition : [for h in c.host_header : tolist(h.values)]]))
      ) == toset([
        "api.hushstore.io.vn",
        "hushstore.io.vn",
        "www.hushstore.io.vn",
    ])
    error_message = "Allowlist Host header phải đúng 3 tên: api.hushstore.io.vn, hushstore.io.vn, www.hushstore.io.vn."
  }

  # Mỗi rule chỉ có ĐÚNG 1 condition, và condition đó phải là host_header:
  # thêm path_pattern hay bỏ host_header đi là ĐỎ.
  assert {
    condition = alltrue([
      length(aws_lb_listener_rule.api[0].condition) == 1,
      length(aws_lb_listener_rule.web[0].condition) == 1,
      length(flatten([for c in aws_lb_listener_rule.api[0].condition : tolist(c.host_header)])) == 1,
      length(flatten([for c in aws_lb_listener_rule.web[0].condition : tolist(c.host_header)])) == 1,
      length(aws_lb_listener_rule.api) + length(aws_lb_listener_rule.web) == 2,
    ])
    error_message = "Phải có đúng 2 listener rule, mỗi rule đúng 1 condition host_header — không thêm điều kiện khác."
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

  assert {
    condition     = length(aws_acm_certificate_validation.this) == 1
    error_message = "Khi enable_alb = true phải có đúng 1 waiter aws_acm_certificate_validation để listener 443 lấy được cert đã ISSUED."
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
      length(aws_lb_listener_rule.web) == 0,
    ])
    error_message = "Khi enable_alb = false, toàn bộ serving stack phải bị destroy để về $0."
  }

  # Waiter PHẢI bị gate: nó là resource ảo (không tạo gì trên AWS) nhưng nếu
  # ngoài gate thì nó nằm trong graph của MỌI apply ở envs/prod và treo 30 phút
  # khi CNAME chưa được thêm tay vào Cloudflare.
  assert {
    condition     = length(aws_acm_certificate_validation.this) == 0
    error_message = "aws_acm_certificate_validation PHẢI bị gate theo enable_alb, nếu không mọi apply không liên quan tới ALB cũng phải chờ waiter này."
  }

  assert {
    condition     = aws_acm_certificate.this.domain_name == "hushstore.io.vn"
    error_message = "ACM cert KHÔNG được gate theo enable_alb — cert miễn phí, và giữ lại để không phải validate lại mỗi lần bật ALB."
  }
}

# Guard độ dài var.project (variables.tf): tên "${project}-tg-web" bị AWS cap ở
# 32 ký tự nên project không được quá 25. mock_provider không kiểm tên nên nếu
# thiếu block validation thì lỗi chỉ lộ ra ở apply — run này giữ cho guard đó
# không bị xoá lặng lẽ.
run "project_dai_hon_25_ky_tu_bi_variable_validation_tu_choi" {
  command = plan

  variables {
    project = "hushstore-tftest-qua-dai-x" # 26 ky tu
  }

  expect_failures = [var.project]
}
