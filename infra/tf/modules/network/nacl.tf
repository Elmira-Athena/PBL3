locals {
  # Gộp 2 subnet /24 liền kề của mỗi tier thành 1 CIDR /23.
  # cidrsubnet không làm được việc này nên tính bằng cách bỏ 1 bit netmask.
  public_tier_cidr = cidrsubnet(var.vpc_cidr, 7, 0)  # 10.20.0.0/23
  app_tier_cidr    = cidrsubnet(var.vpc_cidr, 7, 5)  # 10.20.10.0/23
  db_tier_cidr     = cidrsubnet(var.vpc_cidr, 7, 10) # 10.20.20.0/23

  # ── nacl-public: chứa ALB + NAT Gateway ────────────────────────
  public_ingress = [
    { no = 100, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 80, to = 80 },
    { no = 110, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 443, to = 443 },
    # Return traffic: response của ALB về client, và response từ internet
    # về NAT Gateway. Cả hai đều tới port ephemeral.
    { no = 120, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 1024, to = 65535 },
  ]

  public_egress = [
    { no = 100, proto = "tcp", action = "allow", cidr = local.app_tier_cidr, from = 80, to = 80 },
    { no = 110, proto = "tcp", action = "allow", cidr = local.app_tier_cidr, from = 8080, to = 8080 },
    # Egress qua NAT ra internet: ECR, SSM, yum
    { no = 120, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 80, to = 80 },
    { no = 125, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 443, to = 443 },
    # Response về client, và response từ NAT về app tier
    { no = 130, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 1024, to = 65535 },
  ]

  # ── nacl-app: chứa ECS container instance ──────────────────────
  # THỨ TỰ RULE Ở ĐÂY LÀ ĐIỂM KỸ THUẬT CỐT LÕI — xem ghi chú ở đầu task.
  app_ingress = [
    # 90/95/115: DENY phải đứng trước rule 120
    #
    # 🔴 RULE 95 LÀ THỨ DUY NHẤT CHẶN RULE 120 MỞ CỔNG DB RA INTERNET. Nó tồn
    # tại vì rule 120 buộc phải cho cả dải ephemeral 1024-65535, và 5432 nằm
    # TRONG dải đó — y hệt 1433 trước đây. Xoá rule 95, hay quên đổi số cổng của
    # nó khi đổi engine, là mở 5432 ra 0.0.0.0/0 mà KHÔNG có gì đỏ ở đâu cả.
    { no = 90, proto = "tcp", action = "deny", cidr = "0.0.0.0/0", from = 22, to = 22 },
    { no = 95, proto = "tcp", action = "deny", cidr = "0.0.0.0/0", from = 5432, to = 5432 },
    { no = 100, proto = "tcp", action = "allow", cidr = local.public_tier_cidr, from = 80, to = 80 },
    { no = 110, proto = "tcp", action = "allow", cidr = local.public_tier_cidr, from = 8080, to = 8080 },
    { no = 115, proto = "tcp", action = "deny", cidr = "0.0.0.0/0", from = 8080, to = 8080 },
    # Bắt buộc: return traffic từ internet qua NAT Gateway vào app tier
    # có source 0.0.0.0/0 và destination port ephemeral.
    { no = 120, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 1024, to = 65535 },
  ]

  app_egress = [
    { no = 100, proto = "tcp", action = "allow", cidr = local.db_tier_cidr, from = 5432, to = 5432 },
    { no = 110, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 80, to = 80 },
    { no = 115, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 443, to = 443 },
    # Response về ALB. Chỉ tới public tier, KHÔNG mở ra 0.0.0.0/0.
    { no = 120, proto = "tcp", action = "allow", cidr = local.public_tier_cidr, from = 1024, to = 65535 },
  ]

  # ── nacl-db: chặt nhất, đúng 1 rule mỗi chiều ──────────────────
  db_ingress = [
    { no = 100, proto = "tcp", action = "allow", cidr = local.app_tier_cidr, from = 5432, to = 5432 },
  ]

  db_egress = [
    { no = 100, proto = "tcp", action = "allow", cidr = local.app_tier_cidr, from = 1024, to = 65535 },
  ]
}

# ─── NACL PUBLIC ─────────────────────────────────────────────────
resource "aws_network_acl" "public" {
  vpc_id     = aws_vpc.this.id
  subnet_ids = aws_subnet.public[*].id

  tags = { Name = "${var.project}-nacl-public" }
}

resource "aws_network_acl_rule" "public_ingress" {
  for_each = { for r in local.public_ingress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.public.id
  rule_number    = each.value.no
  egress         = false
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

resource "aws_network_acl_rule" "public_egress" {
  for_each = { for r in local.public_egress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.public.id
  rule_number    = each.value.no
  egress         = true
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

# Rule demo: DENY toàn bộ traffic từ my_ip ở số 50 — nhỏ hơn 100/110 nên
# được xét trước. Chứng minh NACL làm được điều Security Group không làm
# được: SG chỉ có allow-list, không thể chặn riêng một IP.
resource "aws_network_acl_rule" "public_deny_demo" {
  count = var.enable_deny_demo ? 1 : 0

  network_acl_id = aws_network_acl.public.id
  rule_number    = 50
  egress         = false
  protocol       = "-1"
  rule_action    = "deny"
  cidr_block     = var.my_ip
}

# ─── NACL APP ────────────────────────────────────────────────────
resource "aws_network_acl" "app" {
  vpc_id     = aws_vpc.this.id
  subnet_ids = aws_subnet.app[*].id

  tags = { Name = "${var.project}-nacl-app" }
}

resource "aws_network_acl_rule" "app_ingress" {
  for_each = { for r in local.app_ingress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.app.id
  rule_number    = each.value.no
  egress         = false
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

resource "aws_network_acl_rule" "app_egress" {
  for_each = { for r in local.app_egress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.app.id
  rule_number    = each.value.no
  egress         = true
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

# ─── NACL DB ─────────────────────────────────────────────────────
resource "aws_network_acl" "db" {
  vpc_id     = aws_vpc.this.id
  subnet_ids = aws_subnet.db[*].id

  tags = { Name = "${var.project}-nacl-db" }
}

resource "aws_network_acl_rule" "db_ingress" {
  for_each = { for r in local.db_ingress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.db.id
  rule_number    = each.value.no
  egress         = false
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

resource "aws_network_acl_rule" "db_egress" {
  for_each = { for r in local.db_egress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.db.id
  rule_number    = each.value.no
  egress         = true
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}
