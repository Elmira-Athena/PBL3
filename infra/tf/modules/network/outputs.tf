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
  description = "CIDR của public tier — dùng làm source trong NACL app"
  value       = aws_subnet.public[*].cidr_block
}

output "app_subnet_cidrs" {
  description = "CIDR của app tier — dùng làm source trong NACL db"
  value       = aws_subnet.app[*].cidr_block
}

output "db_subnet_cidrs" {
  description = "CIDR của db tier — dùng làm destination trong NACL app"
  value       = aws_subnet.db[*].cidr_block
}

output "nat_gateway_id" {
  description = "ID của NAT Gateway, rỗng khi enable_nat = false"
  value       = var.enable_nat ? aws_nat_gateway.this[0].id : ""
}

output "nacl_public_id" {
  description = "ID của NACL public tier"
  value       = aws_network_acl.public.id
}

output "nacl_app_id" {
  description = "ID của NACL app tier"
  value       = aws_network_acl.app.id
}

output "nacl_db_id" {
  description = "ID của NACL db tier"
  value       = aws_network_acl.db.id
}

output "public_tier_cidr" {
  description = "CIDR /23 gộp của public tier — dùng trong rule NACL"
  value       = local.public_tier_cidr
}

output "app_tier_cidr" {
  description = "CIDR /23 gộp của app tier"
  value       = local.app_tier_cidr
}

output "db_tier_cidr" {
  description = "CIDR /23 gộp của db tier"
  value       = local.db_tier_cidr
}
