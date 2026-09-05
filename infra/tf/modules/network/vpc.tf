# ─── VPC ─────────────────────────────────────────────────────────
resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = { Name = "${var.project}-vpc" }
}

# ─── DEFAULT SECURITY GROUP — khoá về RỖNG ───────────────────────
# AWS tự tạo một security group tên "default" cho MỌI VPC và KHÔNG cho xoá nó.
# Mặc định của nó là "cho phép mọi protocol, mọi port, từ chính nó" — tức nếu
# hai resource cùng nằm trong SG này thì chúng nói chuyện được với nhau qua BẤT
# KỲ port nào, kể cả 22.
#
# Phát hiện lúc chạy lại kiểm thử 2026-08-24: liệt kê MỌI rule ingress trong
# region thì có hai dòng `protocol = -1, from = -1, to = -1` — một của VPC này,
# một của default VPC. `-1` nghĩa là mọi port, nên nó CÓ bao gồm 22. Lúc đó đo
# được 0 ENI đang dùng nên chưa có bề mặt tấn công thật, nhưng đó là may mắn,
# không phải thiết kế: ai launch một instance mà KHÔNG chỉ định security group
# thì AWS gán default SG cho nó, và instance ấy lập tức nằm trong một SG mở.
#
# Khai báo resource này KHÔNG tạo SG mới — Terraform adopt cái AWS đã tạo, rồi
# xoá sạch rule của nó. Khối ingress/egress để TRỐNG là cách diễn đạt "không có
# rule nào", khác với việc bỏ hẳn khối (bỏ hẳn thì Terraform không quản rule và
# giữ nguyên mặc định của AWS).
#
# Không ảnh hưởng gì tới hệ thống: cả 3 SG thật (sg-alb, sg-web, sg-rds) đều
# được khai báo tường minh ở module `security`, không resource nào của ta dùng
# default SG. Giá: $0 — security group không tính phí.
resource "aws_default_security_group" "this" {
  vpc_id = aws_vpc.this.id

  # Cố ý KHÔNG có ingress và egress. Đây là toàn bộ mục đích của resource này.

  tags = { Name = "${var.project}-default-KHONG-DUNG" }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-igw" }
}

# ─── SUBNETS — 3 tier × 2 AZ ─────────────────────────────────────
resource "aws_subnet" "public" {
  count = length(var.public_subnet_cidrs)

  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.public_subnet_cidrs[count.index]
  availability_zone       = var.azs[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name = "${var.project}-public-${substr(var.azs[count.index], -1, 1)}"
    Tier = "public"
  }
}

resource "aws_subnet" "app" {
  count = length(var.app_subnet_cidrs)

  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.app_subnet_cidrs[count.index]
  availability_zone       = var.azs[count.index]
  map_public_ip_on_launch = false

  tags = {
    Name = "${var.project}-app-${substr(var.azs[count.index], -1, 1)}"
    Tier = "app"
  }
}

resource "aws_subnet" "db" {
  count = length(var.db_subnet_cidrs)

  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.db_subnet_cidrs[count.index]
  availability_zone       = var.azs[count.index]
  map_public_ip_on_launch = false

  tags = {
    Name = "${var.project}-db-${substr(var.azs[count.index], -1, 1)}"
    Tier = "db"
  }
}

# ─── NAT GATEWAY (toggle + số lượng) ─────────────────────────────
# `enable_nat` trả lời "có cần egress không". `nat_gateway_count` trả lời
# "egress có cần sống sót khi mất một AZ không". Đó là HAI câu hỏi khác nhau,
# nên là hai biến: gộp lại thì không diễn đạt nổi "bật NAT nhưng chỉ một cái" —
# vốn là cấu hình rẻ nhất và là mặc định.
#
# GIÁ (ap-southeast-1, đo 2026-09-05): $0,059/giờ MỖI gateway, cộng $0,045/GB
# xử lý. Không có bậc miễn phí ở bất kỳ account nào.
locals {
  nat_count = var.enable_nat ? var.nat_gateway_count : 0
}

resource "aws_eip" "nat" {
  count = local.nat_count

  domain = "vpc"

  tags = { Name = "${var.project}-nat-eip-${substr(var.azs[count.index], -1, 1)}" }
}

resource "aws_nat_gateway" "this" {
  count = local.nat_count

  allocation_id = aws_eip.nat[count.index].id

  # NAT thứ i nằm ở public subnet thứ i, tức đúng AZ thứ i. NAT Gateway là
  # resource GẮN VỚI MỘT AZ: nó không tự failover sang AZ khác, và đó chính là
  # lý do nat_gateway_count = 2 tồn tại.
  subnet_id = aws_subnet.public[count.index].id

  tags = { Name = "${var.project}-natgw-${substr(var.azs[count.index], -1, 1)}" }

  depends_on = [aws_internet_gateway.this]
}

# ─── ROUTE TABLES ────────────────────────────────────────────────
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-rt-public" }
}

resource "aws_route" "public_igw" {
  route_table_id         = aws_route_table.public.id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.this.id
}

resource "aws_route_table_association" "public" {
  count = length(aws_subnet.public)

  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

# App tier: MỘT ROUTE TABLE MỖI AZ.
#
# 🚨 Đây là thay đổi cấu trúc, không phải thêm cờ. Trước đó cả hai app subnet
# dùng CHUNG một route table — hoàn toàn đúng khi chỉ có một NAT, vì mọi đường
# ra đều tới cùng một gateway. Nhưng một route table chỉ chứa được MỘT route
# 0.0.0.0/0, nên route table dùng chung làm NAT-per-AZ **không biểu diễn được**:
# hai subnet ở hai AZ buộc phải đi chung một gateway dù có dựng bao nhiêu NAT.
# Tách route table là điều kiện CẦN của nat_gateway_count = 2.
#
# Khi nat_gateway_count = 1 thì cả hai route table cùng trỏ về NAT[0] — hành vi
# y hệt trước đợt này, chỉ khác cách biểu diễn. Route table $0.
#
# Khi NAT tắt, không route table nào có 0.0.0.0/0 ⇒ app tier hoàn toàn không có
# đường ra internet.
resource "aws_route_table" "private" {
  count = length(var.app_subnet_cidrs)

  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-rt-private-${substr(var.azs[count.index], -1, 1)}" }
}

# Route table cũ (không đánh chỉ số) trở thành phần tử [0]. Thiếu khối này thì
# Terraform destroy nó rồi tạo lại — vô hại về tiền nhưng làm plan của lần apply
# kế tiếp trông như mất mạng app tier, và đó là thứ không nên phải đoán.
moved {
  from = aws_route_table.private
  to   = aws_route_table.private[0]
}

# Mỗi app subnet đi ra bằng NAT của CHÍNH AZ mình — khi có đủ NAT.
#
# `min(count.index, local.nat_count - 1)` là chỗ hai cấu hình gặp nhau:
#   • nat_count = 1 → min(0,0)=0 và min(1,0)=0 ⇒ cả hai AZ dùng NAT[0]. Traffic
#     của app-b sang NAT ở AZ-a là CROSS-AZ: $0,01/GB MỖI CHIỀU, cộng thêm trên
#     $0,045/GB xử lý của NAT. Khoản này không hiện ở dòng nào tên "NAT".
#   • nat_count = 2 → 0 và 1 ⇒ mỗi AZ tự đi ra. Hết cross-AZ, và mất một AZ
#     không cắt egress của AZ còn lại.
resource "aws_route" "private_nat" {
  count = local.nat_count > 0 ? length(aws_route_table.private) : 0

  route_table_id         = aws_route_table.private[count.index].id
  destination_cidr_block = "0.0.0.0/0"
  nat_gateway_id         = aws_nat_gateway.this[min(count.index, local.nat_count - 1)].id
}

resource "aws_route_table_association" "app" {
  count = length(aws_subnet.app)

  subnet_id      = aws_subnet.app[count.index].id
  route_table_id = aws_route_table.private[count.index].id
}

# DB tier: route table riêng, KHÔNG BAO GIỜ có route ra internet.
resource "aws_route_table" "db" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-rt-db" }
}

resource "aws_route_table_association" "db" {
  count = length(aws_subnet.db)

  subnet_id      = aws_subnet.db[count.index].id
  route_table_id = aws_route_table.db.id
}

# ─── S3 GATEWAY ENDPOINT ─────────────────────────────────────────
# Miễn phí. Traffic S3 không đi qua NAT nên không bị tính $0.045/GB,
# và vẫn hoạt động khi NAT đã destroy.
resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.this.id
  service_name      = "com.amazonaws.${data.aws_region.current.region}.s3"
  vpc_endpoint_type = "Gateway"

  # Phải phủ MỌI private route table. Sót một cái thì subnet của nó đi S3 vòng
  # qua NAT: vẫn chạy, không lỗi, chỉ âm thầm tính $0,045/GB.
  route_table_ids = concat(
    aws_route_table.private[*].id,
    [aws_route_table.db.id],
  )

  tags = { Name = "${var.project}-s3-endpoint" }
}

data "aws_region" "current" {}

# ─── VPC FLOW LOGS (toggle, default tắt) ─────────────────────────
resource "aws_cloudwatch_log_group" "flow" {
  count = var.enable_flow_logs ? 1 : 0

  name              = "/vpc/${var.project}/flowlogs"
  retention_in_days = var.flow_log_retention_days
}

data "aws_iam_policy_document" "flow_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["vpc-flow-logs.amazonaws.com"]
    }
  }
}

data "aws_iam_policy_document" "flow_write" {
  statement {
    effect = "Allow"

    actions = [
      "logs:CreateLogStream",
      "logs:PutLogEvents",
      "logs:DescribeLogGroups",
      "logs:DescribeLogStreams",
    ]

    resources = ["arn:aws:logs:*:*:log-group:/vpc/${var.project}/flowlogs:*"]
  }
}

resource "aws_iam_role" "flow" {
  count = var.enable_flow_logs ? 1 : 0

  name               = "${var.project}-flowlogs-role"
  assume_role_policy = data.aws_iam_policy_document.flow_assume.json
}

resource "aws_iam_role_policy" "flow" {
  count = var.enable_flow_logs ? 1 : 0

  name   = "${var.project}-flowlogs-write"
  role   = aws_iam_role.flow[0].id
  policy = data.aws_iam_policy_document.flow_write.json
}

resource "aws_flow_log" "vpc" {
  count = var.enable_flow_logs ? 1 : 0

  vpc_id               = aws_vpc.this.id
  traffic_type         = "REJECT"
  iam_role_arn         = aws_iam_role.flow[0].arn
  log_destination      = aws_cloudwatch_log_group.flow[0].arn
  log_destination_type = "cloud-watch-logs"

  tags = { Name = "${var.project}-flowlogs-reject" }
}
