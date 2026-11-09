output "vpc_id" {
  description = "ID của VPC"
  value       = aws_vpc.this.id
}

output "vpc_cidr" {
  description = "CIDR của VPC"
  value       = aws_vpc.this.cidr_block
}

output "public_subnet_ids" {
  description = "ID của 2 public subnet — dùng cho ALB và NAT Gateway"
  value       = aws_subnet.public[*].id
}

output "app_subnet_ids" {
  description = "ID của 2 app subnet — dùng cho ASG"
  value       = aws_subnet.app[*].id
}

output "db_subnet_ids" {
  description = "ID của 2 db subnet — dùng cho DB subnet group"
  value       = aws_subnet.db[*].id
}

output "public_subnet_cidrs" {
  description = "CIDR của các public subnet — chứa ALB và NAT Gateway"
  value       = aws_subnet.public[*].cidr_block
}

output "app_subnet_cidrs" {
  description = "CIDR của các app subnet — chứa ECS container instance"
  value       = aws_subnet.app[*].cidr_block
}

output "db_subnet_cidrs" {
  description = "CIDR của các db subnet — chứa RDS"
  value       = aws_subnet.db[*].cidr_block
}

output "nat_gateway_id" {
  description = "ID của NAT Gateway ĐẦU TIÊN, rỗng khi enable_nat = false. Giữ lại cho tương thích — dùng nat_gateway_ids khi cần đủ danh sách"
  value       = local.nat_count > 0 ? aws_nat_gateway.this[0].id : ""
}

output "nat_gateway_ids" {
  description = "ID của MỌI NAT Gateway đang dựng. Rỗng khi enable_nat = false, 1 phần tử khi nat_gateway_count = 1, 2 phần tử khi = 2"
  value       = aws_nat_gateway.this[*].id
}

output "nat_gateway_count" {
  description = "Số NAT Gateway thực tế đang dựng — dùng cho status.sh để tính tiền đúng thay vì giả định 1"
  value       = local.nat_count
}

output "private_route_table_ids" {
  description = "ID của các private route table, mỗi AZ một cái"
  value       = aws_route_table.private[*].id
}

output "ecr_endpoint_ids" {
  description = "ID của 2 interface endpoint ECR. Rỗng khi enable_ecr_endpoints = false"
  value       = { for k, v in aws_vpc_endpoint.ecr : k => v.id }
}

output "ecr_endpoint_count" {
  description = "Số interface endpoint ECR đang dựng (0 hoặc 2) — dùng cho status.sh tính tiền đúng"
  value       = length(aws_vpc_endpoint.ecr)
}

output "vpce_sg_id" {
  description = "ID Security Group của interface endpoint. Rỗng khi enable_ecr_endpoints = false"
  value       = var.enable_ecr_endpoints ? aws_security_group.vpce[0].id : ""
}

output "default_network_acl_id" {
  description = "ID của default network ACL do AWS tạo; Terraform không quản lý ACL hoặc rule của nó"
  value       = aws_vpc.this.default_network_acl_id
}
