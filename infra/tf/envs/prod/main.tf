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
