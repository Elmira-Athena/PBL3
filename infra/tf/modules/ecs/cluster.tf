# ─── AMI ─────────────────────────────────────────────────────────
# ECS-optimized Amazon Linux 2023: đã có ECS agent, Docker và SSM Agent cài
# sẵn, nên user_data gần như không phải làm gì.
data "aws_ssm_parameter" "ecs_ami" {
  name = "/aws/service/ecs/optimized-ami/amazon-linux-2023/recommended/image_id"
}

# ─── CLUSTER ─────────────────────────────────────────────────────
resource "aws_ecs_cluster" "this" {
  name = var.project

  setting {
    name = "containerInsights"
    # Container Insights tính phí theo custom metric của từng container —
    # không cần cho quy mô đồ án.
    value = "disabled"
  }

  tags = { Name = var.project }
}

# ─── LAUNCH TEMPLATE ─────────────────────────────────────────────
resource "aws_launch_template" "this" {
  name_prefix   = "${var.project}-lt-"
  image_id      = data.aws_ssm_parameter.ecs_ami.value
  instance_type = var.instance_type

  # CỐ TÌNH không đặt key_name: không có SSH key pair nào tồn tại trong hệ
  # thống. Vào host bằng SSM Session Manager, vào container bằng ECS Exec.

  iam_instance_profile {
    name = aws_iam_instance_profile.instance.name
  }

  vpc_security_group_ids = [var.web_sg_id]

  # Instance nằm ở app subnet (map_public_ip_on_launch = false) nên không có
  # public IP. Egress đi qua NAT Gateway.
  metadata_options {
    http_endpoint = "enabled"
    # IMDSv2 bắt buộc: chặn lớp tấn công SSRF đọc credential của instance
    # role bằng một request GET đơn giản tới 169.254.169.254.
    http_tokens = "required"
    # hop_limit = 1: packet tới metadata service không đi qua được thêm hop
    # nào, nên container (bridge network = thêm 1 hop) không tự gọi được.
    http_put_response_hop_limit = 1
  }

  block_device_mappings {
    device_name = "/dev/xvda"

    ebs {
      volume_size           = var.root_volume_size
      volume_type           = "gp3"
      encrypted             = true
      delete_on_termination = true
    }
  }

  user_data = base64encode(templatefile("${path.module}/user_data.sh.tftpl", {
    cluster_name = aws_ecs_cluster.this.name
  }))

  tag_specifications {
    resource_type = "instance"
    tags          = { Name = "${var.project}-container-instance" }
  }

  tag_specifications {
    resource_type = "volume"
    tags          = { Name = "${var.project}-container-instance-vol" }
  }

  lifecycle {
    create_before_destroy = true
  }
}

# ─── AUTO SCALING GROUP ──────────────────────────────────────────
resource "aws_autoscaling_group" "this" {
  name                = "${var.project}-asg"
  vpc_zone_identifier = var.app_subnet_ids

  # min 0 để down.sh hạ về 0: instance bị terminate, EBS root xoá theo, chi
  # phí về $0 thật. Toàn bộ state nằm trong image + Parameter Store nên dựng
  # lại không mất gì — đó là điều Task 16 Step 12 verify.
  min_size         = 0
  max_size         = 1
  desired_capacity = var.instance_count

  launch_template {
    id      = aws_launch_template.this.id
    version = "$Latest"
  }

  health_check_type         = "EC2"
  health_check_grace_period = 180

  # Đợi instance thật sự vào service trước khi apply trả về.
  wait_for_capacity_timeout = "10m"

  tag {
    key                 = "Name"
    value               = "${var.project}-container-instance"
    propagate_at_launch = true
  }

  tag {
    key                 = "Project"
    value               = var.project
    propagate_at_launch = true
  }

  instance_refresh {
    strategy = "Rolling"

    preferences {
      # max_size = 1 nên không thể giữ instance nào healthy trong lúc refresh.
      min_healthy_percentage = 0
    }
  }
}

# ─── CAPACITY PROVIDER ───────────────────────────────────────────
resource "aws_ecs_capacity_provider" "this" {
  name = "${var.project}-cp"

  auto_scaling_group_provider {
    auto_scaling_group_arn = aws_autoscaling_group.this.arn

    # DISABLED: nếu bật, capacity provider giữ instance protection và sẽ không
    # xoá được → terraform destroy / nuke.sh treo vô hạn.
    managed_termination_protection = "DISABLED"

    managed_scaling {
      # DISABLED: max_size = 1 nên không có gì để scale. Bật lên sẽ khiến ECS
      # tạo target-tracking policy tranh desired_capacity với Terraform.
      status = "DISABLED"
    }
  }
}

resource "aws_ecs_cluster_capacity_providers" "this" {
  cluster_name       = aws_ecs_cluster.this.name
  capacity_providers = [aws_ecs_capacity_provider.this.name]

  default_capacity_provider_strategy {
    capacity_provider = aws_ecs_capacity_provider.this.name
    weight            = 1
    base              = 0
  }
}

# ─── LOG GROUPS ──────────────────────────────────────────────────
resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${var.project}-api"
  retention_in_days = var.log_retention_days
}

resource "aws_cloudwatch_log_group" "web" {
  name              = "/ecs/${var.project}-web"
  retention_in_days = var.log_retention_days
}

resource "aws_cloudwatch_log_group" "migrator" {
  name              = "/ecs/${var.project}-migrator"
  retention_in_days = var.log_retention_days
}
