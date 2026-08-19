# ─── LOAD BALANCER ───────────────────────────────────────────────
resource "aws_lb" "this" {
  count = var.enable_alb ? 1 : 0

  name               = "${var.project}-alb"
  load_balancer_type = "application"
  internal           = false
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
  # chèn vẫn tới được container (nằm ở BÊN TRÁI). Thứ thật sự chặn giả mạo là
  # phía ASP.NET: ForwardedHeadersMiddleware đọc XFF từ PHẢI sang, và
  # ForwardedHeadersOptions.ForwardLimit đang để mặc định = 1
  # (src/API/Program.cs chỉ set ForwardedHeaders + KnownNetworks/KnownProxies
  # .Clear()), nên nó chỉ lấy đúng 1 entry phải nhất — entry do ALB thêm, tức
  # IP client thật. Code nào đọc XFF phải lấy phần tử CUỐI, tuyệt đối không lấy
  # phần tử đầu.
  drop_invalid_header_fields = true

  tags = { Name = "${var.project}-alb" }
}

# ─── TARGET GROUPS ───────────────────────────────────────────────
# target_type = instance vì task dùng bridge network mode với static host
# port. ECS service tự đăng ký/rút instance khỏi target group.
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
  # Chỉ 2 rule host-based bên dưới mới được vào target group. Nếu default action
  # forward sang tg-web thì bất kỳ ai biết tên DNS thô của ALB
  # (<project>-alb-*.ap-southeast-1.elb.amazonaws.com) đều vào được origin,
  # bỏ qua hoàn toàn Cloudflare (WAF, rate limit, che IP gốc).
  #
  # HEALTH CHECK KHÔNG BỊ ẢNH HƯỞNG (câu hỏi đầu tiên ai đọc cũng sẽ có): ALB
  # gọi health check THẲNG tới target theo IP:port của instance, không đi qua
  # listener và không qua listener rule, nên nó không mang Host header nào cần
  # match và không bao giờ nhận 403 này.
  #
  # Test trước khi cắt DNS sang Cloudflare: gọi ALB DNS thô giờ trả 403, phải
  # giả Host header —
  #   curl --resolve hushstore.io.vn:443:<IP-ALB> https://hushstore.io.vn/
  # (cách này cert vẫn valid). Hoặc: curl -k -H "Host: hushstore.io.vn"
  # https://<alb-dns>/ — ALB match rule theo Host header, không theo SNI.
  default_action {
    type = "fixed-response"

    fixed_response {
      content_type = "text/plain"
      message_body = "403 Forbidden: unknown host\n"
      status_code  = "403"
    }
  }
}

# Host-based routing: api.hushstore.io.vn -> container API port 8080.
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

# Host-based routing: hushstore.io.vn + www.hushstore.io.vn -> Blazor client.
# Phải là rule tường minh (không dùng default action) để default action giữ
# được vai trò 403 cho mọi Host lạ. www.* nằm cùng rule vì cùng target group;
# lưu ý cert ACM hiện chỉ có SAN cho web_domain + api_domain nên www.* sẽ lỗi
# tên miền ở tầng TLS trước khi tới rule này — giữ sẵn để khi thêm SAN thì
# không phải sửa routing.
resource "aws_lb_listener_rule" "web" {
  count = var.enable_alb ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 200

  condition {
    host_header {
      values = [var.web_domain, "www.${var.web_domain}"]
    }
  }

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.web[0].arn
  }
}
