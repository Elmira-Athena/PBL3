# Policy của role deploy.
#
# ─── BA ACTION BUỘC PHẢI DÙNG Resource = "*" ─────────────────────────────────
# Không phải vì lười mà vì AWS không hỗ trợ resource-level authorization cho
# chúng. Ghi ra tường minh ở đây để bảng least-privilege trong báo cáo nói được
# "chỗ này rộng vì AWS không cho hẹp", chứ không phải "chỗ này rộng vì bỏ qua":
#
#   1. ecr:GetAuthorizationToken   — token đăng nhập registry cấp theo account.
#   2. ecs:RegisterTaskDefinition  — task definition chưa tồn tại lúc gọi, nên
#                                    không có ARN để so. Bán kính thiệt hại bị
#                                    khoá bởi ba thứ khác: PassRole chỉ 3 role,
#                                    RunTask chỉ family migrator, UpdateService
#                                    chỉ 2 service. Đăng ký được task definition
#                                    lạ không có nghĩa là chạy được nó.
#   3. rds:Describe*               — RDS không hỗ trợ resource-level cho Describe.
#
# Ngoài ba chỗ đó, mọi statement đều bị ghim theo ARN hoặc theo condition.

data "aws_iam_policy_document" "deploy" {

  # ── ECR ───────────────────────────────────────────────────────
  statement {
    sid       = "EcrLoginAccountWide"
    effect    = "Allow"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }

  statement {
    sid    = "EcrPushPullOnlyOurFourRepos"
    effect = "Allow"

    # BatchGetImage và GetDownloadUrlForLayer: giữ vì chúng là 2 trong 8 action
    # của policy push chuẩn mà AWS tự công bố, và `docker push` có thể gọi chúng
    # để resolve manifest của layer đã có (cache-from). Chưa có bằng chứng
    # runtime để loại — khi pipeline đã chạy thật và CloudTrail không thấy hai
    # action này thì xoá được.
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload",
      "ecr:PutImage",
      "ecr:BatchGetImage",
      "ecr:GetDownloadUrlForLayer",
      "ecr:DescribeImages",
    ]

    resources = var.ecr_repository_arns
  }

  # ── ECS: đọc trạng thái để quyết định có deploy hay không ──────
  # Pipeline KHÔNG được bật hạ tầng (mỗi giờ bật tốn $0.1954), nên nó chỉ đọc
  # rồi tự phân nhánh. Vì thế ở đây không có autoscaling:SetDesiredCapacity,
  # không có rds:StartDBInstance — cố ý, và đó cũng là lý do role này không thể
  # tự làm phát sinh chi phí.
  statement {
    sid       = "EcsDescribeOurServices"
    effect    = "Allow"
    actions   = ["ecs:DescribeServices"]
    resources = local.service_arns
  }

  # Preflight đếm container instance ACTIVE: migration là task bridge trên EC2
  # launch type nên không có host đăng ký vào cluster thì run-task nằm ở
  # PROVISIONING mãi rồi timeout.
  statement {
    sid       = "EcsListContainerInstancesOfOurCluster"
    effect    = "Allow"
    actions   = ["ecs:ListContainerInstances"]
    resources = [var.cluster_arn]
  }

  # DescribeTaskDefinition không hỗ trợ resource-level. Nhưng nó chỉ đọc, và
  # thứ đọc được (task definition) không chứa secret: giá trị secret đi qua
  # khối `secrets` dạng ARN tham chiếu, không phải plaintext trong `environment`
  # — xem modules/ecs/taskdef.tf.
  statement {
    sid       = "EcsDescribeTaskDefinition"
    effect    = "Allow"
    actions   = ["ecs:DescribeTaskDefinition"]
    resources = ["*"]
  }

  # DescribeTasks không nhận ARN cluster làm Resource, nên siết bằng condition
  # ecs:cluster. Kết quả tương đương: role không nhìn được task của cluster nào
  # khác. Đây là action mà vòng poll của bước migrate gọi mỗi 10 giây để lấy
  # lastStatus và exitCode.
  statement {
    sid    = "EcsReadTasksInOurClusterOnly"
    effect = "Allow"

    actions = [
      "ecs:DescribeTasks",
    ]

    resources = ["*"]

    condition {
      test     = "ArnEquals"
      variable = "ecs:cluster"
      values   = [var.cluster_arn]
    }
  }

  # ── ECS: đăng ký revision mới và trỏ service sang nó ───────────
  statement {
    sid       = "EcsRegisterTaskDefinitionNoResourceLevelSupport"
    effect    = "Allow"
    actions   = ["ecs:RegisterTaskDefinition"]
    resources = ["*"]
  }

  statement {
    sid       = "EcsUpdateOnlyTheTwoServices"
    effect    = "Allow"
    actions   = ["ecs:UpdateService"]
    resources = local.service_arns
  }

  # RunTask bị ghim vào ĐÚNG family migrator. Pipeline không chạy được task
  # api, web hay seeder — kể cả khi nó đăng ký được task definition mới.
  # Riêng seeder là chủ ý: seed dữ liệu ghi đè bảng thật, đó là việc tay có
  # người chịu trách nhiệm, không phải việc của một push vào main.
  statement {
    sid       = "EcsRunOnlyMigratorTask"
    effect    = "Allow"
    actions   = ["ecs:RunTask"]
    resources = [local.migrator_taskdef_arn]

    condition {
      test     = "ArnEquals"
      variable = "ecs:cluster"
      values   = [var.cluster_arn]
    }
  }

  # ── PassRole ──────────────────────────────────────────────────
  # RegisterTaskDefinition tham chiếu taskRoleArn và executionRoleArn, nên nó
  # cần PassRole. Đây là action nguy hiểm nhất trong policy này: PassRole rộng
  # cộng RegisterTaskDefinition là đường leo thang đặc quyền kinh điển trên ECS
  # (đăng ký task chạy image bất kỳ dưới một role đặc quyền, rồi chạy nó).
  #
  # Chặn bằng hai lớp: liệt kê đúng 3 role, và condition PassedToService để
  # role chỉ đi được vào ECS task chứ không đi vào EC2, Lambda hay service nào
  # khác. Danh sách này KHÔNG gồm role của EC2 host (nó là lớp bị cô lập có chủ
  # ý) và KHÔNG gồm execution role của seeder (role duy nhất đọc được mật khẩu
  # DB).
  statement {
    sid       = "PassOnlyTheThreeEcsTaskRoles"
    effect    = "Allow"
    actions   = ["iam:PassRole"]
    resources = var.passable_role_arns

    condition {
      test     = "StringEquals"
      variable = "iam:PassedToService"
      values   = ["ecs-tasks.amazonaws.com"]
    }
  }

  # ── CloudWatch Logs: chỉ log của migrator ─────────────────────
  # Khi migration fail, pipeline phải in được nguyên nhân ngay trong output của
  # Actions — nếu không thì "job đỏ" mà phải mở console AWS mới biết vì sao.
  # Giới hạn đúng một log group: pipeline không đọc được log của api (log api có
  # thể chứa dữ liệu người dùng).
  statement {
    sid    = "ReadMigratorLogsOnly"
    effect = "Allow"

    actions = [
      "logs:GetLogEvents",
      "logs:DescribeLogStreams",
    ]

    resources = [
      var.migrator_log_group_arn,
      "${var.migrator_log_group_arn}:log-stream:*",
    ]
  }

  # ── RDS ───────────────────────────────────────────────────────
  statement {
    sid       = "RdsDescribeNoResourceLevelSupport"
    effect    = "Allow"
    actions   = ["rds:DescribeDBInstances", "rds:DescribeDBSnapshots"]
    resources = ["*"]
  }

  # Snapshot trước khi migrate = điểm quay về cho DB. Chính sách migration là
  # forward-only (không dùng down-migration), nên đây là đường rollback duy nhất
  # cho dữ liệu.
  statement {
    sid       = "SnapshotBeforeMigrate"
    effect    = "Allow"
    actions   = ["rds:CreateDBSnapshot"]
    resources = [var.rds_instance_arn, local.snapshot_arn_pattern]
  }

  # Dọn snapshot cũ để không tích tiền storage vô hạn. Quyền xoá bị ghim theo
  # tiền tố tên `pre-migrate-*`: pipeline KHÔNG xoá được snapshot người tạo tay,
  # cũng không xoá được final snapshot lúc destroy. Ranh giới an toàn nằm ở ARN
  # pattern, không nằm ở logic của script.
  statement {
    sid       = "DeleteOnlyOwnPreMigrateSnapshots"
    effect    = "Allow"
    actions   = ["rds:DeleteDBSnapshot"]
    resources = [local.snapshot_arn_pattern]
  }

  # ── S3: chỉ prefix migrations/ ─────────────────────────────────
  # `migrate-<sha>.sql` sinh bằng `dotnet ef migrations script --idempotent` —
  # để người review đọc được đúng câu SQL sẽ chạy lên DB thật.
  statement {
    sid       = "PutMigrationScriptUnderOnePrefix"
    effect    = "Allow"
    actions   = ["s3:PutObject"]
    resources = ["${var.artifacts_bucket_arn}/migrations/*"]
  }
}

# ─── POLICY DENY CỦA ROLE PLAN ───────────────────────────────────
# Role plan được gắn managed ReadOnlyAccess. ReadOnlyAccess đủ rộng để
# `terraform validate` và `terraform test` chạy mà không phải đoán từng API mà
# provider gọi — nhưng nó cũng cho đọc s3:GetObject, tức đọc được
# terraform.tfstate, tức đọc được master password của RDS ở dạng plaintext.
#
# Bịt bằng explicit Deny, không bằng cách thu hẹp Allow. Lý do là thứ tự đánh
# giá của IAM: explicit Deny luôn thắng mọi Allow, kể cả Allow thêm về sau. Nếu
# ai đó gắn thêm một managed policy rộng hơn cho role này ngày mai, những đường
# dưới đây VẪN đóng. Đây đúng là lập luận đã dùng cho
# hushstore-container-instance-role ở Phase 1.
data "aws_iam_policy_document" "plan_deny" {

  # Đường số 1 tới mật khẩu DB: đọc tfstate.
  statement {
    sid       = "DenyReadingAnyS3Object"
    effect    = "Deny"
    actions   = ["s3:GetObject", "s3:GetObjectVersion"]
    resources = ["*"]
  }

  # Đường số 2: đọc thẳng SecureString của ta.
  statement {
    sid    = "DenyReadingOurParameters"
    effect = "Deny"

    actions = [
      "ssm:GetParameter",
      "ssm:GetParameters",
      "ssm:GetParameterHistory",
      "ssm:GetParametersByPath",
    ]

    resources = ["arn:aws:ssm:*:*:parameter/${var.project}/*"]
  }

  # Đường số 3: giải mã bất cứ thứ gì. Không có kms:Decrypt thì SecureString
  # chỉ trả về blob đã mã hoá.
  statement {
    sid       = "DenyDecryptAnything"
    effect    = "Deny"
    actions   = ["kms:Decrypt", "secretsmanager:GetSecretValue"]
    resources = ["*"]
  }

  # Đường số 4: log của api có thể chứa dữ liệu người dùng.
  statement {
    sid       = "DenyReadingLogContents"
    effect    = "Deny"
    actions   = ["logs:GetLogEvents", "logs:FilterLogEvents"]
    resources = ["*"]
  }

  # Đường số 5: pivot sang role khác. Một PR không được đổi vai.
  statement {
    sid       = "DenyRolePivot"
    effect    = "Deny"
    actions   = ["sts:AssumeRole", "ecs:ExecuteCommand", "ssm:StartSession"]
    resources = ["*"]
  }
}
