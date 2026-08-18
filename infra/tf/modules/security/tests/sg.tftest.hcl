provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project = "hushstore-tftest"
  vpc_id  = "vpc-00000000000000000"
}

run "khong_co_bat_ky_rule_nao_mo_port_22" {
  command = plan

  assert {
    condition = alltrue([
      for r in values(aws_vpc_security_group_ingress_rule.all) :
      !(r.from_port <= 22 && r.to_port >= 22)
    ])
    error_message = "Không Security Group nào được có ingress rule chứa port 22 — admin access chỉ qua SSM Session Manager."
  }
}

run "khong_co_ingress_rule_nao_mo_0000_ngoai_alb" {
  command = plan

  assert {
    condition = alltrue([
      for k, r in aws_vpc_security_group_ingress_rule.all :
      r.cidr_ipv4 == null || startswith(k, "alb-")
    ])
    error_message = "Chỉ sg-alb được nhận traffic từ CIDR công cộng. Mọi SG khác phải dùng referenced_security_group_id."
  }
}

run "alb_chi_mo_dung_2_rule_80_va_443" {
  command = plan

  assert {
    condition = alltrue([
      aws_vpc_security_group_ingress_rule.all["alb-http"].from_port == 80,
      aws_vpc_security_group_ingress_rule.all["alb-http"].to_port == 80,
      aws_vpc_security_group_ingress_rule.all["alb-http"].cidr_ipv4 == "0.0.0.0/0",
      aws_vpc_security_group_ingress_rule.all["alb-https"].from_port == 443,
      aws_vpc_security_group_ingress_rule.all["alb-https"].to_port == 443,
      aws_vpc_security_group_ingress_rule.all["alb-https"].cidr_ipv4 == "0.0.0.0/0",
    ])
    error_message = "sg-alb phải mở đúng 80 và 443 từ 0.0.0.0/0."
  }

  # Đếm theo KEY của for_each — key biết ở plan-time. KHÔNG so
  # r.security_group_id với aws_security_group.alb.id: cả hai là
  # (known after apply) nên Terraform báo lỗi Unknown condition value.
  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_ingress_rule.all) : k if startswith(k, "alb-")
    ]) == 2
    error_message = "sg-alb phải có ĐÚNG 2 ingress rule — thêm rule nào là vi phạm nguyên tắc tối thiểu."
  }
}

run "alb_egress_chi_toi_sg_web_khong_ra_cidr_nao" {
  command = plan

  # cidr_ipv4 == null chứng minh destination là SG reference chứ không phải
  # CIDR, mà không cần biết giá trị ID thật.
  assert {
    condition = alltrue([
      for k, r in aws_vpc_security_group_egress_rule.all :
      r.cidr_ipv4 == null if startswith(k, "alb-")
    ])
    error_message = "sg-alb chỉ được egress tới sg-web qua referenced_security_group_id, không được mở ra CIDR nào."
  }

  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_egress_rule.all) : k if startswith(k, "alb-")
    ]) == 2
    error_message = "sg-alb phải có đúng 2 egress rule (80 và 8080 tới sg-web)."
  }
}

run "web_chi_nhan_traffic_tu_sg_alb" {
  command = plan

  assert {
    condition = alltrue([
      for k, r in aws_vpc_security_group_ingress_rule.all :
      r.cidr_ipv4 == null if startswith(k, "web-")
    ])
    error_message = "sg-web chỉ được nhận traffic từ sg-alb — mọi ingress rule của nó phải dùng referenced_security_group_id, không dùng CIDR."
  }

  assert {
    condition = alltrue([
      aws_vpc_security_group_ingress_rule.all["web-http"].from_port == 80,
      aws_vpc_security_group_ingress_rule.all["web-api"].from_port == 8080,
    ])
    error_message = "sg-web phải nhận đúng port 80 (nginx) và 8080 (API)."
  }

  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_ingress_rule.all) : k if startswith(k, "web-")
    ]) == 2
    error_message = "sg-web phải có ĐÚNG 2 ingress rule."
  }
}

run "rds_chi_nhan_1433_tu_sg_web_va_khong_co_egress" {
  command = plan

  assert {
    condition = alltrue([
      aws_vpc_security_group_ingress_rule.all["rds-mssql"].from_port == 1433,
      aws_vpc_security_group_ingress_rule.all["rds-mssql"].to_port == 1433,
      aws_vpc_security_group_ingress_rule.all["rds-mssql"].cidr_ipv4 == null,
    ])
    error_message = "sg-rds phải nhận đúng 1433 và chỉ qua referenced_security_group_id (sg-web), không qua CIDR."
  }

  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_ingress_rule.all) : k if startswith(k, "rds-")
    ]) == 1
    error_message = "sg-rds phải có ĐÚNG 1 ingress rule."
  }

  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_egress_rule.all) : k if startswith(k, "rds-")
    ]) == 0
    error_message = "sg-rds phải có egress RỖNG — RDS không cần gọi ra ngoài."
  }
}
