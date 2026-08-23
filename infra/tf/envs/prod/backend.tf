terraform {
  required_version = ">= 1.10"

  backend "s3" {
    bucket       = "hushstore-tfstate-667836586836"
    key          = "prod/terraform.tfstate"
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

    # Phase 3: module costguard zip src/ thành artifact của Lambda cost guard.
    # Provider chạy cục bộ, không gọi API AWS và không cần credential.
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.4"
    }
  }
}
