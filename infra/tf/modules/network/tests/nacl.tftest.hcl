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

# KHÔNG assert `aws_network_acl.x.vpc_id == aws_vpc.this.id`: ở `command = plan`
# cả hai `.id` đều unknown, Terraform không so sánh được unknown với unknown và
# sẽ báo lỗi, làm hỏng cả file test. Thay bằng đếm số rule — vừa đánh giá được ở
# plan-time (độ dài map lấy từ locals), vừa kiểm tra đúng tính chất mà đề bài
# chấm: rule mở ở mức tối thiểu.
run "so_luong_rule_dung_muc_toi_thieu" {
  command = plan

  assert {
    condition = alltrue([
      length(aws_network_acl_rule.public_ingress) == 3,
      length(aws_network_acl_rule.public_egress) == 5,
    ])
    error_message = "nacl-public phải có đúng 3 rule inbound và 5 rule outbound — thêm rule nào là vi phạm nguyên tắc tối thiểu."
  }

  assert {
    condition = alltrue([
      length(aws_network_acl_rule.app_ingress) == 6,
      length(aws_network_acl_rule.app_egress) == 4,
    ])
    error_message = "nacl-app phải có đúng 6 rule inbound (90, 95, 100, 110, 115, 120) và 4 rule outbound."
  }

  assert {
    condition = alltrue([
      length(aws_network_acl_rule.db_ingress) == 1,
      length(aws_network_acl_rule.db_egress) == 1,
    ])
    error_message = "nacl-db phải có đúng 1 rule mỗi chiều — đây là tier chặt nhất của thiết kế."
  }
}

run "nacl_app_chan_port_22_truoc_rule_ephemeral" {
  command = plan

  assert {
    condition     = aws_network_acl_rule.app_ingress["90"].rule_action == "deny"
    error_message = "Rule 90 của nacl-app phải là DENY."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["90"].from_port == 22,
      aws_network_acl_rule.app_ingress["90"].to_port == 22,
      aws_network_acl_rule.app_ingress["90"].cidr_block == "0.0.0.0/0",
    ])
    error_message = "Rule 90 phải DENY port 22 từ 0.0.0.0/0 — không ai được SSH vào app tier."
  }
}

run "nacl_app_chan_1433_va_8080_truoc_rule_ephemeral_120" {
  command = plan

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["95"].rule_action == "deny",
      aws_network_acl_rule.app_ingress["95"].from_port == 1433,
      aws_network_acl_rule.app_ingress["95"].cidr_block == "0.0.0.0/0",
    ])
    error_message = "Rule 95 phải DENY 1433 từ 0.0.0.0/0 — vì rule 120 allow 1024-65535 sẽ vô tình mở nó."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["115"].rule_action == "deny",
      aws_network_acl_rule.app_ingress["115"].from_port == 8080,
      aws_network_acl_rule.app_ingress["115"].cidr_block == "0.0.0.0/0",
    ])
    error_message = "Rule 115 phải DENY 8080 từ 0.0.0.0/0 — vì rule 120 allow 1024-65535 sẽ vô tình mở nó."
  }

  # Đây là assertion cốt lõi: DENY phải có số NHỎ HƠN rule allow ephemeral,
  # nếu không NACL sẽ xét rule allow trước và hai port kia lọt ra internet.
  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["95"].rule_number < aws_network_acl_rule.app_ingress["120"].rule_number,
      aws_network_acl_rule.app_ingress["115"].rule_number < aws_network_acl_rule.app_ingress["120"].rule_number,
    ])
    error_message = "Rule DENY 1433 và 8080 phải có số nhỏ hơn rule 120 allow 1024-65535, nếu không sẽ bị bỏ qua."
  }
}

run "nacl_app_chi_nhan_80_va_8080_tu_public_tier" {
  command = plan

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["100"].rule_action == "allow",
      aws_network_acl_rule.app_ingress["100"].from_port == 80,
      aws_network_acl_rule.app_ingress["100"].cidr_block == "10.20.0.0/23",
    ])
    error_message = "Rule 100 phải allow 80 chỉ từ CIDR gộp của public tier, không phải 0.0.0.0/0."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["110"].rule_action == "allow",
      aws_network_acl_rule.app_ingress["110"].from_port == 8080,
      aws_network_acl_rule.app_ingress["110"].cidr_block == "10.20.0.0/23",
    ])
    error_message = "Rule 110 phải allow 8080 chỉ từ CIDR gộp của public tier."
  }
}

run "nacl_db_chi_co_dung_mot_rule_allow_1433" {
  command = plan

  assert {
    condition     = length(aws_network_acl_rule.db_ingress) == 1
    error_message = "NACL db inbound phải có ĐÚNG 1 rule — chỉ 1433 từ app tier. Thêm rule nào là vi phạm nguyên tắc tối thiểu."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.db_ingress["100"].rule_action == "allow",
      aws_network_acl_rule.db_ingress["100"].from_port == 1433,
      aws_network_acl_rule.db_ingress["100"].to_port == 1433,
      aws_network_acl_rule.db_ingress["100"].cidr_block == "10.20.10.0/23",
    ])
    error_message = "Rule duy nhất của NACL db phải là allow 1433 từ CIDR gộp của app tier."
  }

  assert {
    condition     = length(aws_network_acl_rule.db_egress) == 1
    error_message = "NACL db outbound phải có ĐÚNG 1 rule — ephemeral về app tier."
  }

  assert {
    condition     = aws_network_acl_rule.db_egress["100"].cidr_block == "10.20.10.0/23"
    error_message = "NACL db outbound chỉ được trả traffic về app tier, không ra internet."
  }
}

run "deny_demo_tat_theo_default" {
  command = plan

  assert {
    condition     = length(aws_network_acl_rule.public_deny_demo) == 0
    error_message = "Rule DENY theo my_ip phải tắt theo default, chỉ bật khi demo kịch bản 8."
  }
}

run "bat_deny_demo_thi_chan_my_ip_o_rule_50" {
  command = plan

  variables {
    enable_deny_demo = true
  }

  assert {
    condition     = length(aws_network_acl_rule.public_deny_demo) == 1
    error_message = "Khi enable_deny_demo = true phải tạo đúng 1 rule DENY."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.public_deny_demo[0].rule_number == 50,
      aws_network_acl_rule.public_deny_demo[0].rule_action == "deny",
      aws_network_acl_rule.public_deny_demo[0].cidr_block == "203.0.113.45/32",
      aws_network_acl_rule.public_deny_demo[0].protocol == "-1",
    ])
    error_message = "Rule demo phải là số 50, DENY mọi protocol từ my_ip — số nhỏ hơn rule 100/110 allow 80/443."
  }
}
