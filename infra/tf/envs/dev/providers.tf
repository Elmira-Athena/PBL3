provider "aws" {
  region  = var.region
  profile = var.profile

  # Env = "dev" là thứ phân biệt resource của hai môi trường trong Cost
  # Explorer và trong mọi lệnh describe có --filters "Name=tag:Env". Cost guard
  # và status.sh lọc theo tag:Project, mà var.project ở đây là "hushstore-dev"
  # nên chúng KHÔNG chạm vào resource của prod — và ngược lại.
  default_tags {
    tags = {
      Project   = var.project
      ManagedBy = "terraform"
      Env       = "dev"
    }
  }
}
