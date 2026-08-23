terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }

    # Chỉ để zip thư mục src/ thành artifact của Lambda (xem lambda.tf). Provider
    # này chạy hoàn toàn cục bộ, không gọi API nào và không cần credential.
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.4"
    }
  }
}
