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

  ecr_api_url        = module.storage.ecr_api_url
  ecr_web_url        = module.storage.ecr_web_url
  ecr_migrator_url   = module.storage.ecr_migrator_url
  image_tag          = var.image_tag
  assets_bucket_name = module.storage.assets_bucket_name
  allowed_origins    = "https://${var.web_domain}"
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

# Dựng sớm hơn thứ tự plan (Phase 3) theo yêu cầu: bịt rủi ro "quên tắt NAT
# Gateway / ALB" ngay từ bây giờ thay vì đợi tới cuối. Phase 3 sẽ mở rộng module
# này thêm Lambda cost-guard + EventBridge Scheduler.
module "costguard" {
  source = "../../modules/costguard"

  project            = local.name
  alert_email        = var.alert_email
  monthly_budget_usd = var.monthly_budget_usd
}
