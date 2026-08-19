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

  # Bỏ header X-Forwarded-* mà client tự gửi, thay bằng giá trị ALB tự tính.
  # Không có cái này thì client có thể giả mạo X-Forwarded-For.
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

  # 30s thay vì 300s mặc định: rút ngắn thời gian deploy đáng kể, và nginx
  # serve static nên không có request nào chạy lâu cần chờ.
  deregistration_delay = 30

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

  deregistration_delay = 30

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
      protocol    = "HTTPS"
      port        = "443"
      status_code = "HTTP_301"
    }
  }
}

resource "aws_lb_listener" "https" {
  count = var.enable_alb ? 1 : 0

  load_balancer_arn = aws_lb.this[0].arn
  port              = 443
  protocol          = "HTTPS"
  certificate_arn   = aws_acm_certificate_validation.this.certificate_arn

  # TLS 1.3 only. Policy cũ hơn cho phép TLS 1.0/1.1 đã hết hạn hỗ trợ.
  ssl_policy = "ELBSecurityPolicy-TLS13-1-2-2021-06"

  # Mặc định: Blazor client.
  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.web[0].arn
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
