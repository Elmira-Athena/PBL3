# Dải IP công bố của Cloudflare (https://www.cloudflare.com/ips-v4, lấy 2026-11-23) dưới dạng
# managed prefix list, để sg-alb chỉ nhận traffic từ Cloudflare (pentest 2026-11-23 F8).
# Chỉ IPv4: Cloudflare kết nối tới origin qua IPv4 mặc định. Cập nhật khi Cloudflare đổi dải.
# max_entries để dư (20) cho lần Cloudflare thêm dải — tăng max_entries không phá prefix list.
resource "aws_ec2_managed_prefix_list" "cloudflare" {
  name           = "${var.project}-cloudflare-v4"
  address_family = "IPv4"
  max_entries    = 20

  entry {
    cidr        = "173.245.48.0/20"
    description = "cloudflare"
  }
  entry {
    cidr        = "103.21.244.0/22"
    description = "cloudflare"
  }
  entry {
    cidr        = "103.22.200.0/22"
    description = "cloudflare"
  }
  entry {
    cidr        = "103.31.4.0/22"
    description = "cloudflare"
  }
  entry {
    cidr        = "141.101.64.0/18"
    description = "cloudflare"
  }
  entry {
    cidr        = "108.162.192.0/18"
    description = "cloudflare"
  }
  entry {
    cidr        = "190.93.240.0/20"
    description = "cloudflare"
  }
  entry {
    cidr        = "188.114.96.0/20"
    description = "cloudflare"
  }
  entry {
    cidr        = "197.234.240.0/22"
    description = "cloudflare"
  }
  entry {
    cidr        = "198.41.128.0/17"
    description = "cloudflare"
  }
  entry {
    cidr        = "162.158.0.0/15"
    description = "cloudflare"
  }
  entry {
    cidr        = "104.16.0.0/13"
    description = "cloudflare"
  }
  entry {
    cidr        = "104.24.0.0/14"
    description = "cloudflare"
  }
  entry {
    cidr        = "172.64.0.0/13"
    description = "cloudflare"
  }
  entry {
    cidr        = "131.0.72.0/22"
    description = "cloudflare"
  }

  tags = { Name = "${var.project}-cloudflare-v4" }
}
