locals {
  aws_region = data.aws_region.current.region

  # Seeder đi theo image_tag trừ khi bị ghim riêng. Trước Phase 2 hai giá trị
  # buộc phải khác nhau (image seeder được build sau 3 image kia); từ Phase 2
  # pipeline build cả 4 ở cùng một commit nên chúng luôn bằng nhau, và cách
  # diễn đạt điều đó là để seeder_image_tag rỗng thay vì chép SHA hai lần rồi
  # có ngày lệch.
  seeder_tag = var.seeder_image_tag != "" ? var.seeder_image_tag : var.image_tag

  # Cấu hình log dùng chung cho cả 3 task definition.
  log_config = {
    api = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.api.name
        "awslogs-region"        = local.aws_region
        "awslogs-stream-prefix" = "api"
      }
    }
    web = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.web.name
        "awslogs-region"        = local.aws_region
        "awslogs-stream-prefix" = "web"
      }
    }
    migrator = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.migrator.name
        "awslogs-region"        = local.aws_region
        "awslogs-stream-prefix" = "migrator"
      }
    }
    seeder = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.seeder.name
        "awslogs-region"        = local.aws_region
        "awslogs-stream-prefix" = "seeder"
      }
    }
  }

  # Secret dùng khối `secrets` với valueFrom, KHÔNG dùng `environment`.
  # Giá trị trong `environment` hiện nguyên văn trong output của
  # `aws ecs describe-task-definition` — ai có quyền đọc task definition là
  # đọc được connection string.
  app_secrets = [
    {
      name      = "ConnectionStrings__DefaultConnection"
      valueFrom = var.ssm_connection_string_arn
    },
    {
      name      = "JwtSettings__SecretKey"
      valueFrom = var.ssm_jwt_secret_arn
    },
  ]
}

# ─── TASK DEFINITION: API ────────────────────────────────────────
resource "aws_ecs_task_definition" "api" {
  family                   = "${var.project}-api"
  requires_compatibilities = ["EC2"]

  # bridge, không phải awsvpc: awsvpc cấp 1 ENI riêng cho mỗi task, mà
  # t3.micro chỉ hỗ trợ 2 ENI (1 primary + 1 khả dụng) nên không đủ cho 2
  # service. ENI trunking cần instance type lớn hơn.
  network_mode = "bridge"

  execution_role_arn = aws_iam_role.task_execution.arn
  task_role_arn      = aws_iam_role.task_app.arn

  # skip_destroy = true: hầu hết attribute của task definition là ForceNew, nên
  # đổi image_tag sẽ tạo revision mới VÀ (nếu không có dòng này) deregister
  # revision cũ. AWS không cho chạy task/service mới từ một revision đã
  # deregister, nên thiếu skip_destroy làm mất đúng khả năng "rollback về
  # revision trước" mà comment ở variables.tf viện dẫn. Revision cũ không tốn
  # phí, nên chấp nhận tích luỹ revision để đổi lấy rollback tin cậy được.
  skip_destroy = true

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = "${var.ecr_api_url}:${var.image_tag}"
      essential = true

      memory            = var.api_memory_hard
      memoryReservation = var.api_memory_reservation

      # Static host port 8080: nhờ vậy sg-web ingress giữ đúng 2 rule thay vì
      # phải mở dải ephemeral 32768-65535 như khi dùng dynamic port mapping.
      portMappings = [
        { containerPort = 8080, hostPort = 8080, protocol = "tcp" }
      ]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_URLS", value = "http://+:8080" },
        { name = "AllowedOrigins", value = var.allowed_origins },
        { name = "AwsSettings__BucketName", value = var.assets_bucket_name },
        { name = "AwsSettings__Region", value = local.aws_region },
      ]

      secrets = local.app_secrets

      linuxParameters = {
        # initProcessEnabled bắt buộc để ECS Exec vào được container.
        initProcessEnabled = true
        # t3.micro chỉ 1GB RAM. Cho container dùng swap của host để không bị
        # OOM-kill khi task migrator chạy chồng lên.
        maxSwap    = 1024
        swappiness = 60
      }

      logConfiguration = local.log_config.api

      # KHÔNG đặt healthCheck ở tầng container: image aspnet:10.0 không có
      # curl. Sức khoẻ do ALB target group kiểm tra qua /health/ready.
    }
  ])

  tags = { Name = "${var.project}-api" }
}

# ─── TASK DEFINITION: WEB ────────────────────────────────────────
resource "aws_ecs_task_definition" "web" {
  family                   = "${var.project}-web"
  requires_compatibilities = ["EC2"]
  network_mode             = "bridge"

  execution_role_arn = aws_iam_role.task_execution.arn
  # KHÔNG đặt task_role_arn: nginx serve static file, không gọi AWS API nào.

  # skip_destroy = true: giữ ACTIVE các revision cũ khi image_tag đổi (ForceNew
  # thay resource), để luôn có revision hợp lệ để rollback. Xem giải thích đầy
  # đủ ở resource "aws_ecs_task_definition" "api" phía trên.
  skip_destroy = true

  container_definitions = jsonencode([
    {
      name      = "web"
      image     = "${var.ecr_web_url}:${var.image_tag}"
      essential = true

      memory            = 192
      memoryReservation = 96

      portMappings = [
        { containerPort = 80, hostPort = 80, protocol = "tcp" }
      ]

      logConfiguration = local.log_config.web
    }
  ])

  tags = { Name = "${var.project}-web" }
}

# ─── TASK DEFINITION: MIGRATOR (one-off, không có service) ───────
resource "aws_ecs_task_definition" "migrator" {
  family                   = "${var.project}-migrator"
  requires_compatibilities = ["EC2"]
  network_mode             = "bridge"

  execution_role_arn = aws_iam_role.task_execution.arn
  task_role_arn      = aws_iam_role.task_migrator.arn

  # skip_destroy = true: giữ ACTIVE các revision cũ khi image_tag đổi (ForceNew
  # thay resource), để luôn có revision hợp lệ để rollback. Xem giải thích đầy
  # đủ ở resource "aws_ecs_task_definition" "api" phía trên.
  skip_destroy = true

  container_definitions = jsonencode([
    {
      name      = "migrator"
      image     = "${var.ecr_migrator_url}:${var.image_tag}"
      essential = true

      memory            = 512
      memoryReservation = 256

      # KHÔNG map port: đây là one-off task, chạy rồi thoát.
      portMappings = []

      # Dùng CHUNG local.app_secrets với task api, có chủ ý. efbundle chạy lại
      # entry point của API để dựng DbContext, nên nó đọc cấu hình theo đúng cơ
      # chế của app — cấu hình lệch giữa hai task là cách để migration chạy được
      # ở đây mà app lại không khởi động được.
      #
      # Chỉ ConnectionStrings__DefaultConnection là thật sự BẮT BUỘC.
      # JwtSettings__SecretKey thì không: Program.cs:110 có `?? throw` trên
      # `JwtSettings:SecretKey`, nhưng `??` chỉ bắn khi null, mà appsettings.json
      # khai `"SecretKey": ""` — chuỗi RỖNG. Giữ nó vì hai lẽ: cấu hình khớp với
      # task api, và nó vẫn đúng vào ngày dòng rỗng kia bị xoá.
      secrets = local.app_secrets

      logConfiguration = local.log_config.migrator
    }
  ])

  tags = { Name = "${var.project}-migrator" }
}

# ─── TASK DEFINITION: SEEDER (one-off, không có service) ─────────
# Kế hoạch ban đầu là seed bằng cách vào host qua SSM rồi đọc mật khẩu bằng
# `aws ssm get-parameter`. Không làm được: role của container instance bị DENY
# tường minh ssm:GetParameter* trên /hushstore/* — đó là một deliverable của đồ
# án, đã kiểm chứng cả bằng IAM simulator lẫn trên instance thật. Nên mật khẩu đi
# theo đường `secrets` của ECS (execution role đọc), y như task migrator: nó
# không bao giờ chạm host và không nằm trong shell history.
resource "aws_ecs_task_definition" "seeder" {
  family                   = "${var.project}-seeder"
  requires_compatibilities = ["EC2"]
  network_mode             = "bridge"

  execution_role_arn = aws_iam_role.task_execution_seeder.arn

  # KHÔNG có task_role_arn: seeder chỉ nói chuyện với RDS bằng TCP, không gọi
  # API AWS nào. Không cấp role là blast radius bằng 0 nếu image bị chiếm.
  skip_destroy = true

  container_definitions = jsonencode([
    {
      name      = "seeder"
      image     = "${var.ecr_seeder_url}:${local.seeder_tag}"
      essential = true

      memory            = 256
      memoryReservation = 128

      # KHÔNG map port: one-off task, chạy rồi thoát.
      portMappings = []

      # sqlcmd không nhận connection string kiểu .NET nên truyền 4 giá trị rời.
      # Ba giá trị không bí mật đi qua `environment`; chỉ mật khẩu đi qua
      # `secrets`. Tách như vậy để đọc task definition là thấy ngay đúng một thứ
      # là bí mật.
      environment = [
        { name = "DB_HOST", value = var.rds_host },
        { name = "DB_NAME", value = var.db_name },
        { name = "DB_USER", value = var.db_username },
      ]

      secrets = [
        { name = "DB_PASSWORD", valueFrom = var.ssm_db_password_arn },
      ]

      logConfiguration = local.log_config.seeder
    }
  ])

  tags = { Name = "${var.project}-seeder" }
}
