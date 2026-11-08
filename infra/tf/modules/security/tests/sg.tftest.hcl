# Mock IDs are known during plan, so these tests check actual SG identities.
# No AWS credentials or API calls are needed.
mock_provider "aws" {}

override_resource {
  target          = aws_security_group.alb
  override_during = plan
  values          = { id = "sg-00000000000000001" }
}

override_resource {
  target          = aws_security_group.web
  override_during = plan
  values          = { id = "sg-00000000000000002" }
}

override_resource {
  target          = aws_security_group.rds
  override_during = plan
  values          = { id = "sg-00000000000000003" }
}

variables {
  project = "hushstore-tftest"
  vpc_id  = "vpc-00000000000000000"
}

run "exact_ingress_chain" {
  command = plan

  assert {
    condition = length(aws_vpc_security_group_ingress_rule.all) == 5 && alltrue([
      for key, expected in {
        alb-http     = { group = "sg-00000000000000001", port = 80, cidr = "0.0.0.0/0", source = null }
        alb-https    = { group = "sg-00000000000000001", port = 443, cidr = "0.0.0.0/0", source = null }
        web-http     = { group = "sg-00000000000000002", port = 80, cidr = null, source = "sg-00000000000000001" }
        web-api      = { group = "sg-00000000000000002", port = 8080, cidr = null, source = "sg-00000000000000001" }
        rds-postgres = { group = "sg-00000000000000003", port = 5432, cidr = null, source = "sg-00000000000000002" }
        } : alltrue([
          aws_vpc_security_group_ingress_rule.all[key].security_group_id == expected.group,
          aws_vpc_security_group_ingress_rule.all[key].referenced_security_group_id == expected.source,
          aws_vpc_security_group_ingress_rule.all[key].cidr_ipv4 == expected.cidr,
          aws_vpc_security_group_ingress_rule.all[key].ip_protocol == "tcp",
          aws_vpc_security_group_ingress_rule.all[key].from_port == expected.port,
          aws_vpc_security_group_ingress_rule.all[key].to_port == expected.port,
      ])
    ])
    error_message = "Ingress must be exactly Internet→ALB 80/443, ALB→web 80/8080 and web→RDS 5432, with the correct SG identities."
  }

  assert {
    condition = alltrue([
      for rule in values(aws_vpc_security_group_ingress_rule.all) :
      rule.from_port == rule.to_port && rule.from_port != 22 &&
      (rule.security_group_id == "sg-00000000000000001" || rule.cidr_ipv4 == null)
    ])
    error_message = "No SSH, public database/app ingress, or explicit ephemeral return-traffic ranges are allowed."
  }
}

run "exact_egress_chain" {
  command = plan

  assert {
    condition = length(aws_vpc_security_group_egress_rule.all) == 5 && alltrue([
      for key, expected in {
        alb-to-web-http = { group = "sg-00000000000000001", port = 80, cidr = null, target = "sg-00000000000000002" }
        alb-to-web-api  = { group = "sg-00000000000000001", port = 8080, cidr = null, target = "sg-00000000000000002" }
        web-to-rds      = { group = "sg-00000000000000002", port = 5432, cidr = null, target = "sg-00000000000000003" }
        web-https-out   = { group = "sg-00000000000000002", port = 443, cidr = "0.0.0.0/0", target = null }
        web-http-out    = { group = "sg-00000000000000002", port = 80, cidr = "0.0.0.0/0", target = null }
        } : alltrue([
          aws_vpc_security_group_egress_rule.all[key].security_group_id == expected.group,
          aws_vpc_security_group_egress_rule.all[key].referenced_security_group_id == expected.target,
          aws_vpc_security_group_egress_rule.all[key].cidr_ipv4 == expected.cidr,
          aws_vpc_security_group_egress_rule.all[key].ip_protocol == "tcp",
          aws_vpc_security_group_egress_rule.all[key].from_port == expected.port,
          aws_vpc_security_group_egress_rule.all[key].to_port == expected.port,
      ])
    ])
    error_message = "Egress must be exactly ALB→web 80/8080, web→RDS 5432, and web→Internet 80/443."
  }

  assert {
    condition = alltrue([
      for rule in values(aws_vpc_security_group_egress_rule.all) :
      rule.security_group_id != "sg-00000000000000003" && rule.from_port == rule.to_port && rule.from_port != 22
    ])
    error_message = "RDS must have no egress; no SSH or explicit ephemeral return-traffic ranges may be added."
  }
}

run "endpoints_disabled_by_default" {
  command = plan

  assert {
    condition     = length(aws_vpc_security_group_ingress_rule.vpce_443) == 0
    error_message = "No endpoint ingress rule may exist when ECR endpoints are disabled."
  }
}

run "endpoint_https_only_from_web_sg" {
  command = plan

  variables {
    enable_ecr_endpoints = true
    vpce_sg_id           = "sg-00000000000000004"
  }

  assert {
    condition = length(aws_vpc_security_group_ingress_rule.vpce_443) == 1 && alltrue([
      aws_vpc_security_group_ingress_rule.vpce_443[0].security_group_id == "sg-00000000000000004",
      aws_vpc_security_group_ingress_rule.vpce_443[0].referenced_security_group_id == "sg-00000000000000002",
      aws_vpc_security_group_ingress_rule.vpce_443[0].cidr_ipv4 == null,
      aws_vpc_security_group_ingress_rule.vpce_443[0].ip_protocol == "tcp",
      aws_vpc_security_group_ingress_rule.vpce_443[0].from_port == 443,
      aws_vpc_security_group_ingress_rule.vpce_443[0].to_port == 443,
    ])
    error_message = "The endpoint must have exactly one TCP 443 ingress from sg-web, with no CIDR source."
  }

  assert {
    condition     = length(aws_vpc_security_group_ingress_rule.all) == 5 && length(aws_vpc_security_group_egress_rule.all) == 5
    error_message = "Enabling endpoints must preserve the five main ingress and five egress rules, including HTTP/HTTPS outbound."
  }
}

run "sg_dung_name_prefix_chu_khong_dung_name" {
  command = plan

  assert {
    condition = alltrue([
      for sg in [aws_security_group.alb, aws_security_group.web, aws_security_group.rds] :
      sg.name_prefix != null && sg.name_prefix != ""
    ])
    error_message = "All three groups must use name_prefix to support create_before_destroy without duplicate-name errors."
  }
}

run "khong_con_chuoi_sql_server_trong_description" {
  command = plan

  assert {
    condition = length([
      for line in split("\n", file("${path.module}/main.tf")) :
      line if !startswith(trimspace(line), "#") && strcontains(line, "description") && strcontains(line, "SQL Server")
    ]) == 0
    error_message = "Security group descriptions must describe the current PostgreSQL engine."
  }
}

run "enabled_endpoints_require_sg_id" {
  command = plan

  variables {
    enable_ecr_endpoints = true
  }

  expect_failures = [var.vpce_sg_id]
}

run "enabled_endpoints_reject_blank_sg_id" {
  command = plan

  variables {
    enable_ecr_endpoints = true
    vpce_sg_id           = "  "
  }

  expect_failures = [var.vpce_sg_id]
}
