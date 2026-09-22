# ═══════════════════════════════════════════════════════════════════
# MÔI TRƯỜNG DEV
#
# main.tf và outputs.tf ở thư mục này là SYMLINK sang ../prod/. Đó là cố ý:
# hai môi trường phải dựng CÙNG một kiến trúc, khác nhau ở giá trị chứ không
# khác ở cấu trúc. Copy 200 dòng main.tf sang đây là tạo hai nguồn sự thật —
# và loại lỗi sinh ra từ đó là loại tệ nhất: dev xanh, prod đỏ, vì một module
# block chỉ được thêm ở một bên.
#
# Hệ quả phải biết: thêm một `var.foo` mới vào ../prod/main.tf mà quên khai ở
# ĐÂY thì `terraform validate` của dev sẽ đỏ với "Reference to undeclared input
# variable". Đó là hỏng TO TIẾNG, có chủ ý — CI chạy validate cho cả hai env
# nên drift bị chặn ngay ở PR, không đợi tới lúc apply.
#
# 🚨 BA THỨ KHÔNG ĐƯỢC TRÙNG PROD, vì trùng là hai stack giẫm lên nhau:
#   1. backend key      → "dev/terraform.tfstate"  (xem backend.tf)
#   2. var.project      → "hushstore-dev"          (tên mọi resource + tag lọc)
#   3. var.vpc_cidr     → 10.30.0.0/16             (không chồng 10.20.0.0/16)
# ═══════════════════════════════════════════════════════════════════

variable "project" {
  description = "Tiền tố tên cho mọi resource. PHẢI khác prod: nó vừa là tên resource vừa là tag:Project mà status.sh / cost guard / down.sh lọc theo. Trùng tên là down.sh của dev đi tắt RDS của prod"
  type        = string
  default     = "hushstore-dev"

  validation {
    condition     = var.project != "hushstore"
    error_message = "project = \"hushstore\" là giá trị của PROD. Dev trùng tiền tố nghĩa là mọi script lọc theo tag:Project sẽ tác động lên cả hai stack."
  }
}

variable "region" {
  description = "Vùng AWS. Giữ cùng region với prod để đo được cùng đơn giá"
  type        = string
  default     = "ap-southeast-1"
}

variable "profile" {
  description = "AWS CLI profile dùng để authenticate"
  type        = string
  default     = "hushstore"
}

variable "azs" {
  description = "Hai Availability Zone. Vẫn 2 AZ dù dev không cần HA — ALB và DB subnet group bắt buộc 2 AZ, hạ xuống 1 là không apply được"
  type        = list(string)
  default     = ["ap-southeast-1a", "ap-southeast-1b"]

  validation {
    condition     = length(var.azs) == 2
    error_message = "Phải khai báo đúng 2 AZ — ALB cần tối thiểu 2 subnet ở 2 AZ khác nhau."
  }
}

variable "my_ip" {
  description = "IP công cộng của máy tấn công (laptop), dạng CIDR /32. Lấy bằng: curl -s https://checkip.amazonaws.com"
  type        = string

  validation {
    condition     = can(cidrhost(var.my_ip, 0)) && endswith(var.my_ip, "/32")
    error_message = "my_ip phải là CIDR /32, ví dụ 203.0.113.45/32."
  }
}

# ─── CIDR — KHÔNG ĐƯỢC CHỒNG PROD ────────────────────────────────
# Prod dùng 10.20.0.0/16. Dev dùng 10.30.0.0/16 với ĐÚNG cùng cách chia
# offset (x.x.0/1.0 public, x.x.10/11.0 app, x.x.20/21.0 db), nên mọi rule
# NACL sinh ra từ cidrsubnet() giữ nguyên hình dạng, chỉ đổi octet thứ hai.
# Hai dải không chồng nhau là điều kiện để sau này peer được hai VPC (ví dụ
# khi muốn một bastion dùng chung) mà không phải dựng lại.
variable "vpc_cidr" {
  description = "CIDR của VPC dev — 10.30.0.0/16, KHÔNG chồng 10.20.0.0/16 của prod"
  type        = string
  default     = "10.30.0.0/16"

  validation {
    condition     = var.vpc_cidr != "10.20.0.0/16"
    error_message = "10.20.0.0/16 là CIDR của PROD. Hai VPC chồng dải thì không bao giờ peer được với nhau."
  }
}

variable "public_subnet_cidrs" {
  description = "CIDR của 2 public subnet"
  type        = list(string)
  default     = ["10.30.0.0/24", "10.30.1.0/24"]
}

variable "app_subnet_cidrs" {
  description = "CIDR của 2 app subnet"
  type        = list(string)
  default     = ["10.30.10.0/24", "10.30.11.0/24"]
}

variable "db_subnet_cidrs" {
  description = "CIDR của 2 db subnet"
  type        = list(string)
  default     = ["10.30.20.0/24", "10.30.21.0/24"]
}

# ─── CHỖ DEV CỐ Ý KHÁC PROD ──────────────────────────────────────
# Prod ghim nat_gateway_count = 2 và enable_multi_az = true vì đó là thuộc
# tính đã ghi vào báo cáo. Dev hạ cả hai xuống: nó tồn tại để thử `apply` và
# thử một thay đổi Terraform trước khi đụng vào prod, KHÔNG để chứng minh tính
# sẵn sàng cao. Giữ cả hai ở mức prod là trả gấp đôi tiền cho một môi trường
# mà không ai đo tính sẵn sàng của nó.
variable "nat_gateway_count" {
  description = "1 cho dev (prod ghim 2). Cả 2 AZ đi chung một NAT: rẻ hơn $0,059/giờ, đổi lại mất egress ở AZ-b khi AZ-a chết — dev chấp nhận được"
  type        = number
  default     = 1
}

variable "enable_multi_az" {
  description = "false cho dev (prod ghim true). Multi-AZ tính tiền storage cho CẢ HAI AZ kể cả khi RDS stopped, tức nó nâng SÀN chi phí ~$2,3/tháng không tắt được bằng down.sh. Dev không cần cái sàn đó"
  type        = bool
  default     = false
}

variable "enable_nat" {
  description = "Tạo NAT Gateway — $0,059/giờ. Chỉ bật khi cần pull ECR hoặc dùng SSM"
  type        = bool
  default     = false
}

variable "enable_alb" {
  description = "Bật serving stack: ALB + 2 target group + listener + 2 ECS service. $0,0252/giờ"
  type        = bool
  default     = false
}

variable "enable_flow_logs" {
  description = "Bật VPC Flow Logs (chỉ REJECT) cho báo cáo bảo mật"
  type        = bool
  default     = false
}

variable "enable_deny_demo" {
  description = "Bật NACL rule 50 DENY my_ip — kịch bản kiểm thử số 8"
  type        = bool
  default     = false
}

variable "enable_ecr_endpoints" {
  description = "Dựng 2 interface VPC Endpoint cho ECR. ~$0,04/giờ — TÍNH TIỀN NGAY khi apply, không đợi up.sh. Đây là chỗ đúng để thử nó trước khi bật trên prod"
  type        = bool
  default     = false
}

variable "db_engine_version" {
  description = "Major version của PostgreSQL. Dùng prefix ('17'), ĐỪNG ghim minor. GIỮ BẰNG PROD — dev khác version thì nó không còn là nơi thử migration nữa"
  type        = string
}

variable "enable_read_replica" {
  description = "GIỮ false. Có replica thì AWS TỪ CHỐI stop primary ⇒ down.sh và cost guard mất tác dụng, mà RDS stopped lại tự khởi động sau 7 ngày"
  type        = bool
  default     = false
}

variable "alert_email" {
  description = "Email nhận cảnh báo chi phí. Phải bấm xác nhận trong mail AWS gửi mới nhận được"
  type        = string
}

variable "enable_budget" {
  description = "false cho dev. AWS chỉ cho 2 budget miễn phí mỗi ACCOUNT — dev và prod dùng chung hạn mức đó, nên budget của dev là cái thứ 3 và tốn $0,02/ngày"
  type        = bool
  default     = false
}

variable "monthly_budget_usd" {
  description = "Ngưỡng ngân sách tháng (USD). Chỉ có tác dụng khi enable_budget = true"
  type        = number
  default     = 5
}

variable "instance_count" {
  description = "Số EC2 container instance ĐANG chạy — trạng thái, do up.sh/down.sh lật. 0 = tắt hoàn toàn"
  type        = number
  default     = 0
}

variable "max_instance_count" {
  description = "TRẦN số EC2 container instance. Giữ 1: dev không đo hành vi nhiều instance, mà chính LoadProbe ở local mới là nơi đo việc đó"
  type        = number
  default     = 1
}

variable "rate_limiter_is_distributed" {
  description = "Lời khai: bộ đếm rate limit đã dùng chung giữa mọi task. ĐIỀU KIỆN để max_instance_count > 1"
  type        = bool
  default     = false
}

variable "instance_type" {
  description = "Instance type của container instance"
  type        = string
  default     = "t3.micro"
}

variable "image_tag" {
  description = "Git SHA của image trên ECR dev. CHÚ Ý: 4 repo ECR của dev là repo RIÊNG (hushstore-dev-api…), không dùng chung image đã push cho prod — pipeline hiện chỉ push vào repo của prod"
  type        = string
}

variable "seeder_image_tag" {
  description = "Git SHA riêng cho image seeder. Rỗng = dùng chung image_tag"
  type        = string
  default     = ""
}

variable "web_domain" {
  description = "Domain của Blazor client. ACM sẽ ở trạng thái PENDING_VALIDATION tới khi thêm bản ghi CNAME trên Cloudflare — không tốn tiền, và chỉ chặn apply khi enable_alb = true"
  type        = string
  default     = "dev.hushstore.io.vn"
}

variable "api_domain" {
  description = "Domain của API dev"
  type        = string
  default     = "api-dev.hushstore.io.vn"
}

variable "shared_notifications" {
  description = "Ngưỡng cảnh báo ngân sách của người khác dùng chung account. Để rỗng ở dev"

  type = list(object({
    threshold = number
    emails    = list(string)
  }))

  default = []
}

variable "enable_auto_stop" {
  description = "GIỮ true. Cost guard 00:00 giờ VN là lưới an toàn duy nhất khi enable_budget = false — và ở dev thì enable_budget LUÔN false"
  type        = bool
  default     = true
}

variable "stop_cron" {
  description = "Giờ chạy cost guard, theo giờ Việt Nam"
  type        = string
  default     = "cron(0 0 * * ? *)"
}
