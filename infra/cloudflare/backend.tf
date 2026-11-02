terraform {
  # Reuse the encrypted, versioned state bucket with a separate key; no new AWS resource.
  backend "s3" {
    bucket       = "hushstore-tfstate-551897327153"
    key          = "cloudflare/terraform.tfstate"
    region       = "ap-southeast-1"
    profile      = "hushstore"
    encrypt      = true
    use_lockfile = true
  }
}
