variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "github_owner" {
  description = "Owner của repository trên GitHub. PHẢI đúng chữ hoa/chữ thường như GitHub ghi — claim `sub` của OIDC token phân biệt hoa thường, và trust policy so sánh bằng StringEquals"
  type        = string
  default     = "Elmira-Athena"
}

variable "github_repo" {
  description = "Tên repository trên GitHub, cũng phân biệt hoa thường"
  type        = string
  default     = "PBL3"
}

variable "deploy_branch" {
  description = "Nhánh DUY NHẤT được phép assume role deploy. Mọi nhánh khác, mọi tag, mọi PR đều bị từ chối ở tầng trust policy"
  type        = string
  default     = "main"
}

variable "ecr_repository_arns" {
  description = "ARN của 4 ECR repository mà pipeline được push. Liệt kê tường minh thay vì wildcard để role không push được vào repo lạ nếu ai đó tạo thêm"
  type        = list(string)

  validation {
    condition     = length(var.ecr_repository_arns) > 0
    error_message = "Phải truyền ít nhất một ECR repository ARN."
  }
}

variable "cluster_arn" {
  description = "ARN của ECS cluster — dùng làm condition ecs:cluster cho những action AWS không hỗ trợ resource-level"
  type        = string
}

variable "cluster_name" {
  description = "Tên ECS cluster — dùng để dựng ARN của service (service ARN có dạng .../service/<cluster>/<service>)"
  type        = string
}

variable "service_names" {
  description = "Tên 2 ECS service được phép UpdateService. Truyền TÊN chứ không truyền ARN của resource: service chỉ tồn tại khi enable_alb = true, nên tham chiếu aws_ecs_service[0].arn sẽ vỡ mỗi lần tắt stack"
  type        = list(string)
}

variable "migrator_taskdef_family" {
  description = "Family của task definition migrator. RunTask bị giới hạn đúng family này — pipeline không chạy được task api, web hay seeder"
  type        = string
}

variable "passable_role_arns" {
  description = "Các IAM role mà pipeline được PassRole khi đăng ký task definition: execution role, task app role, task migrator role. KHÔNG gồm role của EC2 host và KHÔNG gồm execution role của seeder"
  type        = list(string)
}

variable "migrator_log_group_arn" {
  description = "ARN log group của migrator. Pipeline chỉ đọc log group này để in nguyên nhân khi migration fail — không đọc được log của api hay web"
  type        = string
}

variable "rds_instance_arn" {
  description = "ARN của RDS instance — để tạo snapshot trước khi migrate"
  type        = string
}

variable "artifacts_bucket_arn" {
  description = "ARN bucket artifacts. Quyền ghi bị giới hạn vào prefix migrations/, không phải cả bucket"
  type        = string
}

variable "snapshot_prefix" {
  description = "Tiền tố tên snapshot do pipeline tạo. Quyền DeleteDBSnapshot bị giới hạn theo đúng tiền tố này, nên pipeline không xoá được snapshot do người tạo tay hay snapshot cuối cùng lúc destroy"
  type        = string
  default     = "pre-migrate"
}
