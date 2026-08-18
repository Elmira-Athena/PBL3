provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project             = "hushstore-tftest"
  vpc_cidr            = "10.20.0.0/16"
  azs                 = ["ap-southeast-1a", "ap-southeast-1b"]
  public_subnet_cidrs = ["10.20.0.0/24", "10.20.1.0/24"]
  app_subnet_cidrs    = ["10.20.10.0/24", "10.20.11.0/24"]
  db_subnet_cidrs     = ["10.20.20.0/24", "10.20.21.0/24"]
  my_ip               = "203.0.113.45/32"
  enable_nat          = false
  enable_flow_logs    = false
  enable_deny_demo    = false
}

run "sau_subnet_dung_3_tier_2_az" {
  command = plan

  assert {
    condition     = length(aws_subnet.public) == 2
    error_message = "Phải có đúng 2 public subnet — ALB cần 2 AZ."
  }

  assert {
    condition     = length(aws_subnet.app) == 2
    error_message = "Phải có đúng 2 app subnet."
  }

  assert {
    condition     = length(aws_subnet.db) == 2
    error_message = "Phải có đúng 2 db subnet — DB subnet group cần 2 AZ."
  }
}

run "app_va_db_subnet_khong_tu_gan_public_ip" {
  command = plan

  assert {
    condition = alltrue([
      for s in aws_subnet.app : s.map_public_ip_on_launch == false
    ])
    error_message = "App subnet KHÔNG được tự gán public IP — EC2 phải nằm sau ALB."
  }

  assert {
    condition = alltrue([
      for s in aws_subnet.db : s.map_public_ip_on_launch == false
    ])
    error_message = "DB subnet KHÔNG được tự gán public IP."
  }
}

run "private_route_table_khong_co_route_ra_igw" {
  command = plan

  assert {
    condition     = length(aws_route.private_nat) == 0
    error_message = "Khi enable_nat = false thì private route table không được có route 0.0.0.0/0."
  }

  assert {
    condition     = length(aws_nat_gateway.this) == 0
    error_message = "Khi enable_nat = false thì không được tạo NAT Gateway (tốn $0.045/giờ)."
  }
}

run "flow_logs_tat_theo_default" {
  command = plan

  assert {
    condition     = length(aws_flow_log.vpc) == 0
    error_message = "VPC Flow Logs phải tắt theo default để không tốn phí ingest."
  }
}

run "bat_nat_thi_tao_dung_1_nat_gateway_va_1_route" {
  command = plan

  variables {
    enable_nat = true
  }

  assert {
    condition     = length(aws_nat_gateway.this) == 1
    error_message = "Chỉ tạo 1 NAT Gateway cho cả 2 AZ — max_size = 1 nên không cần NAT per-AZ."
  }

  assert {
    condition     = length(aws_route.private_nat) == 1
    error_message = "Phải có đúng 1 route 0.0.0.0/0 trỏ vào NAT Gateway."
  }

  assert {
    condition     = aws_route.private_nat[0].destination_cidr_block == "0.0.0.0/0"
    error_message = "Route qua NAT phải là 0.0.0.0/0."
  }
}
