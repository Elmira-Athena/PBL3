provider "aws" {
  region  = var.region
  profile = var.profile

  default_tags {
    tags = {
      Project   = var.project
      ManagedBy = "terraform"
      Env       = "prod"
    }
  }
}
