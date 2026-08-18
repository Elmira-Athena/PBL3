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
