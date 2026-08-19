provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project                   = "hushstore-tftest"
  assets_bucket_arn         = "arn:aws:s3:::hushstore-public-assets"
  artifacts_bucket_arn      = "arn:aws:s3:::hushstore-artifacts"
  ssm_connection_string_arn = "arn:aws:ssm:ap-southeast-1:000000000000:parameter/hushstore/prod/connection-string"
  ssm_jwt_secret_arn        = "arn:aws:ssm:ap-southeast-1:000000000000:parameter/hushstore/prod/jwt-secret"
  app_subnet_ids            = ["subnet-00000000000000001", "subnet-00000000000000002"]
  web_sg_id                 = "sg-00000000000000000"
  instance_count            = 1
  instance_type             = "t3.micro"
}

run "asg_gioi_han_dung_1_instance" {
  command = plan

  assert {
    condition     = aws_autoscaling_group.this.max_size == 1
    error_message = "max_size phải = 1: rate limiter là in-memory nên 2 task API sẽ làm giới hạn 5 req/phút thành 10."
  }

  assert {
    condition     = aws_autoscaling_group.this.min_size == 0
    error_message = "min_size phải = 0 để down.sh hạ về 0 instance, xoá luôn EBS root và về $0 thật."
  }

  assert {
    condition     = length(aws_autoscaling_group.this.vpc_zone_identifier) == 2
    error_message = "ASG phải trải trên 2 app subnet ở 2 AZ."
  }
}

run "launch_template_khong_gan_ssh_key_va_bat_imdsv2" {
  command = plan

  assert {
    condition     = aws_launch_template.this.key_name == null || aws_launch_template.this.key_name == ""
    error_message = "Launch template TUYỆT ĐỐI không được gắn SSH key pair — admin access chỉ qua SSM Session Manager."
  }

  assert {
    condition     = aws_launch_template.this.metadata_options[0].http_tokens == "required"
    error_message = "Phải bắt buộc IMDSv2 (http_tokens = required) để chặn SSRF đọc credential từ metadata service."
  }

  assert {
    condition     = aws_launch_template.this.metadata_options[0].http_put_response_hop_limit == 1
    error_message = "hop_limit = 1 để container không tự gọi được metadata của host."
  }
}

run "launch_template_nam_trong_sg_web_va_dung_instance_profile" {
  command = plan

  assert {
    condition     = contains(aws_launch_template.this.vpc_security_group_ids, var.web_sg_id)
    error_message = "Container instance phải dùng sg-web (chỉ nhận traffic từ sg-alb)."
  }

  assert {
    condition     = aws_launch_template.this.iam_instance_profile[0].name == aws_iam_instance_profile.instance.name
    error_message = "Launch template phải gắn instance profile của container-instance-role."
  }
}

run "capacity_provider_tat_managed_scaling_va_termination_protection" {
  command = plan

  assert {
    condition     = aws_ecs_capacity_provider.this.auto_scaling_group_provider[0].managed_termination_protection == "DISABLED"
    error_message = "managed_termination_protection phải DISABLED, nếu không capacity provider không xoá được và nuke.sh sẽ treo."
  }

  assert {
    condition     = aws_ecs_capacity_provider.this.auto_scaling_group_provider[0].managed_scaling[0].status == "DISABLED"
    error_message = "managed_scaling phải DISABLED: max_size = 1 nên không có gì để scale, và bật lên sẽ tranh desired_capacity với Terraform."
  }
}

run "container_insights_tat_de_khong_ton_phi_cloudwatch" {
  command = plan

  assert {
    condition = anytrue([
      for s in aws_ecs_cluster.this.setting :
      s.name == "containerInsights" && s.value == "disabled"
    ])
    error_message = "Container Insights phải tắt — nó tính phí custom metric theo từng container."
  }
}
