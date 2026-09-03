# Các module block được thêm dần theo từng task của Phase 1.
# Task 3-4: module "network"
# Task 5:   module "security"
# Task 6:   module "storage"
# Task 7:   module "data"
# Task 11:  module "ecs" (IAM roles)
# Task 12:  module "ecs" (cluster + ASG)
# Task 13:  module "ecs" (task definitions)
# Task 14:  module "alb"
# Task 15:  module "ecs" (services)

locals {
  name = var.project
}

module "network" {
  source = "../../modules/network"

  project             = local.name
  vpc_cidr            = var.vpc_cidr
  azs                 = var.azs
  public_subnet_cidrs = var.public_subnet_cidrs
  app_subnet_cidrs    = var.app_subnet_cidrs
  db_subnet_cidrs     = var.db_subnet_cidrs
  my_ip               = var.my_ip
  enable_nat          = var.enable_nat
  enable_flow_logs    = var.enable_flow_logs
  enable_deny_demo    = var.enable_deny_demo
}

module "security" {
  source = "../../modules/security"

  project = local.name
  vpc_id  = module.network.vpc_id
}

module "storage" {
  source = "../../modules/storage"

  project = local.name
  region  = var.region
}

module "data" {
  source = "../../modules/data"

  project        = local.name
  db_subnet_ids  = module.network.db_subnet_ids
  rds_sg_id      = module.security.rds_sg_id
  engine_version = var.db_engine_version
}

module "ecs" {
  source = "../../modules/ecs"

  project              = local.name
  assets_bucket_arn    = module.storage.assets_bucket_arn
  artifacts_bucket_arn = module.storage.artifacts_bucket_arn

  ssm_connection_string_arn = module.data.ssm_connection_string_arn
  ssm_jwt_secret_arn        = module.data.ssm_jwt_secret_arn

  app_subnet_ids = module.network.app_subnet_ids
  web_sg_id      = module.security.web_sg_id
  instance_count = var.instance_count
  instance_type  = var.instance_type

  max_instance_count          = var.max_instance_count
  rate_limiter_is_distributed = var.rate_limiter_is_distributed

  # Bám TRẠNG THÁI (instance_count), không bám TRẦN (max_instance_count).
  #
  # Host port là static nên mỗi instance chứa đúng 1 task/service. Nếu số task
  # mong muốn lớn hơn số instance đang chạy thì task thừa không có port nào để
  # xếp lên, service không bao giờ stable, và `aws ecs wait services-stable`
  # trong deploy.yml treo tới timeout rồi rollback. Buộc hai con số vào nhau ở
  # ĐÂY là cách làm cho trạng thái đó không biểu diễn được.
  service_desired_count = var.instance_count

  ecr_api_url        = module.storage.ecr_api_url
  ecr_web_url        = module.storage.ecr_web_url
  ecr_migrator_url   = module.storage.ecr_migrator_url
  image_tag          = var.image_tag
  assets_bucket_name = module.storage.assets_bucket_name
  allowed_origins    = "https://${var.web_domain}"

  # Service bi enable_alb gate cung voi ALB/TG/listener: ECS CreateService fail
  # neu target group chua gan vao load balancer nao. Khi enable_alb = false thi
  # tg_*_arn tra ve "" (KHONG phai null) — module ecs khong doc chung luc do.
  enable_alb = var.enable_alb
  tg_web_arn = module.alb.tg_web_arn
  tg_api_arn = module.alb.tg_api_arn

  # Task seeder (Task 16). Mat khau di qua `secrets` cua ECS bang mot execution
  # role RIENG chi doc dung db-password — role cua api/web/migrator khong he
  # duoc mo rong. Xem modules/ecs/iam.tf.
  ssm_db_password_arn = module.data.ssm_db_password_arn
  ecr_seeder_url      = module.storage.ecr_seeder_url
  seeder_image_tag    = var.seeder_image_tag
  rds_host            = module.data.rds_endpoint
  db_username         = module.data.db_username
  db_name             = module.data.db_name
}

module "alb" {
  source = "../../modules/alb"

  project           = local.name
  vpc_id            = module.network.vpc_id
  public_subnet_ids = module.network.public_subnet_ids
  alb_sg_id         = module.security.alb_sg_id
  logs_bucket       = module.storage.alb_logs_bucket_name
  web_domain        = var.web_domain
  api_domain        = var.api_domain
  enable_alb        = var.enable_alb
}

# Budgets dựng sớm hơn thứ tự plan (Phase 3) theo yêu cầu: bịt rủi ro "quên tắt
# NAT Gateway / ALB" ngay từ đầu thay vì đợi tới cuối. Phase 3 bổ sung phần còn
# lại của module: Lambda cost guard + EventBridge Scheduler + SNS.
module "costguard" {
  source = "../../modules/costguard"

  project            = local.name
  alert_email        = var.alert_email
  enable_budget      = var.enable_budget
  monthly_budget_usd = var.monthly_budget_usd

  # Ngưỡng của người dùng chung account. Giá trị nằm trong terraform.tfvars
  # (gitignore) vì đó là email của người khác. Xem modules/costguard/main.tf.
  shared_notifications = var.shared_notifications

  # ─── PHASE 3 ─────────────────────────────────────────────────
  # Ba giá trị đầu lấy từ output của module (một nguồn sự thật cho tên resource,
  # thay vì lặp lại literal ở hai chỗ rồi lệch nhau khi ai đó đổi một bên).
  cluster_name   = module.ecs.cluster_name
  asg_name       = module.ecs.asg_name
  rds_identifier = module.data.rds_identifier

  # Tên service truyền dạng CHUỖI, KHÔNG dùng module.ecs.service_*_name — đúng
  # cùng lý do đã ghi ở module cicd bên dưới: service bị enable_alb gate nên nó
  # biến mất mỗi lần tắt stack, và IAM policy của Lambda thì phải đứng yên. Dùng
  # output kia sẽ khiến policy đổi nội dung theo trạng thái bật/tắt, tức mỗi lần
  # up.sh/down.sh lại là một diff trong plan.
  service_names = ["${local.name}-web", "${local.name}-api"]

  enable_auto_stop = var.enable_auto_stop
  stop_cron        = var.stop_cron
}

# ─── PHASE 2 ─────────────────────────────────────────────────────
# GitHub OIDC + 2 IAM role. Toàn bộ module này MIỄN PHÍ (IAM role, policy và
# OIDC provider không tính tiền), nên nó tồn tại liên tục không cần toggle.
#
# Tên service truyền dạng CHUỖI, không phải module.ecs.service_*_name: service
# bị enable_alb gate nên nó biến mất mỗi lần tắt stack, còn IAM policy thì phải
# đứng yên. Dùng output kia sẽ khiến policy đổi nội dung theo trạng thái bật/tắt.
module "cicd" {
  source = "../../modules/cicd"

  project = local.name

  ecr_repository_arns = [
    module.storage.ecr_api_arn,
    module.storage.ecr_web_arn,
    module.storage.ecr_migrator_arn,
    module.storage.ecr_seeder_arn,
  ]

  cluster_arn             = module.ecs.cluster_arn
  cluster_name            = module.ecs.cluster_name
  service_names           = ["${local.name}-web", "${local.name}-api"]
  migrator_taskdef_family = module.ecs.taskdef_migrator_family
  migrator_log_group_arn  = module.ecs.migrator_log_group_arn

  # Đúng 3 role được PassRole. KHÔNG có instance role (lớp bị cô lập có chủ ý)
  # và KHÔNG có execution role của seeder (role duy nhất đọc được mật khẩu DB).
  passable_role_arns = [
    module.ecs.task_execution_role_arn,
    module.ecs.task_app_role_arn,
    module.ecs.task_migrator_role_arn,
  ]

  rds_instance_arn     = module.data.rds_arn
  artifacts_bucket_arn = module.storage.artifacts_bucket_arn
}
