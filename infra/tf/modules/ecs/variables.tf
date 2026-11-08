variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "assets_bucket_arn" {
  description = "ARN bucket ảnh sản phẩm — task role của app chỉ được ghi/đọc object trong đây"
  type        = string
}

variable "artifacts_bucket_arn" {
  description = "ARN bucket artifacts — host đọc seed SQL và file ops từ đây"
  type        = string
}

variable "ssm_connection_string_arn" {
  description = "ARN parameter connection string — ECS agent inject vào container"
  type        = string
}

variable "ssm_jwt_secret_arn" {
  description = "ARN của parameter chứa JWT secret"
  type        = string
}

variable "app_subnet_ids" {
  description = "ID của 2 app subnet — nơi ASG đặt container instance"
  type        = list(string)
}

variable "web_sg_id" {
  description = "ID Security Group của container instance"
  type        = string
}

# ─── SỐ INSTANCE: TRẦN, TRẠNG THÁI, VÀ ĐIỀU KIỆN ĐỂ NÂNG TRẦN ────
#
# Ba biến dưới đây tách ba thứ khác nhau, đừng gộp:
#   • max_instance_count          — TRẦN. Ý định của người vận hành.
#   • instance_count              — TRẠNG THÁI. up.sh/down.sh lật; 0 = $0 thật.
#   • rate_limiter_is_distributed — TIỀN ĐỀ TẦNG APP cho phép trần > 1.
#
# 🚨 Vì sao có biến thứ ba, và vì sao nó KHÔNG phải thủ tục rườm rà:
# Trần cũ bị ghim = 1 bởi một test (`tests/cluster.tftest.hcl`) với lý do ghi
# thẳng trong error_message: rate limiter của API đếm trong RAM TIẾN TRÌNH, nên
# 2 task API biến "5 lần đăng nhập/phút mỗi IP" thành 10. Đó không phải suy đoán
# — nó là kịch bản KB6 của báo cáo bảo mật, đã đo được "req 1-5 → 400,
# req 6-20 → 429". Nâng trần lên 2 khi rate limiter còn in-process KHÔNG làm
# hỏng dữ liệu; nó làm **bằng chứng đã nộp trở thành sai sự thật**, và sai âm
# thầm: không log nào, không alarm nào, ALB vẫn xanh.
#
# Nên trần vẫn nâng được — nhưng phải khai tường minh rằng tiền đề đã xử lý.
# Xoá chốt bằng cách sửa test là bỏ mất chính thứ nó tồn tại để nhắc.
#
# Tiền đề coi là ĐÃ XỬ LÝ khi bộ đếm của BỐN policy XÁC THỰC — LoginRateLimit,
# RegisterRateLimit, RefreshRateLimit, LookupRateLimit — nằm ở nơi dùng chung
# giữa mọi task (bảng `RateLimitCounters` trong PostgreSQL, hoặc Redis), hoặc
# rate limit được đẩy lên tầng trước ALB (WAF rate-based rule). Mọi lối đó đều
# KHÔNG kiểm được từ Terraform, nên đây là lời khai của người vận hành, không
# phải phép đo.
#
# ⚠️ CỜ NÀY KHÔNG NÓI GÌ VỀ HAI POLICY CÒN LẠI, và đó là chủ ý, không phải sót.
# `GlobalLimiter` (100 req/10 giây) và `PublicReadRateLimit` (60 req/phút) vẫn
# đếm trong RAM TỪNG TIẾN TRÌNH, nên với N instance chúng thành 100N và 60N.
# Lý do giữ vậy: hai cái đó chạm MỌI request duyệt catalogue; đặt một lần ghi DB
# lên đường nóng đó là biến DB thành cổ chai — đúng ngược mục đích của việc
# scale ra. Đánh đổi được chấp nhận vì chúng là chốt chặn burst THÔ, không phải
# bất biến đang được chấm: không bằng chứng nào đã nộp dựa vào con số của chúng,
# khác hẳn 5 req/phút của KB6. Đừng "sửa" hai cái này để cờ trông trọn vẹn hơn.
variable "max_instance_count" {
  description = "TRẦN số EC2 container instance (max_size của ASG). > 1 đòi rate_limiter_is_distributed = true"
  type        = number
  default     = 1

  validation {
    condition     = var.max_instance_count >= 1 && var.max_instance_count <= 2
    error_message = "max_instance_count chỉ được 1 hoặc 2 — t3.micro 1GB RAM và ngân sách của dự án không đỡ nổi hơn."
  }

  validation {
    condition     = var.max_instance_count == 1 || var.rate_limiter_is_distributed
    error_message = "max_instance_count > 1 đòi rate_limiter_is_distributed = true. Cờ đó khẳng định BỐN policy XÁC THỰC — LoginRateLimit, RegisterRateLimit, RefreshRateLimit, LookupRateLimit — đã đếm ở nơi dùng chung giữa mọi task (bảng RateLimitCounters trong PostgreSQL, hoặc Redis, hoặc WAF), nên 5 lần đăng nhập/phút mỗi IP vẫn là 5 với N task và kịch bản KB6 của báo cáo bảo mật vẫn đúng sự thật. Chưa làm điều đó thì đừng khai cờ: rate limiter đếm trong RAM tiến trình (src/API/Program.cs, RateLimitPartition.GetFixedWindowLimiter) biến 5 req/phút thành 5N, và sai âm thầm — không log, không alarm, ALB vẫn xanh. Cờ này KHÔNG khẳng định GlobalLimiter và PublicReadRateLimit: hai policy đó CỐ Ý vẫn per-instance (thành 100N req/10 giây và 60N req/phút) vì chúng chạm mọi request duyệt catalogue, và ghi DB trên đường nóng đó là biến DB thành cổ chai — đúng ngược mục đích của việc scale ra. Đánh đổi đó ĐƯỢC CHẤP NHẬN: chúng là chốt chặn burst thô, không có bằng chứng nào đã nộp dựa vào con số của chúng."
  }
}

variable "rate_limiter_is_distributed" {
  description = "Lời khai: bộ đếm của 4 policy XÁC THỰC (login/register/refresh/lookup) đã dùng chung giữa mọi task — bảng RateLimitCounters trong PostgreSQL, hoặc Redis/ElastiCache, hoặc đã đẩy lên WAF. KHÔNG nói gì về GlobalLimiter và PublicReadRateLimit: hai policy đó cố ý vẫn đếm per-instance (100N/10 giây và 60N/phút) và điều đó được chấp nhận. Là ĐIỀU KIỆN để max_instance_count > 1"
  type        = bool
  default     = false
}

variable "instance_count" {
  description = "desired_capacity của ASG — TRẠNG THÁI, không phải trần. 0 = tắt hoàn toàn (xoá cả EBS root)"
  type        = number
  default     = 0

  validation {
    condition     = var.instance_count >= 0 && var.instance_count <= var.max_instance_count
    error_message = "instance_count phải nằm trong [0, max_instance_count]. Đặt cao hơn trần thì ASG im lặng kẹp lại và desired_capacity thật khác con số trong tfvars."
  }
}

variable "instance_type" {
  description = "Instance type. t3.micro chỉ 1GB RAM (đã bù bằng 2GB swap). Account này KHÔNG có free tier 12 tháng nên giờ instance trả bằng credit/thẻ — xem credit_specification ở cluster.tf"
  type        = string
  default     = "t3.micro"
}

variable "root_volume_size" {
  description = "Dung lượng EBS root (GB). 30GB là mức free tier CŨ (12 tháng) — account hiện tại không có, EBS tính tiền từ GB đầu tiên"
  type        = number
  default     = 30
}

variable "log_retention_days" {
  description = "Số ngày giữ log container trong CloudWatch"
  type        = number
  default     = 3
}

variable "ecr_api_url" {
  description = "URL repository ECR của image API"
  type        = string
}

variable "ecr_web_url" {
  description = "URL repository ECR của image web"
  type        = string
}

variable "ecr_migrator_url" {
  description = "URL repository ECR của image migrator"
  type        = string
}

variable "image_tag" {
  description = "Tag của cả 3 image — LUÔN là git SHA đầy đủ, không bao giờ dùng latest"
  type        = string

  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.image_tag))
    error_message = "image_tag phải là git SHA đầy đủ (40 ký tự hex thường) — không được là 'latest', tên nhánh, hay SHA rút gọn."
  }
}

variable "assets_bucket_name" {
  description = "Tên bucket ảnh sản phẩm — truyền vào container API qua AwsSettings__BucketName"
  type        = string
}

variable "allowed_origins" {
  description = "Origin được CORS cho phép, phân cách bằng dấu phẩy"
  type        = string
}

variable "api_memory_hard" {
  description = "Giới hạn cứng RAM (MiB) của container API"
  type        = number
  default     = 512
}

variable "api_memory_reservation" {
  description = "RAM (MiB) đặt trước cho container API. Dùng soft limit để không bị OOM-kill sớm trên t3.micro"
  type        = number
  default     = 384
}

variable "enable_alb" {
  description = "Bật serving stack. Service bị gate theo biến này vì ECS CreateService fail nếu target group chưa gắn vào load balancer"
  type        = bool
  default     = false
}

variable "tg_web_arn" {
  description = "ARN target group của Blazor client. Rỗng khi enable_alb = false — module alb trả về \"\" chứ không phải null"
  type        = string
  default     = ""
}

variable "tg_api_arn" {
  description = "ARN target group của API. Rỗng khi enable_alb = false — module alb trả về \"\" chứ không phải null"
  type        = string
  default     = ""
}

# Host port là STATIC (80 cho web, 8080 cho api), nên mỗi instance chứa được
# ĐÚNG MỘT task của mỗi service. Hệ quả: service_desired_count phải ≤ số
# instance đang chạy, không phải ≤ trần.
#
# 🚨 Đặt cao hơn số instance ĐANG chạy thì task thừa không xếp được lên đâu
# (port đã bị chiếm), service không bao giờ stable, và `aws ecs wait
# services-stable` trong deploy.yml treo tới timeout rồi rollback — một deploy
# hỏng vì một con số, không vì code. Vì vậy envs/prod truyền
# `service_desired_count = var.instance_count`, tức bám TRẠNG THÁI chứ không
# bám trần.
variable "service_desired_count" {
  description = "Số task mỗi service. Phải bằng instance_count — static host port cho đúng 1 task/service/instance"
  type        = number
  default     = 1

  validation {
    condition     = var.service_desired_count >= 0 && var.service_desired_count <= var.max_instance_count
    error_message = "service_desired_count phải nằm trong [0, max_instance_count]. Static host port không cho 2 task cùng port trên một instance."
  }
}

variable "ssm_db_password_arn" {
  description = "ARN của SSM parameter chứa mật khẩu master của RDS. CHỈ task seeder dùng — sqlcmd không nhận connection string kiểu .NET nên phải truyền mật khẩu rời"
  type        = string
}

variable "ecr_seeder_url" {
  description = "URL repository ECR của image seeder"
  type        = string
}

variable "seeder_image_tag" {
  description = "Git SHA riêng cho image seeder. Để RỖNG (mặc định) là đúng: khi rỗng, seeder dùng chung image_tag với 3 image kia. Chỉ đặt giá trị khi cần ghim seeder vào một SHA khác — trước Phase 2 điều đó là bắt buộc vì image seeder được build sau, còn từ Phase 2 pipeline build cả 4 ở cùng một commit"
  type        = string
  default     = ""

  validation {
    condition     = var.seeder_image_tag == "" || can(regex("^[0-9a-f]{40}$", var.seeder_image_tag))
    error_message = "seeder_image_tag phải rỗng (dùng chung image_tag) hoặc là git SHA đầy đủ 40 ký tự hex — không dùng latest hay tag tự đặt."
  }
}

variable "rds_host" {
  description = "Hostname của RDS (không kèm port). Task seeder truyền vào sqlcmd -S"
  type        = string
}

variable "db_name" {
  description = "Tên database để seed"
  type        = string
  default     = "HushStoreDB"
}

variable "db_username" {
  description = "User đăng nhập SQL Server cho task seeder"
  type        = string
}
