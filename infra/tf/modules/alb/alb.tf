# ─── LOAD BALANCER ───────────────────────────────────────────────
resource "aws_lb" "this" {
  count = var.enable_alb ? 1 : 0

  name               = "${var.project}-alb"
  load_balancer_type = "application"
  internal           = false
  ip_address_type    = "ipv4"
  security_groups    = [var.alb_sg_id]
  subnets            = var.public_subnet_ids

  # Phải false: nếu bật thì down.sh và nuke.sh không destroy được ALB.
  enable_deletion_protection = false

  # Access log là nguồn bằng chứng chính cho báo cáo kiểm thử bảo mật khi
  # VPC Flow Logs đang tắt (enable_flow_logs default = false).
  access_logs {
    bucket  = var.logs_bucket
    prefix  = var.project
    enabled = true
  }

  # Loại bỏ (không forward xuống target) những header có TÊN chứa ký tự không
  # hợp lệ theo RFC 7230. Đây là hardening chung, giữ true.
  #
  # LƯU Ý — nó KHÔNG chống giả mạo X-Forwarded-For: ALB APPEND IP client vào
  # cuối chuỗi XFF chứ không thay thế chuỗi client gửi, nên giá trị client tự
  # chèn vẫn tới được container (nằm ở BÊN TRÁI). ASP.NET đọc từ PHẢI sang, và
  # ForwardedHeadersOptions.ForwardLimit đang để mặc định = 1
  # (src/API/Program.cs chỉ set ForwardedHeaders + KnownNetworks/KnownProxies
  # .Clear()), nên chỉ lấy entry phải nhất do ALB thêm: IP kết nối trực tiếp
  # tới ALB, tức Cloudflare edge khi DNS proxied, KHÔNG phải IP người dùng.
  # Không dùng XFF/CF-Connecting-IP do client tự gửi làm danh tính đáng tin
  # nếu chưa kiểm soát và xác thực toàn bộ chuỗi proxy.
  drop_invalid_header_fields = true

  tags = { Name = "${var.project}-alb" }
}

# ─── TARGET GROUPS ───────────────────────────────────────────────
# target_type = instance vì task dùng bridge network mode với static host
# port (cổng host 80/8080 thuộc container `waf` — sidecar WAF đứng trước web/api,
# xem modules/ecs/taskdef.tf). ECS service tự đăng ký/rút instance khỏi target group.
resource "aws_lb_target_group" "web" {
  count = var.enable_alb ? 1 : 0

  name        = "${var.project}-tg-web"
  port        = 80
  protocol    = "HTTP"
  target_type = "instance"
  vpc_id      = var.vpc_id

  # 5s thay vì 300s mặc định. Draining chỉ có ích khi ĐÃ có target thay thế
  # đang serve; ở topology này static host port 80/8080 + max_size = 1 buộc ECS
  # service dùng deployment_minimum_healthy_percent = 0, tức STOP task cũ RỒI
  # mới start task mới — lúc deregister thì chẳng còn connection nào để drain,
  # nên mỗi giây delay chỉ là downtime cộng thêm.
  # TĂNG LẠI (30-60s) khi và chỉ khi: chuyển sang dynamic host port (hoặc
  # awsvpc) VÀ có tối thiểu 2 instance, để hai task cùng tồn tại lúc deploy.
  deregistration_delay = 5

  health_check {
    path                = "/healthz"
    protocol            = "HTTP"
    matcher             = "200"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  tags = { Name = "${var.project}-tg-web" }
}

resource "aws_lb_target_group" "api" {
  count = var.enable_alb ? 1 : 0

  name        = "${var.project}-tg-api"
  port        = 8080
  protocol    = "HTTP"
  target_type = "instance"
  vpc_id      = var.vpc_id

  # Xem lý do ở tg-web bên trên: deploy là stop-rồi-start nên draining vô ích.
  deregistration_delay = 5

  health_check {
    # /health/ready CHẠM DB (AddDbContextCheck). Trỏ vào /health cũ sẽ khiến
    # ALB báo healthy dù RDS đã chết — chính lỗi mà Task 8 sửa.
    path                = "/health/ready"
    protocol            = "HTTP"
    matcher             = "200"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  tags = { Name = "${var.project}-tg-api" }
}

# ─── LISTENERS ───────────────────────────────────────────────────
resource "aws_lb_listener" "http" {
  count = var.enable_alb ? 1 : 0

  load_balancer_arn = aws_lb.this[0].arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"

    redirect {
      protocol = "HTTPS"
      port     = "443"

      # Ghi tường minh 3 giá trị này dù chúng đúng bằng default của ELB API:
      # (1) người đọc thấy ngay là 301 giữ nguyên host/path/query, không phải
      # redirect mọi thứ về "/" (deep link của Blazor sẽ chết nếu ai đó sửa
      # thành path = "/"); (2) tránh perpetual diff — nếu để null trong config
      # thì state đọc về "#{host}" và plan có thể báo thay đổi mãi.
      host  = "#{host}"
      path  = "/#{path}"
      query = "#{query}"

      status_code = "HTTP_301"
    }
  }
}

resource "aws_lb_listener" "https" {
  count = var.enable_alb ? 1 : 0

  load_balancer_arn = aws_lb.this[0].arn
  port              = 443
  protocol          = "HTTPS"
  certificate_arn   = aws_acm_certificate_validation.this[0].certificate_arn

  # Sàn TLS 1.2, hỗ trợ cả TLS 1.3 (xác nhận bằng
  # `aws elbv2 describe-ssl-policies`: SslProtocols = TLSv1.2, TLSv1.3).
  # KHÔNG phải "TLS 1.3 only" — policy 1.3-only là ELBSecurityPolicy-TLS13-1-3.
  # Đây là policy AWS khuyến nghị. Tuyệt đối không đổi sang TLS13-1-0/1-1: tên
  # trông mới hơn nhưng chúng hạ sàn xuống TLS 1.0/1.1.
  ssl_policy = "ELBSecurityPolicy-TLS13-1-2-2021-06"

  # ALLOWLIST HOST HEADER — default action là 403, KHÔNG forward.
  # Chỉ 2 rule bên dưới mới được vào target group. Host allowlist chỉ chặn
  # Host lạ: người gọi trực tiếp vẫn giả được Host hợp lệ.
  #
  # HEALTH CHECK KHÔNG BỊ ẢNH HƯỞNG (câu hỏi đầu tiên ai đọc cũng sẽ có): ALB
  # gọi health check THẲNG tới target theo IP:port của instance, không đi qua
  # listener và không qua listener rule, nên nó không mang Host header nào cần
  # match và không bao giờ nhận 403 này.
  #
  # --resolve với Host hợp lệ vẫn tới được origin.
  default_action {
    type = "fixed-response"

    fixed_response {
      content_type = "text/plain"
      message_body = "403 Forbidden: unknown host\n"
      status_code  = "403"
    }
  }
}

# Host-based routing: api.hushstore.io.vn -> cổng host 8080 (container waf của task api).
# Nhờ rule này nginx trong image web KHÔNG cần block proxy_pass nào.
resource "aws_lb_listener_rule" "api" {
  count = var.enable_alb ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 100

  condition {
    host_header {
      values = [var.api_domain]
    }
  }

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api[0].arn
  }
}

# Host-based routing: hushstore.io.vn -> Blazor client.
# Phải là rule tường minh (không dùng default action) để default action giữ
# được vai trò 403 cho mọi Host lạ.
#
# KHÔNG có www.* trong allowlist, có chủ ý. www.hushstore.io.vn không tồn tại
# trong DNS, không được tham chiếu ở đâu trong ứng dụng, và không có SAN trong
# cert ACM — nên nó sẽ lỗi tên miền ở tầng TLS trước khi tới được rule này.
# Một entry allowlist không bao giờ tới được thì tệ hơn là không có: nó làm
# người đọc tin rằng www đang được hỗ trợ.
# Muốn hỗ trợ www thì phải làm CẢ BA cùng lúc: thêm record DNS, thêm SAN vào
# cert (kéo theo một record validation mới trên Cloudflare), rồi mới thêm vào
# allowlist này.
resource "aws_lb_listener_rule" "web" {
  count = var.enable_alb ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 200

  condition {
    host_header {
      values = [var.web_domain]
    }
  }

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.web[0].arn
  }
}
