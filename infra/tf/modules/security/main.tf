# ─── SECURITY GROUPS ─────────────────────────────────────────────
# Không khai báo block ingress/egress inline: Terraform sẽ coi rule set là
# rỗng và thu hồi luôn rule egress allow-all mà AWS tự thêm. Rule thật khai
# báo bằng resource tách rời bên dưới để tránh circular dependency giữa
# sg-alb và sg-web.

# 🚨 `name_prefix`, KHÔNG PHẢI `name` — ĐÂY LÀ LỖI ĐÃ LÀM HỎNG MỘT LẦN APPLY.
# Description của security group là BẤT BIẾN ở AWS: đổi nó buộc Terraform thay
# thế cả SG. Kết hợp `name` cố định với `create_before_destroy` thì lần thay thế
# nào cũng chết, vì Terraform tạo SG mới TRƯỚC khi xoá cái cũ, mà cái cũ vẫn
# đang giữ tên:
#     InvalidGroup.Duplicate: The security group 'hushstore-rds-sg'
#     already exists for VPC 'vpc-...'
# Đã xảy ra thật lúc apply đợt 7 (đổi description "RDS SQL Server: 1433" thành
# "RDS PostgreSQL: 5432"), và hỏng ở giữa: RDS CŨ ĐÃ BỊ XOÁ, RDS MỚI CHƯA DỰNG
# ĐƯỢC vì thiếu SG. Đó là lý do phải sửa cả ba chứ không riêng cái vừa nổ — hai
# cái kia là cùng một quả mìn, chỉ chưa ai giẫm.
#
# `name_prefix` để AWS gắn hậu tố ngẫu nhiên nên SG mới và cũ không đụng tên,
# và create_before_destroy chạy đúng như thiết kế. Không gì phụ thuộc vào tên
# literal (đã kiểm: 0 chỗ trong tests, scripts, costguard, .github) — tag Name
# mới là thứ người và script đọc, và tag đó giữ nguyên.
resource "aws_security_group" "alb" {
  name_prefix = "${var.project}-alb-sg-"
  description = "ALB: nhan 80/443 tu internet, chuyen tiep sang sg-web"
  vpc_id      = var.vpc_id

  tags = { Name = "${var.project}-alb-sg" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "web" {
  name_prefix = "${var.project}-web-sg-"
  description = "ECS container instance: chi nhan traffic tu ALB, khong co port 22"
  vpc_id      = var.vpc_id

  tags = { Name = "${var.project}-web-sg" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "rds" {
  name_prefix = "${var.project}-rds-sg-"
  description = "RDS PostgreSQL: chi nhan 5432 tu sg-web, egress rong"
  vpc_id      = var.vpc_id

  tags = { Name = "${var.project}-rds-sg" }

  lifecycle {
    create_before_destroy = true
  }
}

# ─── INGRESS RULES ───────────────────────────────────────────────
# Gom vào 1 map để test có thể assert trên toàn bộ rule set cùng lúc —
# đó là cách bảo đảm "không có rule nào mở port 22" ở phạm vi cả module.
locals {
  ingress_rules = {
    # sg-alb là SG DUY NHẤT được nhận traffic từ CIDR công cộng.
    "alb-http" = {
      sg_id       = aws_security_group.alb.id
      description = "HTTP tu internet, se bi redirect sang HTTPS o listener"
      from_port   = 80
      to_port     = 80
      cidr_ipv4   = "0.0.0.0/0"
      source_sg   = null
    }
    "alb-https" = {
      sg_id       = aws_security_group.alb.id
      description = "HTTPS tu internet"
      from_port   = 443
      to_port     = 443
      cidr_ipv4   = "0.0.0.0/0"
      source_sg   = null
    }

    # sg-web: chỉ nhận từ sg-alb. KHÔNG có rule port 22.
    "web-http" = {
      sg_id       = aws_security_group.web.id
      description = "nginx container serve Blazor WASM, chi tu ALB"
      from_port   = 80
      to_port     = 80
      cidr_ipv4   = null
      source_sg   = aws_security_group.alb.id
    }
    "web-api" = {
      sg_id       = aws_security_group.web.id
      description = "API container, chi tu ALB"
      from_port   = 8080
      to_port     = 8080
      cidr_ipv4   = null
      source_sg   = aws_security_group.alb.id
    }

    # sg-rds: đúng 1 rule.
    "rds-postgres" = {
      sg_id       = aws_security_group.rds.id
      description = "PostgreSQL, chi tu container instance"
      from_port   = 5432
      to_port     = 5432
      cidr_ipv4   = null
      source_sg   = aws_security_group.web.id
    }
  }

  egress_rules = {
    # sg-alb chỉ được nói chuyện với sg-web, không ra internet.
    "alb-to-web-http" = {
      sg_id       = aws_security_group.alb.id
      description = "Forward sang nginx container"
      from_port   = 80
      to_port     = 80
      cidr_ipv4   = null
      target_sg   = aws_security_group.web.id
    }
    "alb-to-web-api" = {
      sg_id       = aws_security_group.alb.id
      description = "Forward sang API container"
      from_port   = 8080
      to_port     = 8080
      cidr_ipv4   = null
      target_sg   = aws_security_group.web.id
    }

    # sg-web: 5432 tới RDS, và 80/443 ra internet qua NAT để pull ECR,
    # gọi SSM, yum update. NAT Gateway không gắn được SG nên đây là chỗ
    # duy nhất kiểm soát egress ở tầng SG.
    "web-to-rds" = {
      sg_id       = aws_security_group.web.id
      description = "Ket noi PostgreSQL"
      from_port   = 5432
      to_port     = 5432
      cidr_ipv4   = null
      target_sg   = aws_security_group.rds.id
    }
    "web-https-out" = {
      sg_id       = aws_security_group.web.id
      description = "Pull image tu ECR, goi SSM va CloudWatch"
      from_port   = 443
      to_port     = 443
      cidr_ipv4   = "0.0.0.0/0"
      target_sg   = null
    }
    "web-http-out" = {
      sg_id       = aws_security_group.web.id
      description = "yum update tren ECS-optimized AMI"
      from_port   = 80
      to_port     = 80
      cidr_ipv4   = "0.0.0.0/0"
      target_sg   = null
    }

    # sg-rds: KHÔNG có rule nào. RDS không cần egress.
  }
}

resource "aws_vpc_security_group_ingress_rule" "all" {
  for_each = local.ingress_rules

  security_group_id            = each.value.sg_id
  description                  = each.value.description
  ip_protocol                  = "tcp"
  from_port                    = each.value.from_port
  to_port                      = each.value.to_port
  cidr_ipv4                    = each.value.cidr_ipv4
  referenced_security_group_id = each.value.source_sg

  tags = { Name = "${var.project}-in-${each.key}" }
}

resource "aws_vpc_security_group_egress_rule" "all" {
  for_each = local.egress_rules

  security_group_id            = each.value.sg_id
  description                  = each.value.description
  ip_protocol                  = "tcp"
  from_port                    = each.value.from_port
  to_port                      = each.value.to_port
  cidr_ipv4                    = each.value.cidr_ipv4
  referenced_security_group_id = each.value.target_sg

  tags = { Name = "${var.project}-out-${each.key}" }
}
