# ─── TRUST POLICIES ──────────────────────────────────────────────
data "aws_iam_policy_document" "ec2_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

data "aws_iam_policy_document" "ecs_tasks_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

# ─── ROLE 1: EC2 CONTAINER INSTANCE ──────────────────────────────
# Chỉ đủ để host đăng ký vào ECS cluster và nhận lệnh SSM.
# KHÔNG có quyền S3. KHÔNG có quyền đọc secret. Nếu host bị chiếm, kẻ tấn
# công không lấy được connection string hay ghi được vào bucket ảnh.
resource "aws_iam_role" "instance" {
  name               = "${var.project}-container-instance-role"
  description        = "EC2 host: dang ky ECS cluster + SSM Session Manager"
  assume_role_policy = data.aws_iam_policy_document.ec2_assume.json
}

resource "aws_iam_role_policy_attachment" "instance_ecs" {
  role       = aws_iam_role.instance.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonEC2ContainerServiceforEC2Role"
}

resource "aws_iam_role_policy_attachment" "instance_ssm" {
  role       = aws_iam_role.instance.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

# Quyền thêm duy nhất của host: đọc seed SQL từ bucket artifacts (dùng khi
# seed DB qua SSM Session Manager ở Task 16). Chỉ GetObject, chỉ bucket đó.
data "aws_iam_policy_document" "instance_extra" {
  statement {
    sid     = "ReadOpsArtifacts"
    effect  = "Allow"
    actions = ["s3:GetObject"]

    resources = ["${var.artifacts_bucket_arn}/*"]
  }

  # AmazonSSMManagedInstanceCore cấp ssm:GetParameter và ssm:GetParameters trên
  # Resource: "*", nên nếu không chặn thì EC2 host đọc được MỌI SecureString của
  # ta, gồm cả /hushstore/prod/db-password. Explicit Deny luôn thắng Allow, nên
  # statement này bịt đúng lỗ hổng mà không phải bỏ managed policy — giữ được
  # Session Manager, tức giữ được đường admin DUY NHẤT vào instance ở private
  # subnet (không có SSH, không có key pair).
  # Host KHÔNG cần đọc parameter của ta: việc inject secret vào container do ECS
  # agent làm bằng task execution role, không phải instance role.
  statement {
    sid    = "DenyReadingOurSecrets"
    effect = "Deny"

    # Liệt kê ĐỦ BỐN action đọc parameter, không phải ba. GetParameterHistory với
    # WithDecryption=true trả về plaintext của SecureString qua các version cũ, nên
    # thiếu nó là thiếu một đường đọc secret. Hiện AmazonSSMManagedInstanceCore
    # không cấp GetParameterHistory (nên nó đang là implicitDeny), nhưng mục đích
    # của statement này là chặn TRƯỚC bất kể managed policy cấp gì về sau —
    # explicitDeny không bao giờ bị override, implicitDeny thì có.
    actions = [
      "ssm:GetParameter",
      "ssm:GetParameters",
      "ssm:GetParameterHistory",
      "ssm:GetParametersByPath",
    ]

    resources = ["arn:aws:ssm:*:*:parameter/${var.project}/*"]
  }
}

resource "aws_iam_role_policy" "instance_extra" {
  name   = "${var.project}-instance-read-artifacts"
  role   = aws_iam_role.instance.id
  policy = data.aws_iam_policy_document.instance_extra.json
}

resource "aws_iam_instance_profile" "instance" {
  name = "${var.project}-container-instance-profile"
  role = aws_iam_role.instance.name
}

# ─── ROLE 2: TASK EXECUTION (ECS agent dùng lúc khởi task) ───────
resource "aws_iam_role" "task_execution" {
  name               = "${var.project}-task-execution-role"
  description        = "ECS agent: pull ECR, ghi CloudWatch Logs, doc 2 SSM parameter"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume.json
}

# Managed policy này cấp quyền pull ECR và ghi CloudWatch Logs. Grant ECR của
# nó là read-only pull nhưng trên Resource = *, tức pull được mọi repo trong
# account. Chấp nhận có ý thức: account này chỉ có 3 repo của dự án, và siết
# ECR theo ARN sẽ buộc tự quản luôn grant log group — mà log group lại được
# tạo ở Task 12, tức xé một policy ra hai task. Ghi nhận để siết ở Phase 2.
resource "aws_iam_role_policy_attachment" "task_execution_managed" {
  role       = aws_iam_role.task_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Liệt kê ĐÚNG 2 ARN parameter, không dùng wildcard toàn Parameter Store.
data "aws_iam_policy_document" "task_execution_extra" {
  statement {
    sid     = "ReadOnlyTheTwoSecretsWeNeed"
    effect  = "Allow"
    actions = ["ssm:GetParameters"]

    resources = [
      var.ssm_connection_string_arn,
      var.ssm_jwt_secret_arn,
    ]
  }

  statement {
    sid     = "DecryptSecureStringWithDefaultSsmKey"
    effect  = "Allow"
    actions = ["kms:Decrypt"]

    resources = ["*"]

    condition {
      test     = "StringEquals"
      variable = "kms:ViaService"
      values   = ["ssm.${data.aws_region.current.region}.amazonaws.com"]
    }
  }
}

data "aws_region" "current" {}

resource "aws_iam_role_policy" "task_execution_extra" {
  name   = "${var.project}-task-execution-read-secrets"
  role   = aws_iam_role.task_execution.id
  policy = data.aws_iam_policy_document.task_execution_extra.json
}

# ─── ROLE 3: TASK APP (container API lúc runtime) ────────────────
# Đây là thứ thay thế BasicAWSCredentials. AWS SDK trong container tự lấy
# credential tạm thời của role này qua AWS_CONTAINER_CREDENTIALS_RELATIVE_URI.
data "aws_iam_policy_document" "task_app" {
  statement {
    sid    = "UploadAndReadProductImages"
    effect = "Allow"

    actions = [
      "s3:PutObject",
      "s3:GetObject",
      "s3:DeleteObject",
    ]

    resources = ["${var.assets_bucket_arn}/*"]
  }

  # ECS Exec cần channel qua SSM Messages. Không giới hạn được theo resource
  # (AWS không hỗ trợ), nhưng action chỉ mở channel, không đọc/ghi gì.
  statement {
    sid    = "EcsExecChannel"
    effect = "Allow"

    actions = [
      "ssmmessages:CreateControlChannel",
      "ssmmessages:CreateDataChannel",
      "ssmmessages:OpenControlChannel",
      "ssmmessages:OpenDataChannel",
    ]

    resources = ["*"]
  }
}

resource "aws_iam_role" "task_app" {
  name               = "${var.project}-task-app-role"
  description        = "Container API: CHI upload/doc anh S3 + ECS Exec"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume.json
}

resource "aws_iam_role_policy" "task_app" {
  name   = "${var.project}-task-app-s3"
  role   = aws_iam_role.task_app.id
  policy = data.aws_iam_policy_document.task_app.json
}

# ─── ROLE 4: TASK MIGRATOR ───────────────────────────────────────
# Migrator chỉ nói chuyện với RDS qua TCP 1433 — không cần quyền AWS API nào.
# Role rỗng (chỉ có trust policy) để task định danh được trong CloudTrail.
resource "aws_iam_role" "task_migrator" {
  name               = "${var.project}-task-migrator-role"
  description        = "Container migrator: khong can quyen AWS API nao, chi TCP 1433 toi RDS"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume.json
}

# ─── EXECUTION ROLE RIÊNG CHO TASK SEEDER ────────────────────────
# Vì sao KHÔNG dùng chung aws_iam_role.task_execution: seeder cần đọc
# /hushstore/prod/db-password, mà role kia được api + web + migrator dùng chung.
# Thêm db-password vào đó là cấp quyền đọc mật khẩu DB cho execution role của MỌI
# task trong module — nới quyền cho ba thứ không cần nó, chỉ vì thứ thứ tư cần.
# Một test đang canh đúng điều này (`task_execution_role_chi_doc_dung_2_parameter`)
# và nó đã ĐỎ khi tôi thử nới; test đỏ là đúng, thiết kế mới là cái phải sửa.
#
# Role riêng này chỉ đọc ĐÚNG MỘT parameter: db-password. Nó KHÔNG đọc được
# connection-string hay jwt-secret — nghịch lại chiều với role kia. Giao nhau
# bằng rỗng.
resource "aws_iam_role" "task_execution_seeder" {
  name               = "${var.project}-task-execution-seeder-role"
  description        = "Execution role rieng cho task seeder: chi doc db-password"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume.json
}

# Managed policy chuẩn của AWS: pull image từ ECR + ghi CloudWatch Logs. Không
# cấp quyền đọc parameter nào.
resource "aws_iam_role_policy_attachment" "task_execution_seeder_managed" {
  role       = aws_iam_role.task_execution_seeder.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

data "aws_iam_policy_document" "task_execution_seeder_extra" {
  statement {
    sid     = "ReadOnlyTheDbPassword"
    effect  = "Allow"
    actions = ["ssm:GetParameters"]

    resources = [var.ssm_db_password_arn]
  }

  statement {
    sid     = "DecryptSecureStringWithDefaultSsmKey"
    effect  = "Allow"
    actions = ["kms:Decrypt"]

    # SecureString dùng khoá mặc định alias/aws/ssm; ARN của nó không cố định
    # theo account nên không liệt kê được tường minh ở đây. Điều kiện
    # ViaService giới hạn: khoá chỉ được dùng qua SSM, không dùng để giải mã
    # thứ khác.
    resources = ["*"]

    condition {
      test     = "StringEquals"
      variable = "kms:ViaService"
      values   = ["ssm.${local.aws_region}.amazonaws.com"]
    }
  }
}

resource "aws_iam_role_policy" "task_execution_seeder_extra" {
  name   = "${var.project}-task-execution-seeder-read-db-password"
  role   = aws_iam_role.task_execution_seeder.id
  policy = data.aws_iam_policy_document.task_execution_seeder_extra.json
}
