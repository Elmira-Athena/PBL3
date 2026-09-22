# State của dev nằm CÙNG bucket với prod nhưng KHÁC key. Cùng bucket vì bucket
# tfstate do bootstrap/ dựng một lần cho cả account và có versioning — dựng
# bucket thứ hai chỉ để tách key là thêm một thứ phải nhớ xoá.
#
# 🚨 `key` là thứ DUY NHẤT ngăn dev ghi đè state của prod. main.tf ở đây là
# SYMLINK sang ../prod/main.tf, nên nếu key trùng thì `terraform apply` trong
# thư mục dev sẽ nhận diện đúng từng resource của prod và bắt đầu sửa chúng
# theo biến của dev — đổi tên, đổi CIDR, phá VPC prod. Không có bước xác nhận
# nào chặn chuyện đó.
terraform {
  required_version = ">= 1.10"

  backend "s3" {
    bucket       = "hushstore-tfstate-551897327153"
    key          = "dev/terraform.tfstate"
    region       = "ap-southeast-1"
    profile      = "hushstore"
    encrypt      = true
    use_lockfile = true
  }

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.4"
    }
  }
}
