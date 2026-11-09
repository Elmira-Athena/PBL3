# Mock provider keeps all network plans offline.
mock_provider "aws" {
  mock_data "aws_region" {
    defaults = { region = "ap-southeast-1" }
  }
}

override_resource {
  target          = aws_vpc.this
  override_during = plan
  values = {
    id                     = "vpc-00000000000000000"
    default_network_acl_id = "acl-00000000000000000"
  }
}

override_resource {
  target          = aws_subnet.app[0]
  override_during = plan
  values          = { id = "subnet-00000000000000001" }
}

override_resource {
  target          = aws_subnet.app[1]
  override_during = plan
  values          = { id = "subnet-00000000000000002" }
}

variables {
  project             = "hushstore-tftest"
  vpc_cidr            = "10.20.0.0/16"
  azs                 = ["ap-southeast-1a", "ap-southeast-1b"]
  public_subnet_cidrs = ["10.20.0.0/24", "10.20.1.0/24"]
  app_subnet_cidrs    = ["10.20.10.0/24", "10.20.11.0/24"]
  db_subnet_cidrs     = ["10.20.20.0/24", "10.20.21.0/24"]
  enable_nat          = false
  enable_flow_logs    = false
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
    error_message = "Khi enable_nat = false thì không được tạo NAT Gateway (tốn $0,059/giờ mỗi cái)."
  }
}

run "flow_logs_tat_theo_default" {
  command = plan

  assert {
    condition     = length(aws_flow_log.vpc) == 0
    error_message = "VPC Flow Logs phải tắt theo default để không tốn phí ingest."
  }
}

run "bat_nat_mac_dinh_1_gateway_nhung_2_route_table" {
  command = plan

  variables {
    enable_nat = true
  }

  assert {
    condition     = length(aws_nat_gateway.this) == 1
    error_message = "Mặc định nat_gateway_count = 1: chỉ 1 NAT Gateway ($0,059/giờ). Muốn 2 phải khai tường minh."
  }

  # 🚨 Bản trước của test này khẳng định "đúng 1 route". Con số đó ĐÚNG khi hai
  # app subnet dùng chung một route table, và SAI từ lúc route table tách theo
  # AZ: hai route table thì phải có hai route 0.0.0.0/0, kể cả khi cả hai trỏ về
  # cùng một gateway. Route không tính phí; số route KHÔNG phải chỉ báo chi phí.
  assert {
    condition     = length(aws_route.private_nat) == 2
    error_message = "Mỗi private route table phải có route 0.0.0.0/0 riêng — 2 route table ⇒ 2 route."
  }

  # ⚠️ KHÔNG so sánh được `r.nat_gateway_id` với `aws_nat_gateway.this[0].id`:
  # cả hai là ID, tức known-after-apply, nên ở plan-time Terraform đỏ với
  # "Unknown condition value" — một cái đỏ nói về THỜI ĐIỂM, không nói về cấu
  # hình. Thay bằng output `nat_gateway_count`, vốn suy thẳng từ biến nên biết
  # được ngay ở plan. Đây mới là con số quyết định tiền.
  assert {
    condition     = output.nat_gateway_count == 1
    error_message = "Mặc định phải là 1 NAT — mỗi cái $0,059/giờ."
  }

  assert {
    condition = alltrue([
      for r in aws_route.private_nat : r.destination_cidr_block == "0.0.0.0/0"
    ])
    error_message = "Route qua NAT phải là 0.0.0.0/0."
  }
}

run "hai_nat_thi_moi_az_di_ra_bang_nat_cua_chinh_no" {
  command = plan

  variables {
    enable_nat        = true
    nat_gateway_count = 2
  }

  assert {
    condition     = length(aws_nat_gateway.this) == 2
    error_message = "nat_gateway_count = 2 phải dựng 2 NAT Gateway."
  }

  assert {
    condition     = length(aws_eip.nat) == 2
    error_message = "Mỗi NAT Gateway cần một EIP riêng — 2 NAT ⇒ 2 EIP (mỗi public IPv4 cũng tính phí)."
  }

  assert {
    condition     = output.nat_gateway_count == 2
    error_message = "nat_gateway_count = 2 phải cho ra 2 NAT — $0,118/giờ."
  }

  # Bất biến CỐT LÕI: hai NAT phải ở HAI AZ khác nhau. Cùng một AZ thì trả
  # $0,118/giờ mà không mua được khả dụng nào — mất AZ đó là mất cả hai.
  #
  # ⚠️ Ở plan-time KHÔNG kiểm được `subnet_id` (known after apply). Kiểm gián
  # tiếp qua tag Name, vốn nội suy từ `var.azs[count.index]` nên biết được ngay:
  # NAT thứ i phải mang hậu tố của AZ thứ i. Đây là PROXY, không phải phép đo
  # thật — nó chứng minh chỉ số AZ được dùng đúng thứ tự, còn việc subnet thật
  # nằm ở AZ đó chỉ xác nhận được sau apply. Ràng buộc còn lại nằm ở chính mã:
  # `subnet_id = aws_subnet.public[count.index].id`, cùng một count.index.
  assert {
    condition = length(distinct([
      for n in aws_nat_gateway.this : n.tags["Name"]
    ])) == 2
    error_message = "Hai NAT Gateway phải mang tên khác nhau theo AZ — trùng tên nghĩa là cùng đọc var.azs ở một chỉ số, tức cùng một AZ."
  }

  assert {
    condition     = endswith(aws_nat_gateway.this[0].tags["Name"], "-a") && endswith(aws_nat_gateway.this[1].tags["Name"], "-b")
    error_message = "NAT thứ i phải gắn với AZ thứ i (a rồi b) — lệch thứ tự là route của AZ này trỏ vào NAT của AZ kia, vẫn trả cross-AZ mà mất luôn khả dụng."
  }
}

run "nat_nhieu_hon_so_az_thi_bi_chan" {
  command = plan

  variables {
    enable_nat        = true
    nat_gateway_count = 3
  }

  # Ca đối chứng ÂM: nếu validation không chặn, run này pass và ta có một cấu
  # hình trả $0,177/giờ cho 3 NAT trong 2 AZ.
  expect_failures = [var.nat_gateway_count]
}

run "route_table_private_tach_theo_az_ke_ca_khi_nat_tat" {
  command = plan

  assert {
    condition     = length(aws_route_table.private) == 2
    error_message = "Phải có 1 private route table mỗi AZ, độc lập với việc NAT bật hay tắt — route table $0."
  }

  # Mỗi app subnet gắn vào route table CỦA RIÊNG NÓ. Nếu cả hai cùng gắn vào
  # một route table thì nat_gateway_count = 2 trở thành vô nghĩa: hai NAT dựng
  # lên nhưng chỉ một cái có traffic, và AZ kia vẫn chết theo NAT đầu.
  #
  # `route_table_id` của association là known-after-apply, nên kiểm qua tag của
  # chính các route table — hai tên khác nhau nghĩa là hai route table khác nhau,
  # và mã gắn chúng theo cùng một count.index với subnet.
  assert {
    condition = length(distinct([
      for rt in aws_route_table.private : rt.tags["Name"]
    ])) == 2
    error_message = "Hai private route table phải phân biệt được theo AZ."
  }

  assert {
    condition     = length(aws_route_table_association.app) == 2
    error_message = "Phải có đúng 2 association app→private, mỗi subnet một cái."
  }

  # `aws_vpc_endpoint.s3.route_table_ids` là set(string) toàn giá trị unknown ở
  # plan-time, nên `length()` của nó cũng unknown (set không biết trùng lặp khi
  # phần tử chưa biết). Kiểm trên MÃ NGUỒN thay vì trên giá trị — cùng khuôn với
  # tests/rds.tftest.hcl. Bất biến: phải dùng SPLAT, không phải chỉ số cố định.
  # Viết `aws_route_table.private[0].id` thì route table AZ thứ hai đi S3 vòng
  # qua NAT: vẫn chạy, không lỗi, chỉ âm thầm tính $0,045/GB.
  assert {
    condition = length([
      for l in split("\n", file("${path.module}/vpc.tf")) :
      l if !startswith(trimspace(l), "#") && strcontains(l, "aws_route_table.private[*].id")
    ]) == 1
    error_message = "S3 gateway endpoint phải phủ MỌI private route table bằng splat `aws_route_table.private[*].id`. Dùng chỉ số cố định là sót AZ và âm thầm tính $0,045/GB cho traffic S3."
  }
}

run "default_security_group_bi_khoa_ve_rong" {
  command = plan

  assert {
    condition = (
      aws_default_security_group.this.vpc_id == "vpc-00000000000000000" &&
      length(aws_default_security_group.this.ingress) == 0 &&
      length(aws_default_security_group.this.egress) == 0
    )
    error_message = "The VPC default SG must have explicitly empty ingress and egress."
  }

  assert {
    condition     = output.default_network_acl_id == "acl-00000000000000000"
    error_message = "Expose the AWS default ACL ID for association checks without managing its rules."
  }
}

# ═══ ECR INTERFACE ENDPOINT ══════════════════════════════════════
# Ba bất biến, theo thứ tự nguy hiểm giảm dần:
#   1. Mặc định KHÔNG dựng. Đây là resource tính tiền không được công tắc nào
#      khác che chắn, nên "quên tắt" phải là trạng thái không biểu diễn được.
#   2. Bật thì phải có ĐÚNG 2 cái — ecr.api một mình không pull được image, và
#      ecr.dkr một mình không lấy được auth token. Thiếu một cái = trả tiền cho
#      một endpoint vô dụng.
#   3. private_dns_enabled phải TRUE. Đây là chế độ hỏng im lặng: false thì
#      endpoint vẫn dựng, vẫn tính tiền, mà containerd vẫn phân giải tên ECR ra
#      IP công khai rồi đi qua NAT — không lỗi, không log, chỉ tốn tiền hai lần.

run "mac_dinh_khong_dung_ecr_endpoint" {
  command = plan

  assert {
    condition     = length(aws_vpc_endpoint.ecr) == 0
    error_message = "enable_ecr_endpoints mặc định phải là false — endpoint tính tiền ngay khi apply, không đợi up.sh."
  }

  assert {
    condition     = length(aws_security_group.vpce) == 0
    error_message = "Không dựng endpoint thì cũng không dựng SG của nó."
  }
}

run "bat_thi_dung_2_endpoint_va_private_dns_phai_bat" {
  command = plan

  override_resource {
    target          = aws_security_group.vpce[0]
    override_during = plan
    values          = { id = "sg-00000000000000004" }
  }

  variables {
    enable_ecr_endpoints = true
  }

  assert {
    condition     = length(aws_vpc_endpoint.ecr) == 2
    error_message = "Phải có ĐÚNG 2 endpoint: ecr.api (auth token) và ecr.dkr (kéo layer). Một cái không đủ."
  }

  assert {
    condition = alltrue([
      for e in aws_vpc_endpoint.ecr : e.private_dns_enabled == true
    ])
    error_message = "private_dns_enabled phải TRUE — false thì trả tiền endpoint mà traffic vẫn đi qua NAT, và không có gì báo."
  }

  assert {
    condition = alltrue([
      for e in aws_vpc_endpoint.ecr : e.vpc_endpoint_type == "Interface"
    ])
    error_message = "ECR chỉ có Interface endpoint. Gateway endpoint chỉ tồn tại cho S3 và DynamoDB."
  }

  assert {
    condition = alltrue([
      for endpoint in values(aws_vpc_endpoint.ecr) :
      endpoint.subnet_ids == toset(["subnet-00000000000000001", "subnet-00000000000000002"]) &&
      endpoint.security_group_ids == toset(["sg-00000000000000004"])
    ])
    error_message = "Both ECR endpoints must use both app subnets and exactly the endpoint SG."
  }

  assert {
    condition = (
      aws_vpc_endpoint.ecr["api"].service_name == "com.amazonaws.ap-southeast-1.ecr.api" &&
      aws_vpc_endpoint.ecr["dkr"].service_name == "com.amazonaws.ap-southeast-1.ecr.dkr" &&
      length(aws_security_group.vpce) == 1
    )
    error_message = "Enable exactly ECR API and Docker endpoints with one shared SG."
  }
}
