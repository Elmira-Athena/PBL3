terraform {
  required_version = ">= 1.10.0, < 2.0.0"

  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "= 5.28.1"
    }
  }
}

# Authentication is read from CLOUDFLARE_API_TOKEN. Keep this root independent
# of infra/tf: no AWS provider, DNS resources, or shared state key.
provider "cloudflare" {}
