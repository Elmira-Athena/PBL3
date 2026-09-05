variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
  default     = "hushstore"
}

variable "region" {
  description = "Vùng AWS"
  type        = string
  default     = "ap-southeast-1"
}

variable "profile" {
  description = "AWS CLI profile dùng để authenticate"
  type        = string
  default     = "hushstore"
}

variable "azs" {
  description = "Hai Availability Zone dùng cho toàn bộ stack"
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

variable "vpc_cidr" {
  description = "CIDR của VPC — 10.20.0.0/16, KHÔNG trùng 10.0.0.0/16 của stack cũ"
  type        = string
  default     = "10.20.0.0/16"
}

variable "public_subnet_cidrs" {
  description = "CIDR của 2 public subnet"
  type        = list(string)
  default     = ["10.20.0.0/24", "10.20.1.0/24"]
}

variable "app_subnet_cidrs" {
  description = "CIDR của 2 app subnet"
  type        = list(string)
  default     = ["10.20.10.0/24", "10.20.11.0/24"]
}

variable "db_subnet_cidrs" {
  description = "CIDR của 2 db subnet"
  type        = list(string)
  default     = ["10.20.20.0/24", "10.20.21.0/24"]
}

variable "nat_gateway_count" {
  description = "Số NAT Gateway khi enable_nat = true. 1 = mặc định rẻ nhất; 2 = mỗi AZ một cái (+$0,059/giờ), egress không chết theo một AZ"
  type        = number
  default     = 1
}

variable "enable_nat" {
  description = "Tạo NAT Gateway — $0.045/giờ. Chỉ bật khi cần pull ECR hoặc dùng SSM"
  type        = bool
  default     = false
}

variable "enable_alb" {
  description = "Bật serving stack: ALB + 2 target group + listener + 2 ECS service. $0.0225/giờ"
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

variable "db_engine_version" {
  description = "Major version của PostgreSQL. Dùng prefix ('17'), ĐỪNG ghim minor — 17.5/17.6 hết hỗ trợ 31/10/2026. Liệt kê bằng: aws rds describe-db-engine-versions --engine postgres --query 'DBEngineVersions[].EngineVersion' --output text"
  type        = string
}

# ─── HAI CÔNG TẮC ĐỢT 7 — cả hai mặc định TẮT ────────────────────
variable "enable_multi_az" {
  description = "Bật Multi-AZ cho RDS trong cửa sổ demo. Standby KHÔNG phục vụ đọc — availability, không phải read scaling."
  type        = bool
  default     = false
}

variable "enable_read_replica" {
  description = "Dựng read replica. Bật là AWS TỪ CHỐI stop primary ⇒ cơ chế tắt tiền mất tác dụng. Chỉ bật trong cửa sổ đo, huỷ ngay sau đó."
  type        = bool
  default     = false
}

variable "alert_email" {
  description = "Email nhận cảnh báo chi phí từ AWS Budgets. Phải bấm xác nhận trong mail AWS gửi mới nhận được cảnh báo"
  type        = string
}

variable "enable_budget" {
  description = "Tạo AWS Budget cho dự án. AWS chỉ cho 2 budget miễn phí mỗi account; cái thứ 3 tốn $0.02/ngày (~$0.60/tháng). Đặt false khi 2 slot đã bị người dùng chung account chiếm. TẮT = mất lớp backstop, chỉ còn Lambda cost guard, mà chế độ chết của Lambda là im lặng tuyệt đối"
  type        = bool
  default     = true
}

variable "monthly_budget_usd" {
  description = "Ngưỡng ngân sách tháng (USD). Cảnh báo ở 25%, 50%, 100% thực tế và 100% dự báo"
  type        = number
  default     = 20
}

variable "instance_count" {
  description = "Số EC2 container instance ĐANG chạy — trạng thái, do up.sh/down.sh lật. 0 = tắt hoàn toàn, về $0"
  type        = number
  default     = 0
}

# TRẦN, không phải trạng thái. Xem khối comment ở modules/ecs/variables.tf để
# biết vì sao nâng nó lên 2 lại đòi một cờ khai tường minh.
#
# Giữ mặc định 1 là cố ý: max_size cũng là bán kính thiệt hại nếu có lỗi làm
# ASG scale ngoài ý muốn — modules/costguard/lambda.tf ghi rõ nó dựa vào chính
# trần này làm lớp chặn cuối. Nâng trần là nâng luôn bán kính đó, nên phải là
# một quyết định được gõ ra, không phải mặc định thừa hưởng.
variable "max_instance_count" {
  description = "TRẦN số EC2 container instance (1 hoặc 2). > 1 đòi rate_limiter_is_distributed = true"
  type        = number
  default     = 1
}

variable "rate_limiter_is_distributed" {
  description = "Lời khai: bộ đếm rate limit của API đã dùng chung giữa các task (Redis/ElastiCache) hoặc đã đẩy lên WAF. ĐIỀU KIỆN để max_instance_count > 1"
  type        = bool
  default     = false
}

variable "instance_type" {
  description = "Instance type của container instance. t3.micro free tier, 1GB RAM"
  type        = string
  default     = "t3.micro"
}

variable "image_tag" {
  description = "Git SHA của 3 image trên ECR. Lấy bằng: git rev-parse HEAD"
  type        = string
}

variable "web_domain" {
  description = "Domain của Blazor client"
  type        = string
  default     = "hushstore.io.vn"
}

variable "api_domain" {
  description = "Domain của API"
  type        = string
  default     = "api.hushstore.io.vn"
}

variable "seeder_image_tag" {
  description = "Git SHA riêng cho image seeder. Để rỗng (mặc định) thì seeder dùng chung image_tag — đó là trạng thái đúng từ Phase 2 trở đi vì pipeline build cả 4 image ở cùng một commit"
  type        = string
  default     = ""
}

variable "shared_notifications" {
  description = "Ngưỡng cảnh báo ngân sách của người khác dùng chung account. Khai ở đây để terraform apply không xoá chúng. Giá trị đặt trong terraform.tfvars (bị gitignore) vì chứa email của người ngoài dự án"

  type = list(object({
    threshold         = number
    notification_type = string
    emails            = list(string)
  }))

  default = []
}

# ─── PHASE 3: LAMBDA COST GUARD ──────────────────────────────────

variable "enable_auto_stop" {
  description = "Bật EventBridge Scheduler chạy Lambda cost guard 00:00 giờ Việt Nam mỗi đêm. MIỄN PHÍ (Scheduler free 14 triệu lượt/tháng, Lambda 30 lượt/tháng nằm trong free tier). Chỉ đặt false khi CỐ TÌNH để stack chạy qua đêm — lúc đó không còn lưới an toàn nào cho việc RDS tự khởi động lại sau 7 ngày"
  type        = bool
  default     = true
}

variable "stop_cron" {
  description = "Giờ chạy cost guard, theo giờ Việt Nam (timezone Asia/Ho_Chi_Minh hardcode trong module). Mặc định 00:00 hằng đêm"
  type        = string
  default     = "cron(0 0 * * ? *)"
}
