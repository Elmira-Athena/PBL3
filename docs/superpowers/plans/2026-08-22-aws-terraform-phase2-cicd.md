# Phase 2 — CI/CD không credential dài hạn

> **Spec:** [2026-08-17-aws-terraform-ecs-infra-design.md](../specs/2026-08-17-aws-terraform-ecs-infra-design.md) — mục "Phase 2 — CI/CD"

**Goal:** Thay pipeline SSH + GHCR + `latest` bằng GitHub OIDC → ECR (tag = git SHA) → migration làm gate → `ecs update-service`, và bỏ mọi secret dài hạn khỏi GitHub.

**Architecture:** Một module Terraform mới (`cicd`) tạo OIDC provider + 2 IAM role (deploy / plan). Hai workflow: `deploy.yml` (push `main`) và `ci.yml` (PR). Task definition vẫn do Terraform định hình, nhưng revision đang chạy do CI đăng ký — nên service phải `ignore_changes = [task_definition]`.

**Tech Stack:** Terraform 1.15.8 · AWS provider ~> 6.0 · GitHub Actions · `aws-actions/configure-aws-credentials@v4` · AWS CLI v2

---

## Global Constraints

Chép nguyên văn, ràng buộc mọi task:

- **Không có gì được gửi tới `bach.huynhvan@smartdev.com`.** Mọi email cảnh báo dùng `dacvinh2322006@gmail.com`.
- **Không SG rule nào mở port 22, ở bất kỳ đâu.**
- **Không nới `Deny ssm:GetParameter*` trên `/hushstore/*`** của `hushstore-container-instance-role` — đó là deliverable đã được chấm.
- `enable_nat` / `enable_alb` / `instance_count` giữ nguyên giá trị trong `terraform.tfvars` sau khi xong (mặc định tắt).
- Mọi thông báo hướng tới người dùng bằng **tiếng Việt có dấu**.
- **CI không được bật hạ tầng tốn phí.** Không `SetDesiredCapacity`, không `start-db-instance`, không `terraform apply`.
- 4 NuGet package có lỗ hổng chỉ nâng ở **cuối toàn bộ dự án**, không phải ở Phase này.

---

## Quyết định thiết kế và lý do

### 1. CI **không** tự bật hạ tầng — đây là thay đổi so với spec

Spec gốc có bước `ensure-capacity` đặt ASG desired ≥ 1 trước khi `run-task`. Bỏ.

Lý do: `enable_alb` và `instance_count` mặc định tắt, và mỗi giờ bật tốn **$0.1954** (đo thật, xem runbook). Một pipeline tự bật hạ tầng nghĩa là **mỗi lần push vào `main` là một lần bắt đầu tính tiền**, và không ai nhận ra cho tới khi đọc hoá đơn. Rủi ro đó không có trần trên.

Thay vào đó pipeline **đọc** trạng thái rồi phân nhánh:

| Trạng thái thật | CI làm gì |
|---|---|
| RDS `available` **và** có container instance `ACTIVE` **và** 2 service tồn tại | build → push → snapshot → migrate → deploy → health check |
| Bất kỳ điều kiện nào không thoả | build → push → **dừng, job vẫn xanh**, in summary nói rõ chưa deploy và phải làm gì |

Nhánh thứ hai không phải thất bại — nó là trạng thái bình thường của dự án này. Nhưng nó **phải hiện rõ trong summary**, vì "Actions xanh" mà không deploy là đúng loại nhầm lẫn gây mất buổi debug.

### 2. CI **không** chạy `terraform plan` — cũng là thay đổi so với spec

Spec gốc muốn `infra.yml` chạy `plan` rồi comment vào PR. Bỏ phần `plan`.

Lý do: `plan` cần đọc `terraform.tfstate`, và state **chứa master password của RDS** ở dạng plaintext (`random_password` luôn nằm trong state — đây là bản chất của Terraform, không sửa được). Một role đọc được state là một role đọc được mật khẩu DB. Với trigger `pull_request`, claim `sub` của OIDC token là `repo:Elmira-Athena/PBL3:pull_request` — **không phân biệt được PR từ fork**. Đánh đổi sai chiều.

PR chạy `fmt -check` → `validate` → **`terraform test`** (16 test file, chính là chỗ canh các assertion bảo mật về NACL/SG/IAM). Đó là kiểm tra có giá trị nhất trong repo này, và không cần state.

Đường nâng cấp nếu sau này muốn `plan` trên PR: dùng GitHub **environment** có required reviewer, khi đó `sub` thành `repo:.../environment:<name>` và fork không lấy được token mà không có người bấm duyệt. Là cấu hình trên GitHub, không phải Terraform.

### 3. Task definition: Terraform định hình, CI đăng ký revision

CI lấy taskdef hiện tại bằng `describe-task-definition`, đổi đúng field `image`, `register-task-definition`, rồi `update-service`. Hệ quả bắt buộc: `aws_ecs_service` phải `lifecycle { ignore_changes = [task_definition] }`, nếu không lần `terraform apply` kế tiếp sẽ kéo service về revision của Terraform, tức **rollback ngầm**.

Sau Phase 2, `var.image_tag` trong `terraform.tfvars` đổi nghĩa: nó là **image dùng khi dựng lại từ đầu**, không phải image đang chạy. Vì thế thêm cảnh báo vào `up.sh`: nếu ECR có tag mới hơn tag trong tfvars thì in ra, để không ai bật stack lên rồi tưởng mình đang chạy bản mới nhất.

### 4. Ba món nợ rebuild image trả gộp ở đây

Cả ba đều đang nằm trong file nguồn nhưng **chưa vào image đang chạy trên ECR**, vì image được build ở Task 9/16 của Phase 1, trước các commit sửa. Phase 2 build lại cả 4 image nên trả gộp là đúng lúc:

| Nợ | Trạng thái file nguồn | Việc phải làm ở Phase 2 |
|---|---|---|
| nginx cache theo đường dẫn thay vì phần mở rộng | **đã sửa** trong `src/Client/nginx.conf` | chỉ cần rebuild — không sửa gì thêm |
| `ForwardLimit = 1` tường minh | **chưa có** trong `Program.cs` | thêm + comment giải thích |
| `ADD --checksum` cho RDS CA bundle | **chưa có** ở 3 Dockerfile | thêm `--checksum=sha256:3c69…` |

---

## Cấu trúc file

```
infra/tf/modules/cicd/                    # MỚI
├── main.tf          OIDC provider + 2 role + trust policy
├── policy.tf        policy của role deploy (7 statement) + role plan (Deny list)
├── variables.tf
├── outputs.tf
└── tests/cicd.tftest.hcl                 # assertion least-privilege

infra/tf/envs/prod/main.tf                # + module "cicd"
infra/tf/envs/prod/outputs.tf             # + 2 role ARN
infra/tf/envs/prod/variables.tf           # seeder_image_tag: default ""
infra/tf/modules/ecs/service.tf           # + ignore_changes = [task_definition]
infra/tf/modules/ecs/taskdef.tf           # seeder dùng coalesce(seeder_image_tag, image_tag)
infra/tf/scripts/up.sh                    # + cảnh báo ECR có tag mới hơn tfvars

.github/workflows/deploy.yml              # VIẾT LẠI
.github/workflows/ci.yml                  # MỚI (thay tên infra.yml — nó chạy cả test)

Dockerfile                                # + ADD --checksum
src/Infrastructure/Dockerfile.migrator    # + ADD --checksum
src/Infrastructure/Dockerfile.seeder      # + ADD --checksum
src/API/Program.cs                        # + ForwardLimit = 1

docs/terraform-runbook.md                 # mục CI/CD, rollback qua CI, nghĩa mới của image_tag
docs/security-validation-report.md        # KB-11 (giả mạo OIDC) chuyển từ ⏳ sang có kết quả
README.md                                 # mục CI/CD
```

---

## Task

### Task 1 — `modules/cicd`

**Files:** create `infra/tf/modules/cicd/{main,policy,variables,outputs}.tf` và `tests/cicd.tftest.hcl`

**Interfaces — Produces:** `deploy_role_arn`, `plan_role_arn`, `oidc_provider_arn`

Role deploy `hushstore-github-actions-deploy-role`, trust `StringEquals` trên
`sub = repo:Elmira-Athena/PBL3:ref:refs/heads/main` (không `StringLike`, không
wildcard branch) và `aud = sts.amazonaws.com`.

Bảy statement, mỗi cái ghi rõ vì sao phạm vi là như vậy:

| Statement | Action | Resource |
|---|---|---|
| `EcrLogin` | `ecr:GetAuthorizationToken` | `*` — AWS không hỗ trợ resource-level cho action này |
| `EcrPushPull` | 8 action layer/image | đúng 4 ARN repo |
| `EcsRead` | `Describe{Services,Tasks,TaskDefinition}`, `List{Tasks,ContainerInstances}` | service/cluster ARN nơi hỗ trợ, `*` + condition `ecs:cluster` nơi không |
| `EcsRegisterTaskDef` | `ecs:RegisterTaskDefinition` | `*` — **AWS không hỗ trợ resource-level**, ghi nhận tường minh |
| `EcsDeploy` | `ecs:UpdateService` | đúng 2 service ARN |
| `EcsRunMigrator` | `ecs:RunTask` | `task-definition/hushstore-migrator:*` + condition `ecs:cluster` |
| `PassTaskRoles` | `iam:PassRole` | đúng 3 role ARN + condition `iam:PassedToService = ecs-tasks.amazonaws.com` |
| `ReadMigratorLogs` | `logs:GetLogEvents`, `logs:DescribeLogStreams` | đúng log group migrator |
| `SnapshotBeforeMigrate` | `rds:CreateDBSnapshot` | ARN instance + ARN `snapshot:pre-migrate-*` |
| `SnapshotHousekeeping` | `rds:DescribeDBSnapshots`, `rds:DeleteDBSnapshot` | Delete **chỉ** trên `snapshot:pre-migrate-*` |
| `RdsRead` | `rds:DescribeDBInstances` | `*` — không hỗ trợ resource-level |
| `PutMigrationScript` | `s3:PutObject` | `artifacts/migrations/*`, không phải cả bucket |

Role plan `hushstore-github-actions-plan-role`, trust `sub = repo:Elmira-Athena/PBL3:pull_request`.
Quyền = managed `ReadOnlyAccess` **cộng một inline policy chỉ có Deny**:
`s3:GetObject` (bịt đường đọc tfstate → mật khẩu RDS), `ssm:GetParameter*` trên
`/hushstore/*`, `kms:Decrypt`, `secretsmanager:GetSecretValue`,
`logs:GetLogEvents`, `sts:AssumeRole` (chặn pivot). Explicit Deny luôn thắng
Allow — cùng lập luận đã dùng cho instance role ở Phase 1.

**Test:** deploy role không có statement nào `Resource = "*"` ngoài đúng 3 action
AWS không hỗ trợ resource-level; `iam:PassRole` phải có condition
`PassedToService`; trust policy không được chứa `StringLike`; role plan phải có
Deny cho `s3:GetObject` và `kms:Decrypt`.

### Task 2 — Wire vào `envs/prod`

`module "cicd"` trong `main.tf`, output 2 role ARN. Không biến mới trong tfvars
(owner/repo có default).

### Task 3 — `ignore_changes` + gộp `seeder_image_tag`

`lifecycle { ignore_changes = [task_definition] }` cho cả `aws_ecs_service.web`
và `.api`, kèm comment nói rõ vì sao (CI đăng ký revision; thiếu dòng này là
rollback ngầm).

`variable "seeder_image_tag"` nhận `default = ""`; taskdef seeder dùng
`var.seeder_image_tag != "" ? var.seeder_image_tag : var.image_tag`. Xoá dòng
`seeder_image_tag` khỏi `terraform.tfvars` để nó đi theo `image_tag`.

### Task 4 — Trả 3 món nợ image

`Program.cs`: `options.ForwardLimit = 1;` + comment giải thích rằng ALB **append**
vào `X-Forwarded-For` chứ không replace, nên phần tử **phải phía** là IP mà ALB
thấy; đọc đúng 1 phần tử nghĩa là header do client tự bơm bị bỏ qua.

3 Dockerfile: `ADD --checksum=sha256:3c696020a3b7c6721085d182211c28024ab01873ade35dcc7eeebb89c20ee979`
(đo bằng `curl … | shasum -a 256` ngày 2026-08-22).

### Task 5 — `deploy.yml`

Job `build` (luôn chạy): OIDC → ECR login → build+push 4 image tag `$GITHUB_SHA`
→ sinh `migrate-<sha>.sql` bằng `dotnet ef migrations script --idempotent` →
upload S3.

Job `preflight`: đọc RDS state, container instance, 2 service → output `ready`.

Job `deploy` (`if: needs.preflight.outputs.ready == 'true'`): snapshot →
`run-task` migrator → **gate exit code** (khác 0 → in log CloudWatch, fail,
không deploy) → register 2 taskdef revision → `update-service` ×2 →
`wait services-stable` → health check `curl` → rollback về revision trước nếu fail.

Job `summary`: luôn chạy, in rõ đã deploy hay chỉ build.

### Task 6 — `ci.yml`

`pull_request` + `push` nhánh khác `main`: `fmt -check -recursive` →
`init -backend=false && validate` (envs/prod + 8 module) → `terraform test`
mỗi module. Shim `~/.aws/config` với `credential_source = Environment` để 16
test file giữ nguyên `profile = "hushstore"`.

### Task 7 — `up.sh` cảnh báo tag lệch

Sau khi RDS lên, gọi `aws ecr describe-images` lấy tag mới nhất của repo `api`,
so với `image_tag` trong tfvars; lệch thì in cảnh báo vàng kèm câu lệnh sửa.

### Task 8 — Tài liệu + việc tay

Runbook: mục CI/CD, rollback qua CI, nghĩa mới của `image_tag`, 2 dòng
troubleshooting mới. README: mục CI/CD. `security-validation-report.md`: KB-11.

Việc tay của người dùng (không tự làm được — `gh` chưa cài, và xoá secret là
hành động không hoàn tác được trên tài khoản của họ):

1. Thêm 2 repository **variable** (không phải secret): `AWS_DEPLOY_ROLE_ARN`, `AWS_PLAN_ROLE_ARN`
2. Xoá 3 secret: `EC2_SSH_KEY`, `EC2_HOST`, và GHCR token nếu có

---

## Verification

```bash
terraform -chdir=infra/tf/envs/prod fmt -check -recursive ../..
terraform -chdir=infra/tf/modules/cicd init -backend=false && terraform -chdir=infra/tf/modules/cicd test
terraform -chdir=infra/tf/envs/prod plan     # chỉ IAM + OIDC, $0, không tạo gì tốn phí
```

- `terraform plan` phải cho thấy **chỉ** resource IAM/OIDC được thêm, không NAT, không ALB, không instance.
- Sau `apply`: `aws iam get-role --role-name hushstore-github-actions-deploy-role` trả về trust policy đúng branch.
- Push một commit nhỏ vào `main` **khi stack đang tắt** → Actions xanh, 4 image mới trên ECR, summary nói rõ "chưa deploy".
- Bật stack rồi push lại → deploy thật, `describe-task-definition` cho revision mới trỏ `:<sha>`.
- Gate migration: push một migration lỗi → job fail ở `run-task`, service **không** đổi.
- KB-11: `aws sts assume-role-with-web-identity` với token bịa → `AccessDenied`.
