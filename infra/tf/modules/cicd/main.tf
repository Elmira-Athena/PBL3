# ─── OIDC PROVIDER ───────────────────────────────────────────────
# Đây là thứ thay thế toàn bộ credential dài hạn của pipeline. Trước Phase 2,
# deploy đi bằng `EC2_SSH_KEY` — một private key nằm trong GitHub Secrets, không
# hết hạn, và ai đọc được secret đó là vào được máy chủ. Sau Phase 2 GitHub
# không giữ credential nào: mỗi job xin AWS một JWT ngắn hạn do GitHub ký, AWS
# xác thực chữ ký rồi đổi thành credential tạm 1 giờ.
#
# `thumbprint_list` cố tình để trống. AWS đã tự tin cậy các IdP phổ biến
# (token.actions.githubusercontent.com nằm trong danh sách đó) và tự điền
# thumbprint. Hardcode thumbprint là tự nhận việc theo dõi vòng đời cert của
# GitHub — mà cert đó hết hạn thì pipeline chết mà không ai biết lý do.
resource "aws_iam_openid_connect_provider" "github" {
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]

  tags = { Name = "${var.project}-github-oidc" }
}

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

locals {
  account_id = data.aws_caller_identity.current.account_id
  region     = data.aws_region.current.region

  # Claim `sub` của GitHub OIDC token. GitHub đặt nó theo TRIGGER, không theo
  # người:
  #   • push vào nhánh main  -> repo:owner/repo:ref:refs/heads/main
  #   • pull_request         -> repo:owner/repo:pull_request
  #
  # Tính chất phải giữ, và là lý do có hai role: role DEPLOY (role ghi được) chỉ
  # nhận giá trị thứ nhất. Một PR KHÔNG BAO GIỜ lấy được token khớp nó, nên một
  # PR không bao giờ assume được role deploy — kể cả PR do chính chủ repo mở.
  #
  # Chiều ngược lại thì KHÔNG đối xứng, có chủ ý: role plan nhận CẢ HAI giá trị
  # (xem data.aws_iam_policy_document.assume_plan). Quy ước của dự án này là
  # commit thẳng lên main, nên nếu role plan chỉ nhận `pull_request` thì
  # `terraform test` — nơi chứa các assertion bảo mật — không bao giờ chạy trong
  # CI. Đặc quyền chỉ chảy một chiều: plan là ReadOnlyAccess + 5 nhóm Deny,
  # deploy mới là role sửa được hạ tầng.
  sub_deploy = "repo:${var.github_owner}/${var.github_repo}:ref:refs/heads/${var.deploy_branch}"
  sub_plan   = "repo:${var.github_owner}/${var.github_repo}:pull_request"

  # ARN của OIDC provider dựng bằng CHUỖI, không phải
  # aws_iam_openid_connect_provider.github.arn. Lý do là kiểm thử được: tham
  # chiếu resource là giá trị chưa biết lúc plan, và điều đó làm CẢ
  # aws_iam_policy_document trở thành "(known after apply)" — nên không assertion
  # nào đọc được nội dung trust policy trước khi apply. Mà trust policy đúng là
  # thứ đáng canh nhất trong module này.
  #
  # ARN của provider là tiền định: nó chỉ phụ thuộc account id và hostname của
  # IdP. Đánh đổi duy nhất là mất quan hệ phụ thuộc ngầm, nên hai role phải khai
  # `depends_on` tường minh — thiếu nó, Terraform có thể tạo role trước provider
  # và IAM trả MalformedPolicyDocument vì principal Federated chưa tồn tại.
  oidc_provider_arn = "arn:aws:iam::${local.account_id}:oidc-provider/token.actions.githubusercontent.com"

  # Service ARN dựng bằng tay thay vì tham chiếu resource. Xem comment ở
  # variable "service_names": service bị enable_alb gate nên nó biến mất mỗi
  # lần tắt stack, còn IAM policy thì phải tồn tại liên tục.
  service_arns = [
    for name in var.service_names :
    "arn:aws:ecs:${local.region}:${local.account_id}:service/${var.cluster_name}/${name}"
  ]

  migrator_taskdef_arn = "arn:aws:ecs:${local.region}:${local.account_id}:task-definition/${var.migrator_taskdef_family}:*"

  snapshot_arn_pattern = "arn:aws:rds:${local.region}:${local.account_id}:snapshot:${var.snapshot_prefix}-*"
}

# ─── TRUST POLICY ────────────────────────────────────────────────
# Dùng StringEquals, KHÔNG dùng StringLike. Với StringLike thì một giá trị như
# `repo:Elmira-Athena/PBL3:*` sẽ khớp cả pull_request, cả mọi nhánh, cả mọi tag
# — tức mở role deploy cho bất kỳ ai mở được PR. Đây là lỗi cấu hình OIDC phổ
# biến nhất và nó không có triệu chứng nào cho tới lúc bị lợi dụng.
#
# Liệt kê NHIỀU giá trị trong `values` thì khác hẳn: `StringEquals` vẫn là so
# khớp chính xác từng chuỗi, chỉ là khớp một trong một danh sách đóng. Role plan
# dùng cách đó; role deploy thì cố tình chỉ có MỘT giá trị.
#
# Điều kiện `aud` cũng bắt buộc. Thiếu nó, một token do GitHub ký cho MỘT
# audience khác (ví dụ một cloud provider khác) vẫn thoả trust policy này.
data "aws_iam_policy_document" "assume_deploy" {
  statement {
    sid     = "GitHubOidcMainBranchOnly"
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values   = [local.sub_deploy]
    }
  }
}

# Role plan nhận HAI giá trị sub, và cả hai là so khớp CHÍNH XÁC — `StringEquals`
# với danh sách nghĩa là "khớp một trong các giá trị này", không phải wildcard.
# Vì sao cần giá trị thứ hai: dự án commit thẳng lên `main`, nên nếu chỉ nhận
# `pull_request` thì job `terraform-test` trong ci.yml không có đường nào chạy, và
# các assertion bảo mật (NACL stateless, SG không mở 22, IAM least privilege) chỉ
# tồn tại trên máy cá nhân.
#
# Điều này KHÔNG mở rộng bán kính thiệt hại: tập người lấy được token push-main
# và tập người lấy được token pull_request là cùng một tập — cộng tác viên có
# quyền ghi vào repo. Ai push được lên main thì cũng push được một nhánh rồi mở
# PR, tức đã tới được role plan từ trước. Cái mất đi là một tính chất kiểm toán:
# từ nay một session của role plan không còn CHỨNG MINH được rằng lần chạy đó là
# một PR. Bù bằng `role-session-name` trong workflow (gha-tf-test).
#
# Điều tuyệt đối KHÔNG được làm là chiều ngược lại: thêm `pull_request` vào
# assume_deploy. Xem assert trong tests/cicd.tftest.hcl.
data "aws_iam_policy_document" "assume_plan" {
  statement {
    sid     = "GitHubOidcPullRequestOrMainBranch"
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values   = [local.sub_plan, local.sub_deploy]
    }
  }
}

# ─── ROLE 1: DEPLOY ──────────────────────────────────────────────
resource "aws_iam_role" "deploy" {
  name        = "${var.project}-github-actions-deploy-role"
  description = "GitHub Actions push main: build/push ECR, migrate, update ECS service"

  assume_role_policy = data.aws_iam_policy_document.assume_deploy.json

  # Xem comment ở local.oidc_provider_arn: trust policy nhắc tới provider bằng
  # chuỗi, nên quan hệ phụ thuộc phải khai tường minh.
  depends_on = [aws_iam_openid_connect_provider.github]

  # 1 giờ. Job deploy dài nhất đo được là ~12 phút (build 4 image + migrate +
  # wait services-stable). Mặc định của AWS cũng là 1 giờ; ghi tường minh để
  # thấy đây là lựa chọn, và để không ai nới lên 12 giờ cho "chắc".
  max_session_duration = 3600

  tags = { Name = "${var.project}-github-actions-deploy-role" }
}

resource "aws_iam_role_policy" "deploy" {
  name   = "${var.project}-github-actions-deploy"
  role   = aws_iam_role.deploy.id
  policy = data.aws_iam_policy_document.deploy.json
}

# ─── ROLE 2: PLAN / KIỂM TRA TRONG CI ────────────────────────────
# Role này chỉ dùng cho `terraform fmt/validate/test`, trên PR và trên push vào
# `main` (xem assume_plan: hai giá trị sub, cả hai khớp chính xác). Nó KHÔNG chạy
# `terraform plan`, và đó là quyết định có chủ ý — xem docs/superpowers/plans/
# 2026-08-22-aws-terraform-phase2-cicd.md mục "Quyết định thiết kế" số 2:
# `plan` phải đọc tfstate, mà tfstate chứa master password của RDS ở dạng
# plaintext (random_password luôn nằm trong state — bản chất của Terraform).
resource "aws_iam_role" "plan" {
  name        = "${var.project}-github-actions-plan-role"
  description = "GitHub Actions CI (PR va push main): fmt/validate/test. KHONG doc duoc tfstate va khong giai ma duoc secret"

  assume_role_policy = data.aws_iam_policy_document.assume_plan.json

  # Xem comment ở local.oidc_provider_arn: trust policy nhắc tới provider bằng
  # chuỗi, nên quan hệ phụ thuộc phải khai tường minh.
  depends_on = [aws_iam_openid_connect_provider.github]

  max_session_duration = 3600

  tags = { Name = "${var.project}-github-actions-plan-role" }
}

# ReadOnlyAccess đủ rộng để `terraform validate` và `terraform test` chạy được
# mà không phải đoán từng action mà provider gọi. Rủi ro của nó được bịt bằng
# inline policy CHỈ CÓ DENY ở dưới — không phải bằng cách thu hẹp Allow.
resource "aws_iam_role_policy_attachment" "plan_readonly" {
  role       = aws_iam_role.plan.name
  policy_arn = "arn:aws:iam::aws:policy/ReadOnlyAccess"
}

resource "aws_iam_role_policy" "plan_deny" {
  name   = "${var.project}-github-actions-plan-deny-secrets"
  role   = aws_iam_role.plan.id
  policy = data.aws_iam_policy_document.plan_deny.json
}
