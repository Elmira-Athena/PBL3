# AWS Terraform Phase 1 — Hạ tầng nền + ứng dụng chạy được

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dựng toàn bộ hạ tầng AWS bằng Terraform (VPC 3 tier, NACL, Security Group, RDS, ECS trên EC2, ALB) và đưa HushStore chạy được trên đó, truy cập qua ALB DNS name.

**Architecture:** Stack Terraform greenfield module hoá trong `infra/tf/`. VPC `10.20.0.0/16` chia 3 tier × 2 AZ. Ứng dụng đóng gói thành 3 image (api / web / migrator) đẩy lên ECR, chạy bằng ECS EC2 launch type với `bridge` network mode và static host port. ALB terminate TLS bằng ACM cert, route theo Host header sang 2 target group. Migration DB do một one-off ECS task chạy EF Core migration bundle, không còn `MigrateAsync()` lúc startup.

**Tech Stack:** Terraform ≥ 1.10 (native S3 state locking), AWS provider ~> 6.0, `terraform test` (HCL, `command = plan`), Docker, .NET 10, ECS EC2 launch type, RDS SQL Server Express, ACM, SSM Parameter Store, SSM Session Manager.

**Spec:** [docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md](../specs/2026-08-17-aws-terraform-ecs-infra-design.md)

## Global Constraints

- **Terraform** `>= 1.10` — bắt buộc, vì backend S3 dùng `use_lockfile = true` (native lockfile, không DynamoDB).
- **AWS provider** `~> 6.0`.
- **`terraform test` với `command = plan` chỉ được assert trên giá trị biết-ở-plan-time.** Đó là: literal trong `locals`, giá trị `variable`, key của `for_each`, và attribute được set trực tiếp từ chúng (`from_port`, `cidr_ipv4`, `rule_number`, `network_mode`, `type`...). **KHÔNG được so sánh hai attribute mà cả hai là `(known after apply)`** — ví dụ `r.security_group_id == aws_security_group.x.id`, `taskdef.task_role_arn == aws_iam_role.y.arn`, `listener.target_group_arn == aws_lb_target_group.z.arn`. Terraform không chứng minh được hai unknown bằng nhau nên **báo lỗi `Unknown condition value` và bỏ luôn các run còn lại trong file**, chứ không trả `false`. Muốn biết một rule thuộc resource nào thì đếm theo **key của `for_each`** (`startswith(k, "alb-")`), và muốn chứng minh source là SG chứ không phải CIDR thì assert `cidr_ipv4 == null`.
- **Mỗi module phải có `versions.tf`** khai báo `required_version >= 1.10` và `aws ~> 6.0`. Không có nó, `terraform init` khi chạy `terraform test` trong thư mục module sẽ lấy provider mới nhất thay vì bản root đang dùng — test có thể validate trên provider khác với bản thật sự apply. Nội dung giống nhau ở mọi module:
  ```hcl
  terraform {
    required_version = ">= 1.10"

    required_providers {
      aws = {
        source  = "hashicorp/aws"
        version = "~> 6.0"
      }
    }
  }
  ```
  Riêng module `data` thêm `random = { source = "hashicorp/random", version = "~> 3.6" }`.
- **Region** `ap-southeast-1`. **AWS CLI profile** `hushstore` — trỏ vào **account mới của kỳ này** (account cũ `408194747451` đã hết free tier và đã bị xoá sạch tài nguyên, không dùng nữa).
- **Danh tính là IAM Identity Center (SSO)**, không phải IAM user + access key. Mỗi ngày làm việc chạy `aws sso login --profile hushstore` một lần. Permission set `AdministratorAccess`, session 8 giờ.
- **Account ID không hardcode trong plan.** Mọi lệnh dùng biến `$ACCT`; đặt nó ở đầu mỗi phiên làm việc:
  ```bash
  export ACCT=$(aws sts get-caller-identity --query Account --output text --profile hushstore)
  ```
  Ngoại lệ duy nhất là `backend.tf` — block `backend "s3"` không nhận biến, phải điền chuỗi thật (Task 2 Step 6).
- **VPC CIDR** `10.20.0.0/16`.
- Mọi resource phải có tag `Project = "hushstore"` và `ManagedBy = "terraform"` (đặt bằng `default_tags` ở provider).
- **Tuyệt đối không có Security Group rule nào mở port 22**, ở bất kỳ đâu. Admin access chỉ qua SSM Session Manager và ECS Exec.
- **ASG `max_size = 1`**, instance type `t3.micro` (free tier).
- **ECR image tag = git SHA đầy đủ** (immutable), không dùng `latest`.
- **Tiếng Việt — phân biệt hai loại chuỗi, đừng trộn:**
  - **Chuỗi Terraform-local** (`description` của `variable`/`output`, `error_message` của `validation` và của `terraform test`, comment, message trong script bash): **tiếng Việt CÓ DẤU**. Chúng không bao giờ được gửi lên AWS.
  - **Chuỗi gửi vào AWS API** (`description` của `aws_security_group` và của rule, `description` của IAM role, `description` của SSM parameter, `db_subnet_group_description`, text trong ECR lifecycle policy): **tiếng Việt KHÔNG DẤU (ASCII)**. `description` của EC2 Security Group chỉ nhận ASCII — bỏ dấu vào là AWS từ chối request và `apply` fail. Các field còn lại giữ ASCII cho nhất quán và tránh rủi ro encoding ở Console.
  - Đây là lý do trong plan có những chuỗi như `"ALB: nhan 80/443 tu internet"` — **không được "sửa cho có dấu"**.
- **Kỷ luật chi phí:** giữ `enable_nat = false` và `enable_alb = false` trong `terraform.tfvars` cho tới khi task nào cần mới bật. Sau mỗi phiên làm việc, đặt lại về `false` và `terraform apply`. NAT Gateway $0.045/h và ALB $0.0225/h tính theo giờ, không có free tier.
- **Thứ tự apply luôn tăng dần:** mỗi task thêm module block vào `infra/tf/envs/prod/main.tf` rồi `apply`. Không task nào được `destroy` resource của task trước, trừ Task 17.

---

### Task 1: Chuẩn bị — cài Terraform, đóng băng chi phí stack cũ, archive script legacy

**Files:**
- Create: `infra/legacy-cli/README.md`
- Move: `infra/setup.sh`, `infra/teardown.sh`, `infra/start.sh`, `infra/stop.sh`, `infra/config.example.json` → `infra/legacy-cli/`
- Move: `nginx/hushstore.conf`, `deploy.sh` → `infra/legacy-cli/`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: nothing.
- Produces: Terraform CLI khả dụng; profile `hushstore` đăng nhập được vào **account mới** bằng SSO; `infra/tf/` là thư mục trống sẵn sàng cho Task 2.

> **Không còn gì để migrate.** Spec ban đầu giả định stack cũ đang chạy trên account `408194747451` và đặt teardown ở bước 0. Thực tế đã kiểm tra: account đó **không còn tài nguyên nào** — không S3 bucket, không EC2, không RDS, không snapshot, không EIP/NAT/ALB/EBS/ECR. Toàn bộ đã bị xoá sau báo cáo kỳ trước, và account cũng đã hết free tier nên kỳ này dùng account khác.
>
> Hệ quả với plan: **không có bước stop/teardown/snapshot nào**, **không có `import` block cho bucket ảnh** (bucket đó không còn tồn tại), và **không cần giữ đường lùi**. Đây là greenfield thật sự. Các thay đổi tương ứng đã áp vào Task 6, Task 7 và Task 17.
>
> Hai thứ còn sót ở account cũ là 2 VPC `10.0.0.0/16` và key pair `hushstore-key` — đều **miễn phí**, dọn lúc nào cũng được (Task 17 Step tuỳ chọn).

- [ ] **Step 1: Cài Terraform và xác nhận version**

```bash
brew install terraform
terraform version
```

Expected: `Terraform v1.10.x` hoặc mới hơn. Nếu ra version < 1.10, chạy `brew upgrade terraform` — backend `use_lockfile` cần ≥ 1.10.

- [ ] **Step 2: Bật IAM Identity Center trên account mới (thao tác tay trên Console)**

Đăng nhập account mới bằng **root** — đây là một trong số ít việc chính đáng phải dùng root, vì account mới chưa có identity nào khác. Sau bước này không dùng root nữa.

1. **Bật MFA cho root trước tiên**: tên account (góc phải) → Security credentials → MFA.
2. **IAM Identity Center** → Enable. Chọn region **`ap-southeast-1`** — region này gần như không đổi được sau khi bật.
3. **Users** → Add user (username + email của bạn). Mở mail kích hoạt, đặt mật khẩu và MFA.
4. **Permission sets** → Create → Predefined `AdministratorAccess` → **Session duration: 8 hours** (mặc định 1 giờ sẽ hết hạn giữa lúc `terraform apply` tạo RDS, mất ~15 phút).
5. **AWS accounts** → chọn account → Assign users → gán user + permission set vừa tạo.
6. **Settings** → copy **AWS access portal URL**, dạng `https://d-xxxxxxxxxx.awsapps.com/start`.

> `AdministratorAccess` là rộng, và đó là lựa chọn có ý thức: Terraform phải tạo IAM role, VPC, ECS, RDS, ACM — một policy siết chặt cho chính Terraform tốn nhiều công dò hơn giá trị nó mang lại trên một account chuyên dụng. Điểm least-privilege của đề bài nằm ở **4 role workload** (Task 11), không nằm ở role của người vận hành. Trong báo cáo nên tách bạch đúng như vậy.

- [ ] **Step 3: Cấu hình profile `hushstore` trỏ vào account mới bằng SSO**

Giữ nguyên tên profile là `hushstore` để không phải sửa dòng nào trong plan — plan dùng `--profile hushstore` ở khoảng 50 chỗ, gồm cả provider block của Terraform và các file `.tftest.hcl`.

```bash
aws configure sso --profile hushstore
#   SSO session name          : hushstore
#   SSO start URL             : https://d-xxxxxxxxxx.awsapps.com/start
#   SSO region                : ap-southeast-1
#   SSO registration scopes   : (Enter để lấy mặc định)
#   → trình duyệt mở, đăng nhập, chọn account + AdministratorAccess
#   CLI default client Region : ap-southeast-1
#   CLI default output format : json

aws sso login --profile hushstore
```

- [ ] **Step 4: Xác nhận danh tính và đặt biến `$ACCT`**

```bash
aws sts get-caller-identity --profile hushstore --no-cli-pager
export ACCT=$(aws sts get-caller-identity --query Account --output text --profile hushstore)
echo "Account đang dùng: $ACCT"
```

Expected: `Arn` dạng `arn:aws:sts::<ACCT>:assumed-role/AWSReservedSSO_AdministratorAccess_xxx/<email>` — chữ `assumed-role` xác nhận đây là credential ngắn hạn từ SSO, không phải access key dài hạn. `$ACCT` **không** được là `408194747451`.

> Đặt lại `export ACCT=...` ở đầu mỗi phiên làm việc mới, cùng lúc với `aws sso login`. Nhiều lệnh verify ở các task sau dùng biến này.

- [ ] **Step 5: Archive script legacy**

```bash
mkdir -p infra/legacy-cli
git mv infra/setup.sh infra/teardown.sh infra/start.sh infra/stop.sh \
       infra/config.example.json infra/legacy-cli/
git mv nginx/hushstore.conf infra/legacy-cli/
git mv deploy.sh infra/legacy-cli/
rmdir nginx
mkdir -p infra/tf
```

- [ ] **Step 6: Viết `infra/legacy-cli/README.md`**

```markdown
# Legacy — hạ tầng dựng bằng AWS CLI (đã ngừng dùng)

Các script trong thư mục này dựng hạ tầng HushStore bằng AWS CLI imperative,
được dùng từ 2026-05 tới 2026-08. Chúng đã bị thay thế bởi stack Terraform ở
`infra/tf/`.

**Giữ lại làm gì:** đây là spec tham chiếu cho stack Terraform mới — mọi
resource trong `setup.sh` đều có bản Terraform tương ứng. Xem
`docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md` để đối
chiếu.

| File | Vai trò cũ | Thay thế bởi |
|------|-----------|--------------|
| `setup.sh` | Tạo VPC, subnet, SG, RDS, EC2, budget alert | `infra/tf/modules/{network,security,data}` |
| `teardown.sh` | Xoá toàn bộ resource | `terraform destroy` |
| `start.sh` / `stop.sh` | Bật/tắt EC2 + RDS tiết kiệm chi phí | `infra/tf/scripts/{up,down}.sh` (Phase 3) |
| `config.example.json` | Config đầu vào | `infra/tf/envs/prod/terraform.tfvars` |
| `hushstore.conf` | nginx trên host: SSL + serve WASM + proxy API | `src/Client/nginx.conf` (trong image) + ALB |
| `deploy.sh` | Deploy tay trên EC2 | `.github/workflows/deploy.yml` (Phase 2) |

**KHÔNG chạy các script này nữa** — chúng sẽ tạo resource nằm ngoài Terraform
state và gây drift.
```

- [ ] **Step 7: Thêm Terraform artifacts vào `.gitignore`**

Thêm vào cuối `.gitignore`:

```gitignore
# Terraform
**/.terraform/*
*.tfstate
*.tfstate.*
crash.log
crash.*.log
*.tfvars
!*.tfvars.example
override.tf
override.tf.json
*_override.tf
*_override.tf.json
.terraformrc
terraform.rc
.terraform.lock.hcl.bak
```

> `*.tfvars` bị ignore vì chứa `my_ip` và có thể chứa giá trị nhạy cảm. File mẫu `terraform.tfvars.example` được commit.

- [ ] **Step 8: Xác nhận không còn file legacy ở vị trí cũ**

```bash
ls infra/ && test ! -f deploy.sh && test ! -d nginx && echo "OK: legacy đã archive"
```

Expected: `infra/` chứa `legacy-cli` và `tf`; in ra `OK: legacy đã archive`.

- [ ] **Step 9: Commit**

```bash
git add -A infra nginx deploy.sh .gitignore
git commit -m "chore(infra): archive AWS CLI scripts, chuẩn bị cho Terraform stack

Stack cũ đã stop (EC2 + RDS) để ngừng phí compute nhưng chưa teardown —
giữ làm đường lùi tới khi stack Terraform mới chạy được (Task 17)."
```

---

### Task 2: Bootstrap remote state backend

**Files:**
- Create: `infra/tf/bootstrap/main.tf`
- Create: `infra/tf/bootstrap/variables.tf`
- Create: `infra/tf/bootstrap/outputs.tf`
- Create: `infra/tf/envs/prod/backend.tf`
- Create: `infra/tf/envs/prod/providers.tf`
- Create: `infra/tf/envs/prod/variables.tf`
- Create: `infra/tf/envs/prod/main.tf`
- Create: `infra/tf/envs/prod/outputs.tf`
- Create: `infra/tf/envs/prod/terraform.tfvars.example`

**Interfaces:**
- Consumes: Terraform CLI + AWS profile `hushstore` (Task 1).
- Produces: S3 bucket `hushstore-tfstate-<ACCT>` (versioned, encrypted, private); thư mục `infra/tf/envs/prod` đã `terraform init` thành công với backend S3; biến root `project`, `region`, `profile`, `azs`, `my_ip` sẵn sàng cho các module sau.

> `bootstrap/` dùng backend **local** (state của nó nằm trong file `terraform.tfstate` cạnh nó, không commit). Đây là ngoại lệ cố ý: không thể lưu state của bucket state vào chính bucket đó. Chạy một lần rồi gần như không bao giờ chạm lại.

- [ ] **Step 1: Viết `infra/tf/bootstrap/variables.tf`**

```hcl
variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
  default     = "hushstore"
}

variable "region" {
  description = "Vùng AWS"
  type        = string
  default     = "ap-southeast-1"
}

variable "profile" {
  description = "AWS CLI profile dùng để authenticate"
  type        = string
  default     = "hushstore"
}
```

- [ ] **Step 2: Viết `infra/tf/bootstrap/main.tf`**

```hcl
terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region  = var.region
  profile = var.profile

  default_tags {
    tags = {
      Project   = var.project
      ManagedBy = "terraform"
    }
  }
}

data "aws_caller_identity" "current" {}

resource "aws_s3_bucket" "tfstate" {
  bucket = "${var.project}-tfstate-${data.aws_caller_identity.current.account_id}"

  # State bucket không được xoá vô tình — phải bỏ dòng này rồi apply mới destroy được
  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_versioning" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Dọn version cũ của state để bucket không phình vô hạn
resource "aws_s3_bucket_lifecycle_configuration" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  rule {
    id     = "expire-old-state-versions"
    status = "Enabled"

    filter {}

    noncurrent_version_expiration {
      noncurrent_days = 90
    }
  }
}
```

- [ ] **Step 3: Viết `infra/tf/bootstrap/outputs.tf`**

```hcl
output "state_bucket" {
  description = "Tên bucket chứa Terraform state — dán vào envs/prod/backend.tf"
  value       = aws_s3_bucket.tfstate.id
}

output "account_id" {
  description = "ID tài khoản AWS"
  value       = data.aws_caller_identity.current.account_id
}
```

- [ ] **Step 4: Apply bootstrap**

```bash
cd infra/tf/bootstrap
terraform init
terraform apply
```

Expected: `Apply complete! Resources: 5 added`. Output in ra `state_bucket = "hushstore-tfstate-<ACCT>"` với `<ACCT>` là account ID thật.

- [ ] **Step 5: Xác nhận bucket tồn tại và đã bật versioning**

```bash
aws s3api get-bucket-versioning --bucket "hushstore-tfstate-${ACCT}" \
  --profile hushstore --no-cli-pager
```

Expected: `{"Status": "Enabled"}`.

- [ ] **Step 6: Viết `infra/tf/envs/prod/backend.tf`**

> Backend block **KHÔNG nhận biến** — mọi giá trị phải là literal. Đây là chỗ duy nhất trong plan phải điền account ID thật. Thay `<ACCT>` bằng giá trị từ output ở Step 4, hoặc sinh file bằng lệnh:
>
> ```bash
> cd infra/tf/envs/prod
> sed -i '' "s|hushstore-tfstate-<ACCT>|hushstore-tfstate-${ACCT}|" backend.tf
> grep bucket backend.tf
> ```

```hcl
terraform {
  required_version = ">= 1.10"

  backend "s3" {
    bucket       = "hushstore-tfstate-<ACCT>"
    key          = "prod/terraform.tfstate"
    region       = "ap-southeast-1"
    profile      = "hushstore"
    encrypt      = true
    use_lockfile = true
  }

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}
```

- [ ] **Step 7: Viết `infra/tf/envs/prod/providers.tf`**

```hcl
provider "aws" {
  region  = var.region
  profile = var.profile

  default_tags {
    tags = {
      Project   = var.project
      ManagedBy = "terraform"
      Env       = "prod"
    }
  }
}
```

- [ ] **Step 8: Viết `infra/tf/envs/prod/variables.tf`**

```hcl
variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
  default     = "hushstore"
}

variable "region" {
  description = "Vùng AWS"
  type        = string
  default     = "ap-southeast-1"
}

variable "profile" {
  description = "AWS CLI profile dùng để authenticate"
  type        = string
  default     = "hushstore"
}

variable "azs" {
  description = "Hai Availability Zone dùng cho toàn bộ stack"
  type        = list(string)
  default     = ["ap-southeast-1a", "ap-southeast-1b"]

  validation {
    condition     = length(var.azs) == 2
    error_message = "Phải khai báo đúng 2 AZ — ALB cần tối thiểu 2 subnet ở 2 AZ khác nhau."
  }
}

variable "my_ip" {
  description = "IP công cộng của máy tấn công (laptop), dạng CIDR /32. Lấy bằng: curl -s https://checkip.amazonaws.com"
  type        = string

  validation {
    condition     = can(cidrhost(var.my_ip, 0)) && endswith(var.my_ip, "/32")
    error_message = "my_ip phải là CIDR /32, ví dụ 203.0.113.45/32."
  }
}
```

- [ ] **Step 9: Viết `infra/tf/envs/prod/main.tf` (skeleton, module block thêm dần từ Task 3)**

```hcl
# Các module block được thêm dần theo từng task của Phase 1.
# Task 3-4: module "network"
# Task 5:   module "security"
# Task 6:   module "storage"
# Task 7:   module "data"
# Task 11:  module "ecs" (IAM roles)
# Task 12:  module "ecs" (cluster + ASG)
# Task 13:  module "ecs" (task definitions)
# Task 14:  module "alb"
# Task 15:  module "ecs" (services)

locals {
  name = var.project
}
```

- [ ] **Step 10: Viết `infra/tf/envs/prod/outputs.tf` (skeleton)**

```hcl
# Output được thêm dần theo từng task.
```

- [ ] **Step 11: Viết `infra/tf/envs/prod/terraform.tfvars.example`**

```hcl
# Copy thành terraform.tfvars rồi điền giá trị thật.
# terraform.tfvars bị .gitignore — KHÔNG commit.

# IP công cộng của laptop, dùng cho NACL deny rule demo và kiểm thử bảo mật.
# Lấy bằng: curl -s https://checkip.amazonaws.com
my_ip = "203.0.113.45/32"

# ── Toggle chi phí ──────────────────────────────────────────────
# Giữ false khi không làm việc. NAT Gateway $0.045/h, ALB $0.0225/h.
enable_nat = false
enable_alb = false

# Số EC2 container instance (0 hoặc 1). max_size cố định = 1.
instance_count = 0

# VPC Flow Logs — chỉ bật khi cần bằng chứng REJECT cho báo cáo bảo mật.
enable_flow_logs = false

# NACL deny rule theo my_ip — chỉ bật khi demo kịch bản test số 8.
enable_deny_demo = false
```

- [ ] **Step 12: Tạo `terraform.tfvars` thật và init backend**

```bash
cd infra/tf/envs/prod
cp terraform.tfvars.example terraform.tfvars
MY_IP=$(curl -s https://checkip.amazonaws.com)
sed -i '' "s|^my_ip = .*|my_ip = \"${MY_IP}/32\"|" terraform.tfvars
grep my_ip terraform.tfvars
terraform init
```

Expected: `grep` in ra IP thật của bạn. `terraform init` in `Successfully configured the backend "s3"!` và `Terraform has been successfully initialized!`.

> `terraform.tfvars` hiện có `enable_nat`, `enable_alb`, `instance_count`, `enable_flow_logs`, `enable_deny_demo` — các biến này chưa được khai báo nên `terraform plan` sẽ cảnh báo "Value for undeclared variable". Cảnh báo này bình thường, sẽ hết khi các task sau khai báo chúng.

- [ ] **Step 13: Xác nhận state đã nằm trên S3**

```bash
terraform apply -auto-approve
aws s3 ls s3://hushstore-tfstate-${ACCT}/prod/ --profile hushstore --no-cli-pager
```

Expected: `apply` báo `No changes` hoặc `0 added` (chưa có resource nào), và `s3 ls` liệt kê `terraform.tfstate`.

- [ ] **Step 14: Commit**

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): bootstrap Terraform S3 backend + skeleton envs/prod

State bucket hushstore-tfstate-<account-id> bat versioning, encryption,
block public access, lifecycle xoá version cũ sau 90 ngày. Backend dùng
use_lockfile (native S3 locking, Terraform >= 1.10) nên không cần DynamoDB."
```

---

### Task 3: Module `network` — VPC, subnet, routing, NAT Gateway, S3 endpoint, Flow Logs

**Files:**
- Create: `infra/tf/modules/network/variables.tf`
- Create: `infra/tf/modules/network/vpc.tf`
- Create: `infra/tf/modules/network/outputs.tf`
- Create: `infra/tf/modules/network/tests/vpc.tftest.hcl`
- Modify: `infra/tf/envs/prod/main.tf` (thêm `module "network"`)
- Modify: `infra/tf/envs/prod/variables.tf` (thêm `enable_nat`, `enable_flow_logs`)
- Modify: `infra/tf/envs/prod/outputs.tf`

**Interfaces:**
- Consumes: `var.project`, `var.azs`, và các biến CIDR từ root (Task 2). Module **không** nhận `var.region` — region lấy từ provider qua `data.aws_region.current.region` (chú ý: AWS provider v6 dùng `.region`, v5 dùng `.name`).
- Produces — outputs của `module.network` mà các task sau dùng:
  - `vpc_id` (string)
  - `vpc_cidr` (string)
  - `public_subnet_ids` (list(string), 2 phần tử)
  - `app_subnet_ids` (list(string), 2 phần tử)
  - `db_subnet_ids` (list(string), 2 phần tử)
  - `public_subnet_cidrs` (list(string))
  - `app_subnet_cidrs` (list(string))
  - `db_subnet_cidrs` (list(string))
  - `nat_gateway_id` (string, `""` khi `enable_nat = false`)

- [ ] **Step 1: Viết test trước — `infra/tf/modules/network/tests/vpc.tftest.hcl`**

```hcl
provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project             = "hushstore-tftest"
  vpc_cidr            = "10.20.0.0/16"
  azs                 = ["ap-southeast-1a", "ap-southeast-1b"]
  public_subnet_cidrs = ["10.20.0.0/24", "10.20.1.0/24"]
  app_subnet_cidrs    = ["10.20.10.0/24", "10.20.11.0/24"]
  db_subnet_cidrs     = ["10.20.20.0/24", "10.20.21.0/24"]
  my_ip               = "203.0.113.45/32"
  enable_nat          = false
  enable_flow_logs    = false
  enable_deny_demo    = false
}

run "sau_subnet_dung_3_tier_2_az" {
  command = plan

  assert {
    condition     = length(aws_subnet.public) == 2
    error_message = "Phải có đúng 2 public subnet — ALB cần 2 AZ."
  }

  assert {
    condition     = length(aws_subnet.app) == 2
    error_message = "Phải có đúng 2 app subnet."
  }

  assert {
    condition     = length(aws_subnet.db) == 2
    error_message = "Phải có đúng 2 db subnet — DB subnet group cần 2 AZ."
  }
}

run "app_va_db_subnet_khong_tu_gan_public_ip" {
  command = plan

  assert {
    condition = alltrue([
      for s in aws_subnet.app : s.map_public_ip_on_launch == false
    ])
    error_message = "App subnet KHÔNG được tự gán public IP — EC2 phải nằm sau ALB."
  }

  assert {
    condition = alltrue([
      for s in aws_subnet.db : s.map_public_ip_on_launch == false
    ])
    error_message = "DB subnet KHÔNG được tự gán public IP."
  }
}

run "private_route_table_khong_co_route_ra_igw" {
  command = plan

  assert {
    condition     = length(aws_route.private_nat) == 0
    error_message = "Khi enable_nat = false thì private route table không được có route 0.0.0.0/0."
  }

  assert {
    condition     = length(aws_nat_gateway.this) == 0
    error_message = "Khi enable_nat = false thì không được tạo NAT Gateway (tốn $0.045/giờ)."
  }
}

run "flow_logs_tat_theo_default" {
  command = plan

  assert {
    condition     = length(aws_flow_log.vpc) == 0
    error_message = "VPC Flow Logs phải tắt theo default để không tốn phí ingest."
  }
}

run "bat_nat_thi_tao_dung_1_nat_gateway_va_1_route" {
  command = plan

  variables {
    enable_nat = true
  }

  assert {
    condition     = length(aws_nat_gateway.this) == 1
    error_message = "Chỉ tạo 1 NAT Gateway cho cả 2 AZ — max_size = 1 nên không cần NAT per-AZ."
  }

  assert {
    condition     = length(aws_route.private_nat) == 1
    error_message = "Phải có đúng 1 route 0.0.0.0/0 trỏ vào NAT Gateway."
  }

  assert {
    condition     = aws_route.private_nat[0].destination_cidr_block == "0.0.0.0/0"
    error_message = "Route qua NAT phải là 0.0.0.0/0."
  }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/network
terraform init
terraform test
```

Expected: FAIL với lỗi kiểu `Reference to undeclared resource` / `No configuration files` — module chưa có resource nào.

- [ ] **Step 3: Viết `infra/tf/modules/network/variables.tf`**

```hcl
variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "vpc_cidr" {
  description = "CIDR của VPC"
  type        = string
}

variable "azs" {
  description = "Hai Availability Zone dùng cho stack"
  type        = list(string)
}

variable "public_subnet_cidrs" {
  description = "CIDR của 2 public subnet — chứa ALB và NAT Gateway"
  type        = list(string)
}

variable "app_subnet_cidrs" {
  description = "CIDR của 2 app subnet — chứa ECS container instance, không public IP"
  type        = list(string)
}

variable "db_subnet_cidrs" {
  description = "CIDR của 2 db subnet — chứa RDS, isolated"
  type        = list(string)
}

variable "my_ip" {
  description = "IP laptop dạng /32, dùng cho NACL deny rule demo"
  type        = string
}

variable "enable_nat" {
  description = "Tạo NAT Gateway. $0.045/giờ + $0.045/GB — chỉ bật khi cần egress (pull ECR, SSM)"
  type        = bool
  default     = false
}

variable "enable_flow_logs" {
  description = "Bật VPC Flow Logs (chỉ log REJECT) để thu bằng chứng cho báo cáo bảo mật"
  type        = bool
  default     = false
}

variable "flow_log_retention_days" {
  description = "Số ngày giữ Flow Logs trong CloudWatch"
  type        = number
  default     = 1
}

variable "enable_deny_demo" {
  description = "Bật NACL rule 50 DENY toàn bộ traffic từ my_ip — dùng cho kịch bản kiểm thử số 8"
  type        = bool
  default     = false
}
```

- [ ] **Step 4: Viết `infra/tf/modules/network/vpc.tf`**

```hcl
# ─── VPC ─────────────────────────────────────────────────────────
resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = { Name = "${var.project}-vpc" }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-igw" }
}

# ─── SUBNETS — 3 tier × 2 AZ ─────────────────────────────────────
resource "aws_subnet" "public" {
  count = length(var.public_subnet_cidrs)

  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.public_subnet_cidrs[count.index]
  availability_zone       = var.azs[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name = "${var.project}-public-${substr(var.azs[count.index], -1, 1)}"
    Tier = "public"
  }
}

resource "aws_subnet" "app" {
  count = length(var.app_subnet_cidrs)

  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.app_subnet_cidrs[count.index]
  availability_zone       = var.azs[count.index]
  map_public_ip_on_launch = false

  tags = {
    Name = "${var.project}-app-${substr(var.azs[count.index], -1, 1)}"
    Tier = "app"
  }
}

resource "aws_subnet" "db" {
  count = length(var.db_subnet_cidrs)

  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.db_subnet_cidrs[count.index]
  availability_zone       = var.azs[count.index]
  map_public_ip_on_launch = false

  tags = {
    Name = "${var.project}-db-${substr(var.azs[count.index], -1, 1)}"
    Tier = "db"
  }
}

# ─── NAT GATEWAY (toggle) ────────────────────────────────────────
resource "aws_eip" "nat" {
  count = var.enable_nat ? 1 : 0

  domain = "vpc"

  tags = { Name = "${var.project}-nat-eip" }
}

resource "aws_nat_gateway" "this" {
  count = var.enable_nat ? 1 : 0

  allocation_id = aws_eip.nat[0].id
  subnet_id     = aws_subnet.public[0].id

  tags = { Name = "${var.project}-natgw" }

  depends_on = [aws_internet_gateway.this]
}

# ─── ROUTE TABLES ────────────────────────────────────────────────
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-rt-public" }
}

resource "aws_route" "public_igw" {
  route_table_id         = aws_route_table.public.id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.this.id
}

resource "aws_route_table_association" "public" {
  count = length(aws_subnet.public)

  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

# App tier: route 0.0.0.0/0 chỉ tồn tại khi NAT bật.
# Khi NAT tắt, app tier hoàn toàn không có đường ra internet.
resource "aws_route_table" "private" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-rt-private" }
}

resource "aws_route" "private_nat" {
  count = var.enable_nat ? 1 : 0

  route_table_id         = aws_route_table.private.id
  destination_cidr_block = "0.0.0.0/0"
  nat_gateway_id         = aws_nat_gateway.this[0].id
}

resource "aws_route_table_association" "app" {
  count = length(aws_subnet.app)

  subnet_id      = aws_subnet.app[count.index].id
  route_table_id = aws_route_table.private.id
}

# DB tier: route table riêng, KHÔNG BAO GIỜ có route ra internet.
resource "aws_route_table" "db" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-rt-db" }
}

resource "aws_route_table_association" "db" {
  count = length(aws_subnet.db)

  subnet_id      = aws_subnet.db[count.index].id
  route_table_id = aws_route_table.db.id
}

# ─── S3 GATEWAY ENDPOINT ─────────────────────────────────────────
# Miễn phí. Traffic S3 không đi qua NAT nên không bị tính $0.045/GB,
# và vẫn hoạt động khi NAT đã destroy.
resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.this.id
  service_name      = "com.amazonaws.${data.aws_region.current.region}.s3"
  vpc_endpoint_type = "Gateway"

  route_table_ids = [
    aws_route_table.private.id,
    aws_route_table.db.id,
  ]

  tags = { Name = "${var.project}-s3-endpoint" }
}

data "aws_region" "current" {}

# ─── VPC FLOW LOGS (toggle, default tắt) ─────────────────────────
resource "aws_cloudwatch_log_group" "flow" {
  count = var.enable_flow_logs ? 1 : 0

  name              = "/vpc/${var.project}/flowlogs"
  retention_in_days = var.flow_log_retention_days
}

data "aws_iam_policy_document" "flow_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["vpc-flow-logs.amazonaws.com"]
    }
  }
}

data "aws_iam_policy_document" "flow_write" {
  statement {
    effect = "Allow"

    actions = [
      "logs:CreateLogStream",
      "logs:PutLogEvents",
      "logs:DescribeLogGroups",
      "logs:DescribeLogStreams",
    ]

    resources = ["arn:aws:logs:*:*:log-group:/vpc/${var.project}/flowlogs:*"]
  }
}

resource "aws_iam_role" "flow" {
  count = var.enable_flow_logs ? 1 : 0

  name               = "${var.project}-flowlogs-role"
  assume_role_policy = data.aws_iam_policy_document.flow_assume.json
}

resource "aws_iam_role_policy" "flow" {
  count = var.enable_flow_logs ? 1 : 0

  name   = "${var.project}-flowlogs-write"
  role   = aws_iam_role.flow[0].id
  policy = data.aws_iam_policy_document.flow_write.json
}

resource "aws_flow_log" "vpc" {
  count = var.enable_flow_logs ? 1 : 0

  vpc_id               = aws_vpc.this.id
  traffic_type         = "REJECT"
  iam_role_arn         = aws_iam_role.flow[0].arn
  log_destination      = aws_cloudwatch_log_group.flow[0].arn
  log_destination_type = "cloud-watch-logs"

  tags = { Name = "${var.project}-flowlogs-reject" }
}
```

- [ ] **Step 5: Viết `infra/tf/modules/network/outputs.tf`**

```hcl
output "vpc_id" {
  description = "ID của VPC"
  value       = aws_vpc.this.id
}

output "vpc_cidr" {
  description = "CIDR của VPC"
  value       = aws_vpc.this.cidr_block
}

output "public_subnet_ids" {
  description = "ID của 2 public subnet — dùng cho ALB và NAT Gateway"
  value       = aws_subnet.public[*].id
}

output "app_subnet_ids" {
  description = "ID của 2 app subnet — dùng cho ASG"
  value       = aws_subnet.app[*].id
}

output "db_subnet_ids" {
  description = "ID của 2 db subnet — dùng cho DB subnet group"
  value       = aws_subnet.db[*].id
}

output "public_subnet_cidrs" {
  description = "CIDR của public tier — dùng làm source trong NACL app"
  value       = aws_subnet.public[*].cidr_block
}

output "app_subnet_cidrs" {
  description = "CIDR của app tier — dùng làm source trong NACL db"
  value       = aws_subnet.app[*].cidr_block
}

output "db_subnet_cidrs" {
  description = "CIDR của db tier — dùng làm destination trong NACL app"
  value       = aws_subnet.db[*].cidr_block
}

output "nat_gateway_id" {
  description = "ID của NAT Gateway, rỗng khi enable_nat = false"
  value       = var.enable_nat ? aws_nat_gateway.this[0].id : ""
}
```

- [ ] **Step 6: Chạy test để xác nhận nó pass**

```bash
cd infra/tf/modules/network
terraform init
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `terraform test` in `5 passed, 0 failed.` (5 run block, trong đó block cuối override `enable_nat = true`). `fmt -check` không in gì. `validate` in `Success!`.

- [ ] **Step 7: Thêm biến toggle vào `infra/tf/envs/prod/variables.tf`**

Thêm vào cuối file:

```hcl
variable "vpc_cidr" {
  description = "CIDR của VPC — 10.20.0.0/16, KHÔNG trùng 10.0.0.0/16 của stack cũ"
  type        = string
  default     = "10.20.0.0/16"
}

variable "public_subnet_cidrs" {
  description = "CIDR của 2 public subnet"
  type        = list(string)
  default     = ["10.20.0.0/24", "10.20.1.0/24"]
}

variable "app_subnet_cidrs" {
  description = "CIDR của 2 app subnet"
  type        = list(string)
  default     = ["10.20.10.0/24", "10.20.11.0/24"]
}

variable "db_subnet_cidrs" {
  description = "CIDR của 2 db subnet"
  type        = list(string)
  default     = ["10.20.20.0/24", "10.20.21.0/24"]
}

variable "enable_nat" {
  description = "Tạo NAT Gateway — $0.045/giờ. Chỉ bật khi cần pull ECR hoặc dùng SSM"
  type        = bool
  default     = false
}

variable "enable_flow_logs" {
  description = "Bật VPC Flow Logs (chỉ REJECT) cho báo cáo bảo mật"
  type        = bool
  default     = false
}

variable "enable_deny_demo" {
  description = "Bật NACL rule 50 DENY my_ip — kịch bản kiểm thử số 8"
  type        = bool
  default     = false
}
```

- [ ] **Step 8: Thêm `module "network"` vào `infra/tf/envs/prod/main.tf`**

Thêm sau block `locals`:

```hcl
module "network" {
  source = "../../modules/network"

  project             = local.name
  vpc_cidr            = var.vpc_cidr
  azs                 = var.azs
  public_subnet_cidrs = var.public_subnet_cidrs
  app_subnet_cidrs    = var.app_subnet_cidrs
  db_subnet_cidrs     = var.db_subnet_cidrs
  my_ip               = var.my_ip
  enable_nat          = var.enable_nat
  enable_flow_logs    = var.enable_flow_logs
  enable_deny_demo    = var.enable_deny_demo
}
```

- [ ] **Step 9: Thêm output vào `infra/tf/envs/prod/outputs.tf`**

```hcl
output "vpc_id" {
  description = "ID của VPC"
  value       = module.network.vpc_id
}

output "app_subnet_ids" {
  description = "ID của app subnet — nơi EC2 container instance chạy"
  value       = module.network.app_subnet_ids
}
```

- [ ] **Step 10: Apply và xác nhận topology thật trên AWS**

```bash
cd infra/tf/envs/prod
terraform init
terraform apply
```

Expected: `Apply complete! Resources: 17 added` (1 vpc, 1 igw, 6 subnet, 3 route table, 1 route, 6 association... con số có thể lệch vài đơn vị, quan trọng là không có lỗi và không có NAT Gateway).

- [ ] **Step 11: Xác nhận app tier không có đường ra internet khi NAT tắt**

```bash
VPC_ID=$(terraform output -raw vpc_id)
aws ec2 describe-route-tables \
  --filters "Name=vpc-id,Values=$VPC_ID" "Name=tag:Name,Values=hushstore-rt-private" \
  --query 'RouteTables[0].Routes[].DestinationCidrBlock' --output text \
  --profile hushstore --no-cli-pager
```

Expected: chỉ in `10.20.0.0/16` (route local) — **không có** `0.0.0.0/0`. Đây là bằng chứng app tier bị cô lập khi NAT tắt.

- [ ] **Step 12: Xác nhận S3 Gateway Endpoint đã gắn vào private và db route table**

```bash
aws ec2 describe-vpc-endpoints \
  --filters "Name=vpc-id,Values=$VPC_ID" \
  --query 'VpcEndpoints[0].{Service:ServiceName,Type:VpcEndpointType,RouteTables:RouteTableIds}' \
  --profile hushstore --no-cli-pager
```

Expected: `Service` chứa `.s3`, `Type` là `Gateway`, `RouteTables` có 2 phần tử.

- [ ] **Step 13: Commit**

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): module network — VPC 3 tier x 2 AZ, NAT Gateway toggle, S3 endpoint

VPC 10.20.0.0/16 chia public/app/db, mỗi tier 2 AZ. App và db tier không tự
gán public IP. Route 0.0.0.0/0 của app tier chỉ tồn tại khi enable_nat = true;
db tier có route table riêng không bao giờ ra internet. S3 Gateway Endpoint
(miễn phí) gắn vào cả private và db route table nên S3 vẫn dùng được khi NAT
đã destroy. Flow Logs default tắt.

6 terraform test assertion phủ: số subnet mỗi tier, map_public_ip_on_launch,
không có NAT/route khi toggle tắt, đúng 1 NAT khi toggle bật."
```

---

### Task 4: Module `network` — Network ACL (deliverable trọng tâm của đề bài)

**Files:**
- Create: `infra/tf/modules/network/nacl.tf`
- Create: `infra/tf/modules/network/tests/nacl.tftest.hcl`
- Modify: `infra/tf/modules/network/outputs.tf` (thêm output ID của 3 NACL)

**Interfaces:**
- Consumes: `aws_vpc.this`, `aws_subnet.{public,app,db}` từ Task 3; `var.my_ip`, `var.enable_deny_demo`.
- Produces: outputs `nacl_public_id`, `nacl_app_id`, `nacl_db_id` (string) — dùng để verify bằng AWS CLI ở Task 16 và kiểm thử ở Phase 3.

> **Đây là phần kỹ thuật quan trọng nhất của đề bài.** NACL là **stateless**: mỗi chiều phải khai báo cả traffic đi và traffic trả về. Rule số nhỏ hơn được xét trước. Hai điểm phải hiểu đúng khi review task này:
>
> 1. Rule allow dải ephemeral `1024-65535` là **bắt buộc** ở inbound của app tier, vì return traffic từ internet qua NAT Gateway vào subnet với source `0.0.0.0/0` và destination port ephemeral.
> 2. Nhưng `1433` và `8080` **đều nằm trong dải `1024-65535`** — nên rule đó vô tình mở chúng ra internet ở tầng NACL. Vì vậy phải đặt rule **DENY 1433 (số 95)** và **DENY 8080 (số 115)** ở số nhỏ hơn để chặn trước khi rule 120 được xét. Đây chính là minh hoạ cho thứ tự rule + tính stateless của NACL, và là lý do vẫn cần Security Group làm lớp thứ hai.

- [ ] **Step 1: Viết test trước — `infra/tf/modules/network/tests/nacl.tftest.hcl`**

```hcl
provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project             = "hushstore-tftest"
  vpc_cidr            = "10.20.0.0/16"
  azs                 = ["ap-southeast-1a", "ap-southeast-1b"]
  public_subnet_cidrs = ["10.20.0.0/24", "10.20.1.0/24"]
  app_subnet_cidrs    = ["10.20.10.0/24", "10.20.11.0/24"]
  db_subnet_cidrs     = ["10.20.20.0/24", "10.20.21.0/24"]
  my_ip               = "203.0.113.45/32"
  enable_nat          = false
  enable_flow_logs    = false
  enable_deny_demo    = false
}

# KHÔNG assert `aws_network_acl.x.vpc_id == aws_vpc.this.id`: ở `command = plan`
# cả hai `.id` đều unknown, Terraform không so sánh được unknown với unknown và
# sẽ báo lỗi, làm hỏng cả file test. Thay bằng đếm số rule — vừa đánh giá được ở
# plan-time (độ dài map lấy từ locals), vừa kiểm tra đúng tính chất mà đề bài
# chấm: rule mở ở mức tối thiểu.
run "so_luong_rule_dung_muc_toi_thieu" {
  command = plan

  assert {
    condition = alltrue([
      length(aws_network_acl_rule.public_ingress) == 3,
      length(aws_network_acl_rule.public_egress) == 5,
    ])
    error_message = "nacl-public phải có đúng 3 rule inbound và 5 rule outbound — thêm rule nào là vi phạm nguyên tắc tối thiểu."
  }

  assert {
    condition = alltrue([
      length(aws_network_acl_rule.app_ingress) == 6,
      length(aws_network_acl_rule.app_egress) == 4,
    ])
    error_message = "nacl-app phải có đúng 6 rule inbound (90, 95, 100, 110, 115, 120) và 4 rule outbound."
  }

  assert {
    condition = alltrue([
      length(aws_network_acl_rule.db_ingress) == 1,
      length(aws_network_acl_rule.db_egress) == 1,
    ])
    error_message = "nacl-db phải có đúng 1 rule mỗi chiều — đây là tier chặt nhất của thiết kế."
  }
}

run "nacl_app_chan_port_22_truoc_rule_ephemeral" {
  command = plan

  assert {
    condition     = aws_network_acl_rule.app_ingress["90"].rule_action == "deny"
    error_message = "Rule 90 của nacl-app phải là DENY."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["90"].from_port == 22,
      aws_network_acl_rule.app_ingress["90"].to_port == 22,
      aws_network_acl_rule.app_ingress["90"].cidr_block == "0.0.0.0/0",
    ])
    error_message = "Rule 90 phải DENY port 22 từ 0.0.0.0/0 — không ai được SSH vào app tier."
  }
}

run "nacl_app_chan_1433_va_8080_truoc_rule_ephemeral_120" {
  command = plan

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["95"].rule_action == "deny",
      aws_network_acl_rule.app_ingress["95"].from_port == 1433,
      aws_network_acl_rule.app_ingress["95"].cidr_block == "0.0.0.0/0",
    ])
    error_message = "Rule 95 phải DENY 1433 từ 0.0.0.0/0 — vì rule 120 allow 1024-65535 sẽ vô tình mở nó."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["115"].rule_action == "deny",
      aws_network_acl_rule.app_ingress["115"].from_port == 8080,
      aws_network_acl_rule.app_ingress["115"].cidr_block == "0.0.0.0/0",
    ])
    error_message = "Rule 115 phải DENY 8080 từ 0.0.0.0/0 — vì rule 120 allow 1024-65535 sẽ vô tình mở nó."
  }

  # Đây là assertion cốt lõi: DENY phải có số NHỎ HƠN rule allow ephemeral,
  # nếu không NACL sẽ xét rule allow trước và hai port kia lọt ra internet.
  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["95"].rule_number < aws_network_acl_rule.app_ingress["120"].rule_number,
      aws_network_acl_rule.app_ingress["115"].rule_number < aws_network_acl_rule.app_ingress["120"].rule_number,
    ])
    error_message = "Rule DENY 1433 và 8080 phải có số nhỏ hơn rule 120 allow 1024-65535, nếu không sẽ bị bỏ qua."
  }
}

run "nacl_app_chi_nhan_80_va_8080_tu_public_tier" {
  command = plan

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["100"].rule_action == "allow",
      aws_network_acl_rule.app_ingress["100"].from_port == 80,
      aws_network_acl_rule.app_ingress["100"].cidr_block == "10.20.0.0/23",
    ])
    error_message = "Rule 100 phải allow 80 chỉ từ CIDR gộp của public tier, không phải 0.0.0.0/0."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.app_ingress["110"].rule_action == "allow",
      aws_network_acl_rule.app_ingress["110"].from_port == 8080,
      aws_network_acl_rule.app_ingress["110"].cidr_block == "10.20.0.0/23",
    ])
    error_message = "Rule 110 phải allow 8080 chỉ từ CIDR gộp của public tier."
  }
}

run "nacl_db_chi_co_dung_mot_rule_allow_1433" {
  command = plan

  assert {
    condition     = length(aws_network_acl_rule.db_ingress) == 1
    error_message = "NACL db inbound phải có ĐÚNG 1 rule — chỉ 1433 từ app tier. Thêm rule nào là vi phạm nguyên tắc tối thiểu."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.db_ingress["100"].rule_action == "allow",
      aws_network_acl_rule.db_ingress["100"].from_port == 1433,
      aws_network_acl_rule.db_ingress["100"].to_port == 1433,
      aws_network_acl_rule.db_ingress["100"].cidr_block == "10.20.10.0/23",
    ])
    error_message = "Rule duy nhất của NACL db phải là allow 1433 từ CIDR gộp của app tier."
  }

  assert {
    condition     = length(aws_network_acl_rule.db_egress) == 1
    error_message = "NACL db outbound phải có ĐÚNG 1 rule — ephemeral về app tier."
  }

  assert {
    condition     = aws_network_acl_rule.db_egress["100"].cidr_block == "10.20.10.0/23"
    error_message = "NACL db outbound chỉ được trả traffic về app tier, không ra internet."
  }
}

run "deny_demo_tat_theo_default" {
  command = plan

  assert {
    condition     = length(aws_network_acl_rule.public_deny_demo) == 0
    error_message = "Rule DENY theo my_ip phải tắt theo default, chỉ bật khi demo kịch bản 8."
  }
}

run "bat_deny_demo_thi_chan_my_ip_o_rule_50" {
  command = plan

  variables {
    enable_deny_demo = true
  }

  assert {
    condition     = length(aws_network_acl_rule.public_deny_demo) == 1
    error_message = "Khi enable_deny_demo = true phải tạo đúng 1 rule DENY."
  }

  assert {
    condition = alltrue([
      aws_network_acl_rule.public_deny_demo[0].rule_number == 50,
      aws_network_acl_rule.public_deny_demo[0].rule_action == "deny",
      aws_network_acl_rule.public_deny_demo[0].cidr_block == "203.0.113.45/32",
      aws_network_acl_rule.public_deny_demo[0].protocol == "-1",
    ])
    error_message = "Rule demo phải là số 50, DENY mọi protocol từ my_ip — số nhỏ hơn rule 100/110 allow 80/443."
  }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/network
terraform test -filter=tests/nacl.tftest.hcl
```

Expected: FAIL với `Reference to undeclared resource "aws_network_acl"` — chưa có `nacl.tf`.

- [ ] **Step 3: Viết `infra/tf/modules/network/nacl.tf`**

> Ba `locals` list dưới đây đọc y hệt bảng NACL trong spec — đó là chủ ý, để đối chiếu code với tài liệu bằng mắt. Mỗi tier gộp 2 subnet CIDR thành 1 CIDR `/23` để rule ngắn gọn (`10.20.0.0/24` + `10.20.1.0/24` = `10.20.0.0/23`).

```hcl
locals {
  # Gộp 2 subnet /24 liền kề của mỗi tier thành 1 CIDR /23.
  # cidrsubnet không làm được việc này nên tính bằng cách bỏ 1 bit netmask.
  public_tier_cidr = cidrsubnet(var.vpc_cidr, 7, 0)  # 10.20.0.0/23
  app_tier_cidr    = cidrsubnet(var.vpc_cidr, 7, 5)  # 10.20.10.0/23
  db_tier_cidr     = cidrsubnet(var.vpc_cidr, 7, 10) # 10.20.20.0/23

  # ── nacl-public: chứa ALB + NAT Gateway ────────────────────────
  public_ingress = [
    { no = 100, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 80, to = 80 },
    { no = 110, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 443, to = 443 },
    # Return traffic: response của ALB về client, và response từ internet
    # về NAT Gateway. Cả hai đều tới port ephemeral.
    { no = 120, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 1024, to = 65535 },
  ]

  public_egress = [
    { no = 100, proto = "tcp", action = "allow", cidr = local.app_tier_cidr, from = 80, to = 80 },
    { no = 110, proto = "tcp", action = "allow", cidr = local.app_tier_cidr, from = 8080, to = 8080 },
    # Egress qua NAT ra internet: ECR, SSM, yum
    { no = 120, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 80, to = 80 },
    { no = 125, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 443, to = 443 },
    # Response về client, và response từ NAT về app tier
    { no = 130, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 1024, to = 65535 },
  ]

  # ── nacl-app: chứa ECS container instance ──────────────────────
  # THỨ TỰ RULE Ở ĐÂY LÀ ĐIỂM KỸ THUẬT CỐT LÕI — xem ghi chú ở đầu task.
  app_ingress = [
    # 90/95/115: DENY phải đứng trước rule 120
    { no = 90, proto = "tcp", action = "deny", cidr = "0.0.0.0/0", from = 22, to = 22 },
    { no = 95, proto = "tcp", action = "deny", cidr = "0.0.0.0/0", from = 1433, to = 1433 },
    { no = 100, proto = "tcp", action = "allow", cidr = local.public_tier_cidr, from = 80, to = 80 },
    { no = 110, proto = "tcp", action = "allow", cidr = local.public_tier_cidr, from = 8080, to = 8080 },
    { no = 115, proto = "tcp", action = "deny", cidr = "0.0.0.0/0", from = 8080, to = 8080 },
    # Bắt buộc: return traffic từ internet qua NAT Gateway vào app tier
    # có source 0.0.0.0/0 và destination port ephemeral.
    { no = 120, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 1024, to = 65535 },
  ]

  app_egress = [
    { no = 100, proto = "tcp", action = "allow", cidr = local.db_tier_cidr, from = 1433, to = 1433 },
    { no = 110, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 80, to = 80 },
    { no = 115, proto = "tcp", action = "allow", cidr = "0.0.0.0/0", from = 443, to = 443 },
    # Response về ALB. Chỉ tới public tier, KHÔNG mở ra 0.0.0.0/0.
    { no = 120, proto = "tcp", action = "allow", cidr = local.public_tier_cidr, from = 1024, to = 65535 },
  ]

  # ── nacl-db: chặt nhất, đúng 1 rule mỗi chiều ──────────────────
  db_ingress = [
    { no = 100, proto = "tcp", action = "allow", cidr = local.app_tier_cidr, from = 1433, to = 1433 },
  ]

  db_egress = [
    { no = 100, proto = "tcp", action = "allow", cidr = local.app_tier_cidr, from = 1024, to = 65535 },
  ]
}

# ─── NACL PUBLIC ─────────────────────────────────────────────────
resource "aws_network_acl" "public" {
  vpc_id     = aws_vpc.this.id
  subnet_ids = aws_subnet.public[*].id

  tags = { Name = "${var.project}-nacl-public" }
}

resource "aws_network_acl_rule" "public_ingress" {
  for_each = { for r in local.public_ingress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.public.id
  rule_number    = each.value.no
  egress         = false
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

resource "aws_network_acl_rule" "public_egress" {
  for_each = { for r in local.public_egress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.public.id
  rule_number    = each.value.no
  egress         = true
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

# Rule demo: DENY toàn bộ traffic từ my_ip ở số 50 — nhỏ hơn 100/110 nên
# được xét trước. Chứng minh NACL làm được điều Security Group không làm
# được: SG chỉ có allow-list, không thể chặn riêng một IP.
resource "aws_network_acl_rule" "public_deny_demo" {
  count = var.enable_deny_demo ? 1 : 0

  network_acl_id = aws_network_acl.public.id
  rule_number    = 50
  egress         = false
  protocol       = "-1"
  rule_action    = "deny"
  cidr_block     = var.my_ip
}

# ─── NACL APP ────────────────────────────────────────────────────
resource "aws_network_acl" "app" {
  vpc_id     = aws_vpc.this.id
  subnet_ids = aws_subnet.app[*].id

  tags = { Name = "${var.project}-nacl-app" }
}

resource "aws_network_acl_rule" "app_ingress" {
  for_each = { for r in local.app_ingress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.app.id
  rule_number    = each.value.no
  egress         = false
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

resource "aws_network_acl_rule" "app_egress" {
  for_each = { for r in local.app_egress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.app.id
  rule_number    = each.value.no
  egress         = true
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

# ─── NACL DB ─────────────────────────────────────────────────────
resource "aws_network_acl" "db" {
  vpc_id     = aws_vpc.this.id
  subnet_ids = aws_subnet.db[*].id

  tags = { Name = "${var.project}-nacl-db" }
}

resource "aws_network_acl_rule" "db_ingress" {
  for_each = { for r in local.db_ingress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.db.id
  rule_number    = each.value.no
  egress         = false
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}

resource "aws_network_acl_rule" "db_egress" {
  for_each = { for r in local.db_egress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.db.id
  rule_number    = each.value.no
  egress         = true
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
}
```

- [ ] **Step 4: Thêm output NACL vào `infra/tf/modules/network/outputs.tf`**

Thêm vào cuối file:

```hcl
output "nacl_public_id" {
  description = "ID của NACL public tier"
  value       = aws_network_acl.public.id
}

output "nacl_app_id" {
  description = "ID của NACL app tier"
  value       = aws_network_acl.app.id
}

output "nacl_db_id" {
  description = "ID của NACL db tier"
  value       = aws_network_acl.db.id
}

output "public_tier_cidr" {
  description = "CIDR /23 gộp của public tier — dùng trong rule NACL"
  value       = local.public_tier_cidr
}

output "app_tier_cidr" {
  description = "CIDR /23 gộp của app tier"
  value       = local.app_tier_cidr
}

output "db_tier_cidr" {
  description = "CIDR /23 gộp của db tier"
  value       = local.db_tier_cidr
}
```

- [ ] **Step 5: Xác nhận CIDR gộp tính đúng trước khi chạy test**

```bash
cd infra/tf/modules/network
terraform console <<'EOF'
cidrsubnet("10.20.0.0/16", 7, 0)
cidrsubnet("10.20.0.0/16", 7, 5)
cidrsubnet("10.20.0.0/16", 7, 10)
EOF
```

Expected: lần lượt `"10.20.0.0/23"`, `"10.20.10.0/23"`, `"10.20.20.0/23"`. Nếu ra khác, sửa index trong `locals` cho khớp — 3 giá trị này phải bao đúng cặp subnet của từng tier và không chồng lấn nhau.

- [ ] **Step 6: Chạy toàn bộ test của module**

```bash
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `terraform test` in `12 passed, 0 failed.` (5 run của `vpc.tftest.hcl` + 7 run của `nacl.tftest.hcl`).

- [ ] **Step 7: Apply và xác nhận NACL thật trên AWS**

```bash
cd ../../envs/prod
terraform apply
```

Expected: `Apply complete!` với 3 `aws_network_acl` + 20 `aws_network_acl_rule` added (3+5 public, 6+4 app, 1+1 db).

- [ ] **Step 8: Verify rule của NACL app đúng thứ tự trên AWS thật**

```bash
VPC_ID=$(terraform output -raw vpc_id)
aws ec2 describe-network-acls \
  --filters "Name=vpc-id,Values=$VPC_ID" "Name=tag:Name,Values=hushstore-nacl-app" \
  --query 'NetworkAcls[0].Entries[?Egress==`false`].[RuleNumber,RuleAction,Protocol,PortRange.From,PortRange.To,CidrBlock]' \
  --output table --profile hushstore --no-cli-pager
```

Expected: bảng có đúng thứ tự rule number tăng dần `90 deny 22`, `95 deny 1433`, `100 allow 80`, `110 allow 8080`, `115 deny 8080`, `120 allow 1024-65535`, `32767 deny all`. Xác nhận bằng mắt rằng **các rule deny 95 và 115 đứng TRƯỚC rule 120**.

- [ ] **Step 9: Verify NACL db chỉ có đúng 1 rule allow mỗi chiều**

```bash
aws ec2 describe-network-acls \
  --filters "Name=vpc-id,Values=$VPC_ID" "Name=tag:Name,Values=hushstore-nacl-db" \
  --query 'NetworkAcls[0].Entries[?RuleAction==`allow`].[RuleNumber,Egress,PortRange.From,PortRange.To,CidrBlock]' \
  --output table --profile hushstore --no-cli-pager
```

Expected: đúng 2 dòng — inbound `100 False 1433 1433 10.20.10.0/23` và outbound `100 True 1024 65535 10.20.10.0/23`. Không có dòng nào khác.

- [ ] **Step 10: Commit**

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): 3 Network ACL theo nguyên tắc tối thiểu

nacl-public: allow 80/443 + ephemeral return; rule 50 DENY my_ip (toggle) để
demo NACL làm được điều SG không làm được.

nacl-app: rule 90 DENY 22, rule 95 DENY 1433, rule 115 DENY 8080 — tất cả đặt
trước rule 120 allow 1024-65535. Rule 120 là bắt buộc cho return traffic qua
NAT Gateway, nhưng 1433 và 8080 nằm trong dải đó nên phải chặn trước bằng rule
số nhỏ hơn. Đây là minh hoạ thứ tự rule + tính stateless của NACL.

nacl-db: đúng 1 rule mỗi chiều — 1433 từ app tier, ephemeral về app tier.

7 terraform test assertion, trong đó có assertion so sánh rule_number để bảo
đảm DENY luôn nhỏ hơn allow ephemeral."
```

---

### Task 5: Module `security` — 3 Security Group, không có rule port 22 nào

**Files:**
- Create: `infra/tf/modules/security/versions.tf`
- Create: `infra/tf/modules/network/versions.tf` (bù cho Task 3 — cùng dạng file, làm gộp ở đây)
- Create: `infra/tf/modules/security/variables.tf`
- Create: `infra/tf/modules/security/main.tf`
- Create: `infra/tf/modules/security/outputs.tf`
- Create: `infra/tf/modules/security/tests/sg.tftest.hcl`
- Modify: `infra/tf/envs/prod/main.tf` (thêm `module "security"`)

**Interfaces:**
- Consumes: `module.network.vpc_id`.
- Produces — outputs của `module.security`:
  - `alb_sg_id` (string) — dùng ở Task 14 cho ALB
  - `web_sg_id` (string) — dùng ở Task 12 cho launch template
  - `rds_sg_id` (string) — dùng ở Task 7 cho RDS

> **Điểm thiết kế:** mọi rule dùng `referenced_security_group_id` chứ không dùng CIDR, trừ chỗ buộc phải mở ra internet (ALB ingress 80/443, web egress 80/443 để pull ECR). Dùng `aws_vpc_security_group_ingress_rule` / `aws_vpc_security_group_egress_rule` (resource tách rời) thay vì block `ingress`/`egress` inline — nếu dùng inline sẽ tạo circular dependency giữa `sg-alb` và `sg-web` vì hai SG tham chiếu lẫn nhau.
>
> `sg-rds` có egress **rỗng hoàn toàn**. AWS tự thêm rule egress allow-all khi tạo SG, nhưng `aws_security_group` không khai báo block `egress` inline nào sẽ khiến Terraform thu hồi rule mặc định đó. Step 8 verify điều này bằng CLI.

- [ ] **Step 1: Viết test trước — `infra/tf/modules/security/tests/sg.tftest.hcl`**

```hcl
provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project = "hushstore-tftest"
  vpc_id  = "vpc-00000000000000000"
}

run "khong_co_bat_ky_rule_nao_mo_port_22" {
  command = plan

  assert {
    condition = alltrue([
      for r in values(aws_vpc_security_group_ingress_rule.all) :
      !(r.from_port <= 22 && r.to_port >= 22)
    ])
    error_message = "Không Security Group nào được có ingress rule chứa port 22 — admin access chỉ qua SSM Session Manager."
  }
}

run "khong_co_ingress_rule_nao_mo_0000_ngoai_alb" {
  command = plan

  assert {
    condition = alltrue([
      for k, r in aws_vpc_security_group_ingress_rule.all :
      r.cidr_ipv4 == null || startswith(k, "alb-")
    ])
    error_message = "Chỉ sg-alb được nhận traffic từ CIDR công cộng. Mọi SG khác phải dùng referenced_security_group_id."
  }
}

run "alb_chi_mo_dung_2_rule_80_va_443" {
  command = plan

  assert {
    condition = alltrue([
      aws_vpc_security_group_ingress_rule.all["alb-http"].from_port == 80,
      aws_vpc_security_group_ingress_rule.all["alb-http"].to_port == 80,
      aws_vpc_security_group_ingress_rule.all["alb-http"].cidr_ipv4 == "0.0.0.0/0",
      aws_vpc_security_group_ingress_rule.all["alb-https"].from_port == 443,
      aws_vpc_security_group_ingress_rule.all["alb-https"].to_port == 443,
      aws_vpc_security_group_ingress_rule.all["alb-https"].cidr_ipv4 == "0.0.0.0/0",
    ])
    error_message = "sg-alb phải mở đúng 80 và 443 từ 0.0.0.0/0."
  }

  # Đếm theo KEY của for_each — key biết ở plan-time. KHÔNG so
  # r.security_group_id với aws_security_group.alb.id: cả hai là
  # (known after apply) nên Terraform báo lỗi Unknown condition value.
  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_ingress_rule.all) : k if startswith(k, "alb-")
    ]) == 2
    error_message = "sg-alb phải có ĐÚNG 2 ingress rule — thêm rule nào là vi phạm nguyên tắc tối thiểu."
  }
}

run "alb_egress_chi_toi_sg_web_khong_ra_cidr_nao" {
  command = plan

  # cidr_ipv4 == null chứng minh source/destination là SG reference chứ không
  # phải CIDR, mà không cần biết giá trị ID thật.
  assert {
    condition = alltrue([
      for k, r in aws_vpc_security_group_egress_rule.all :
      r.cidr_ipv4 == null if startswith(k, "alb-")
    ])
    error_message = "sg-alb chỉ được egress tới sg-web qua referenced_security_group_id, không được mở ra CIDR nào."
  }

  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_egress_rule.all) : k if startswith(k, "alb-")
    ]) == 2
    error_message = "sg-alb phải có đúng 2 egress rule (80 và 8080 tới sg-web)."
  }
}

run "web_chi_nhan_traffic_tu_sg_alb" {
  command = plan

  assert {
    condition = alltrue([
      for k, r in aws_vpc_security_group_ingress_rule.all :
      r.cidr_ipv4 == null if startswith(k, "web-")
    ])
    error_message = "sg-web chỉ được nhận traffic từ sg-alb — mọi ingress rule của nó phải dùng referenced_security_group_id, không dùng CIDR."
  }

  assert {
    condition = alltrue([
      aws_vpc_security_group_ingress_rule.all["web-http"].from_port == 80,
      aws_vpc_security_group_ingress_rule.all["web-api"].from_port == 8080,
    ])
    error_message = "sg-web phải nhận đúng port 80 (nginx) và 8080 (API)."
  }

  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_ingress_rule.all) : k if startswith(k, "web-")
    ]) == 2
    error_message = "sg-web phải có ĐÚNG 2 ingress rule."
  }
}

run "rds_chi_nhan_1433_tu_sg_web_va_khong_co_egress" {
  command = plan

  assert {
    condition = alltrue([
      aws_vpc_security_group_ingress_rule.all["rds-mssql"].from_port == 1433,
      aws_vpc_security_group_ingress_rule.all["rds-mssql"].to_port == 1433,
      aws_vpc_security_group_ingress_rule.all["rds-mssql"].cidr_ipv4 == null,
    ])
    error_message = "sg-rds phải nhận đúng 1433 và chỉ qua referenced_security_group_id (sg-web), không qua CIDR."
  }

  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_ingress_rule.all) : k if startswith(k, "rds-")
    ]) == 1
    error_message = "sg-rds phải có ĐÚNG 1 ingress rule."
  }

  assert {
    condition = length([
      for k in keys(aws_vpc_security_group_egress_rule.all) : k if startswith(k, "rds-")
    ]) == 0
    error_message = "sg-rds phải có egress RỖNG — RDS không cần gọi ra ngoài."
  }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/security
terraform init
terraform test
```

Expected: FAIL với `Reference to undeclared resource` — module chưa có resource.

- [ ] **Step 3: Viết `infra/tf/modules/security/variables.tf`**

```hcl
variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "vpc_id" {
  description = "ID của VPC chứa các Security Group"
  type        = string
}
```

- [ ] **Step 4: Viết `infra/tf/modules/security/main.tf`**

```hcl
# ─── SECURITY GROUPS ─────────────────────────────────────────────
# Không khai báo block ingress/egress inline: Terraform sẽ coi rule set là
# rỗng và thu hồi luôn rule egress allow-all mà AWS tự thêm. Rule thật khai
# báo bằng resource tách rời bên dưới để tránh circular dependency giữa
# sg-alb và sg-web.

resource "aws_security_group" "alb" {
  name        = "${var.project}-alb-sg"
  description = "ALB: nhan 80/443 tu internet, chuyen tiep sang sg-web"
  vpc_id      = var.vpc_id

  tags = { Name = "${var.project}-alb-sg" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "web" {
  name        = "${var.project}-web-sg"
  description = "ECS container instance: chi nhan traffic tu ALB, khong co port 22"
  vpc_id      = var.vpc_id

  tags = { Name = "${var.project}-web-sg" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "rds" {
  name        = "${var.project}-rds-sg"
  description = "RDS SQL Server: chi nhan 1433 tu sg-web, egress rong"
  vpc_id      = var.vpc_id

  tags = { Name = "${var.project}-rds-sg" }

  lifecycle {
    create_before_destroy = true
  }
}

# ─── INGRESS RULES ───────────────────────────────────────────────
# Gom vào 1 map để test có thể assert trên toàn bộ rule set cùng lúc —
# đó là cách bảo đảm "không có rule nào mở port 22" ở phạm vi cả module.
locals {
  ingress_rules = {
    # sg-alb là SG DUY NHẤT được nhận traffic từ CIDR công cộng.
    "alb-http" = {
      sg_id       = aws_security_group.alb.id
      description = "HTTP tu internet, se bi redirect sang HTTPS o listener"
      from_port   = 80
      to_port     = 80
      cidr_ipv4   = "0.0.0.0/0"
      source_sg   = null
    }
    "alb-https" = {
      sg_id       = aws_security_group.alb.id
      description = "HTTPS tu internet"
      from_port   = 443
      to_port     = 443
      cidr_ipv4   = "0.0.0.0/0"
      source_sg   = null
    }

    # sg-web: chỉ nhận từ sg-alb. KHÔNG có rule port 22.
    "web-http" = {
      sg_id       = aws_security_group.web.id
      description = "nginx container serve Blazor WASM, chi tu ALB"
      from_port   = 80
      to_port     = 80
      cidr_ipv4   = null
      source_sg   = aws_security_group.alb.id
    }
    "web-api" = {
      sg_id       = aws_security_group.web.id
      description = "API container, chi tu ALB"
      from_port   = 8080
      to_port     = 8080
      cidr_ipv4   = null
      source_sg   = aws_security_group.alb.id
    }

    # sg-rds: đúng 1 rule.
    "rds-mssql" = {
      sg_id       = aws_security_group.rds.id
      description = "SQL Server, chi tu container instance"
      from_port   = 1433
      to_port     = 1433
      cidr_ipv4   = null
      source_sg   = aws_security_group.web.id
    }
  }

  egress_rules = {
    # sg-alb chỉ được nói chuyện với sg-web, không ra internet.
    "alb-to-web-http" = {
      sg_id       = aws_security_group.alb.id
      description = "Forward sang nginx container"
      from_port   = 80
      to_port     = 80
      cidr_ipv4   = null
      target_sg   = aws_security_group.web.id
    }
    "alb-to-web-api" = {
      sg_id       = aws_security_group.alb.id
      description = "Forward sang API container"
      from_port   = 8080
      to_port     = 8080
      cidr_ipv4   = null
      target_sg   = aws_security_group.web.id
    }

    # sg-web: 1433 tới RDS, và 80/443 ra internet qua NAT để pull ECR,
    # gọi SSM, yum update. NAT Gateway không gắn được SG nên đây là chỗ
    # duy nhất kiểm soát egress ở tầng SG.
    "web-to-rds" = {
      sg_id       = aws_security_group.web.id
      description = "Ket noi SQL Server"
      from_port   = 1433
      to_port     = 1433
      cidr_ipv4   = null
      target_sg   = aws_security_group.rds.id
    }
    "web-https-out" = {
      sg_id       = aws_security_group.web.id
      description = "Pull image tu ECR, goi SSM va CloudWatch"
      from_port   = 443
      to_port     = 443
      cidr_ipv4   = "0.0.0.0/0"
      target_sg   = null
    }
    "web-http-out" = {
      sg_id       = aws_security_group.web.id
      description = "yum update tren ECS-optimized AMI"
      from_port   = 80
      to_port     = 80
      cidr_ipv4   = "0.0.0.0/0"
      target_sg   = null
    }

    # sg-rds: KHÔNG có rule nào. RDS không cần egress.
  }
}

resource "aws_vpc_security_group_ingress_rule" "all" {
  for_each = local.ingress_rules

  security_group_id            = each.value.sg_id
  description                  = each.value.description
  ip_protocol                  = "tcp"
  from_port                    = each.value.from_port
  to_port                      = each.value.to_port
  cidr_ipv4                    = each.value.cidr_ipv4
  referenced_security_group_id = each.value.source_sg

  tags = { Name = "${var.project}-in-${each.key}" }
}

resource "aws_vpc_security_group_egress_rule" "all" {
  for_each = local.egress_rules

  security_group_id            = each.value.sg_id
  description                  = each.value.description
  ip_protocol                  = "tcp"
  from_port                    = each.value.from_port
  to_port                      = each.value.to_port
  cidr_ipv4                    = each.value.cidr_ipv4
  referenced_security_group_id = each.value.target_sg

  tags = { Name = "${var.project}-out-${each.key}" }
}
```

- [ ] **Step 5: Viết `infra/tf/modules/security/outputs.tf`**

```hcl
output "alb_sg_id" {
  description = "ID của Security Group cho ALB"
  value       = aws_security_group.alb.id
}

output "web_sg_id" {
  description = "ID của Security Group cho ECS container instance"
  value       = aws_security_group.web.id
}

output "rds_sg_id" {
  description = "ID của Security Group cho RDS"
  value       = aws_security_group.rds.id
}
```

- [ ] **Step 6: Chạy test để xác nhận nó pass**

```bash
cd infra/tf/modules/security
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `6 passed, 0 failed.`

- [ ] **Step 7: Thêm `module "security"` vào `infra/tf/envs/prod/main.tf` rồi apply**

Thêm sau `module "network"`:

```hcl
module "security" {
  source = "../../modules/security"

  project = local.name
  vpc_id  = module.network.vpc_id
}
```

```bash
cd infra/tf/envs/prod
terraform init
terraform apply
```

Expected: `Apply complete!` với 3 `aws_security_group` + 5 ingress rule + 5 egress rule added.

- [ ] **Step 8: Verify sg-rds có egress rỗng thật trên AWS**

```bash
VPC_ID=$(terraform output -raw vpc_id)
RDS_SG=$(aws ec2 describe-security-groups \
  --filters "Name=vpc-id,Values=$VPC_ID" "Name=group-name,Values=hushstore-rds-sg" \
  --query 'SecurityGroups[0].GroupId' --output text --profile hushstore --no-cli-pager)
aws ec2 describe-security-groups --group-ids "$RDS_SG" \
  --query 'SecurityGroups[0].{Ingress:length(IpPermissions),Egress:length(IpPermissionsEgress)}' \
  --profile hushstore --no-cli-pager
```

Expected: `{"Ingress": 1, "Egress": 0}`. Nếu `Egress` ra `1`, AWS default rule chưa bị thu hồi — thêm `revoke_rules_on_delete = true` vào `aws_security_group.rds` rồi apply lại, hoặc xoá tay bằng `aws ec2 revoke-security-group-egress`.

- [ ] **Step 9: Verify toàn VPC không có rule nào mở port 22**

```bash
aws ec2 describe-security-groups --filters "Name=vpc-id,Values=$VPC_ID" \
  --query 'SecurityGroups[].IpPermissions[?FromPort<=`22` && ToPort>=`22`]' \
  --output text --profile hushstore --no-cli-pager
```

Expected: **không in gì cả**. Đây là bằng chứng cho kịch bản kiểm thử số 5.

- [ ] **Step 10: Commit**

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): 3 Security Group tham chieu lan nhau, khong co port 22

sg-alb: dung 2 ingress rule 80/443 tu 0.0.0.0/0, egress CHI toi sg-web.
sg-web: dung 2 ingress rule 80/8080 CHI tu sg-alb, khong co rule 22 nao.
sg-rds: dung 1 ingress rule 1433 CHI tu sg-web, egress RONG.

Dung aws_vpc_security_group_{ingress,egress}_rule tach roi thay vi block
inline de tranh circular dependency giua sg-alb va sg-web. Gom rule vao map
local nen terraform test assert duoc tren toan bo rule set cung luc — do la
cach bao dam 'khong rule nao mo port 22' o pham vi ca module."
```

---

### Task 6: Module `storage` — 3 ECR repository + 3 S3 bucket

**Files:**
- Create: `infra/tf/modules/storage/versions.tf`
- Create: `infra/tf/modules/storage/variables.tf`
- Create: `infra/tf/modules/storage/ecr.tf`
- Create: `infra/tf/modules/storage/s3.tf`
- Create: `infra/tf/modules/storage/outputs.tf`
- Create: `infra/tf/modules/storage/tests/storage.tftest.hcl`
- Modify: `infra/tf/envs/prod/main.tf` (thêm `module "storage"` + `import` block)
- Modify: `infra/tf/envs/prod/outputs.tf`

**Interfaces:**
- Consumes: `var.project`, `var.region`.
- Produces — outputs của `module.storage`:
  - `ecr_api_url`, `ecr_web_url`, `ecr_migrator_url` (string) — URL đầy đủ để `docker push`
  - `ecr_api_arn`, `ecr_web_arn`, `ecr_migrator_arn` (string) — dùng cho IAM policy ở Task 11
  - `assets_bucket_name`, `assets_bucket_arn` (string)
  - `artifacts_bucket_name`, `artifacts_bucket_arn` (string)
  - `alb_logs_bucket_name` (string) — dùng ở Task 14

> **Không còn `import` block nào.** Spec ban đầu dự tính `import` bucket ảnh sản phẩm `hushstore-public-assets` vì nó chứa ảnh thật. Nhưng account cũ đã bị xoá sạch — kiểm tra thực tế cho thấy **không còn S3 bucket nào**, nên bucket đó không tồn tại và DB kỳ này cũng seed từ đầu, không có URL ảnh cũ nào để giữ. Cả 3 bucket đều tạo mới.
>
> **Tên S3 bucket là duy nhất toàn cầu**, không chỉ trong account — nên **cả ba bucket đều gắn hậu tố account ID**. Đây là quyết định sau khi thử cách khác và thất bại: bản đầu của plan cho bucket ảnh giữ tên đẹp `hushstore-public-assets` nếu `head-bucket` báo còn trống. Thực tế `head-bucket` trả **404** cho đúng cái tên mà `CreateBucket` sau đó báo **409 `BucketAlreadyExists`** — vì với bucket thuộc account khác, S3 che sự tồn tại bằng 404 thay vì 403. Kết luận: **không có cách read-only nào kiểm tra được tên bucket còn trống toàn cầu**; cách duy nhất đáng tin là thử tạo. Nên đừng đoán — hậu tố account ID cho cả ba, tên dài hơn một chút trong URL ảnh là giá phải trả và nó chấp nhận được (DB kỳ này seed từ đầu, không có URL ảnh cũ nào).

- [ ] **Step 1: Đặt biến `$ACCT` để đối chiếu tên bucket sau khi apply**

```bash
export ACCT=$(aws sts get-caller-identity --query Account --output text --profile hushstore)
echo "Ba bucket sẽ có tên:"
echo "  hushstore-public-assets-${ACCT}"
echo "  hushstore-artifacts-${ACCT}"
echo "  hushstore-alb-logs-${ACCT}"
```

Không có bước kiểm tra tên còn trống, vì **không kiểm được** — xem ghi chú ở trên. Tên do module tự ghép từ `data.aws_caller_identity.current.account_id`, không có biến nào để đặt sai.

- [ ] **Step 2: Viết test trước — `infra/tf/modules/storage/tests/storage.tftest.hcl`**

```hcl
provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project             = "hushstore"
  region              = "ap-southeast-1"
  alb_logs_retention  = 7
  artifacts_retention = 30
}

run "co_dung_3_ecr_repository_va_deu_immutable" {
  command = plan

  assert {
    condition     = length(aws_ecr_repository.this) == 3
    error_message = "Phải có đúng 3 ECR repository: api, web, migrator."
  }

  assert {
    condition = alltrue([
      for r in aws_ecr_repository.this : r.image_tag_mutability == "IMMUTABLE"
    ])
    error_message = "ECR phải IMMUTABLE — tag là git SHA, không được ghi đè. Đây là điều kiện để rollback đáng tin."
  }

  assert {
    condition = alltrue([
      for r in aws_ecr_repository.this : r.image_scanning_configuration[0].scan_on_push == true
    ])
    error_message = "ECR phải bật scan_on_push để phát hiện CVE trong image."
  }
}

run "artifacts_va_alb_logs_bucket_chan_public_hoan_toan" {
  command = plan

  assert {
    condition = alltrue([
      aws_s3_bucket_public_access_block.artifacts.block_public_acls,
      aws_s3_bucket_public_access_block.artifacts.block_public_policy,
      aws_s3_bucket_public_access_block.artifacts.ignore_public_acls,
      aws_s3_bucket_public_access_block.artifacts.restrict_public_buckets,
    ])
    error_message = "Bucket artifacts phải chặn public hoàn toàn — nó chứa migrate SQL và file ops."
  }

  assert {
    condition = alltrue([
      aws_s3_bucket_public_access_block.alb_logs.block_public_acls,
      aws_s3_bucket_public_access_block.alb_logs.block_public_policy,
      aws_s3_bucket_public_access_block.alb_logs.ignore_public_acls,
      aws_s3_bucket_public_access_block.alb_logs.restrict_public_buckets,
    ])
    error_message = "Bucket alb-logs phải chặn public hoàn toàn — access log lộ IP và path của người dùng."
  }
}

run "co_lifecycle_don_du_lieu_cu" {
  command = plan

  assert {
    condition     = aws_s3_bucket_lifecycle_configuration.alb_logs.rule[0].expiration[0].days == 7
    error_message = "ALB access log phải hết hạn sau 7 ngày để không phình phí lưu trữ."
  }

  assert {
    condition     = aws_s3_bucket_lifecycle_configuration.artifacts.rule[0].expiration[0].days == 30
    error_message = "Artifacts phải hết hạn sau 30 ngày."
  }

  assert {
    condition = alltrue([
      for p in aws_ecr_lifecycle_policy.this : can(jsondecode(p.policy).rules[0].selection.countNumber)
    ])
    error_message = "Mỗi ECR repo phải có lifecycle policy giới hạn số image giữ lại."
  }
}
```

- [ ] **Step 3: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/storage
terraform init
terraform test
```

Expected: FAIL với `Reference to undeclared resource`.

- [ ] **Step 4: Viết `infra/tf/modules/storage/variables.tf`**

```hcl
variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "region" {
  description = "Vùng AWS"
  type        = string
}

variable "ecr_keep_images" {
  description = "Số image gần nhất giữ lại trong mỗi ECR repository"
  type        = number
  default     = 5
}

variable "alb_logs_retention" {
  description = "Số ngày giữ ALB access log"
  type        = number
  default     = 7
}

variable "artifacts_retention" {
  description = "Số ngày giữ file trong bucket artifacts"
  type        = number
  default     = 30
}
```

- [ ] **Step 5: Viết `infra/tf/modules/storage/ecr.tf`**

```hcl
locals {
  ecr_repos = ["api", "web", "migrator"]
}

resource "aws_ecr_repository" "this" {
  for_each = toset(local.ecr_repos)

  name = "${var.project}-${each.key}"

  # IMMUTABLE: tag là git SHA nên không bao giờ được ghi đè. Đây là điều
  # kiện để rollback bằng cách trỏ lại task definition revision cũ thực sự
  # đáng tin — image của revision cũ chắc chắn còn nguyên nội dung.
  image_tag_mutability = "IMMUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  # Cho phép terraform destroy xoá repo kể cả khi còn image bên trong.
  force_delete = true

  tags = { Name = "${var.project}-${each.key}" }
}

resource "aws_ecr_lifecycle_policy" "this" {
  for_each = aws_ecr_repository.this

  repository = each.value.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Chi giu ${var.ecr_keep_images} image gan nhat"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = var.ecr_keep_images
        }
        action = { type = "expire" }
      }
    ]
  })
}
```

- [ ] **Step 6: Viết `infra/tf/modules/storage/s3.tf`**

```hcl
data "aws_caller_identity" "current" {}

locals {
  # Cả ba bucket gắn hậu tố account ID. Tên bucket S3 duy nhất toàn cầu và
  # KHÔNG có cách read-only nào kiểm tra được tên còn trống (head-bucket trả 404
  # cho cả bucket của account khác), nên đừng đoán — ghép account ID là xong.
  assets_bucket   = "${var.project}-public-assets-${data.aws_caller_identity.current.account_id}"
  artifacts_bucket = "${var.project}-artifacts-${data.aws_caller_identity.current.account_id}"
  alb_logs_bucket  = "${var.project}-alb-logs-${data.aws_caller_identity.current.account_id}"
}

# ─── BUCKET ẢNH SẢN PHẨM ─────────────────────────────────────────
resource "aws_s3_bucket" "assets" {
  bucket = local.assets_bucket

  tags = { Name = local.assets_bucket }

  # force_destroy = false (mặc định) là lưới an toàn đúng mức ở đây:
  # terraform destroy sẽ THẤT BẠI nếu bucket còn object, buộc phải xoá ảnh
  # một cách có ý thức trước. Không dùng prevent_destroy vì nó chặn cả
  # nuke.sh ngay cả khi bucket rỗng.
  force_destroy = false
}

# S3StorageService trả về URL public dạng
# https://<bucket>.s3.<region>.amazonaws.com/<key>, nên object phải đọc
# được công khai. Cho phép public policy nhưng vẫn chặn ACL.
resource "aws_s3_bucket_public_access_block" "assets" {
  bucket = aws_s3_bucket.assets.id

  block_public_acls       = true
  ignore_public_acls      = true
  block_public_policy     = false
  restrict_public_buckets = false
}

data "aws_iam_policy_document" "assets_public_read" {
  statement {
    sid     = "PublicReadObjects"
    effect  = "Allow"
    actions = ["s3:GetObject"]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    resources = ["${aws_s3_bucket.assets.arn}/*"]
  }
}

resource "aws_s3_bucket_policy" "assets" {
  bucket = aws_s3_bucket.assets.id
  policy = data.aws_iam_policy_document.assets_public_read.json

  depends_on = [aws_s3_bucket_public_access_block.assets]
}

resource "aws_s3_bucket_cors_configuration" "assets" {
  bucket = aws_s3_bucket.assets.id

  cors_rule {
    allowed_methods = ["GET", "HEAD"]
    allowed_origins = ["*"]
    allowed_headers = ["*"]
    max_age_seconds = 3600
  }
}

# ─── BUCKET ARTIFACTS ────────────────────────────────────────────
resource "aws_s3_bucket" "artifacts" {
  bucket        = local.artifacts_bucket
  force_destroy = true

  tags = { Name = "${var.project}-artifacts" }
}

resource "aws_s3_bucket_public_access_block" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  rule {
    id     = "expire-artifacts"
    status = "Enabled"

    filter {}

    expiration {
      days = var.artifacts_retention
    }
  }
}

# ─── BUCKET ALB ACCESS LOGS ──────────────────────────────────────
resource "aws_s3_bucket" "alb_logs" {
  bucket        = local.alb_logs_bucket
  force_destroy = true

  tags = { Name = "${var.project}-alb-logs" }
}

resource "aws_s3_bucket_public_access_block" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id

  rule {
    id     = "expire-alb-logs"
    status = "Enabled"

    filter {}

    expiration {
      days = var.alb_logs_retention
    }
  }
}

# ap-southeast-1 là region ra đời trước 08/2022 nên ALB ghi log bằng ELB
# account ID của region. Region mới hơn dùng service principal
# logdelivery.elasticloadbalancing.amazonaws.com. Cấp cả hai để chắc chắn.
data "aws_elb_service_account" "current" {}

data "aws_iam_policy_document" "alb_logs" {
  statement {
    sid     = "ElbAccountWrite"
    effect  = "Allow"
    actions = ["s3:PutObject"]

    principals {
      type        = "AWS"
      identifiers = [data.aws_elb_service_account.current.arn]
    }

    resources = ["${aws_s3_bucket.alb_logs.arn}/*"]
  }

  statement {
    sid     = "LogDeliveryServiceWrite"
    effect  = "Allow"
    actions = ["s3:PutObject"]

    principals {
      type        = "Service"
      identifiers = ["logdelivery.elasticloadbalancing.amazonaws.com"]
    }

    resources = ["${aws_s3_bucket.alb_logs.arn}/*"]
  }
}

resource "aws_s3_bucket_policy" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id
  policy = data.aws_iam_policy_document.alb_logs.json

  depends_on = [aws_s3_bucket_public_access_block.alb_logs]
}
```

- [ ] **Step 7: Viết `infra/tf/modules/storage/outputs.tf`**

```hcl
output "ecr_api_url" {
  description = "URL repository ECR của image API"
  value       = aws_ecr_repository.this["api"].repository_url
}

output "ecr_web_url" {
  description = "URL repository ECR của image web (nginx + Blazor WASM)"
  value       = aws_ecr_repository.this["web"].repository_url
}

output "ecr_migrator_url" {
  description = "URL repository ECR của image migrator (EF Core bundle)"
  value       = aws_ecr_repository.this["migrator"].repository_url
}

output "ecr_api_arn" {
  description = "ARN repository ECR của image API"
  value       = aws_ecr_repository.this["api"].arn
}

output "ecr_web_arn" {
  description = "ARN repository ECR của image web"
  value       = aws_ecr_repository.this["web"].arn
}

output "ecr_migrator_arn" {
  description = "ARN repository ECR của image migrator"
  value       = aws_ecr_repository.this["migrator"].arn
}

output "assets_bucket_name" {
  description = "Tên bucket ảnh sản phẩm"
  value       = aws_s3_bucket.assets.id
}

output "assets_bucket_arn" {
  description = "ARN bucket ảnh sản phẩm — dùng cho IAM policy của ECS task role"
  value       = aws_s3_bucket.assets.arn
}

output "artifacts_bucket_name" {
  description = "Tên bucket artifacts"
  value       = aws_s3_bucket.artifacts.id
}

output "artifacts_bucket_arn" {
  description = "ARN của bucket artifacts"
  value       = aws_s3_bucket.artifacts.arn
}

output "alb_logs_bucket_name" {
  description = "Tên bucket chứa ALB access log"
  value       = aws_s3_bucket.alb_logs.id
}
```

- [ ] **Step 8: Chạy test để xác nhận nó pass**

```bash
cd infra/tf/modules/storage
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `3 passed, 0 failed.`

- [ ] **Step 9: Thêm `module "storage"` vào `infra/tf/envs/prod/main.tf` và chốt tên bucket ảnh**

Thêm sau `module "security"`:

```hcl
module "storage" {
  source = "../../modules/storage"

  project = local.name
  region  = var.region
}
```

Không có biến `assets_bucket_name` — tên do module tự ghép, nên không có chỗ nào đặt sai được.

- [ ] **Step 10: Plan và kiểm tra không có gì bị destroy**

```bash
cd infra/tf/envs/prod
terraform init
terraform plan
```

Expected: chỉ có `to add`, **`0 to destroy`**. Không có `import` block nào trong stack này — cả 3 bucket đều tạo mới. Nếu `plan` báo lỗi `BucketAlreadyExists` ở bước apply sau, quay lại Step 1 để chốt lại tên.

- [ ] **Step 11: Apply**

```bash
terraform apply
```

Expected: `Apply complete!` với 3 `aws_ecr_repository`, 3 `aws_ecr_lifecycle_policy`, 3 `aws_s3_bucket` và các resource cấu hình bucket đi kèm.

- [ ] **Step 12: Verify 3 ECR repository đều IMMUTABLE**

```bash
aws ecr describe-repositories \
  --query 'repositories[?starts_with(repositoryName, `hushstore-`)].{Name:repositoryName,Mutability:imageTagMutability,Scan:imageScanningConfiguration.scanOnPush}' \
  --output table --profile hushstore --no-cli-pager
```

Expected: 3 dòng `hushstore-api`, `hushstore-web`, `hushstore-migrator`, tất cả `IMMUTABLE` và `True`.

- [ ] **Step 13: Verify bucket ảnh đọc công khai được (điều kiện để upload ảnh hoạt động ở Task 16)**

```bash
ASSETS=$(terraform output -raw assets_bucket)
echo "probe" > /tmp/probe.txt
aws s3 cp /tmp/probe.txt "s3://${ASSETS}/probe/probe.txt" --profile hushstore --no-cli-pager
curl -s -o /dev/null -w "public-read=%{http_code}\n" \
  "https://${ASSETS}.s3.ap-southeast-1.amazonaws.com/probe/probe.txt"
aws s3 rm "s3://${ASSETS}/probe/probe.txt" --profile hushstore --no-cli-pager
```

Expected: `public-read=200`. Nếu ra `403`, bucket policy hoặc public access block chưa đúng — `S3StorageService` trả URL công khai nên ảnh phải đọc được mà không cần credential.

> Thêm output `assets_bucket` vào `infra/tf/envs/prod/outputs.tf` ở Step 14 để lệnh trên lấy được tên bucket.

- [ ] **Step 14: Thêm output vào `infra/tf/envs/prod/outputs.tf` và commit**

```hcl
output "ecr_urls" {
  description = "URL 3 ECR repository để docker push"
  value = {
    api      = module.storage.ecr_api_url
    web      = module.storage.ecr_web_url
    migrator = module.storage.ecr_migrator_url
  }
}

output "artifacts_bucket" {
  description = "Bucket chứa migrate SQL và file ops"
  value       = module.storage.artifacts_bucket_name
}

output "assets_bucket" {
  description = "Bucket ảnh sản phẩm — tên này hiện trong URL ảnh công khai"
  value       = module.storage.assets_bucket_name
}
```

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): module storage — 3 ECR repo IMMUTABLE + 3 S3 bucket

ECR api/web/migrator, image_tag_mutability IMMUTABLE (tag la git SHA nen
khong duoc ghi de — dieu kien de rollback dang tin), scan_on_push, lifecycle
giu 5 image gan nhat.

S3: ca 3 bucket deu tao moi — account cu da bi xoa sach nen khong con bucket
anh nao de import, va DB ky nay cung seed tu dau. artifacts va alb-logs gan hau
to account ID vi ten bucket S3 la duy nhat toan cau va hai ten do qua pho thong;
bucket anh giu ten dep khong hau to vi no hien trong URL anh cong khai. Ca hai
bucket rieng tu chan public hoan toan, lifecycle 30/7 ngay. Policy alb-logs cap cho ca ELB account ID cua region va service
principal logdelivery de chay dung o ap-southeast-1."
```

---

### Task 7: Module `data` — RDS SQL Server Express + SSM Parameter Store

**Files:**
- Create: `infra/tf/modules/data/versions.tf`
- Create: `infra/tf/modules/data/variables.tf`
- Create: `infra/tf/modules/data/main.tf`
- Create: `infra/tf/modules/data/outputs.tf`
- Create: `infra/tf/modules/data/tests/rds.tftest.hcl`
- Modify: `infra/tf/envs/prod/main.tf` (thêm `module "data"`)
- Modify: `infra/tf/envs/prod/variables.tf` (thêm `db_engine_version`)
- Modify: `infra/tf/envs/prod/terraform.tfvars.example`
- Modify: `infra/tf/envs/prod/outputs.tf`

**Interfaces:**
- Consumes: `module.network.db_subnet_ids`, `module.security.rds_sg_id`.
- Produces — outputs của `module.data`:
  - `rds_endpoint` (string) — hostname, ví dụ `hushstore-db.xxxx.ap-southeast-1.rds.amazonaws.com`
  - `rds_identifier` (string) — `hushstore-db-tf`
  - `ssm_connection_string_arn` (string) — ARN parameter, dùng trong khối `secrets` của task definition
  - `ssm_jwt_secret_arn` (string) — ARN parameter
  - `ssm_path_prefix` (string) — `/hushstore/prod` — dùng cho IAM policy ở Task 11

> **Ba điểm quan trọng:**
> 1. **Không đặt `db_name`.** `aws_db_instance.db_name` không được hỗ trợ cho engine SQL Server. Database `HushStoreDB` sẽ do EF Core migration bundle tự tạo ở Task 13 (`Migrate()` tạo DB nếu chưa có).
> 2. **Dùng SSM Parameter Store SecureString, không dùng Secrets Manager.** Parameter Store với KMS key mặc định `alias/aws/ssm` là **miễn phí**; Secrets Manager tốn $0.40/secret/tháng. ECS inject được cả hai qua khối `secrets`.
> 3. **RDS identifier là `hushstore-db-tf`.** Hậu tố `-tf` để phân biệt rõ đây là instance do Terraform quản, tránh nhầm với `hushstore-db` dựng tay ở kỳ trước (đã xoá).

- [ ] **Step 1: Lấy engine version mới nhất của SQL Server Express**

```bash
aws rds describe-db-engine-versions --engine sqlserver-ex \
  --query 'sort_by(DBEngineVersions,&EngineVersion)[-1].EngineVersion' \
  --output text --profile hushstore --no-cli-pager
```

Expected: một chuỗi version, ví dụ `16.00.4210.1.v1`. Ghi lại giá trị này — Step 8 sẽ đặt vào `terraform.tfvars`.

- [ ] **Step 2: Viết test trước — `infra/tf/modules/data/tests/rds.tftest.hcl`**

```hcl
provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project        = "hushstore-tftest"
  db_subnet_ids  = ["subnet-00000000000000001", "subnet-00000000000000002"]
  rds_sg_id      = "sg-00000000000000000"
  engine_version = "16.00.4210.1.v1"
  db_username    = "dbadmin"
  db_name        = "HushStoreDB"
}

run "rds_khong_bao_gio_public_accessible" {
  command = plan

  assert {
    condition     = aws_db_instance.this.publicly_accessible == false
    error_message = "RDS TUYỆT ĐỐI không được publicly_accessible — đây là lớp phòng thủ đầu tiên cho kịch bản kiểm thử số 3."
  }

  assert {
    condition     = aws_db_instance.this.multi_az == false
    error_message = "SQL Server Express không hỗ trợ Multi-AZ, và Multi-AZ nằm ngoài free tier."
  }
}

run "rds_nam_trong_db_subnet_va_dung_sg_rds" {
  command = plan

  assert {
    condition     = length(aws_db_subnet_group.this.subnet_ids) == 2
    error_message = "DB subnet group phải có 2 subnet ở 2 AZ — yêu cầu của RDS."
  }

  assert {
    condition     = contains(aws_db_instance.this.vpc_security_group_ids, var.rds_sg_id)
    error_message = "RDS phải dùng đúng sg-rds (chỉ nhận 1433 từ sg-web)."
  }
}

run "rds_khong_dat_db_name_vi_sql_server_khong_ho_tro" {
  command = plan

  assert {
    condition     = aws_db_instance.this.db_name == null || aws_db_instance.this.db_name == ""
    error_message = "Không được đặt db_name cho engine SQL Server — database do EF Core migration bundle tạo."
  }
}

run "rds_co_the_destroy_duoc_de_phuc_vu_nuke_sh" {
  command = plan

  assert {
    condition     = aws_db_instance.this.deletion_protection == false
    error_message = "deletion_protection phải false, nếu không nuke.sh sẽ treo."
  }
}

run "secret_dung_ssm_securestring_khong_dung_secrets_manager" {
  command = plan

  assert {
    condition = alltrue([
      aws_ssm_parameter.db_password.type == "SecureString",
      aws_ssm_parameter.connection_string.type == "SecureString",
      aws_ssm_parameter.jwt_secret.type == "SecureString",
    ])
    error_message = "Cả 3 parameter phải là SecureString — Parameter Store miễn phí, Secrets Manager tốn $0.40/secret/tháng."
  }

  assert {
    condition = alltrue([
      startswith(aws_ssm_parameter.db_password.name, "/hushstore-tftest/prod/"),
      startswith(aws_ssm_parameter.connection_string.name, "/hushstore-tftest/prod/"),
      startswith(aws_ssm_parameter.jwt_secret.name, "/hushstore-tftest/prod/"),
    ])
    error_message = "Parameter phải nằm dưới cùng một path prefix để IAM policy giới hạn được bằng wildcard."
  }
}

run "mat_khau_du_dai_va_khong_chua_ky_tu_rds_cam" {
  command = plan

  assert {
    condition     = random_password.db.length >= 24
    error_message = "Mật khẩu DB phải tối thiểu 24 ký tự."
  }

  assert {
    condition     = random_password.jwt.length >= 48
    error_message = "JWT secret phải tối thiểu 48 ký tự để đủ 256-bit entropy."
  }
}
```

- [ ] **Step 3: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/data
terraform init
terraform test
```

Expected: FAIL với `Reference to undeclared resource`.

- [ ] **Step 4: Viết `infra/tf/modules/data/versions.tf`**

```hcl
terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}
```

- [ ] **Step 5: Viết `infra/tf/modules/data/variables.tf`**

```hcl
variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "db_subnet_ids" {
  description = "ID của 2 db subnet (isolated tier)"
  type        = list(string)
}

variable "rds_sg_id" {
  description = "ID Security Group của RDS — chỉ nhận 1433 từ sg-web"
  type        = string
}

variable "engine_version" {
  description = "Version của sqlserver-ex. Lấy bằng: aws rds describe-db-engine-versions --engine sqlserver-ex"
  type        = string
}

variable "instance_class" {
  description = "Instance class của RDS. db.t3.micro nằm trong free tier 750h/tháng"
  type        = string
  default     = "db.t3.micro"
}

variable "allocated_storage" {
  description = "Dung lượng GB. 20GB là mức tối thiểu cho SQL Server và nằm trong free tier"
  type        = number
  default     = 20
}

variable "db_username" {
  description = "Master username của RDS"
  type        = string
  default     = "dbadmin"
}

variable "db_name" {
  description = "Tên database ứng dụng. KHÔNG truyền vào aws_db_instance (SQL Server không hỗ trợ) — chỉ dùng để dựng connection string"
  type        = string
  default     = "HushStoreDB"
}

variable "backup_retention_days" {
  description = "Số ngày giữ backup tự động. Miễn phí tới mức bằng allocated_storage"
  type        = number
  default     = 7
}

variable "skip_final_snapshot" {
  description = "Bỏ qua final snapshot khi destroy. true để nuke.sh chạy nhanh"
  type        = bool
  default     = true
}
```

- [ ] **Step 6: Viết `infra/tf/modules/data/main.tf`**

```hcl
locals {
  ssm_prefix = "/${var.project}/prod"
}

# ─── MẬT KHẨU SINH TỰ ĐỘNG ───────────────────────────────────────
# RDS SQL Server cấm các ký tự: / ' " @ và khoảng trắng trong master
# password. override_special dưới đây đã loại hết chúng.
resource "random_password" "db" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
  min_upper        = 2
  min_lower        = 2
  min_numeric      = 2
  min_special      = 2
}

resource "random_password" "jwt" {
  length  = 64
  special = false
}

# ─── DB SUBNET GROUP ─────────────────────────────────────────────
resource "aws_db_subnet_group" "this" {
  name        = "${var.project}-db-subnet-group"
  description = "HushStore RDS - db tier isolated, 2 AZ"
  subnet_ids  = var.db_subnet_ids

  tags = { Name = "${var.project}-db-subnet-group" }
}

# ─── RDS SQL SERVER EXPRESS ──────────────────────────────────────
resource "aws_db_instance" "this" {
  identifier = "${var.project}-db-tf"

  engine         = "sqlserver-ex"
  engine_version = var.engine_version
  license_model  = "license-included"
  instance_class = var.instance_class

  allocated_storage = var.allocated_storage
  storage_type      = "gp2"
  storage_encrypted = true

  username = var.db_username
  password = random_password.db.result

  # KHÔNG đặt db_name — aws_db_instance.db_name không được hỗ trợ cho engine
  # SQL Server. Database HushStoreDB do EF Core migration bundle tạo ở Task 13.

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [var.rds_sg_id]
  publicly_accessible    = false
  multi_az               = false

  backup_retention_period = var.backup_retention_days
  auto_minor_version_upgrade = true

  # Phải false để nuke.sh / terraform destroy chạy được.
  deletion_protection = false
  skip_final_snapshot = var.skip_final_snapshot

  # Không bật Performance Insights / Enhanced Monitoring — tốn phí, không cần
  # cho quy mô đồ án.
  performance_insights_enabled = false

  tags = { Name = "${var.project}-db-tf" }
}

# ─── SSM PARAMETER STORE (SecureString, miễn phí) ────────────────
resource "aws_ssm_parameter" "db_password" {
  name        = "${local.ssm_prefix}/db-password"
  description = "Master password cua RDS SQL Server"
  type        = "SecureString"
  value       = random_password.db.result

  tags = { Name = "${var.project}-db-password" }
}

resource "aws_ssm_parameter" "connection_string" {
  name        = "${local.ssm_prefix}/connection-string"
  description = "Connection string day du, inject vao container qua khoi secrets cua ECS"
  type        = "SecureString"

  value = join("", [
    "Server=${aws_db_instance.this.address},1433;",
    "Database=${var.db_name};",
    "User Id=${var.db_username};",
    "Password=${random_password.db.result};",
    "TrustServerCertificate=True;",
    "MultipleActiveResultSets=True;",
  ])

  tags = { Name = "${var.project}-connection-string" }
}

resource "aws_ssm_parameter" "jwt_secret" {
  name        = "${local.ssm_prefix}/jwt-secret"
  description = "JwtSettings__SecretKey — 64 ky tu, tren 256-bit entropy"
  type        = "SecureString"
  value       = random_password.jwt.result

  tags = { Name = "${var.project}-jwt-secret" }
}
```

- [ ] **Step 7: Viết `infra/tf/modules/data/outputs.tf`**

```hcl
output "rds_endpoint" {
  description = "Hostname của RDS (không kèm port)"
  value       = aws_db_instance.this.address
}

output "rds_identifier" {
  description = "DB instance identifier — dùng cho aws rds start/stop-db-instance"
  value       = aws_db_instance.this.identifier
}

output "rds_arn" {
  description = "ARN của RDS instance"
  value       = aws_db_instance.this.arn
}

output "ssm_connection_string_arn" {
  description = "ARN parameter chứa connection string — dùng trong khối secrets của task definition"
  value       = aws_ssm_parameter.connection_string.arn
}

output "ssm_jwt_secret_arn" {
  description = "ARN parameter chứa JWT secret"
  value       = aws_ssm_parameter.jwt_secret.arn
}

output "ssm_db_password_arn" {
  description = "ARN parameter chứa master password của RDS"
  value       = aws_ssm_parameter.db_password.arn
}

output "ssm_path_prefix" {
  description = "Path prefix của mọi parameter — dùng cho IAM policy wildcard"
  value       = local.ssm_prefix
}
```

- [ ] **Step 8: Chạy test để xác nhận nó pass**

```bash
cd infra/tf/modules/data
terraform init
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `6 passed, 0 failed.`

- [ ] **Step 9: Thêm `db_engine_version` vào `infra/tf/envs/prod/variables.tf`**

```hcl
variable "db_engine_version" {
  description = "Version của sqlserver-ex. Lấy bằng: aws rds describe-db-engine-versions --engine sqlserver-ex --query 'sort_by(DBEngineVersions,&EngineVersion)[-1].EngineVersion' --output text"
  type        = string
}
```

Thêm vào `infra/tf/envs/prod/terraform.tfvars.example`:

```hcl
# Version sqlserver-ex. Lấy bằng:
#   aws rds describe-db-engine-versions --engine sqlserver-ex \
#     --query 'sort_by(DBEngineVersions,&EngineVersion)[-1].EngineVersion' \
#     --output text --profile hushstore
db_engine_version = "16.00.4210.1.v1"
```

- [ ] **Step 10: Đặt version thật vào `terraform.tfvars`**

```bash
cd infra/tf/envs/prod
EV=$(aws rds describe-db-engine-versions --engine sqlserver-ex \
  --query 'sort_by(DBEngineVersions,&EngineVersion)[-1].EngineVersion' \
  --output text --profile hushstore --no-cli-pager)
echo "db_engine_version = \"${EV}\"" >> terraform.tfvars
grep db_engine_version terraform.tfvars
```

Expected: in ra dòng `db_engine_version = "16.00.xxxx.x.v1"` với version thật.

- [ ] **Step 11: Thêm `module "data"` vào `infra/tf/envs/prod/main.tf`**

Thêm sau `module "storage"`:

```hcl
module "data" {
  source = "../../modules/data"

  project        = local.name
  db_subnet_ids  = module.network.db_subnet_ids
  rds_sg_id      = module.security.rds_sg_id
  engine_version = var.db_engine_version
}
```

- [ ] **Step 12: Apply (mất ~10-15 phút vì RDS khởi tạo chậm)**

```bash
terraform apply
```

Expected: `Apply complete!` với `aws_db_instance`, `aws_db_subnet_group`, 3 `aws_ssm_parameter`, 2 `random_password` added. Bước `aws_db_instance.this: Still creating...` chạy khoảng 10-15 phút — bình thường.

- [ ] **Step 13: Verify RDS không truy cập được từ internet**

```bash
RDS_HOST=$(terraform output -raw rds_endpoint)
echo "RDS endpoint: $RDS_HOST"
nc -z -w 5 "$RDS_HOST" 1433 && echo "SAI: RDS truy cập được từ internet" || echo "ĐÚNG: RDS không truy cập được từ internet"
```

Expected: in `ĐÚNG: RDS không truy cập được từ internet`. Đây là bằng chứng đầu tiên cho kịch bản kiểm thử số 3. Nếu in `SAI`, dừng lại kiểm tra `publicly_accessible` và `sg-rds`.

- [ ] **Step 14: Verify 3 SSM parameter tồn tại và là SecureString**

```bash
aws ssm get-parameters-by-path --path /hushstore/prod --recursive \
  --query 'Parameters[].{Name:Name,Type:Type}' --output table \
  --profile hushstore --no-cli-pager
```

Expected: bảng 3 dòng `/hushstore/prod/connection-string`, `/hushstore/prod/db-password`, `/hushstore/prod/jwt-secret`, tất cả `SecureString`.

> **Không chạy `get-parameter --with-decryption`** để in giá trị ra terminal — secret sẽ nằm lại trong shell history và log. Nếu cần giá trị, đọc trực tiếp vào biến môi trường như Step 5 của Task 16.

- [ ] **Step 15: Thêm output và commit**

Thêm vào `infra/tf/envs/prod/outputs.tf`:

```hcl
output "rds_endpoint" {
  description = "Hostname RDS — chỉ truy cập được từ trong app tier"
  value       = module.data.rds_endpoint
}

output "rds_identifier" {
  description = "DB identifier để start/stop tiết kiệm chi phí"
  value       = module.data.rds_identifier
}
```

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): module data — RDS SQL Server Express + SSM Parameter Store

RDS sqlserver-ex db.t3.micro 20GB gp2, storage encrypted, single-AZ (Express
khong ho tro Multi-AZ), publicly_accessible = false, nam trong db subnet
isolated, chi nhan 1433 tu sg-web.

KHONG dat db_name — aws_db_instance.db_name khong ho tro engine SQL Server.
Database HushStoreDB se do EF Core migration bundle tao o Task 13.

Mat khau sinh bang random_password (32 ky tu, da loai / ' \" @ va space vi RDS
SQL Server cam), luu vao SSM Parameter Store SecureString — mien phi, thay vi
Secrets Manager \$0.40/secret/thang. JWT secret 64 ky tu cung o day.

identifier la hushstore-db-tf — hau to -tf de phan biet ro day la instance do
Terraform quan."
```

---

### Task 8: Sửa `Program.cs` (4 điểm) + build và push image API lên ECR

**Files:**
- Modify: `src/API/Program.cs`
- Modify: `src/API/API.csproj` (thêm package health check EF Core)
- Modify: `src/API/appsettings.json` (xoá `AccessKeyId`, `SecretAccessKey`)
- Modify: `.env.example` (xoá 2 biến AWS key)
- Modify: `.dockerignore` (bỏ dòng loại trừ `src/Client/`)

**Interfaces:**
- Consumes: ECR URL từ `module.storage.ecr_api_url` (Task 6).
- Produces: image `<ecr_api_url>:<git-sha>` trên ECR; API expose `/health/live` và `/health/ready` (`/health/ready` trả 503 khi DB không kết nối được); app không còn chứa static IAM credential nào; không còn `MigrateAsync()` lúc startup.

> **Vì sao `.dockerignore` phải sửa:** file hiện có dòng `src/Client/` để image API không mang theo code client. Nhưng Task 9 sẽ build image web từ cùng build context (repo root), nên nếu vẫn loại trừ `src/Client/` thì `dotnet publish src/Client` trong image đó sẽ không tìm thấy file. Bỏ dòng đó ra; image API vẫn không chứa client vì `Dockerfile` chỉ `dotnet publish src/API/API.csproj`.

- [ ] **Step 1: Bật SQL Server local để test được health check**

```bash
cd Infrastructure/db
docker compose up -d
sleep 30
docker ps --filter name=hushstore_sqlserver_dev --format '{{.Names}} {{.Status}}'
```

Expected: `hushstore_sqlserver_dev Up ... (healthy)` hoặc `Up 30 seconds`. Nếu container không lên, kiểm tra `Infrastructure/db/.env` có `SA_PASSWORD`.

- [ ] **Step 2: Thêm package health check cho EF Core**

```bash
cd "$(git rev-parse --show-toplevel)"
dotnet add src/API/API.csproj package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
dotnet build src/API/API.csproj
```

Expected: `Build succeeded`. `API.csproj` có thêm `PackageReference` tới `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`.

- [ ] **Step 3: Thay đổi 1 — đăng ký health check có kiểm tra DB**

Trong `src/API/Program.cs`, thêm ngay trước dòng `builder.Services.AddRateLimiter(options =>`:

```csharp
// Health check cho ALB target group. /health/ready CHẠM DB thật — nếu RDS
// chết thì ALB phải rút instance khỏi target group, không được báo healthy.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<HushStoreDbContext>("database");
```

- [ ] **Step 4: Thay đổi 1 (tiếp) — expose 2 endpoint health check**

Trong `src/API/Program.cs`, thay dòng `app.MapGet("/health", ...)` bằng:

```csharp
// /health/live: chỉ trả lời "process còn sống", không chạm dependency nào.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// /health/ready: chạy toàn bộ health check, gồm cả DbContextCheck.
// Trả 200 Healthy / 503 Unhealthy. Đây là endpoint ALB tg-api trỏ vào.
app.MapHealthChecks("/health/ready").AllowAnonymous();

// Giữ /health cũ để tương thích với script và bookmark hiện có.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();
```

Thêm using ở đầu file (nếu chưa có):

```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
```

- [ ] **Step 5: Thay đổi 2 — đọc `X-Forwarded-*` từ ALB, bỏ HTTPS redirect ở Production**

Trong `src/API/Program.cs`, thêm vào phần đăng ký service (trước `var app = builder.Build();`):

```csharp
// ALB terminate TLS rồi forward HTTP xuống container. Không có block này thì
// app không biết request gốc là HTTPS, khiến mọi URL sinh ra bị sai scheme.
//
// KnownNetworks/KnownProxies bị clear vì container chạy bridge network mode:
// source IP mà app thấy là gateway của docker bridge (172.17.0.1), KHÔNG phải
// IP của ALB trong VPC — nên không thể whitelist theo VPC CIDR. An toàn vì
// sg-web chỉ nhận traffic từ sg-alb, không ai khác chạm tới được container.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
```

Thêm using:

```csharp
using Microsoft.AspNetCore.HttpOverrides;
```

Trong phần middleware, thay `app.UseHttpsRedirection();` bằng:

```csharp
// UseForwardedHeaders phải chạy TRƯỚC mọi middleware đọc scheme hoặc IP.
app.UseForwardedHeaders();

// Ở Production, ALB đã redirect 80 -> 443 ở tầng listener rồi. Bật thêm ở
// đây sẽ gây redirect loop khi ALB forward request HTTP xuống container.
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
```

- [ ] **Step 6: Thay đổi 3 — bỏ static IAM credential, dùng default credential chain**

Trong `src/API/Program.cs`, thay khối `builder.Services.AddSingleton<IAmazonS3>(...)` bằng:

```csharp
// DI: AWS S3 Storage
// KHÔNG dùng BasicAWSCredentials. AmazonS3Client không truyền credential sẽ
// dùng default credential chain: trên ECS nó tự lấy credential tạm thời của
// task role qua AWS_CONTAINER_CREDENTIALS_RELATIVE_URI. Nhờ vậy trong toàn hệ
// thống không còn static access key nào.
// Local dev: đặt AWS_PROFILE=hushstore trước khi chạy để upload ảnh hoạt động.
var awsCfg = builder.Configuration.GetSection("AwsSettings");
builder.Services.AddSingleton<IAmazonS3>(_ =>
    new AmazonS3Client(RegionEndpoint.GetBySystemName(awsCfg["Region"] ?? "ap-southeast-1")));
builder.Services.AddScoped<IStorageService, PBL3.Service.Storage.S3StorageService>();
```

- [ ] **Step 7: Thay đổi 4 — xoá `MigrateAsync()` khỏi startup**

Trong `src/API/Program.cs`, **xoá hoàn toàn** khối này:

```csharp
// Auto-apply EF Core migrations khi khởi động — chỉ chạy trên Production
// (tránh lỗi khi dev chạy local với DB chưa up)
if (app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HushStoreDbContext>();
    await db.Database.MigrateAsync();
}
```

Thay bằng comment giải thích:

```csharp
// Migration KHÔNG chạy ở startup nữa. Từ Task 13 trở đi, schema do một ECS
// task riêng chạy EF Core migration bundle dựng lên, và pipeline chỉ deploy
// khi task đó exit 0. Lý do: migration ở startup không ai gate được, fail thì
// container crash-loop, và nhiều task cùng lên sẽ race trên bảng
// __EFMigrationsHistory.
// Xem docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md
```

Giữ nguyên khối seed role `Technician` ngay bên dưới — nó idempotent và chạy sau khi schema đã có.

- [ ] **Step 8: Xoá static key khỏi `appsettings.json`**

Trong `src/API/appsettings.json`, sửa khối `AwsSettings` thành:

```json
  "AwsSettings": {
    "BucketName": "hushstore-public-assets",
    "Region": "ap-southeast-1"
  },
```

- [ ] **Step 9: Xoá static key khỏi `.env.example`**

Xoá 3 dòng này khỏi `.env.example`:

```bash
# ---- AWS S3 (image storage) ----
AwsSettings__AccessKeyId=<YOUR_IAM_ACCESS_KEY_ID>
AwsSettings__SecretAccessKey=<YOUR_IAM_SECRET_ACCESS_KEY>
```

Thay bằng:

```bash
# ---- AWS S3 (image storage) ----
# KHÔNG còn access key. Trên ECS, credential lấy tự động từ task role.
# Local dev: export AWS_PROFILE=hushstore trước khi chạy API.
```

- [ ] **Step 10: Bỏ dòng loại trừ `src/Client/` khỏi `.dockerignore`**

Xoá 2 dòng này khỏi `.dockerignore`:

```
# Client (not built in API image)
src/Client/
```

- [ ] **Step 11: Build và xác nhận không còn tham chiếu static credential nào**

```bash
dotnet build src/API/API.csproj
grep -rn "BasicAWSCredentials\|AccessKeyId\|SecretAccessKey" src/ --include=*.cs --include=*.json
```

Expected: `Build succeeded`. `grep` **không in gì cả**.

- [ ] **Step 12: Chạy API local và verify `/health/ready` trả 200 khi DB sống**

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HushStoreDB;User Id=sa;Password=$(grep SA_PASSWORD Infrastructure/db/.env | cut -d= -f2);TrustServerCertificate=True;"
export JwtSettings__SecretKey="$(openssl rand -base64 48)"
export AWS_PROFILE=hushstore
dotnet run --project src/API/API.csproj &
API_PID=$!
sleep 25
curl -s -o /dev/null -w "health/live=%{http_code}\n" http://localhost:5111/health/live
curl -s -o /dev/null -w "health/ready=%{http_code}\n" http://localhost:5111/health/ready
```

Expected: `health/live=200` và `health/ready=200`.

> Database `HushStoreDB` chưa có schema nên `AddDbContextCheck` vẫn trả Healthy — nó chỉ kiểm tra `CanConnectAsync()`, đúng như ta cần cho ALB health check.

- [ ] **Step 13: Verify `/health/ready` trả 503 khi DB chết — đây là bài test quan trọng nhất của task**

```bash
docker stop hushstore_sqlserver_dev
sleep 5
curl -s -o /dev/null -w "health/live=%{http_code}\n" http://localhost:5111/health/live
curl -s -o /dev/null -w "health/ready=%{http_code}\n" http://localhost:5111/health/ready
```

Expected: `health/live=200` (process vẫn sống) và **`health/ready=503`** (DB chết). Đây chính là hành vi mà `/health` cũ không có — nó luôn trả 200 kể cả khi RDS chết, khiến ALB giữ nguyên một instance không dùng được trong target group.

- [ ] **Step 14: Dọn môi trường test**

```bash
kill $API_PID
docker start hushstore_sqlserver_dev
unset ConnectionStrings__DefaultConnection JwtSettings__SecretKey
```

- [ ] **Step 15: Build image API và push lên ECR**

```bash
cd infra/tf/envs/prod
ECR_API=$(terraform output -json ecr_urls | jq -r .api)
REGISTRY="${ECR_API%%/*}"
cd "$(git rev-parse --show-toplevel)"
SHA=$(git rev-parse HEAD)

aws ecr get-login-password --region ap-southeast-1 --profile hushstore \
  | docker login --username AWS --password-stdin "$REGISTRY"

docker build -t "${ECR_API}:${SHA}" -f Dockerfile .
docker push "${ECR_API}:${SHA}"
echo "Đã push: ${ECR_API}:${SHA}"
```

Expected: `docker push` hoàn tất, in ra digest. Ghi lại `${SHA}` — Task 9, 10 và 13 dùng lại nó.

> `SHA` ở đây là commit HEAD **trước** khi commit task này. Sau Step 17 hãy chạy lại Step 15 với SHA mới nếu muốn image khớp đúng commit — hoặc để Task 13 build lại. Phase 2 sẽ tự động hoá hoàn toàn việc này.

- [ ] **Step 16: Verify image nằm trên ECR**

```bash
aws ecr describe-images --repository-name hushstore-api \
  --query 'imageDetails[].{Tags:imageTags,Size:imageSizeInBytes,Pushed:imagePushedAt}' \
  --output table --profile hushstore --no-cli-pager
```

Expected: 1 dòng với tag là SHA vừa push.

- [ ] **Step 17: Commit**

```bash
git add src/API/Program.cs src/API/API.csproj src/API/appsettings.json .env.example .dockerignore
git commit -m "refactor(api): health check cham DB, ForwardedHeaders, bo static IAM key, bo MigrateAsync

1. /health/ready chay AddDbContextCheck nen tra 503 khi RDS chet — /health cu
   luon tra 200, khien ALB giu nguyen instance khong dung duoc trong target
   group. Them /health/live cho liveness, giu /health cu de tuong thich.

2. UseForwardedHeaders doc X-Forwarded-For/Proto tu ALB, dat truoc moi
   middleware doc scheme. Tat UseHttpsRedirection o Production vi ALB da
   redirect 80->443 o tang listener — bat ca hai gay redirect loop.
   KnownNetworks/KnownProxies phai clear vi bridge network mode lam app thay
   source IP la docker bridge gateway chu khong phai IP ALB trong VPC.

3. Bo BasicAWSCredentials, dung default credential chain -> ECS task role.
   Sau thay doi nay trong toan he thong khong con static access key nao.

4. Bo MigrateAsync() khoi startup. Schema se do mot ECS task rieng chay EF
   Core migration bundle dung len, pipeline chi deploy khi task do exit 0.

Bo dong src/Client/ khoi .dockerignore de Task 9 build duoc image web tu cung
build context."
```

---

### Task 9: Dockerize Blazor client thành image `hushstore-web`

**Files:**
- Create: `src/Client/Dockerfile`
- Create: `src/Client/nginx.conf`

**Interfaces:**
- Consumes: `module.storage.ecr_web_url` (Task 6); `.dockerignore` đã bỏ dòng `src/Client/` (Task 8).
- Produces: image `<ecr_web_url>:<git-sha>` — nginx nghe port 80, serve Blazor WASM bundle bake sẵn trong image, SPA fallback về `index.html`. **Không có block SSL, không có block proxy** — TLS do ALB terminate, và request tới `api.hushstore.io.vn` do ALB route thẳng sang `tg-api`.

> `Client.csproj` chỉ tham chiếu `Shared.csproj`, và `Shared.csproj` không tham chiếu project nào — nên Dockerfile chỉ cần copy 2 file `.csproj`.
>
> `ApiBaseUrl` bị bake vào bundle lúc build (đây là bản chất của Blazor WASM standalone), nên nó là `ARG` của Docker. Phase 2 sẽ truyền `--build-arg` theo môi trường.

- [ ] **Step 1: Viết `src/Client/nginx.conf`**

```nginx
# nginx trong image hushstore-web. Chỉ serve static file.
#
# KHÔNG có block ssl_certificate: TLS do ALB terminate bằng ACM cert.
# KHÔNG có block proxy_pass: request tới api.hushstore.io.vn được ALB route
# thẳng sang target group tg-api (container API port 8080), không đi qua nginx.
server {
    listen 80;
    server_name _;

    root  /usr/share/nginx/html;
    index index.html;

    # Blazor publish sẵn file .gz và .br. gzip_static để nginx trả bản nén
    # thay vì nén lại mỗi request — bundle WASM khá lớn nên khác biệt rõ.
    gzip_static on;

    # SPA fallback: mọi route không khớp file đều trả index.html để Blazor
    # router xử lý phía client.
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Asset có hash trong tên nên cache vĩnh viễn được.
    location ~* \.(js|css|wasm|dat|woff2?|png|jpg|jpeg|ico|svg)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # index.html KHÔNG được cache, nếu không người dùng sẽ dính bundle cũ sau
    # mỗi lần deploy.
    location = /index.html {
        expires -1;
        add_header Cache-Control "no-store, no-cache, must-revalidate";
    }

    # Endpoint để ALB tg-web health check. Không ghi access log cho khoẻ.
    location = /healthz {
        access_log off;
        return 200 "ok\n";
        add_header Content-Type text/plain;
    }
}
```

- [ ] **Step 2: Viết `src/Client/Dockerfile`**

```dockerfile
# Build context là REPO ROOT, không phải src/Client.
#   docker build -f src/Client/Dockerfile -t hushstore-web:tag .

# ─── Stage 1: publish Blazor WASM ────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Client chỉ tham chiếu Shared, Shared không tham chiếu project nào.
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Client/Client.csproj src/Client/
RUN dotnet restore src/Client/Client.csproj

COPY src/Shared/ src/Shared/
COPY src/Client/ src/Client/

# ApiBaseUrl bị bake vào bundle lúc build — đó là bản chất của Blazor WASM
# standalone, không đọc được biến môi trường lúc runtime.
ARG API_BASE_URL=https://api.hushstore.io.vn
RUN sed -i "s|\"ApiBaseUrl\": *\"[^\"]*\"|\"ApiBaseUrl\": \"${API_BASE_URL}\"|" \
        src/Client/wwwroot/appsettings.json \
    && grep ApiBaseUrl src/Client/wwwroot/appsettings.json

RUN dotnet publish src/Client/Client.csproj -c Release -o /app/publish --no-restore

# ─── Stage 2: nginx serve static ─────────────────────────────────
FROM nginx:alpine AS runtime

COPY src/Client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80
```

- [ ] **Step 3: Build image và xác nhận `ApiBaseUrl` được thay đúng**

```bash
cd "$(git rev-parse --show-toplevel)"
docker build -f src/Client/Dockerfile -t hushstore-web:local .
```

Expected: build thành công, và trong log của layer `grep ApiBaseUrl` in ra `"ApiBaseUrl": "https://api.hushstore.io.vn"`.

- [ ] **Step 4: Chạy container local và verify nginx serve được bundle**

```bash
docker run -d --name web-test -p 8081:80 hushstore-web:local
sleep 3
curl -s -o /dev/null -w "index=%{http_code}\n"        http://localhost:8081/
curl -s -o /dev/null -w "healthz=%{http_code}\n"      http://localhost:8081/healthz
curl -s -o /dev/null -w "spa-fallback=%{http_code}\n" http://localhost:8081/san-pham/abc
curl -s http://localhost:8081/ | grep -o 'blazor[^"]*\.js' | head -1
```

Expected: `index=200`, `healthz=200`, `spa-fallback=200` (SPA fallback hoạt động — route không tồn tại vẫn trả `index.html`), và grep in ra tên file loader của Blazor.

- [ ] **Step 5: Verify `ApiBaseUrl` trong bundle đã publish là URL production**

```bash
docker exec web-test cat /usr/share/nginx/html/appsettings.json
```

Expected: `{"ApiBaseUrl": "https://api.hushstore.io.vn"}`. Nếu vẫn là `localhost`, dòng `sed` trong Dockerfile không khớp — kiểm tra lại định dạng thật của file bằng `cat src/Client/wwwroot/appsettings.json`.

- [ ] **Step 6: Verify `gzip_static` hoạt động (module có sẵn trong nginx:alpine)**

```bash
docker logs web-test 2>&1 | grep -i "emerg\|unknown directive" || echo "OK: nginx khởi động sạch, gzip_static được hỗ trợ"
WASM=$(docker exec web-test sh -c 'ls /usr/share/nginx/html/_framework/*.wasm 2>/dev/null | head -1 | xargs basename')
curl -s -H "Accept-Encoding: gzip" -o /dev/null -w "wasm-encoding=%{content_type} %{size_download}\n" \
  "http://localhost:8081/_framework/${WASM}"
```

Expected: in `OK: nginx khởi động sạch, gzip_static được hỗ trợ`. Nếu thấy `unknown directive "gzip_static"`, bỏ dòng đó khỏi `nginx.conf` và build lại — bundle vẫn chạy, chỉ là không trả bản nén sẵn.

- [ ] **Step 7: Dọn container test**

```bash
docker rm -f web-test
```

- [ ] **Step 8: Push image lên ECR**

```bash
cd infra/tf/envs/prod
ECR_WEB=$(terraform output -json ecr_urls | jq -r .web)
REGISTRY="${ECR_WEB%%/*}"
cd "$(git rev-parse --show-toplevel)"
SHA=$(git rev-parse HEAD)

aws ecr get-login-password --region ap-southeast-1 --profile hushstore \
  | docker login --username AWS --password-stdin "$REGISTRY"

docker tag hushstore-web:local "${ECR_WEB}:${SHA}"
docker push "${ECR_WEB}:${SHA}"
echo "Đã push: ${ECR_WEB}:${SHA}"
```

Expected: push hoàn tất.

- [ ] **Step 9: Commit**

```bash
git add src/Client/Dockerfile src/Client/nginx.conf
git commit -m "feat(client): dockerize Blazor WASM thanh image nginx:alpine

WASM bundle bake san trong image — bo han buoc rsync/s3 sync wwwroot va bind
mount tren host. nginx chi serve static: khong co block ssl (ALB terminate TLS
bang ACM cert), khong co block proxy (ALB route api.hushstore.io.vn thang sang
tg-api).

index.html dat no-store de nguoi dung khong dinh bundle cu sau deploy; asset co
hash trong ten thi cache 1 nam immutable. Them /healthz cho ALB tg-web.

ApiBaseUrl la ARG cua Docker vi Blazor WASM standalone bake config luc build,
khong doc duoc bien moi truong luc runtime."
```

---

### Task 10: Image `hushstore-migrator` — EF Core migration bundle

**Files:**
- Create: `src/Infrastructure/Dockerfile.migrator`

**Interfaces:**
- Consumes: `module.storage.ecr_migrator_url` (Task 6); `Program.cs` đã bỏ `MigrateAsync()` (Task 8).
- Produces: image `<ecr_migrator_url>:<git-sha>` — entrypoint là `efbundle` self-contained, đọc connection string từ biến môi trường `ConnectionStrings__DefaultConnection`, chạy xong thoát với exit code 0 (thành công) hoặc khác 0 (thất bại). Đây là gate của pipeline ở Phase 2.

> **Hai điều dễ sai ở task này:**
> 1. `efbundle` chạy lại entry point của startup project để dựng `DbContext`. `Program.cs` **throw nếu `JwtSettings:SecretKey` chưa đặt**, và dòng đó nằm trước `builder.Build()` — nên container migrator **bắt buộc phải được inject cả `JwtSettings__SecretKey`**, không chỉ connection string. Task 13 sẽ inject cả hai.
> 2. Dùng `--self-contained -r linux-x64` nên image runtime là `runtime-deps` (không cần .NET runtime hay ASP.NET runtime cài sẵn). Nếu bỏ `--self-contained` thì phải đổi base image sang `aspnet:10.0`, vì bundle sinh từ startup project ASP.NET Core sẽ tham chiếu assembly của ASP.NET.

- [ ] **Step 1: Viết `src/Infrastructure/Dockerfile.migrator`**

```dockerfile
# Build context là REPO ROOT.
#   docker build -f src/Infrastructure/Dockerfile.migrator -t hushstore-migrator:tag .
#
# Sinh ra một executable độc lập chứa toàn bộ migration của EF Core. Chạy nó
# là áp dụng migration; exit code 0 = thành công. Pipeline dùng exit code này
# làm gate: migration fail thì KHÔNG deploy, app cũ vẫn phục vụ.

# ─── Stage 1: sinh migration bundle ──────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="/root/.dotnet/tools:${PATH}"

COPY src/Shared/Shared.csproj src/Shared/
COPY src/Core/Core.csproj src/Core/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Service/Service.csproj src/Service/
COPY src/API/API.csproj src/API/
RUN dotnet restore src/API/API.csproj

COPY src/Shared/ src/Shared/
COPY src/Core/ src/Core/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Service/ src/Service/
COPY src/API/ src/API/

# --self-contained nên image runtime chỉ cần runtime-deps.
RUN dotnet ef migrations bundle \
        --project src/Infrastructure/Infrastructure.csproj \
        --startup-project src/API/API.csproj \
        --configuration Release \
        --self-contained -r linux-x64 \
        --force \
        -o /app/efbundle

# ─── Stage 2: runtime tối giản ───────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/efbundle .
RUN chmod +x ./efbundle

# efbundle chạy lại entry point của API để dựng DbContext, nên nó đọc cấu hình
# theo đúng cơ chế của app: biến môi trường ConnectionStrings__DefaultConnection.
# LƯU Ý: Program.cs throw nếu JwtSettings__SecretKey chưa đặt (dòng đó nằm
# trước builder.Build()), nên task definition PHẢI inject cả hai biến.
ENTRYPOINT ["./efbundle", "--verbose"]
```

- [ ] **Step 2: Build image**

```bash
cd "$(git rev-parse --show-toplevel)"
docker build -f src/Infrastructure/Dockerfile.migrator -t hushstore-migrator:local .
```

Expected: build thành công. Log có dòng `Build bundle succeeded` hoặc `Building bundle...` từ `dotnet ef`.

- [ ] **Step 3: Verify bundle chạy được và áp dụng migration lên SQL Server local**

```bash
docker ps --filter name=hushstore_sqlserver_dev --format '{{.Status}}'
SA_PW=$(grep SA_PASSWORD Infrastructure/db/.env | cut -d= -f2)

docker run --rm --network host \
  -e ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HushStoreMigTest;User Id=sa;Password=${SA_PW};TrustServerCertificate=True;" \
  -e JwtSettings__SecretKey="$(openssl rand -base64 48)" \
  hushstore-migrator:local
echo "exit code = $?"
```

Expected: log in ra danh sách migration được áp dụng, kết thúc bằng `Applying migration ...` / `Done.` và **`exit code = 0`**. Nếu lỗi `JwtSettings:SecretKey chưa được cấu hình`, nghĩa là biến môi trường thứ hai bị thiếu — đúng như ghi chú đầu task.

> `--network host` chỉ hoạt động trên Linux. Trên macOS dùng `Server=host.docker.internal,1433` và bỏ `--network host`.

- [ ] **Step 4: Verify schema thật đã được tạo trong DB**

```bash
docker exec hushstore_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PW" -C -d HushStoreMigTest \
  -Q "SELECT COUNT(*) AS MigrationsApplied FROM __EFMigrationsHistory;"
```

Expected: `MigrationsApplied` = `20` (số migration hiện có trong `src/Infrastructure/Migrations/`).

> Nếu `mssql-tools18` không có trong image, thử `/opt/mssql-tools/bin/sqlcmd` và bỏ flag `-C`.

- [ ] **Step 5: Verify bundle idempotent — chạy lần hai không lỗi**

```bash
docker run --rm --network host \
  -e ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HushStoreMigTest;User Id=sa;Password=${SA_PW};TrustServerCertificate=True;" \
  -e JwtSettings__SecretKey="$(openssl rand -base64 48)" \
  hushstore-migrator:local
echo "exit code lần 2 = $?"
```

Expected: `exit code lần 2 = 0`, log nói `No migrations were applied. The database is already up to date.` Tính idempotent là điều kiện để pipeline chạy migrator ở **mọi** lần deploy mà không cần biết trước có migration mới hay không.

- [ ] **Step 6: Verify exit code khác 0 khi không kết nối được DB — đây là điều kiện để gate hoạt động**

```bash
docker run --rm \
  -e ConnectionStrings__DefaultConnection="Server=khong-ton-tai.invalid,1433;Database=X;User Id=sa;Password=x;TrustServerCertificate=True;" \
  -e JwtSettings__SecretKey="$(openssl rand -base64 48)" \
  hushstore-migrator:local
echo "exit code khi DB chet = $?"
```

Expected: **`exit code khi DB chet` khác 0**. Nếu ra 0, gate của pipeline sẽ vô dụng — dừng lại điều tra trước khi tiếp tục.

- [ ] **Step 7: Dọn DB test**

```bash
docker exec hushstore_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PW" -C \
  -Q "ALTER DATABASE HushStoreMigTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE HushStoreMigTest;"
```

- [ ] **Step 8: Push image lên ECR**

```bash
cd infra/tf/envs/prod
ECR_MIG=$(terraform output -json ecr_urls | jq -r .migrator)
REGISTRY="${ECR_MIG%%/*}"
cd "$(git rev-parse --show-toplevel)"
SHA=$(git rev-parse HEAD)

aws ecr get-login-password --region ap-southeast-1 --profile hushstore \
  | docker login --username AWS --password-stdin "$REGISTRY"

docker tag hushstore-migrator:local "${ECR_MIG}:${SHA}"
docker push "${ECR_MIG}:${SHA}"
echo "Đã push: ${ECR_MIG}:${SHA}"
```

- [ ] **Step 9: Commit**

```bash
git add src/Infrastructure/Dockerfile.migrator
git commit -m "feat(infra): image migrator chay EF Core migration bundle

dotnet ef migrations bundle dong goi 20 migration thanh mot executable
self-contained (--self-contained -r linux-x64 nen base image chi can
runtime-deps). Exit code 0 = thanh cong; day la gate cua pipeline o Phase 2:
migration fail thi KHONG deploy, app cu van phuc vu.

Da verify local: ap dung du 20 migration len DB rong, idempotent khi chay lai
lan hai (No migrations were applied), va tra exit code khac 0 khi khong ket noi
duoc DB — dieu kien de gate hoat dong.

LUU Y: efbundle chay lai entry point cua API de dung DbContext, ma Program.cs
throw neu JwtSettings:SecretKey chua dat. Nen task definition phai inject CA
HAI bien ConnectionStrings__DefaultConnection va JwtSettings__SecretKey."
```

---

### Task 11: Module `ecs` — 3 IAM role tách phạm vi

**Files:**
- Create: `infra/tf/modules/ecs/versions.tf`
- Create: `infra/tf/modules/ecs/variables.tf`
- Create: `infra/tf/modules/ecs/iam.tf`
- Create: `infra/tf/modules/ecs/outputs.tf`
- Create: `infra/tf/modules/ecs/tests/iam.tftest.hcl`
- Modify: `infra/tf/envs/prod/main.tf` (thêm `module "ecs"`)

**Interfaces:**
- Consumes: `module.storage.{assets_bucket_arn, artifacts_bucket_arn}`, `module.data.{ssm_connection_string_arn, ssm_jwt_secret_arn}`.
- Produces — outputs của `module.ecs` mà Task 12/13 dùng:
  - `instance_profile_name` (string) — gắn vào launch template
  - `task_execution_role_arn` (string) — `execution_role_arn` của task definition
  - `task_app_role_arn` (string) — `task_role_arn` của task definition API
  - `task_migrator_role_arn` (string) — `task_role_arn` của task definition migrator

> **Đây là điểm least-privilege mạnh nhất của toàn thiết kế.** ECS cho phép tách 3 phạm vi mà một EC2 instance profile đơn lẻ không tách được:
>
> | Role | Ai dùng | Được làm gì |
> |---|---|---|
> | `role-container-instance` | EC2 host | Chỉ đăng ký vào ECS cluster + SSM Session Manager. **Không có quyền S3, không có quyền đọc secret nào.** |
> | `role-task-execution` | ECS agent, lúc khởi task | Pull ECR, ghi CloudWatch Logs, đọc 2 SSM parameter cụ thể. Hết. |
> | `role-task-app` | Container API, lúc runtime | **Chỉ** `s3:PutObject`/`GetObject` trên bucket ảnh + `ssmmessages` cho ECS Exec. |
>
> Nếu container API bị chiếm quyền, kẻ tấn công chỉ ghi được ảnh vào một bucket — không đọc được secret, không chạm được RDS API, không sờ được ECR. Kịch bản kiểm thử số 10 chứng minh điều này.

- [ ] **Step 1: Viết test trước — `infra/tf/modules/ecs/tests/iam.tftest.hcl`**

```hcl
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
}

run "container_instance_role_khong_co_quyen_s3_hay_secret" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.instance_extra.json).Statement :
      !anytrue([for a in s.Action : startswith(a, "s3:")])
    ])
    error_message = "Role của EC2 host KHÔNG được có quyền S3 — S3 là việc của task role, không phải của host."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.instance_extra.json).Statement :
      !anytrue([for a in s.Action : startswith(a, "ssm:GetParameter")])
    ])
    error_message = "Role của EC2 host KHÔNG được đọc SSM parameter — secret chỉ do ECS agent inject qua task execution role."
  }
}

run "task_app_role_chi_duoc_s3_tren_dung_bucket_anh" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.task_app.json).Statement :
      alltrue([
        for a in s.Action :
        startswith(a, "s3:") || startswith(a, "ssmmessages:")
      ])
    ])
    error_message = "Task role của app chỉ được có action s3:* và ssmmessages:* — không rds, không ecr, không ssm:GetParameter."
  }

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.task_app.json).Statement :
      contains(try(s.Resource, []), "arn:aws:s3:::hushstore-public-assets/*")
    ])
    error_message = "Quyền S3 phải giới hạn đúng vào object của bucket ảnh sản phẩm, không dùng Resource = *."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.task_app.json).Statement :
      !contains(try(s.Resource, []), "*") || alltrue([for a in s.Action : startswith(a, "ssmmessages:")])
    ])
    error_message = "Chỉ ssmmessages (ECS Exec) được dùng Resource = *; quyền S3 phải giới hạn theo ARN."
  }
}

run "task_execution_role_chi_doc_dung_2_parameter_khong_dung_wildcard_toan_bo" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.task_execution_extra.json).Statement :
      !contains(try(s.Resource, []), "arn:aws:ssm:*:*:parameter/*")
    ])
    error_message = "Task execution role không được đọc toàn bộ Parameter Store — phải liệt kê đúng ARN của 2 parameter cần dùng."
  }

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.task_execution_extra.json).Statement :
      contains(try(s.Resource, []), var.ssm_connection_string_arn)
    ])
    error_message = "Task execution role phải đọc được parameter connection-string để inject vào container."
  }
}

run "moi_role_chi_cho_dung_service_principal_duoc_assume" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.ec2_assume.json).Statement :
      contains(s.Principal.Service, "ec2.amazonaws.com")
    ])
    error_message = "Trust policy của instance role chỉ cho ec2.amazonaws.com assume."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.ecs_tasks_assume.json).Statement :
      contains(s.Principal.Service, "ecs-tasks.amazonaws.com")
    ])
    error_message = "Trust policy của task role chỉ cho ecs-tasks.amazonaws.com assume."
  }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/ecs
terraform init
terraform test
```

Expected: FAIL với `Reference to undeclared ... data.aws_iam_policy_document`.

- [ ] **Step 3: Viết `infra/tf/modules/ecs/variables.tf`**

```hcl
variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "assets_bucket_arn" {
  description = "ARN bucket ảnh sản phẩm — task role của app chỉ được ghi/đọc object trong đây"
  type        = string
}

variable "artifacts_bucket_arn" {
  description = "ARN bucket artifacts — host đọc seed SQL và file ops từ đây"
  type        = string
}

variable "ssm_connection_string_arn" {
  description = "ARN parameter connection string — ECS agent inject vào container"
  type        = string
}

variable "ssm_jwt_secret_arn" {
  description = "ARN của parameter chứa JWT secret"
  type        = string
}
```

- [ ] **Step 4: Viết `infra/tf/modules/ecs/iam.tf`**

```hcl
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
```

- [ ] **Step 5: Viết `infra/tf/modules/ecs/outputs.tf`**

```hcl
output "instance_profile_name" {
  description = "Tên instance profile gắn vào launch template"
  value       = aws_iam_instance_profile.instance.name
}

output "instance_role_name" {
  description = "Tên IAM role của EC2 container instance"
  value       = aws_iam_role.instance.name
}

output "task_execution_role_arn" {
  description = "ARN role ECS agent dùng lúc khởi task (pull ECR, đọc secret, ghi log)"
  value       = aws_iam_role.task_execution.arn
}

output "task_app_role_arn" {
  description = "ARN role container API dùng lúc runtime (chỉ S3 + ECS Exec)"
  value       = aws_iam_role.task_app.arn
}

output "task_migrator_role_arn" {
  description = "ARN role container migrator (không có quyền AWS API nào)"
  value       = aws_iam_role.task_migrator.arn
}
```

- [ ] **Step 6: Chạy test để xác nhận nó pass**

```bash
cd infra/tf/modules/ecs
terraform init
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `4 passed, 0 failed.`

- [ ] **Step 7: Thêm `module "ecs"` vào `infra/tf/envs/prod/main.tf` rồi apply**

Thêm sau `module "data"`:

```hcl
module "ecs" {
  source = "../../modules/ecs"

  project              = local.name
  assets_bucket_arn    = module.storage.assets_bucket_arn
  artifacts_bucket_arn = module.storage.artifacts_bucket_arn

  ssm_connection_string_arn = module.data.ssm_connection_string_arn
  ssm_jwt_secret_arn        = module.data.ssm_jwt_secret_arn
}
```

```bash
cd infra/tf/envs/prod
terraform init
terraform apply
```

Expected: `Apply complete!` với 4 `aws_iam_role`, 1 `aws_iam_instance_profile`, 3 `aws_iam_role_policy`, 3 `aws_iam_role_policy_attachment` added.

- [ ] **Step 8: Verify role của app thật sự không chạm được RDS hay Parameter Store**

```bash
aws iam simulate-principal-policy \
  --policy-source-arn "arn:aws:iam::${ACCT}:role/hushstore-task-app-role" \
  --action-names rds:DescribeDBInstances ssm:GetParameter ecr:GetAuthorizationToken \
  --query 'EvaluationResults[].{Action:EvalActionName,Decision:EvalDecision}' \
  --output table --profile hushstore --no-cli-pager
```

Expected: cả 3 action đều `implicitDeny`. Đây là bằng chứng cho kịch bản kiểm thử số 10 — blast radius của container API chỉ gói trong một bucket S3.

- [ ] **Step 9: Verify role của app ĐƯỢC ghi vào bucket ảnh (không siết quá tay)**

```bash
aws iam simulate-principal-policy \
  --policy-source-arn "arn:aws:iam::${ACCT}:role/hushstore-task-app-role" \
  --action-names s3:PutObject \
  --resource-arns "arn:aws:s3:::hushstore-public-assets/products/test.jpg" \
  --query 'EvaluationResults[].{Action:EvalActionName,Decision:EvalDecision}' \
  --output table --profile hushstore --no-cli-pager
```

Expected: `s3:PutObject` → `allowed`. Nếu ra `implicitDeny`, upload ảnh sẽ chết ở Task 16 — sửa `resources` trong `data.aws_iam_policy_document.task_app` trước khi đi tiếp.

- [ ] **Step 10: Verify role của EC2 host không đọc được secret**

```bash
aws iam simulate-principal-policy \
  --policy-source-arn "arn:aws:iam::${ACCT}:role/hushstore-container-instance-role" \
  --action-names ssm:GetParameter s3:PutObject \
  --query 'EvaluationResults[].{Action:EvalActionName,Decision:EvalDecision}' \
  --output table --profile hushstore --no-cli-pager
```

Expected: cả hai `implicitDeny`. Host chỉ được đăng ký cluster, nhận lệnh SSM, và `GetObject` trên bucket artifacts.

- [ ] **Step 11: Commit**

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): 4 IAM role tach pham vi cho ECS

Day la diem least-privilege manh nhat cua thiet ke — ECS tach duoc 3 pham vi
ma mot EC2 instance profile don le khong tach duoc:

- container-instance-role: CHI dang ky ECS cluster + SSM Session Manager +
  GetObject bucket artifacts. Khong co quyen S3 anh, khong doc duoc secret nao.
- task-execution-role: pull ECR, ghi CloudWatch Logs, ssm:GetParameters tren
  DUNG 2 ARN parameter (khong wildcard toan Parameter Store), kms:Decrypt gioi
  han bang condition kms:ViaService.
- task-app-role: CHI s3:PutObject/GetObject/DeleteObject tren object cua bucket
  anh + ssmmessages cho ECS Exec. Day la thu thay the BasicAWSCredentials.
- task-migrator-role: rong, chi co trust policy — migrator chi can TCP 1433.

Da verify bang iam simulate-principal-policy: task-app-role bi implicitDeny
voi rds:DescribeDBInstances, ssm:GetParameter, ecr:GetAuthorizationToken nhung
duoc allowed voi s3:PutObject tren bucket anh. Bang chung cho kich ban kiem thu
so 10."
```

---

### Task 12: Module `ecs` — cluster, launch template, ASG, capacity provider

**Files:**
- Create: `infra/tf/modules/ecs/cluster.tf`
- Create: `infra/tf/modules/ecs/user_data.sh.tftpl`
- Create: `infra/tf/modules/ecs/tests/cluster.tftest.hcl`
- Modify: `infra/tf/modules/ecs/variables.tf`
- Modify: `infra/tf/modules/ecs/outputs.tf`
- Modify: `infra/tf/envs/prod/main.tf`
- Modify: `infra/tf/envs/prod/variables.tf` (thêm `instance_count`, `instance_type`)
- Modify: `infra/tf/envs/prod/outputs.tf`

**Interfaces:**
- Consumes: `module.network.app_subnet_ids`, `module.security.web_sg_id`, `instance_profile_name` (Task 11).
- Produces — outputs của `module.ecs`:
  - `cluster_name` (string) — `hushstore`
  - `cluster_arn` (string)
  - `asg_name` (string) — `hushstore-asg`, dùng cho `aws autoscaling set-desired-capacity`
  - `capacity_provider_name` (string) — dùng trong `capacity_provider_strategy` của service ở Task 15

> **Ba quyết định cần hiểu khi review:**
> 1. **`managed_scaling = DISABLED`, `managed_termination_protection = DISABLED`.** Với `max_size = 1` thì không có gì để ECS tự scale, và bật managed scaling sẽ khiến ECS tranh `desired_capacity` với Terraform. Termination protection bật sẽ làm capacity provider không xoá được → `nuke.sh` treo.
> 2. **Không có `key_name`.** Launch template cố tình không gắn SSH key pair nào. Vào host bằng SSM Session Manager.
> 3. **`http_tokens = "required"`** (IMDSv2 bắt buộc) — chặn lớp tấn công SSRF đọc credential từ metadata service bằng một request GET đơn giản.
>
> Task này là task ĐẦU TIÊN cần `enable_nat = true`: ECS agent phải gọi được ECS control plane và SSM endpoint để đăng ký.

- [ ] **Step 1: Viết test trước — `infra/tf/modules/ecs/tests/cluster.tftest.hcl`**

```hcl
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
```

- [ ] **Step 2: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/ecs
terraform test -filter=tests/cluster.tftest.hcl
```

Expected: FAIL với `Reference to undeclared resource "aws_ecs_cluster"`.

- [ ] **Step 3: Thêm biến vào `infra/tf/modules/ecs/variables.tf`**

```hcl
variable "app_subnet_ids" {
  description = "ID của 2 app subnet — nơi ASG đặt container instance"
  type        = list(string)
}

variable "web_sg_id" {
  description = "ID Security Group của container instance"
  type        = string
}

variable "instance_count" {
  description = "desired_capacity của ASG. 0 = tắt hoàn toàn (xoá cả EBS root)"
  type        = number
  default     = 0

  validation {
    condition     = var.instance_count >= 0 && var.instance_count <= 1
    error_message = "instance_count chỉ được 0 hoặc 1 — max_size của ASG cố định = 1."
  }
}

variable "instance_type" {
  description = "Instance type. t3.micro nằm trong free tier nhưng chỉ 1GB RAM (đã bù bằng 2GB swap)"
  type        = string
  default     = "t3.micro"
}

variable "root_volume_size" {
  description = "Dung lượng EBS root (GB). 30GB là mức free tier"
  type        = number
  default     = 30
}

variable "log_retention_days" {
  description = "Số ngày giữ log container trong CloudWatch"
  type        = number
  default     = 3
}
```

- [ ] **Step 4: Viết `infra/tf/modules/ecs/user_data.sh.tftpl`**

```bash
#!/bin/bash
set -euxo pipefail

# ─── SWAP ────────────────────────────────────────────────────────
# t3.micro chỉ có 1GB RAM. ECS agent (~100MB) + nginx (~15MB) + .NET API
# (~250MB) là chật. 2GB swap giữ cho instance không bị OOM-kill khi task
# migrator chạy chồng lên hai container kia.
# Phải làm TRƯỚC khi ECS agent khởi động để container dùng được swap.
if [ ! -f /swapfile ]; then
  dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
  sysctl -w vm.swappiness=60
fi

# ─── ĐĂNG KÝ VÀO ECS CLUSTER ─────────────────────────────────────
# Đây là toàn bộ việc user_data phải làm. Mọi thứ khác (nginx, WASM bundle,
# .NET runtime, secret) đã nằm trong image hoặc do ECS agent inject — nên
# instance có thể bị xoá và dựng lại bất cứ lúc nào mà không mất gì.
{
  echo "ECS_CLUSTER=${cluster_name}"
  echo "ECS_ENABLE_CONTAINER_METADATA=true"
  echo "ECS_ENABLE_SPOT_INSTANCE_DRAINING=false"
  echo "ECS_CONTAINER_STOP_TIMEOUT=30s"
} >> /etc/ecs/ecs.config

systemctl enable --now ecs || true
```

- [ ] **Step 5: Viết `infra/tf/modules/ecs/cluster.tf`**

```hcl
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
```

- [ ] **Step 6: Thêm output vào `infra/tf/modules/ecs/outputs.tf`**

```hcl
output "cluster_name" {
  description = "Tên ECS cluster"
  value       = aws_ecs_cluster.this.name
}

output "cluster_arn" {
  description = "ARN của ECS cluster"
  value       = aws_ecs_cluster.this.arn
}

output "asg_name" {
  description = "Tên ASG — dùng cho aws autoscaling set-desired-capacity khi bật/tắt"
  value       = aws_autoscaling_group.this.name
}

output "capacity_provider_name" {
  description = "Tên capacity provider — dùng trong capacity_provider_strategy của service"
  value       = aws_ecs_capacity_provider.this.name
}
```

- [ ] **Step 7: Chạy test để xác nhận nó pass**

```bash
cd infra/tf/modules/ecs
terraform init
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `9 passed, 0 failed.` (4 run của `iam.tftest.hcl` + 5 run của `cluster.tftest.hcl`).

- [ ] **Step 8: Thêm biến vào `infra/tf/envs/prod/variables.tf`**

```hcl
variable "instance_count" {
  description = "Số EC2 container instance (0 hoặc 1). 0 = tắt hoàn toàn, về $0"
  type        = number
  default     = 0
}

variable "instance_type" {
  description = "Instance type của container instance. t3.micro free tier, 1GB RAM"
  type        = string
  default     = "t3.micro"
}
```

- [ ] **Step 9: Cập nhật `module "ecs"` trong `infra/tf/envs/prod/main.tf`**

Thêm 4 dòng vào block `module "ecs"` đã có:

```hcl
  app_subnet_ids = module.network.app_subnet_ids
  web_sg_id      = module.security.web_sg_id
  instance_count = var.instance_count
  instance_type  = var.instance_type
```

- [ ] **Step 10: Bật NAT và instance rồi apply — đây là lần đầu tốn phí NAT Gateway**

```bash
cd infra/tf/envs/prod
sed -i '' 's|^enable_nat = .*|enable_nat = true|'      terraform.tfvars
sed -i '' 's|^instance_count = .*|instance_count = 1|' terraform.tfvars
grep -E 'enable_nat|instance_count' terraform.tfvars
terraform apply
```

Expected: `grep` in `enable_nat = true` và `instance_count = 1`. `apply` tạo NAT Gateway + EIP + route + cluster + launch template + ASG + capacity provider + 3 log group. Bước ASG mất ~2-3 phút để instance vào service.

> Từ giờ NAT Gateway đang tính $0.045/giờ. Nhớ đặt lại `false` khi nghỉ.

- [ ] **Step 11: Verify instance đã đăng ký vào cluster**

```bash
CLUSTER=$(terraform output -raw ecs_cluster_name 2>/dev/null || echo hushstore)
for i in 1 2 3 4 5 6; do
  N=$(aws ecs list-container-instances --cluster "$CLUSTER" \
        --query 'length(containerInstanceArns)' --output text \
        --profile hushstore --no-cli-pager)
  echo "lần $i: $N container instance"
  [ "$N" -ge 1 ] && break
  sleep 30
done
```

Expected: in ra `1 container instance` trong vòng 3 phút. Nếu sau 3 phút vẫn `0`, ECS agent không gọi được control plane — kiểm tra `enable_nat = true` đã apply và route `0.0.0.0/0` của private RT đã trỏ vào NAT Gateway.

- [ ] **Step 12: Verify instance không có public IP và có swap**

```bash
INSTANCE_ID=$(aws ecs list-container-instances --cluster "$CLUSTER" \
  --query 'containerInstanceArns[0]' --output text --profile hushstore --no-cli-pager \
  | xargs -I{} aws ecs describe-container-instances --cluster "$CLUSTER" \
      --container-instances {} --query 'containerInstances[0].ec2InstanceId' \
      --output text --profile hushstore --no-cli-pager)
echo "Instance: $INSTANCE_ID"

aws ec2 describe-instances --instance-ids "$INSTANCE_ID" \
  --query 'Reservations[0].Instances[0].{PublicIp:PublicIpAddress,PrivateIp:PrivateIpAddress,KeyName:KeyName}' \
  --profile hushstore --no-cli-pager
```

Expected: `PublicIp` là `null`, `PrivateIp` nằm trong `10.20.10.0/23` hoặc `10.20.11.0/23`, `KeyName` là `null` (không có SSH key nào).

- [ ] **Step 13: Verify vào được host bằng SSM Session Manager (không cần SSH)**

```bash
aws ssm start-session --target "$INSTANCE_ID" --profile hushstore
```

Trong session, chạy:

```bash
free -m | grep -i swap
sudo cat /etc/ecs/ecs.config
curl -s http://localhost:51678/v1/metadata | head -c 200
exit
```

Expected: `free -m` cho thấy dòng `Swap:` có tổng ~2048MB; `ecs.config` chứa `ECS_CLUSTER=hushstore`; endpoint metadata của ECS agent trả JSON có `"Cluster":"hushstore"`.

> Nếu `start-session` báo `TargetNotConnected`, đợi thêm 1-2 phút để SSM Agent đăng ký, và xác nhận `enable_nat = true`.

- [ ] **Step 14: Thêm output và commit**

Thêm vào `infra/tf/envs/prod/outputs.tf`:

```hcl
output "ecs_cluster_name" {
  description = "Tên ECS cluster"
  value       = module.ecs.cluster_name
}

output "asg_name" {
  description = "Tên ASG — dùng để bật/tắt instance tiết kiệm chi phí"
  value       = module.ecs.asg_name
}

output "capacity_provider_name" {
  description = "Tên ECS capacity provider — dùng cho aws ecs run-task"
  value       = module.ecs.capacity_provider_name
}
```

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): ECS cluster + launch template + ASG + capacity provider

AMI ECS-optimized AL2023 (co san ECS agent, Docker, SSM Agent) nen user_data
chi lam 2 viec: tao 2GB swap (t3.micro chi 1GB RAM, phai lam truoc khi ECS
agent len) va ghi ECS_CLUSTER vao /etc/ecs/ecs.config. Moi thu khac nam trong
image hoac do ECS agent inject — instance xoa va dung lai bat cu luc nao khong
mat gi.

Launch template CO TINH khong gan key_name: khong co SSH key pair nao ton tai
trong he thong. IMDSv2 bat buoc (http_tokens = required, hop_limit = 1) de chan
SSRF doc credential tu metadata service.

ASG min 0 / max 1: min 0 de down.sh ha ve 0 instance, xoa luon EBS root va ve
\$0 that. max 1 vi rate limiter la in-memory.

Capacity provider tat ca managed_scaling va managed_termination_protection —
bat termination protection se lam capacity provider khong xoa duoc va nuke.sh
treo vo han. Container Insights tat de khong ton phi custom metric."
```

---

### Task 13: Module `ecs` — 3 task definition + chạy migration dựng schema

**Files:**
- Create: `infra/tf/modules/ecs/taskdef.tf`
- Create: `infra/tf/modules/ecs/tests/taskdef.tftest.hcl`
- Modify: `infra/tf/modules/ecs/variables.tf`
- Modify: `infra/tf/modules/ecs/outputs.tf`
- Modify: `infra/tf/envs/prod/main.tf`
- Modify: `infra/tf/envs/prod/variables.tf` (thêm `image_tag`, `api_domain`, `web_domain`)
- Modify: `infra/tf/envs/prod/terraform.tfvars.example`

**Interfaces:**
- Consumes: `module.storage.{ecr_api_url, ecr_web_url, ecr_migrator_url, assets_bucket_name}`, `module.data.{ssm_connection_string_arn, ssm_jwt_secret_arn}`, các IAM role (Task 11), log group (Task 12), image tag đã push (Task 8/9/10).
- Produces:
  - outputs `taskdef_api_arn`, `taskdef_web_arn`, `taskdef_migrator_arn`, `taskdef_api_family`, `taskdef_web_family`, `taskdef_migrator_family`
  - **Schema database `HushStoreDB` đã được dựng trên RDS** — đây là deliverable thật của task, verify ở Step 12.

> Task definition migrator **phải inject cả `ConnectionStrings__DefaultConnection` và `JwtSettings__SecretKey`** — `efbundle` chạy lại entry point của API để dựng `DbContext`, và `Program.cs` throw nếu thiếu JWT secret (dòng đó nằm trước `builder.Build()`). Task 10 Step 3 đã chứng minh điều này bằng thực nghiệm.

- [ ] **Step 1: Viết test trước — `infra/tf/modules/ecs/tests/taskdef.tftest.hcl`**

```hcl
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
  ecr_api_url               = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-api"
  ecr_web_url               = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-web"
  ecr_migrator_url          = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-migrator"
  image_tag                 = "abc123def456"
  assets_bucket_name        = "hushstore-public-assets"
  allowed_origins           = "https://hushstore.io.vn"
}

run "tat_ca_task_def_dung_bridge_va_ec2_launch_type" {
  command = plan

  assert {
    condition = alltrue([
      aws_ecs_task_definition.api.network_mode == "bridge",
      aws_ecs_task_definition.web.network_mode == "bridge",
      aws_ecs_task_definition.migrator.network_mode == "bridge",
    ])
    error_message = "Phải dùng bridge network mode: awsvpc cấp 1 ENI cho mỗi task, t3.micro chỉ có 2 ENI nên không đủ cho 2 service."
  }

  assert {
    condition = alltrue([
      contains(aws_ecs_task_definition.api.requires_compatibilities, "EC2"),
      contains(aws_ecs_task_definition.web.requires_compatibilities, "EC2"),
      contains(aws_ecs_task_definition.migrator.requires_compatibilities, "EC2"),
    ])
    error_message = "Phải là EC2 launch type — đề bài yêu cầu triển khai website thông qua EC2 Instance, không phải Fargate."
  }
}

run "static_host_port_dung_80_va_8080_de_sg_web_giu_dung_2_rule" {
  command = plan

  assert {
    condition = anytrue([
      for c in jsondecode(aws_ecs_task_definition.web.container_definitions) :
      anytrue([for p in c.portMappings : p.hostPort == 80 && p.containerPort == 80])
    ])
    error_message = "Container web phải map static hostPort 80 — dynamic port mapping sẽ buộc mở dải 32768-65535 trên sg-web."
  }

  assert {
    condition = anytrue([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) :
      anytrue([for p in c.portMappings : p.hostPort == 8080 && p.containerPort == 8080])
    ])
    error_message = "Container API phải map static hostPort 8080."
  }
}

run "khong_container_nao_nhan_secret_qua_environment" {
  command = plan

  assert {
    condition = alltrue([
      for c in concat(
        jsondecode(aws_ecs_task_definition.api.container_definitions),
        jsondecode(aws_ecs_task_definition.web.container_definitions),
        jsondecode(aws_ecs_task_definition.migrator.container_definitions),
        ) : alltrue([
        for e in try(c.environment, []) :
        !anytrue([
          for kw in ["Password", "SecretKey", "ConnectionStrings"] : strcontains(e.name, kw)
        ])
      ])
    ])
    error_message = "Secret KHÔNG được truyền qua environment (hiện trong describe-task-definition) — phải dùng khối secrets với valueFrom."
  }
}

run "migrator_nhan_ca_connection_string_va_jwt_secret" {
  command = plan

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.migrator.container_definitions) :
      length([for s in c.secrets : s.name]) == 2
    ])
    error_message = "Migrator phải nhận ĐÚNG 2 secret."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.migrator.container_definitions) :
      contains([for s in c.secrets : s.name], "JwtSettings__SecretKey")
    ])
    error_message = "Migrator PHẢI có JwtSettings__SecretKey: efbundle chạy lại entry point của API, và Program.cs throw nếu thiếu nó (dòng đó nằm trước builder.Build())."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.migrator.container_definitions) :
      contains([for s in c.secrets : s.name], "ConnectionStrings__DefaultConnection")
    ])
    error_message = "Migrator phải có ConnectionStrings__DefaultConnection."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.migrator.container_definitions) :
      length(try(c.portMappings, [])) == 0
    ])
    error_message = "Migrator không được map port nào — nó là one-off task, không phục vụ request."
  }
}

run "image_tag_khong_bao_gio_la_latest" {
  command = plan

  assert {
    condition = alltrue([
      for c in concat(
        jsondecode(aws_ecs_task_definition.api.container_definitions),
        jsondecode(aws_ecs_task_definition.web.container_definitions),
        jsondecode(aws_ecs_task_definition.migrator.container_definitions),
      ) : !endswith(c.image, ":latest")
    ])
    error_message = "Image tag phải là git SHA, không được dùng :latest — nếu không thì rollback về revision cũ sẽ không đáng tin."
  }
}

run "api_bat_ecs_exec_va_co_task_role_rieng" {
  command = plan

  # KHÔNG so task_role_arn với aws_iam_role.x.arn — cả hai là (known after
  # apply). Thay bằng tính chất biết-ở-plan-time và có giá trị bảo mật thật:
  # container web KHÔNG được gán task role nào cả.
  assert {
    condition     = aws_ecs_task_definition.web.task_role_arn == null || aws_ecs_task_definition.web.task_role_arn == ""
    error_message = "Task definition web KHÔNG được có task_role_arn — nginx serve static file, không gọi AWS API nào."
  }

  assert {
    condition = alltrue([
      for c in jsondecode(aws_ecs_task_definition.api.container_definitions) :
      try(c.linuxParameters.initProcessEnabled, false) == true
    ])
    error_message = "Phải bật initProcessEnabled trên container API để ECS Exec hoạt động (kịch bản kiểm thử số 10)."
  }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/ecs
terraform test -filter=tests/taskdef.tftest.hcl
```

Expected: FAIL với `Reference to undeclared resource "aws_ecs_task_definition"`.

- [ ] **Step 3: Thêm biến vào `infra/tf/modules/ecs/variables.tf`**

```hcl
variable "ecr_api_url" {
  description = "URL repository ECR của image API"
  type        = string
}

variable "ecr_web_url" {
  description = "URL repository ECR của image web"
  type        = string
}

variable "ecr_migrator_url" {
  description = "URL repository ECR của image migrator"
  type        = string
}

variable "image_tag" {
  description = "Tag của cả 3 image — LUÔN là git SHA đầy đủ, không bao giờ dùng latest"
  type        = string

  validation {
    condition     = var.image_tag != "latest"
    error_message = "image_tag không được là 'latest' — rollback về task definition revision cũ chỉ đáng tin khi tag immutable."
  }
}

variable "assets_bucket_name" {
  description = "Tên bucket ảnh sản phẩm — truyền vào container API qua AwsSettings__BucketName"
  type        = string
}

variable "allowed_origins" {
  description = "Origin được CORS cho phép, phân cách bằng dấu phẩy"
  type        = string
}

variable "api_memory_hard" {
  description = "Giới hạn cứng RAM (MiB) của container API"
  type        = number
  default     = 512
}

variable "api_memory_reservation" {
  description = "RAM (MiB) đặt trước cho container API. Dùng soft limit để không bị OOM-kill sớm trên t3.micro"
  type        = number
  default     = 384
}
```

- [ ] **Step 4: Viết `infra/tf/modules/ecs/taskdef.tf`**

```hcl
locals {
  aws_region = data.aws_region.current.region

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

  container_definitions = jsonencode([
    {
      name      = "migrator"
      image     = "${var.ecr_migrator_url}:${var.image_tag}"
      essential = true

      memory            = 512
      memoryReservation = 256

      # KHÔNG map port: đây là one-off task, chạy rồi thoát.
      portMappings = []

      # Cả HAI secret đều bắt buộc. efbundle chạy lại entry point của API để
      # dựng DbContext, và Program.cs throw nếu JwtSettings:SecretKey chưa đặt
      # — dòng đó nằm trước builder.Build(). Task 10 Step 3 đã chứng minh.
      secrets = local.app_secrets

      logConfiguration = local.log_config.migrator
    }
  ])

  tags = { Name = "${var.project}-migrator" }
}
```

- [ ] **Step 5: Thêm output vào `infra/tf/modules/ecs/outputs.tf`**

```hcl
output "taskdef_api_arn" {
  description = "ARN revision hiện tại của task definition API"
  value       = aws_ecs_task_definition.api.arn
}

output "taskdef_web_arn" {
  description = "ARN revision hiện tại của task definition web"
  value       = aws_ecs_task_definition.web.arn
}

output "taskdef_migrator_arn" {
  description = "ARN revision hiện tại của task definition migrator"
  value       = aws_ecs_task_definition.migrator.arn
}

output "taskdef_api_family" {
  description = "Family của task definition API — dùng cho ecs update-service"
  value       = aws_ecs_task_definition.api.family
}

output "taskdef_web_family" {
  description = "Family của task definition web"
  value       = aws_ecs_task_definition.web.family
}

output "taskdef_migrator_family" {
  description = "Family của task definition migrator — dùng cho ecs run-task"
  value       = aws_ecs_task_definition.migrator.family
}
```

- [ ] **Step 6: Chạy test để xác nhận nó pass**

```bash
cd infra/tf/modules/ecs
terraform init
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `15 passed, 0 failed.` (4 iam + 5 cluster + 6 taskdef).

- [ ] **Step 7: Thêm biến vào `infra/tf/envs/prod/variables.tf`**

```hcl
variable "image_tag" {
  description = "Git SHA của 3 image trên ECR. Lấy bằng: git rev-parse HEAD"
  type        = string
}

variable "web_domain" {
  description = "Domain của Blazor client"
  type        = string
  default     = "hushstore.io.vn"
}

variable "api_domain" {
  description = "Domain của API"
  type        = string
  default     = "api.hushstore.io.vn"
}
```

Thêm vào `infra/tf/envs/prod/terraform.tfvars.example`:

```hcl
# Git SHA của 3 image đã push lên ECR. Lấy bằng: git rev-parse HEAD
# Phase 2 sẽ truyền giá trị này tự động từ GitHub Actions.
image_tag = "0000000000000000000000000000000000000000"
```

- [ ] **Step 8: Cập nhật `module "ecs"` trong `infra/tf/envs/prod/main.tf`**

Thêm vào block `module "ecs"` đã có:

```hcl
  ecr_api_url        = module.storage.ecr_api_url
  ecr_web_url        = module.storage.ecr_web_url
  ecr_migrator_url   = module.storage.ecr_migrator_url
  image_tag          = var.image_tag
  assets_bucket_name = module.storage.assets_bucket_name
  allowed_origins    = "https://${var.web_domain}"
```

- [ ] **Step 9: Đặt `image_tag` bằng SHA của image đã push và apply**

```bash
cd infra/tf/envs/prod
# Dùng đúng SHA đã push ở Task 8/9/10. Nếu đã commit thêm từ lúc đó, lấy SHA
# của các image thật đang có trên ECR:
TAG=$(aws ecr describe-images --repository-name hushstore-api \
  --query 'sort_by(imageDetails,&imagePushedAt)[-1].imageTags[0]' \
  --output text --profile hushstore --no-cli-pager)
echo "image_tag = \"${TAG}\"" >> terraform.tfvars
grep image_tag terraform.tfvars

# Xác nhận CẢ BA repo đều có tag này — nếu thiếu, task sẽ fail lúc pull.
for r in api web migrator; do
  echo -n "hushstore-$r: "
  aws ecr describe-images --repository-name "hushstore-$r" --image-ids imageTag="$TAG" \
    --query 'imageDetails[0].imageTags[0]' --output text --profile hushstore --no-cli-pager 2>&1 | tail -1
done

terraform apply
```

Expected: cả 3 repo in ra `$TAG`. `apply` tạo 3 `aws_ecs_task_definition`. Nếu repo nào báo `ImageNotFoundException`, quay lại task tương ứng build và push image với đúng SHA đó.

- [ ] **Step 10: Chạy task migrator để dựng schema — deliverable chính của task này**

```bash
CLUSTER=$(terraform output -raw ecs_cluster_name)
CP=$(terraform output -raw capacity_provider_name)
SUBNETS=$(terraform output -json app_subnet_ids | jq -r 'join(",")')

TASK_ARN=$(aws ecs run-task \
  --cluster "$CLUSTER" \
  --task-definition hushstore-migrator \
  --capacity-provider-strategy "capacityProvider=${CP},weight=1" \
  --count 1 \
  --query 'tasks[0].taskArn' --output text \
  --profile hushstore --no-cli-pager)
echo "Task: $TASK_ARN"

aws ecs wait tasks-stopped --cluster "$CLUSTER" --tasks "$TASK_ARN" \
  --profile hushstore --no-cli-pager
```

Expected: `run-task` trả về task ARN (không phải `failures`), và `wait` trả về sau ~1-3 phút. Nếu `run-task` trả `failures` với `RESOURCE:MEMORY`, instance không đủ RAM — tạm thời chưa có service nào chạy nên không nên xảy ra.

- [ ] **Step 11: Kiểm tra exit code của task migrator**

```bash
aws ecs describe-tasks --cluster "$CLUSTER" --tasks "$TASK_ARN" \
  --query 'tasks[0].containers[0].{ExitCode:exitCode,Reason:reason,Status:lastStatus}' \
  --profile hushstore --no-cli-pager
```

Expected: `{"ExitCode": 0, "Reason": null, "Status": "STOPPED"}`. **`ExitCode` phải là 0.** Nếu khác 0, đọc log:

```bash
TASK_ID="${TASK_ARN##*/}"
aws logs tail "/ecs/hushstore-migrator" --log-stream-names "migrator/migrator/${TASK_ID}" \
  --profile hushstore --no-cli-pager
```

- [ ] **Step 12: Verify schema thật đã có trên RDS (deliverable của task)**

```bash
cd infra/tf/envs/prod
INSTANCE_ID=$(aws ecs list-container-instances --cluster "$CLUSTER" \
  --query 'containerInstanceArns[0]' --output text --profile hushstore --no-cli-pager \
  | xargs -I{} aws ecs describe-container-instances --cluster "$CLUSTER" \
      --container-instances {} --query 'containerInstances[0].ec2InstanceId' \
      --output text --profile hushstore --no-cli-pager)
RDS_HOST=$(terraform output -raw rds_endpoint)
echo "Vào SSM session tới $INSTANCE_ID, rồi chạy các lệnh ở dưới."
aws ssm start-session --target "$INSTANCE_ID" --profile hushstore
```

Trong SSM session:

```bash
DB_PW=$(aws ssm get-parameter --name /hushstore/prod/db-password \
  --with-decryption --query 'Parameter.Value' --output text --region ap-southeast-1)
RDS_HOST="<dán giá trị rds_endpoint vào đây>"

sudo docker run --rm mcr.microsoft.com/mssql-tools18 \
  /opt/mssql-tools18/bin/sqlcmd -S "${RDS_HOST},1433" -U dbadmin -P "$DB_PW" -C \
  -d HushStoreDB \
  -Q "SELECT COUNT(*) AS MigrationsApplied FROM __EFMigrationsHistory;"

sudo docker run --rm mcr.microsoft.com/mssql-tools18 \
  /opt/mssql-tools18/bin/sqlcmd -S "${RDS_HOST},1433" -U dbadmin -P "$DB_PW" -C \
  -d HushStoreDB \
  -Q "SELECT COUNT(*) AS UserTables FROM sys.tables;"
exit
```

Expected: `MigrationsApplied` = `20` và `UserTables` > `20`. Database `HushStoreDB` do `efbundle` tự tạo (không đặt `db_name` trong `aws_db_instance` vì SQL Server không hỗ trợ).

> Lệnh `get-parameter --with-decryption` ở đây chạy trong SSM session trên host, không phải trên máy bạn — secret không lọt vào shell history local. Biến `DB_PW` mất khi thoát session.

- [ ] **Step 13: Hạ chi phí nếu nghỉ giữa task, rồi commit**

```bash
cd infra/tf/envs/prod
# CHỈ chạy nếu bạn nghỉ ở đây. Task 14 cần instance chạy lại.
# sed -i '' 's|^enable_nat = .*|enable_nat = false|'      terraform.tfvars
# sed -i '' 's|^instance_count = .*|instance_count = 0|'  terraform.tfvars
# terraform apply

cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): 3 task definition + dung schema RDS bang migration task

api: bridge + static hostPort 8080, memory 512 hard / 384 reservation, maxSwap
1024, initProcessEnabled cho ECS Exec, task-app-role. Khong dat healthCheck o
tang container vi image aspnet:10.0 khong co curl — suc khoe do ALB target
group kiem tra qua /health/ready.

web: bridge + static hostPort 80, memory 192/96, KHONG co task_role_arn vi
nginx khong goi AWS API nao.

migrator: khong map port, task-migrator-role, inject CA HAI secret. Bat buoc
phai co JwtSettings__SecretKey vi efbundle chay lai entry point cua API va
Program.cs throw neu thieu no.

Chon static host port thay vi dynamic port mapping de sg-web ingress giu dung
2 rule thay vi phai mo dai 32768-65535.

Secret dung khoi secrets voi valueFrom, KHONG dung environment — gia tri trong
environment hien nguyen van trong output cua describe-task-definition.

Da chay run-task migrator: exit code 0, 20 migration ap dung len RDS, database
HushStoreDB do efbundle tu tao."
```

---

### Task 14: Module `alb` — ACM cert, ALB, 2 target group, listener theo Host header

**Files:**
- Create: `infra/tf/modules/alb/versions.tf`
- Create: `infra/tf/modules/alb/variables.tf`
- Create: `infra/tf/modules/alb/acm.tf`
- Create: `infra/tf/modules/alb/alb.tf`
- Create: `infra/tf/modules/alb/outputs.tf`
- Create: `infra/tf/modules/alb/tests/alb.tftest.hcl`
- Modify: `infra/tf/envs/prod/main.tf` (thêm `module "alb"`)
- Modify: `infra/tf/envs/prod/variables.tf` (thêm `enable_alb`)
- Modify: `infra/tf/envs/prod/outputs.tf`

**Interfaces:**
- Consumes: `module.network.{vpc_id, public_subnet_ids}`, `module.security.alb_sg_id`, `module.storage.alb_logs_bucket_name`, `var.web_domain`, `var.api_domain`.
- Produces — outputs của `module.alb`:
  - `alb_dns_name` (string) — hostname để test trước khi cắt DNS
  - `alb_zone_id` (string)
  - `tg_web_arn` (string), `tg_api_arn` (string) — dùng ở Task 15
  - `acm_validation_records` (list) — CNAME cần thêm vào Cloudflare
  - `certificate_arn` (string)

> **ACM cert KHÔNG bị `enable_alb` gate.** Cert miễn phí, và validation mất thời gian — nếu gate nó thì mỗi lần bật/tắt ALB lại phải validate lại. Cert và `aws_acm_certificate_validation` tồn tại vĩnh viễn; chỉ ALB, target group và listener bị gate.
>
> **`enable_alb` gate cả target group.** ECS `CreateService` sẽ fail nếu target group chưa gắn vào load balancer nào — nên Task 15 cũng gate service theo `enable_alb`. Nhóm ALB + TG + listener + service chính là "serving stack", bật tắt cùng nhau.

- [ ] **Step 1: Viết test trước — `infra/tf/modules/alb/tests/alb.tftest.hcl`**

```hcl
provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project           = "hushstore-tftest"
  vpc_id            = "vpc-00000000000000000"
  public_subnet_ids = ["subnet-00000000000000001", "subnet-00000000000000002"]
  alb_sg_id         = "sg-00000000000000000"
  logs_bucket       = "hushstore-alb-logs"
  web_domain        = "hushstore.io.vn"
  api_domain        = "api.hushstore.io.vn"
  enable_alb        = true
}

run "alb_internet_facing_tren_2_az_va_khong_bat_deletion_protection" {
  command = plan

  assert {
    condition     = aws_lb.this[0].internal == false
    error_message = "ALB phải internet-facing để người dùng truy cập được."
  }

  assert {
    condition     = length(aws_lb.this[0].subnets) == 2
    error_message = "ALB bắt buộc phải nằm trên tối thiểu 2 subnet ở 2 AZ khác nhau."
  }

  assert {
    condition     = aws_lb.this[0].enable_deletion_protection == false
    error_message = "deletion_protection phải false, nếu không down.sh và nuke.sh không destroy được ALB."
  }
}

run "listener_80_redirect_301_sang_443" {
  command = plan

  assert {
    condition     = aws_lb_listener.http[0].default_action[0].type == "redirect"
    error_message = "Listener 80 phải redirect, không được forward — mọi traffic phải đi qua HTTPS."
  }

  assert {
    condition = alltrue([
      aws_lb_listener.http[0].default_action[0].redirect[0].protocol == "HTTPS",
      aws_lb_listener.http[0].default_action[0].redirect[0].port == "443",
      aws_lb_listener.http[0].default_action[0].redirect[0].status_code == "HTTP_301",
    ])
    error_message = "Redirect phải là 301 sang HTTPS port 443."
  }
}

run "listener_443_dung_tls_policy_hien_dai" {
  command = plan

  assert {
    condition     = startswith(aws_lb_listener.https[0].ssl_policy, "ELBSecurityPolicy-TLS13")
    error_message = "Phải dùng SSL policy TLS 1.3, không dùng policy cũ cho phép TLS 1.0/1.1."
  }
}

run "target_group_web_health_check_healthz_va_api_health_ready" {
  command = plan

  assert {
    condition = alltrue([
      aws_lb_target_group.web[0].port == 80,
      aws_lb_target_group.web[0].target_type == "instance",
      aws_lb_target_group.web[0].health_check[0].path == "/healthz",
    ])
    error_message = "tg-web phải trỏ port 80, target_type instance, health check /healthz (endpoint đã thêm vào nginx.conf)."
  }

  assert {
    condition = alltrue([
      aws_lb_target_group.api[0].port == 8080,
      aws_lb_target_group.api[0].target_type == "instance",
      aws_lb_target_group.api[0].health_check[0].path == "/health/ready",
    ])
    error_message = "tg-api PHẢI health check /health/ready (chạm DB), không phải /health — nếu không ALB giữ nguyên instance dù RDS chết."
  }
}

run "listener_rule_route_api_domain_sang_tg_api" {
  command = plan

  assert {
    condition = anytrue([
      for c in aws_lb_listener_rule.api[0].condition :
      contains(try(c.host_header[0].values, []), "api.hushstore.io.vn")
    ])
    error_message = "Phải có listener rule route Host = api.hushstore.io.vn sang tg-api."
  }

  # KHÔNG so target_group_arn với aws_lb_target_group.web[0].arn — cả hai là
  # (known after apply). Assert type thay thế; việc route đúng tg-web được
  # kiểm chứng thật bằng curl ở Task 15 Step 10-11.
  assert {
    condition     = aws_lb_listener.https[0].default_action[0].type == "forward"
    error_message = "Default action của listener 443 phải là forward (sang tg-web), không phải redirect hay fixed-response."
  }
}

run "acm_cert_bao_ca_hai_domain_va_dung_dns_validation" {
  command = plan

  assert {
    condition     = aws_acm_certificate.this.domain_name == "hushstore.io.vn"
    error_message = "Cert phải cấp cho domain gốc hushstore.io.vn."
  }

  assert {
    condition     = contains(aws_acm_certificate.this.subject_alternative_names, "api.hushstore.io.vn")
    error_message = "Cert phải có SAN api.hushstore.io.vn để listener phục vụ được cả 2 hostname."
  }

  assert {
    condition     = aws_acm_certificate.this.validation_method == "DNS"
    error_message = "Phải dùng DNS validation — email validation không tự động hoá được."
  }
}

run "tat_alb_thi_khong_tao_alb_va_target_group_nhung_van_giu_cert" {
  command = plan

  variables {
    enable_alb = false
  }

  assert {
    condition = alltrue([
      length(aws_lb.this) == 0,
      length(aws_lb_target_group.web) == 0,
      length(aws_lb_target_group.api) == 0,
      length(aws_lb_listener.https) == 0,
    ])
    error_message = "Khi enable_alb = false, toàn bộ serving stack phải bị destroy để về $0."
  }

  assert {
    condition     = aws_acm_certificate.this.domain_name == "hushstore.io.vn"
    error_message = "ACM cert KHÔNG được gate theo enable_alb — cert miễn phí, và giữ lại để không phải validate lại mỗi lần bật ALB."
  }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/alb
terraform init
terraform test
```

Expected: FAIL với `Reference to undeclared resource "aws_lb"`.

- [ ] **Step 3: Viết `infra/tf/modules/alb/variables.tf`**

```hcl
variable "project" {
  description = "Tiền tố tên cho mọi resource"
  type        = string
}

variable "vpc_id" {
  description = "ID VPC chứa target group"
  type        = string
}

variable "public_subnet_ids" {
  description = "ID của 2 public subnet — ALB bắt buộc nằm trên 2 AZ"
  type        = list(string)
}

variable "alb_sg_id" {
  description = "ID Security Group của ALB"
  type        = string
}

variable "logs_bucket" {
  description = "Tên bucket nhận ALB access log"
  type        = string
}

variable "web_domain" {
  description = "Domain của Blazor client — là domain chính của cert"
  type        = string
}

variable "api_domain" {
  description = "Domain của API — là SAN của cert, route bằng listener rule"
  type        = string
}

variable "enable_alb" {
  description = "Bật serving stack (ALB + target group + listener). $0.0225/giờ"
  type        = bool
  default     = false
}
```

- [ ] **Step 4: Viết `infra/tf/modules/alb/acm.tf`**

```hcl
# Cert KHÔNG bị enable_alb gate: ACM miễn phí, và DNS validation mất thời gian
# nên giữ cert tồn tại vĩnh viễn để bật/tắt ALB không phải validate lại.
resource "aws_acm_certificate" "this" {
  domain_name               = var.web_domain
  subject_alternative_names = [var.api_domain]
  validation_method         = "DNS"

  tags = { Name = "${var.project}-cert" }

  lifecycle {
    create_before_destroy = true
  }
}

# DNS record phải thêm TAY vào Cloudflare (DNS không do Terraform quản).
# Resource này chờ tới khi ACM thấy record và chuyển cert sang ISSUED.
resource "aws_acm_certificate_validation" "this" {
  certificate_arn         = aws_acm_certificate.this.arn
  validation_record_fqdns = [for o in aws_acm_certificate.this.domain_validation_options : o.resource_record_name]

  timeouts {
    create = "30m"
  }
}
```

- [ ] **Step 5: Viết `infra/tf/modules/alb/alb.tf`**

```hcl
# ─── LOAD BALANCER ───────────────────────────────────────────────
resource "aws_lb" "this" {
  count = var.enable_alb ? 1 : 0

  name               = "${var.project}-alb"
  load_balancer_type = "application"
  internal           = false
  security_groups    = [var.alb_sg_id]
  subnets            = var.public_subnet_ids

  # Phải false: nếu bật thì down.sh và nuke.sh không destroy được ALB.
  enable_deletion_protection = false

  # Access log là nguồn bằng chứng chính cho báo cáo kiểm thử bảo mật khi
  # VPC Flow Logs đang tắt (enable_flow_logs default = false).
  access_logs {
    bucket  = var.logs_bucket
    prefix  = var.project
    enabled = true
  }

  # Bỏ header X-Forwarded-* mà client tự gửi, thay bằng giá trị ALB tự tính.
  # Không có cái này thì client có thể giả mạo X-Forwarded-For.
  drop_invalid_header_fields = true

  tags = { Name = "${var.project}-alb" }
}

# ─── TARGET GROUPS ───────────────────────────────────────────────
# target_type = instance vì task dùng bridge network mode với static host
# port. ECS service tự đăng ký/rút instance khỏi target group.
resource "aws_lb_target_group" "web" {
  count = var.enable_alb ? 1 : 0

  name        = "${var.project}-tg-web"
  port        = 80
  protocol    = "HTTP"
  target_type = "instance"
  vpc_id      = var.vpc_id

  # 30s thay vì 300s mặc định: rút ngắn thời gian deploy đáng kể, và nginx
  # serve static nên không có request nào chạy lâu cần chờ.
  deregistration_delay = 30

  health_check {
    path                = "/healthz"
    protocol            = "HTTP"
    matcher             = "200"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  tags = { Name = "${var.project}-tg-web" }
}

resource "aws_lb_target_group" "api" {
  count = var.enable_alb ? 1 : 0

  name        = "${var.project}-tg-api"
  port        = 8080
  protocol    = "HTTP"
  target_type = "instance"
  vpc_id      = var.vpc_id

  deregistration_delay = 30

  health_check {
    # /health/ready CHẠM DB (AddDbContextCheck). Trỏ vào /health cũ sẽ khiến
    # ALB báo healthy dù RDS đã chết — chính lỗi mà Task 8 sửa.
    path                = "/health/ready"
    protocol            = "HTTP"
    matcher             = "200"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  tags = { Name = "${var.project}-tg-api" }
}

# ─── LISTENERS ───────────────────────────────────────────────────
resource "aws_lb_listener" "http" {
  count = var.enable_alb ? 1 : 0

  load_balancer_arn = aws_lb.this[0].arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"

    redirect {
      protocol    = "HTTPS"
      port        = "443"
      status_code = "HTTP_301"
    }
  }
}

resource "aws_lb_listener" "https" {
  count = var.enable_alb ? 1 : 0

  load_balancer_arn = aws_lb.this[0].arn
  port              = 443
  protocol          = "HTTPS"
  certificate_arn   = aws_acm_certificate_validation.this.certificate_arn

  # TLS 1.3 only. Policy cũ hơn cho phép TLS 1.0/1.1 đã hết hạn hỗ trợ.
  ssl_policy = "ELBSecurityPolicy-TLS13-1-2-2021-06"

  # Mặc định: Blazor client.
  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.web[0].arn
  }
}

# Host-based routing: api.hushstore.io.vn -> container API port 8080.
# Nhờ rule này nginx trong image web KHÔNG cần block proxy_pass nào.
resource "aws_lb_listener_rule" "api" {
  count = var.enable_alb ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 100

  condition {
    host_header {
      values = [var.api_domain]
    }
  }

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api[0].arn
  }
}
```

- [ ] **Step 6: Viết `infra/tf/modules/alb/outputs.tf`**

```hcl
output "alb_dns_name" {
  description = "Hostname của ALB — dùng để test trước khi cắt DNS sang Cloudflare"
  value       = var.enable_alb ? aws_lb.this[0].dns_name : ""
}

output "alb_zone_id" {
  description = "Hosted zone ID của ALB"
  value       = var.enable_alb ? aws_lb.this[0].zone_id : ""
}

output "tg_web_arn" {
  description = "ARN target group của Blazor client"
  value       = var.enable_alb ? aws_lb_target_group.web[0].arn : ""
}

output "tg_api_arn" {
  description = "ARN target group của API"
  value       = var.enable_alb ? aws_lb_target_group.api[0].arn : ""
}

output "certificate_arn" {
  description = "ARN của ACM certificate"
  value       = aws_acm_certificate.this.arn
}

output "acm_validation_records" {
  description = "CNAME phải thêm TAY vào Cloudflare để ACM cấp cert"
  value = [
    for o in aws_acm_certificate.this.domain_validation_options : {
      name  = o.resource_record_name
      type  = o.resource_record_type
      value = o.resource_record_value
    }
  ]
}
```

- [ ] **Step 7: Chạy test để xác nhận nó pass**

```bash
cd infra/tf/modules/alb
terraform init
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `7 passed, 0 failed.`

- [ ] **Step 8: Thêm biến và module vào `infra/tf/envs/prod`**

Thêm vào `variables.tf`:

```hcl
variable "enable_alb" {
  description = "Bật serving stack: ALB + 2 target group + listener + 2 ECS service. $0.0225/giờ"
  type        = bool
  default     = false
}
```

Thêm vào `main.tf` sau `module "ecs"`:

```hcl
module "alb" {
  source = "../../modules/alb"

  project           = local.name
  vpc_id            = module.network.vpc_id
  public_subnet_ids = module.network.public_subnet_ids
  alb_sg_id         = module.security.alb_sg_id
  logs_bucket       = module.storage.alb_logs_bucket_name
  web_domain        = var.web_domain
  api_domain        = var.api_domain
  enable_alb        = var.enable_alb
}
```

- [ ] **Step 9: Tạo RIÊNG cert trước, để lấy record validation mà chưa bị chờ**

```bash
cd infra/tf/envs/prod
terraform init
terraform apply -target=module.alb.aws_acm_certificate.this
```

Expected: `Apply complete! Resources: 1 added`. Chỉ cert được tạo, chưa có resource nào chờ validation.

> Phải `-target` ở bước này. Nếu apply thẳng, `aws_acm_certificate_validation` sẽ chờ 30 phút một CNAME chưa tồn tại.

- [ ] **Step 10: In ra CNAME cần thêm vào Cloudflare**

```bash
terraform output -json acm_validation_records 2>/dev/null \
  || terraform state show module.alb.aws_acm_certificate.this \
     | grep -A3 domain_validation_options
```

Nếu output chưa khai báo, lấy trực tiếp:

```bash
CERT_ARN=$(terraform state show module.alb.aws_acm_certificate.this | grep -m1 '^\s*arn' | awk -F'"' '{print $2}')
aws acm describe-certificate --certificate-arn "$CERT_ARN" \
  --query 'Certificate.DomainValidationOptions[].{Domain:DomainName,Name:ResourceRecord.Name,Value:ResourceRecord.Value}' \
  --output table --profile hushstore --no-cli-pager
```

Expected: bảng 2 dòng (một cho `hushstore.io.vn`, một cho `api.hushstore.io.vn`). Hai record thường có cùng `Value` nếu ACM gộp — khi đó chỉ cần thêm 1 CNAME.

- [ ] **Step 11: Thêm CNAME validation vào Cloudflare (thao tác tay)**

Trong Cloudflare dashboard → domain `hushstore.io.vn` → DNS → Records, thêm mỗi record ở Step 10:

- **Type:** `CNAME`
- **Name:** phần trước `.hushstore.io.vn` của cột `Name` (Cloudflare tự thêm domain, đừng dán cả FQDN)
- **Target:** cột `Value` (bỏ dấu `.` ở cuối nếu có)
- **Proxy status:** **DNS only** (mây xám) — bắt buộc, mây vàng sẽ làm ACM không đọc được record
- **TTL:** Auto

- [ ] **Step 12: Chờ ACM cấp cert**

```bash
for i in $(seq 1 20); do
  S=$(aws acm describe-certificate --certificate-arn "$CERT_ARN" \
      --query 'Certificate.Status' --output text --profile hushstore --no-cli-pager)
  echo "lần $i: $S"
  [ "$S" = "ISSUED" ] && break
  sleep 30
done
```

Expected: `ISSUED` trong vòng ~10 phút. Nếu quá lâu vẫn `PENDING_VALIDATION`, kiểm tra record bằng `dig +short CNAME <tên record đầy đủ>` — nếu không trả gì thì record sai tên hoặc còn bật proxy mây vàng.

- [ ] **Step 13: Bật ALB và apply toàn bộ**

```bash
sed -i '' 's|^enable_alb = .*|enable_alb = true|' terraform.tfvars
grep enable_alb terraform.tfvars
terraform apply
```

Expected: `enable_alb = true`. `apply` tạo `aws_acm_certificate_validation`, ALB, 2 target group, 2 listener, 1 listener rule. ALB mất ~2-3 phút để `active`.

- [ ] **Step 14: Verify ALB trả 503 (chưa có target nào) và HTTPS hoạt động**

```bash
ALB_DNS=$(terraform output -raw alb_dns_name)
echo "ALB: $ALB_DNS"
curl -s -o /dev/null -w "http-redirect=%{http_code} -> %{redirect_url}\n" "http://${ALB_DNS}/"
curl -sk -o /dev/null -w "https=%{http_code}\n" "https://${ALB_DNS}/"
```

Expected: `http-redirect=301` với `redirect_url` là `https://...`, và `https=503`. **503 là đúng ở bước này** — ALB đã chạy và TLS đã hoạt động, chỉ chưa có target nào vì ECS service chưa tồn tại (Task 15).

> `curl -k` vì đang gọi bằng ALB DNS name, không khớp CN của cert (`hushstore.io.vn`). Đúng như vậy.

- [ ] **Step 15: Verify chỉ 80 và 443 mở trên ALB — kịch bản kiểm thử số 1 và 4**

```bash
nmap -Pn -p 22,80,443,1433,8080 "$ALB_DNS"
```

Expected: `80/tcp open`, `443/tcp open`, còn `22`, `1433`, `8080` đều `filtered` hoặc `closed`. Đây là bằng chứng cho kịch bản 1 và 4 trong báo cáo bảo mật.

- [ ] **Step 16: Thêm output và commit**

Thêm vào `infra/tf/envs/prod/outputs.tf`:

```hcl
output "alb_dns_name" {
  description = "Hostname ALB — trỏ CNAME của Cloudflare vào đây"
  value       = module.alb.alb_dns_name
}

output "acm_validation_records" {
  description = "CNAME cần thêm vào Cloudflare để ACM cấp cert"
  value       = module.alb.acm_validation_records
}
```

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): ALB + ACM cert + 2 target group route theo Host header

Listener 80 redirect 301 sang 443; listener 443 dung ACM cert (SAN cho ca
hushstore.io.vn va api.hushstore.io.vn) voi SSL policy TLS13-1-2-2021-06.
Default action -> tg-web; listener rule Host = api.hushstore.io.vn -> tg-api.
Nho rule nay nginx trong image web KHONG can block proxy_pass nao.

tg-api health check /health/ready (CHAM DB) chu khong phai /health — day chinh
la loi ma Task 8 sua: /health luon tra 200 nen ALB giu nguyen instance du RDS
da chet. tg-web health check /healthz cua nginx.

ACM cert va certificate_validation CO TINH khong bi enable_alb gate: cert mien
phi va DNS validation mat thoi gian, giu lai de bat/tat ALB khong phai validate
lai. Chi ALB + target group + listener bi gate.

drop_invalid_header_fields = true de client khong gia mao duoc X-Forwarded-For.
deregistration_delay 30s thay vi 300s mac dinh de rut ngan thoi gian deploy.

Da verify: HTTP 301 -> HTTPS, HTTPS tra 503 (dung, chua co target), nmap chi
thay 80 va 443 mo."
```

---

### Task 15: Module `ecs` — 2 ECS service, website lên được qua ALB

**Files:**
- Create: `infra/tf/modules/ecs/service.tf`
- Create: `infra/tf/modules/ecs/tests/service.tftest.hcl`
- Modify: `infra/tf/modules/ecs/variables.tf`
- Modify: `infra/tf/modules/ecs/outputs.tf`
- Modify: `infra/tf/envs/prod/main.tf`

**Interfaces:**
- Consumes: `module.alb.{tg_web_arn, tg_api_arn}`, task definition + cluster + capacity provider (Task 12/13), `var.enable_alb`.
- Produces: outputs `service_web_name`, `service_api_name`; **website phục vụ được qua `https://<alb_dns_name>`** — deliverable thật của task.

> **Service bị `enable_alb` gate** vì ECS `CreateService` fail nếu target group chưa gắn vào load balancer. ALB + TG + listener + service là một khối bật/tắt cùng nhau.
>
> **`deployment_minimum_healthy_percent = 0`, `maximum_percent = 100`.** Static host port 80/8080 + đúng 1 instance nên không thể chạy 2 bản song song — port sẽ xung đột. Hệ quả: deploy có downtime ~20-40s. Đây là đánh đổi có ý thức để `sg-web` giữ đúng 2 ingress rule; đường zero-downtime là dynamic port mapping + 2 instance, nhưng phải mở dải `32768-65535` trên `sg-web`.

- [ ] **Step 1: Viết test trước — `infra/tf/modules/ecs/tests/service.tftest.hcl`**

```hcl
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
  ecr_api_url               = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-api"
  ecr_web_url               = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-web"
  ecr_migrator_url          = "000000000000.dkr.ecr.ap-southeast-1.amazonaws.com/hushstore-migrator"
  image_tag                 = "abc123def456"
  assets_bucket_name        = "hushstore-public-assets"
  allowed_origins           = "https://hushstore.io.vn"
  enable_alb                = true
  tg_web_arn                = "arn:aws:elasticloadbalancing:ap-southeast-1:000000000000:targetgroup/hushstore-tg-web/aaaaaaaaaaaaaaaa"
  tg_api_arn                = "arn:aws:elasticloadbalancing:ap-southeast-1:000000000000:targetgroup/hushstore-tg-api/bbbbbbbbbbbbbbbb"
}

run "deployment_percent_phu_hop_voi_static_host_port_1_instance" {
  command = plan

  assert {
    condition = alltrue([
      aws_ecs_service.api[0].deployment_minimum_healthy_percent == 0,
      aws_ecs_service.api[0].deployment_maximum_percent == 100,
    ])
    error_message = "Static host port 8080 + 1 instance nên không chạy 2 bản song song được. min 0 / max 100 là bắt buộc, nếu không deploy sẽ treo vì xung đột port."
  }

  assert {
    condition = alltrue([
      aws_ecs_service.web[0].deployment_minimum_healthy_percent == 0,
      aws_ecs_service.web[0].deployment_maximum_percent == 100,
    ])
    error_message = "Service web cũng phải min 0 / max 100 vì static host port 80."
  }
}

run "service_gan_dung_target_group_va_container" {
  command = plan

  assert {
    condition = alltrue([
      aws_ecs_service.api[0].load_balancer[0].target_group_arn == var.tg_api_arn,
      aws_ecs_service.api[0].load_balancer[0].container_name == "api",
      aws_ecs_service.api[0].load_balancer[0].container_port == 8080,
    ])
    error_message = "Service api phải gắn tg-api, container tên 'api', port 8080."
  }

  assert {
    condition = alltrue([
      aws_ecs_service.web[0].load_balancer[0].target_group_arn == var.tg_web_arn,
      aws_ecs_service.web[0].load_balancer[0].container_name == "web",
      aws_ecs_service.web[0].load_balancer[0].container_port == 80,
    ])
    error_message = "Service web phải gắn tg-web, container tên 'web', port 80."
  }
}

run "service_api_bat_ecs_exec" {
  command = plan

  assert {
    condition     = aws_ecs_service.api[0].enable_execute_command == true
    error_message = "Phải bật ECS Exec trên service api để chạy kịch bản kiểm thử số 10 (chứng minh blast radius của task role)."
  }
}

run "service_dung_capacity_provider_khong_dung_launch_type" {
  command = plan

  assert {
    condition = alltrue([
      aws_ecs_service.api[0].capacity_provider_strategy[0].capacity_provider == aws_ecs_capacity_provider.this.name,
      aws_ecs_service.web[0].capacity_provider_strategy[0].capacity_provider == aws_ecs_capacity_provider.this.name,
    ])
    error_message = "Service phải dùng capacity_provider_strategy — launch_type và capacity_provider_strategy loại trừ nhau."
  }
}

run "tat_alb_thi_khong_tao_service_nao" {
  command = plan

  variables {
    enable_alb = false
  }

  assert {
    condition = alltrue([
      length(aws_ecs_service.api) == 0,
      length(aws_ecs_service.web) == 0,
    ])
    error_message = "Khi enable_alb = false phải destroy cả service — ECS CreateService fail nếu target group chưa gắn vào load balancer."
  }

  assert {
    condition = alltrue([
      aws_ecs_task_definition.api.network_mode == "bridge",
      aws_ecs_cluster.this.name == "hushstore-tftest",
    ])
    error_message = "Cluster và task definition KHÔNG được gate theo enable_alb — chúng miễn phí và cần tồn tại để run-task migrator."
  }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó fail**

```bash
cd infra/tf/modules/ecs
terraform test -filter=tests/service.tftest.hcl
```

Expected: FAIL với `Reference to undeclared resource "aws_ecs_service"`.

- [ ] **Step 3: Thêm biến vào `infra/tf/modules/ecs/variables.tf`**

```hcl
variable "enable_alb" {
  description = "Bật serving stack. Service bị gate theo biến này vì ECS CreateService fail nếu target group chưa gắn vào load balancer"
  type        = bool
  default     = false
}

variable "tg_web_arn" {
  description = "ARN target group của Blazor client"
  type        = string
  default     = ""
}

variable "tg_api_arn" {
  description = "ARN target group của API"
  type        = string
  default     = ""
}

variable "service_desired_count" {
  description = "Số task mỗi service. Giữ 1 — max_size của ASG là 1 và host port là static"
  type        = number
  default     = 1

  validation {
    condition     = var.service_desired_count >= 0 && var.service_desired_count <= 1
    error_message = "service_desired_count chỉ được 0 hoặc 1 — static host port không cho phép 2 task cùng port trên 1 instance."
  }
}
```

- [ ] **Step 4: Viết `infra/tf/modules/ecs/service.tf`**

```hcl
# Service bị enable_alb gate: ECS CreateService trả lỗi nếu target group chưa
# gắn vào load balancer nào. ALB + TG + listener + service là một khối bật/tắt
# cùng nhau. Cluster, capacity provider, ASG và task definition thì KHÔNG bị
# gate — chúng miễn phí và cần tồn tại để run-task migrator.

resource "aws_ecs_service" "web" {
  count = var.enable_alb ? 1 : 0

  name            = "${var.project}-web"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.web.arn
  desired_count   = var.service_desired_count

  capacity_provider_strategy {
    capacity_provider = aws_ecs_capacity_provider.this.name
    weight            = 1
    base              = 0
  }

  # Static host port 80 + đúng 1 instance nên không thể giữ bản cũ chạy trong
  # lúc bản mới lên — port sẽ xung đột. Hệ quả: deploy có downtime ~20-40s.
  # Đây là đánh đổi có ý thức để sg-web giữ đúng 2 ingress rule.
  deployment_minimum_healthy_percent = 0
  deployment_maximum_percent         = 100

  load_balancer {
    target_group_arn = var.tg_web_arn
    container_name   = "web"
    container_port   = 80
  }

  health_check_grace_period_seconds = 60

  # Task definition revision do pipeline (Phase 2) cập nhật bằng
  # ecs update-service. Không ignore ở đây vì Phase 1 vẫn dùng Terraform để
  # đổi image_tag; Phase 2 sẽ thêm ignore_changes khi CI nắm quyền.
  tags = { Name = "${var.project}-web" }

  depends_on = [aws_ecs_cluster_capacity_providers.this]
}

resource "aws_ecs_service" "api" {
  count = var.enable_alb ? 1 : 0

  name            = "${var.project}-api"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.service_desired_count

  capacity_provider_strategy {
    capacity_provider = aws_ecs_capacity_provider.this.name
    weight            = 1
    base              = 0
  }

  deployment_minimum_healthy_percent = 0
  deployment_maximum_percent         = 100

  load_balancer {
    target_group_arn = var.tg_api_arn
    container_name   = "api"
    container_port   = 8080
  }

  # API cần thời gian chạy migration check + kết nối RDS trước khi
  # /health/ready trả 200. 120s để ALB không rút task quá sớm.
  health_check_grace_period_seconds = 120

  # ECS Exec: vào được shell trong container để chạy kịch bản kiểm thử số 10.
  # Cần initProcessEnabled trong task definition và ssmmessages trong task role.
  enable_execute_command = true

  tags = { Name = "${var.project}-api" }

  depends_on = [aws_ecs_cluster_capacity_providers.this]
}
```

- [ ] **Step 5: Thêm output vào `infra/tf/modules/ecs/outputs.tf`**

```hcl
output "service_web_name" {
  description = "Tên ECS service của Blazor client"
  value       = var.enable_alb ? aws_ecs_service.web[0].name : ""
}

output "service_api_name" {
  description = "Tên ECS service của API"
  value       = var.enable_alb ? aws_ecs_service.api[0].name : ""
}
```

- [ ] **Step 6: Chạy toàn bộ test của module `ecs`**

```bash
cd infra/tf/modules/ecs
terraform init
terraform fmt -check -recursive
terraform validate
terraform test
```

Expected: `20 passed, 0 failed.` (4 iam + 5 cluster + 6 taskdef + 5 service).

- [ ] **Step 7: Cập nhật `module "ecs"` trong `infra/tf/envs/prod/main.tf` rồi apply**

Thêm vào block `module "ecs"`:

```hcl
  enable_alb = var.enable_alb
  tg_web_arn = module.alb.tg_web_arn
  tg_api_arn = module.alb.tg_api_arn
```

```bash
cd infra/tf/envs/prod
terraform apply
```

Expected: `Apply complete!` với 2 `aws_ecs_service` added.

- [ ] **Step 8: Chờ 2 service ổn định**

```bash
CLUSTER=$(terraform output -raw ecs_cluster_name)
aws ecs wait services-stable --cluster "$CLUSTER" \
  --services hushstore-web hushstore-api \
  --profile hushstore --no-cli-pager && echo "2 service đã stable"
```

Expected: in `2 service đã stable` trong vòng ~3-5 phút. Nếu timeout, xem Step 12 để chẩn đoán.

- [ ] **Step 9: Verify cả 2 target group đều healthy**

```bash
for TG in tg-web tg-api; do
  ARN=$(aws elbv2 describe-target-groups --names "hushstore-${TG}" \
    --query 'TargetGroups[0].TargetGroupArn' --output text --profile hushstore --no-cli-pager)
  echo "--- $TG ---"
  aws elbv2 describe-target-health --target-group-arn "$ARN" \
    --query 'TargetHealthDescriptions[].{Target:Target.Id,Port:Target.Port,State:TargetHealth.State,Reason:TargetHealth.Reason}' \
    --output table --profile hushstore --no-cli-pager
done
```

Expected: mỗi target group có 1 target `State = healthy`. `tg-api` healthy chính là bằng chứng `/health/ready` kết nối được RDS — nghĩa là SG, NACL, connection string và task role đều đúng.

- [ ] **Step 10: Verify Blazor client load được qua ALB**

```bash
ALB_DNS=$(terraform output -raw alb_dns_name)
curl -sk -o /dev/null -w "web-root=%{http_code}\n"    "https://${ALB_DNS}/"
curl -sk -o /dev/null -w "spa-route=%{http_code}\n"   "https://${ALB_DNS}/san-pham/abc"
curl -sk "https://${ALB_DNS}/appsettings.json"
```

Expected: `web-root=200`, `spa-route=200`, và `appsettings.json` in ra `{"ApiBaseUrl": "https://api.hushstore.io.vn"}`.

- [ ] **Step 11: Verify API trả lời qua Host header của ALB**

```bash
curl -sk -H "Host: ${API_DOMAIN:-api.hushstore.io.vn}" \
  -o /dev/null -w "api-health-ready=%{http_code}\n" "https://${ALB_DNS}/health/ready"
curl -sk -H "Host: ${API_DOMAIN:-api.hushstore.io.vn}" \
  "https://${ALB_DNS}/health/ready"
echo
curl -sk -H "Host: evil.com" \
  -o /dev/null -w "host-evil=%{http_code}\n" "https://${ALB_DNS}/health/ready"
```

Expected: `api-health-ready=200`, body in `Healthy`. Với `Host: evil.com` thì trả `200` nhưng là **nội dung của Blazor client** (default action → tg-web), **không** lọt sang tg-api — đó là bằng chứng cho kịch bản kiểm thử số 7. Kiểm tra bằng cách xem body:

```bash
curl -sk -H "Host: evil.com" "https://${ALB_DNS}/health/ready" | head -c 100
```

Expected: HTML của `index.html` (SPA fallback của nginx), không phải chữ `Healthy`.

- [ ] **Step 12: Nếu service không stable — quy trình chẩn đoán**

```bash
# Sự kiện của service: lý do task không lên được
aws ecs describe-services --cluster "$CLUSTER" --services hushstore-api \
  --query 'services[0].events[0:8].message' --output text --profile hushstore --no-cli-pager

# Lý do task dừng: OOM, pull image fail, health check fail
aws ecs list-tasks --cluster "$CLUSTER" --service-name hushstore-api \
  --desired-status STOPPED --query 'taskArns[0]' --output text \
  --profile hushstore --no-cli-pager \
  | xargs -I{} aws ecs describe-tasks --cluster "$CLUSTER" --tasks {} \
      --query 'tasks[0].{StopCode:stopCode,Reason:stoppedReason,Containers:containers[].{Name:name,Exit:exitCode,Reason:reason}}' \
      --profile hushstore --no-cli-pager

# Log của container API
aws logs tail /ecs/hushstore-api --since 10m --profile hushstore --no-cli-pager
```

Ba nguyên nhân thường gặp:
- `RESOURCE:MEMORY` → t3.micro hết RAM. Giảm `api_memory_hard` xuống 448, hoặc đặt `instance_type = "t3.small"` trong `terraform.tfvars`.
- `CannotPullContainerError` → `image_tag` không tồn tại trên ECR, hoặc `enable_nat = false`.
- Log API có `A network-related or instance-specific error` → không tới được RDS. Kiểm tra `sg-rds` ingress và NACL db.

- [ ] **Step 13: Commit**

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): 2 ECS service, website len duoc qua ALB

Service bi enable_alb gate vi ECS CreateService fail neu target group chua gan
vao load balancer — ALB + TG + listener + service la mot khoi bat/tat cung
nhau. Cluster, capacity provider, ASG va task definition KHONG bi gate: chung
mien phi va can ton tai de run-task migrator.

deployment_minimum_healthy_percent = 0 / maximum = 100: static host port 80 va
8080 voi dung 1 instance nen khong the chay 2 ban song song, port se xung dot.
He qua la deploy co downtime ~20-40s — danh doi co y thuc de sg-web giu dung 2
ingress rule thay vi phai mo dai 32768-65535 cho dynamic port mapping.

enable_execute_command tren service api de chay kich ban kiem thu so 10.
health_check_grace_period 120s cho API (can thoi gian ket noi RDS), 60s cho web.

Da verify: 2 target group healthy, Blazor client load qua ALB, SPA fallback
hoat dong, API tra Healthy qua Host header, va Host: evil.com KHONG lot sang
tg-api (bang chung kich ban kiem thu so 7)."
```

---

### Task 16: Seed dữ liệu + kiểm chứng end-to-end + chứng minh hạ tầng bất biến

**Files:**
- Create: `infra/tf/scripts/seed-db.sh`
- Modify: `infra/tf/envs/prod/outputs.tf` (thêm output tổng hợp)

**Interfaces:**
- Consumes: schema đã dựng (Task 13), 2 service đang healthy (Task 15), bucket artifacts (Task 6).
- Produces: DB có dữ liệu (roles, tài khoản admin, sản phẩm mẫu); **bằng chứng đã kiểm chứng cho 5 hành vi**: login trả JWT, upload ảnh trả URL S3 hợp lệ (chứng minh ECS task role hoạt động, không còn static key), ALB health check phản ứng đúng khi RDS chết, hạ tầng dựng lại được từ 0, và blast radius của task role bị giới hạn.

> **Lưu ý về `seed_data.sql`:** file bắt đầu bằng `USE [HushStoreDb]` (chữ `b` thường), còn connection string dùng `Database=HushStoreDB`. SQL Server không phân biệt hoa thường ở tên database nên vẫn chạy, nhưng script `seed-db.sh` dưới đây vẫn xoá dòng `USE`/`GO` đầu tiên và truyền `-d` cho `sqlcmd` để không phụ thuộc vào collation của server.

- [ ] **Step 1: Upload 2 file seed lên bucket artifacts**

```bash
cd "$(git rev-parse --show-toplevel)"
ARTIFACTS=$(cd infra/tf/envs/prod && terraform output -raw artifacts_bucket)
aws s3 cp Infrastructure/db/seed_data.sql         "s3://${ARTIFACTS}/seed/" --profile hushstore --no-cli-pager
aws s3 cp Infrastructure/db/seed_product_data.sql "s3://${ARTIFACTS}/seed/" --profile hushstore --no-cli-pager
aws s3 ls "s3://${ARTIFACTS}/seed/" --profile hushstore --no-cli-pager
```

Expected: liệt kê 2 file `seed_data.sql` và `seed_product_data.sql`.

- [ ] **Step 2: Viết `infra/tf/scripts/seed-db.sh` (chạy TRÊN container instance, không phải trên laptop)**

```bash
#!/usr/bin/env bash
# =============================================================
# HushStore — seed dữ liệu vào RDS
#
# Script này chạy TRÊN EC2 container instance, vào bằng:
#   aws ssm start-session --target <instance-id> --profile hushstore
# rồi:
#   aws s3 cp s3://<artifacts>/scripts/seed-db.sh . && bash seed-db.sh <rds-host> <artifacts-bucket>
#
# Không chạy được từ laptop: RDS nằm trong db subnet isolated, chỉ app tier
# tới được port 1433.
# =============================================================
set -euo pipefail

RDS_HOST="${1:?Thiếu tham số 1: RDS endpoint}"
ARTIFACTS="${2:?Thiếu tham số 2: tên bucket artifacts}"
DB_NAME="HushStoreDB"
REGION="ap-southeast-1"
TOOLS_IMAGE="mcr.microsoft.com/mssql-tools18"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  HushStore — seed dữ liệu vào $RDS_HOST"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Đọc mật khẩu vào biến, không in ra stdout.
DB_PW=$(aws ssm get-parameter --name /hushstore/prod/db-password \
  --with-decryption --query 'Parameter.Value' --output text --region "$REGION")

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

echo "[1/3] Tải file seed từ s3://${ARTIFACTS}/seed/ ..."
aws s3 cp "s3://${ARTIFACTS}/seed/seed_data.sql"         "$WORK/" --region "$REGION"
aws s3 cp "s3://${ARTIFACTS}/seed/seed_product_data.sql" "$WORK/" --region "$REGION"

# Bỏ mọi dòng USE [...] và GO ngay sau nó: tên database do sqlcmd -d quyết
# định, không để file seed tự chọn (file dùng HushStoreDb, ta dùng HushStoreDB).
echo "[2/3] Chuẩn hoá script (bỏ dòng USE) ..."
for f in "$WORK"/*.sql; do
  sed -i -E '/^[[:space:]]*USE[[:space:]]*\[/Id' "$f"
  echo "  → $(basename "$f") sẵn sàng"
done

echo "[3/3] Chạy seed ..."
sudo docker pull -q "$TOOLS_IMAGE"
for f in seed_data.sql seed_product_data.sql; do
  echo "  → đang chạy $f"
  sudo docker run --rm -v "$WORK:/sql:ro" "$TOOLS_IMAGE" \
    /opt/mssql-tools18/bin/sqlcmd \
      -S "${RDS_HOST},1433" -U dbadmin -P "$DB_PW" -C \
      -d "$DB_NAME" -b -i "/sql/${f}"
  echo "  → $f xong"
done

echo ""
echo "✓ Seed hoàn thành. Kiểm tra nhanh:"
sudo docker run --rm "$TOOLS_IMAGE" \
  /opt/mssql-tools18/bin/sqlcmd \
    -S "${RDS_HOST},1433" -U dbadmin -P "$DB_PW" -C -d "$DB_NAME" \
    -Q "SELECT (SELECT COUNT(*) FROM AppRoles) AS Roles, (SELECT COUNT(*) FROM AppUsers) AS Users, (SELECT COUNT(*) FROM Products) AS Products;"
```

- [ ] **Step 3: Upload script rồi chạy seed qua SSM Session Manager**

```bash
cd "$(git rev-parse --show-toplevel)"
chmod +x infra/tf/scripts/seed-db.sh
aws s3 cp infra/tf/scripts/seed-db.sh "s3://${ARTIFACTS}/scripts/" --profile hushstore --no-cli-pager

cd infra/tf/envs/prod
CLUSTER=$(terraform output -raw ecs_cluster_name)
RDS_HOST=$(terraform output -raw rds_endpoint)
INSTANCE_ID=$(aws ecs list-container-instances --cluster "$CLUSTER" \
  --query 'containerInstanceArns[0]' --output text --profile hushstore --no-cli-pager \
  | xargs -I{} aws ecs describe-container-instances --cluster "$CLUSTER" \
      --container-instances {} --query 'containerInstances[0].ec2InstanceId' \
      --output text --profile hushstore --no-cli-pager)

echo "Chạy trong SSM session:"
echo "  aws s3 cp s3://${ARTIFACTS}/scripts/seed-db.sh . --region ap-southeast-1"
echo "  bash seed-db.sh ${RDS_HOST} ${ARTIFACTS}"
aws ssm start-session --target "$INSTANCE_ID" --profile hushstore
```

Expected: script in `✓ Seed hoàn thành.` và bảng cuối có `Roles` ≥ 3, `Users` ≥ 1, `Products` > 0.

- [ ] **Step 4: Verify login trả JWT — bằng chứng API + RDS + secret injection đều đúng**

```bash
ALB_DNS=$(terraform output -raw alb_dns_name)
RESP=$(curl -sk -X POST "https://${ALB_DNS}/api/auth/login" \
  -H "Host: api.hushstore.io.vn" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@hushstore.com","password":"Admin@123"}')
echo "$RESP" | head -c 400
echo
echo "$RESP" | jq -r '.data.accessToken // .data.token // empty' | cut -c1-40
```

Expected: response có `"isSuccess": true` (hoặc tương đương) và in ra 40 ký tự đầu của JWT. Nếu trả 401, xác nhận seed đã chạy và mật khẩu hash trong `seed_data.sql` khớp `Admin@123`.

- [ ] **Step 5: Verify upload ảnh trả URL S3 — bằng chứng ECS task role hoạt động, không còn static key**

```bash
TOKEN=$(echo "$RESP" | jq -r '.data.accessToken // .data.token')
printf '\x89PNG\r\n\x1a\n' > /tmp/probe.png
head -c 512 /dev/urandom >> /tmp/probe.png

# Route thật là /api/images (số nhiều) và `folder` là QUERY param, không phải
# form field — xem src/API/Controllers/Admin/ImageController.cs:11,29,31
curl -sk -X POST "https://${ALB_DNS}/api/images/upload?folder=probe" \
  -H "Host: api.hushstore.io.vn" \
  -H "Authorization: Bearer ${TOKEN}" \
  -F "file=@/tmp/probe.png;type=image/png" | tee /tmp/upload.json
echo
URL=$(jq -r '.data // .data.url // empty' /tmp/upload.json)
echo "URL trả về: $URL"
curl -s -o /dev/null -w "anh-doc-duoc=%{http_code}\n" "$URL"
```

Expected: response chứa URL dạng `https://hushstore-public-assets.s3.ap-southeast-1.amazonaws.com/probe/<guid>.png`, và `anh-doc-duoc=200`.

**Đây là bằng chứng quan trọng nhất của Task 8:** container không có access key nào, nó ghi được vào S3 hoàn toàn nhờ credential tạm thời của `task-app-role` lấy qua `AWS_CONTAINER_CREDENTIALS_RELATIVE_URI`.

> Controller có `[Authorize]` ở cấp class nên bắt buộc phải có Bearer token.

- [ ] **Step 6: Verify blast radius của task role — kịch bản kiểm thử số 10**

```bash
CLUSTER=$(terraform output -raw ecs_cluster_name)
TASK=$(aws ecs list-tasks --cluster "$CLUSTER" --service-name hushstore-api \
  --query 'taskArns[0]' --output text --profile hushstore --no-cli-pager)

aws ecs execute-command --cluster "$CLUSTER" --task "$TASK" \
  --container api --interactive --command "/bin/sh" --profile hushstore
```

Trong shell của container:

```sh
echo "$AWS_CONTAINER_CREDENTIALS_RELATIVE_URI"
env | grep -c "AccessKeyId\|SecretAccessKey" || echo "0 static key trong env — dung"
env | grep -c "ConnectionStrings__DefaultConnection"
exit
```

Expected: biến `AWS_CONTAINER_CREDENTIALS_RELATIVE_URI` có giá trị (credential đến từ task role); `0 static key trong env — dung`; và connection string CÓ trong env (do ECS inject từ Parameter Store, không nằm trong file trên disk).

Kết hợp với `iam simulate-principal-policy` đã chạy ở Task 11 Step 8 (`rds:DescribeDBInstances` → `implicitDeny`), đây là bằng chứng đầy đủ: container chỉ ghi được vào một bucket S3.

- [ ] **Step 7: Verify ALB phản ứng đúng khi RDS chết — bài test giá trị nhất của health check mới**

```bash
RDS_ID=$(terraform output -raw rds_identifier)
aws rds stop-db-instance --db-instance-identifier "$RDS_ID" --profile hushstore --no-cli-pager > /dev/null
echo "Đang stop RDS, đợi ~3 phút rồi kiểm tra target health..."
sleep 180

TG_API=$(aws elbv2 describe-target-groups --names hushstore-tg-api \
  --query 'TargetGroups[0].TargetGroupArn' --output text --profile hushstore --no-cli-pager)
aws elbv2 describe-target-health --target-group-arn "$TG_API" \
  --query 'TargetHealthDescriptions[].TargetHealth.State' --output text \
  --profile hushstore --no-cli-pager

TG_WEB=$(aws elbv2 describe-target-groups --names hushstore-tg-web \
  --query 'TargetGroups[0].TargetGroupArn' --output text --profile hushstore --no-cli-pager)
aws elbv2 describe-target-health --target-group-arn "$TG_WEB" \
  --query 'TargetHealthDescriptions[].TargetHealth.State' --output text \
  --profile hushstore --no-cli-pager
```

Expected: `tg-api` chuyển sang **`unhealthy`**, còn `tg-web` vẫn **`healthy`**.

Đây chính là hành vi mà `/health` cũ không có — nó luôn trả 200 nên ALB sẽ tiếp tục gửi request tới một API không dùng được. Đồng thời `tg-web` vẫn healthy chứng minh 2 target group độc lập: Blazor client vẫn load được khi API chết.

- [ ] **Step 8: Bật RDS lại và chờ service hồi phục**

```bash
aws rds start-db-instance --db-instance-identifier "$RDS_ID" --profile hushstore --no-cli-pager > /dev/null
aws rds wait db-instance-available --db-instance-identifier "$RDS_ID" --profile hushstore --no-cli-pager
sleep 60
aws elbv2 describe-target-health --target-group-arn "$TG_API" \
  --query 'TargetHealthDescriptions[].TargetHealth.State' --output text \
  --profile hushstore --no-cli-pager
```

Expected: quay về `healthy` (health check `interval 15` × `healthy_threshold 2` nên hồi phục trong ~30-60s sau khi RDS available).

- [ ] **Step 9: Chứng minh hạ tầng bất biến — hạ toàn bộ về $0**

```bash
cd infra/tf/envs/prod
sed -i '' 's|^enable_alb = .*|enable_alb = false|'     terraform.tfvars
sed -i '' 's|^instance_count = .*|instance_count = 0|' terraform.tfvars
sed -i '' 's|^enable_nat = .*|enable_nat = false|'     terraform.tfvars
terraform apply
aws rds stop-db-instance --db-instance-identifier "$RDS_ID" --profile hushstore --no-cli-pager > /dev/null
```

Expected: `apply` destroy 2 ECS service, ALB, 2 target group, 2 listener, listener rule, NAT Gateway, EIP, route NAT; ASG về `desired = 0` (instance bị terminate, EBS root xoá theo).

- [ ] **Step 10: Verify thật sự không còn gì tính phí theo giờ**

```bash
aws elbv2 describe-load-balancers --query 'length(LoadBalancers)' --output text --profile hushstore --no-cli-pager
aws ec2 describe-nat-gateways --filter "Name=state,Values=available,pending" \
  --query 'length(NatGateways)' --output text --profile hushstore --no-cli-pager
aws autoscaling describe-auto-scaling-groups --auto-scaling-group-names hushstore-asg \
  --query 'AutoScalingGroups[0].{Desired:DesiredCapacity,Instances:length(Instances)}' \
  --profile hushstore --no-cli-pager
terraform plan
```

Expected: `0` load balancer, `0` NAT Gateway, ASG `Desired: 0` và `Instances: 0`. `terraform plan` in **`No changes`** — state không lệch sau khi toggle.

- [ ] **Step 11: Bật lại toàn bộ từ 0**

```bash
aws rds start-db-instance --db-instance-identifier "$RDS_ID" --profile hushstore --no-cli-pager > /dev/null
aws rds wait db-instance-available --db-instance-identifier "$RDS_ID" --profile hushstore --no-cli-pager

sed -i '' 's|^enable_nat = .*|enable_nat = true|'       terraform.tfvars
sed -i '' 's|^instance_count = .*|instance_count = 1|'  terraform.tfvars
sed -i '' 's|^enable_alb = .*|enable_alb = true|'       terraform.tfvars
terraform apply

CLUSTER=$(terraform output -raw ecs_cluster_name)
aws ecs wait services-stable --cluster "$CLUSTER" \
  --services hushstore-web hushstore-api --profile hushstore --no-cli-pager
```

- [ ] **Step 12: Verify instance MỚI HOÀN TOÀN vẫn tự lên đủ — không thao tác tay nào**

```bash
ALB_DNS=$(terraform output -raw alb_dns_name)
curl -sk -o /dev/null -w "web=%{http_code}\n" "https://${ALB_DNS}/"
curl -sk -H "Host: api.hushstore.io.vn" "https://${ALB_DNS}/health/ready"
echo
curl -sk -X POST "https://${ALB_DNS}/api/auth/login" \
  -H "Host: api.hushstore.io.vn" -H "Content-Type: application/json" \
  -d '{"email":"admin@hushstore.com","password":"Admin@123"}' | head -c 120
```

Expected: `web=200`, `/health/ready` trả `Healthy`, login vẫn trả token.

**Đây là bằng chứng hạ tầng bất biến:** instance cũ đã bị terminate cùng EBS root, ALB có DNS name mới, nhưng không cần cài nginx, không cần copy WASM bundle, không cần tạo file `.env` nào — mọi thứ nằm trong image và Parameter Store. Đây cũng là điều kiện để `down.sh`/`up.sh` của Phase 3 dùng được hằng ngày.

> ALB DNS name **thay đổi** sau khi tạo lại. Đó là lý do Task 17 trỏ Cloudflare bằng CNAME tới DNS name chứ không dùng IP.

- [ ] **Step 13: Thêm output tổng hợp và commit**

Thêm vào `infra/tf/envs/prod/outputs.tf`:

```hcl
output "verify_commands" {
  description = "Lệnh kiểm chứng nhanh sau mỗi lần up.sh"
  value = {
    web   = "curl -sk -o /dev/null -w '%%{http_code}\\n' https://${module.alb.alb_dns_name}/"
    api   = "curl -sk -H 'Host: ${var.api_domain}' https://${module.alb.alb_dns_name}/health/ready"
    login = "curl -sk -X POST https://${module.alb.alb_dns_name}/api/auth/login -H 'Host: ${var.api_domain}' -H 'Content-Type: application/json' -d '{\"email\":\"admin@hushstore.com\",\"password\":\"Admin@123\"}'"
  }
}
```

```bash
cd "$(git rev-parse --show-toplevel)"
git add infra/tf
git commit -m "feat(infra): seed DB + kiem chung end-to-end + chung minh ha tang bat bien

seed-db.sh chay tren container instance qua SSM Session Manager (RDS nam trong
db subnet isolated nen laptop khong toi duoc 1433). Script bo dong USE [...]
trong file seed va truyen -d cho sqlcmd de khong phu thuoc collation cua server
(file dung HushStoreDb, connection string dung HushStoreDB).

Da kiem chung 5 hanh vi:
1. Login tra JWT — API + RDS + secret injection tu Parameter Store deu dung.
2. Upload anh tra URL S3 doc duoc 200 — container KHONG co access key nao, ghi
   duoc vao S3 hoan toan nho credential tam thoi cua task-app-role. Day la bang
   chung quan trong nhat cua Task 8.
3. ECS Exec vao container: 0 static key trong env, connection string do ECS
   inject chu khong nam trong file tren disk (kich ban kiem thu so 10).
4. Stop RDS -> tg-api chuyen unhealthy nhung tg-web van healthy. Day chinh la
   hanh vi ma /health cu khong co.
5. Ha toan bo ve \$0 roi bat lai: instance MOI HOAN TOAN tu len du, khong thao
   tac tay nao. terraform plan sau khi toggle van 'No changes' — state khong
   lech. Dieu kien de down.sh/up.sh cua Phase 3 dung duoc hang ngay."
```

---

### Task 17: Cắt DNS sang ALB, viết runbook

**Files:**
- Create: `docs/terraform-runbook.md`
- Modify: `README.md` (cập nhật phần deploy)

**Interfaces:**
- Consumes: stack đã kiểm chứng end-to-end (Task 16).
- Produces: `https://hushstore.io.vn` và `https://api.hushstore.io.vn` phục vụ từ stack Terraform; runbook đủ để vận hành Phase 1.

> **Cắt DNS ở đây gần như không có rủi ro.** Record `A @` và `A api` hiện đang trỏ vào IP của EC2 kỳ trước — instance đó đã bị xoá, nên hai record đang chết sẵn. Không có traffic thật nào để làm gián đoạn.
>
> **Nhưng có một lý do thật để phải cắt DNS:** `AllowedOrigins` của API đặt là `https://hushstore.io.vn` (Task 13). Khi test qua ALB DNS name, trình duyệt gửi `Origin` là hostname của ALB nên **CORS sẽ chặn** — nghĩa là không thể kiểm chứng luồng thật trên trình duyệt cho tới khi domain trỏ đúng. `curl` không bị ảnh hưởng vì nó không gửi `Origin`, đó là lý do Task 16 vẫn verify được bằng `curl`. Nếu muốn mở trình duyệt kiểm tra sớm, làm Step 1-4 của task này ngay sau Task 15.

- [ ] **Step 1: Xác nhận stack mới đang chạy và khoẻ trước khi chạm vào DNS**

```bash
cd infra/tf/envs/prod
ALB_DNS=$(terraform output -raw alb_dns_name)
CLUSTER=$(terraform output -raw ecs_cluster_name)
aws ecs describe-services --cluster "$CLUSTER" --services hushstore-web hushstore-api \
  --query 'services[].{Name:serviceName,Running:runningCount,Desired:desiredCount}' \
  --output table --profile hushstore --no-cli-pager
curl -sk -o /dev/null -w "web=%{http_code}\n" "https://${ALB_DNS}/"
curl -sk -H "Host: api.hushstore.io.vn" -o /dev/null -w "api=%{http_code}\n" \
  "https://${ALB_DNS}/health/ready"
echo "ALB DNS để dán vào Cloudflare: $ALB_DNS"
```

Expected: cả 2 service `Running = 1`, `web=200`, `api=200`. **Không đi tiếp nếu bất kỳ giá trị nào sai.**

- [ ] **Step 2: Cắt DNS ở Cloudflare (thao tác tay)**

Trong Cloudflare dashboard → `hushstore.io.vn` → DNS → Records:

1. **Xoá** record `A @` và `A api` — chúng đang trỏ vào IP của EC2 kỳ trước đã bị xoá, tức là record chết.
2. **Thêm** record mới:
   - Type `CNAME`, Name `@`, Target `<ALB_DNS ở Step 1>`, Proxy **Proxied** (mây vàng), TTL Auto
   - Type `CNAME`, Name `api`, Target `<ALB_DNS ở Step 1>`, Proxy **Proxied** (mây vàng), TTL Auto
3. SSL/TLS → Overview → đặt encryption mode **Full (strict)**.

> Cloudflare tự làm CNAME flattening ở apex nên `CNAME @` hợp lệ. Mây vàng dùng được vì ALB có ACM cert hợp lệ cho cả 2 hostname — đó là điều kiện của Full (strict). Giữ record CNAME validation của ACM ở trạng thái **DNS only** (mây xám) như đã đặt ở Task 14.

- [ ] **Step 3: Verify DNS đã trỏ sang ALB**

```bash
dig +short hushstore.io.vn
dig +short api.hushstore.io.vn
for i in 1 2 3 4 5 6; do
  W=$(curl -s -o /dev/null -w '%{http_code}' https://hushstore.io.vn/)
  A=$(curl -s -o /dev/null -w '%{http_code}' https://api.hushstore.io.vn/health/ready)
  echo "lần $i: web=$W api=$A"
  [ "$W" = "200" ] && [ "$A" = "200" ] && break
  sleep 30
done
```

Expected: `web=200 api=200` trong vài phút. `dig` trả IP của Cloudflare (mây vàng nên không thấy IP của ALB — đúng như vậy).

- [ ] **Step 4: Verify cert thật (không dùng `-k`) và luồng nghiệp vụ trên domain thật**

```bash
curl -s -o /dev/null -w "https-cert-hop-le=%{http_code}\n" https://hushstore.io.vn/
curl -s -o /dev/null -w "http-redirect=%{http_code}\n" -I http://hushstore.io.vn/
curl -s -X POST https://api.hushstore.io.vn/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@hushstore.com","password":"Admin@123"}' | head -c 200
```

Expected: `https-cert-hop-le=200` (không cần `-k` nữa — cert khớp domain), `http-redirect=301`, và login trả token. Mở `https://hushstore.io.vn` trên browser, kiểm tra biểu tượng khoá và duyệt được danh sách sản phẩm.

- [ ] **Step 5: Verify account chỉ chứa đúng stack Terraform, không có gì lạc**

```bash
a() { command aws --profile hushstore --no-cli-pager --region ap-southeast-1 "$@"; }
echo "--- VPC ---"
a ec2 describe-vpcs --query 'Vpcs[].[VpcId,CidrBlock,IsDefault]' --output text
echo "--- EC2 dang chay ---"
a ec2 describe-instances --filters "Name=instance-state-name,Values=running" \
  --query 'Reservations[].Instances[].[InstanceId,PublicIpAddress]' --output text
echo "--- RDS ---"
a rds describe-db-instances --query 'DBInstances[].[DBInstanceIdentifier,DBInstanceStatus]' --output text
echo "--- Key pair (phai rong) ---"
a ec2 describe-key-pairs --query 'KeyPairs[].KeyName' --output text
echo "--- EIP chua gan (tinh phi neu co) ---"
a ec2 describe-addresses --query 'Addresses[?AssociationId==null].PublicIp' --output text
```

Expected: VPC chỉ có `10.20.0.0/16` (cộng default VPC `172.31.0.0/16` nếu account mới còn giữ); EC2 đang chạy **không có public IP**; RDS chỉ có `hushstore-db-tf`; **key pair rỗng**; không có EIP nào chưa gắn.

`KeyPairs` rỗng là bằng chứng nửa đầu cho kịch bản kiểm thử số 5 — không có SSH key nào tồn tại trong account. Nửa còn lại là Task 5 Step 9 (không SG rule nào mở port 22).

- [ ] **Step 6: (Tuỳ chọn) Dọn nốt account cũ `408194747451`**

Account kỳ trước còn sót 2 VPC `10.0.0.0/16` và key pair `hushstore-key`. Cả hai **miễn phí**, không ảnh hưởng gì tới đồ án kỳ này — chỉ là dọn cho gọn. Cần profile riêng vì `hushstore` giờ trỏ account mới:

```bash
o() { command aws --profile hushstore-old --no-cli-pager --region ap-southeast-1 "$@"; }
o sts get-caller-identity --query Account --output text   # phai in 408194747451

o ec2 delete-key-pair --key-name hushstore-key

for V in $(o ec2 describe-vpcs --filters "Name=isDefault,Values=false" \
             --query 'Vpcs[].VpcId' --output text); do
  echo "=== $V ==="
  for S in $(o ec2 describe-subnets --filters "Name=vpc-id,Values=$V" \
               --query 'Subnets[].SubnetId' --output text); do o ec2 delete-subnet --subnet-id "$S"; done
  for R in $(o ec2 describe-route-tables --filters "Name=vpc-id,Values=$V" \
               --query 'RouteTables[?length(Associations[?Main==`true`])==`0`].RouteTableId' --output text); do
    o ec2 delete-route-table --route-table-id "$R"; done
  for G in $(o ec2 describe-security-groups --filters "Name=vpc-id,Values=$V" \
               --query 'SecurityGroups[?GroupName!=`default`].GroupId' --output text); do
    o ec2 delete-security-group --group-id "$G"; done
  for I in $(o ec2 describe-internet-gateways --filters "Name=attachment.vpc-id,Values=$V" \
               --query 'InternetGateways[].InternetGatewayId' --output text); do
    o ec2 detach-internet-gateway --internet-gateway-id "$I" --vpc-id "$V"
    o ec2 delete-internet-gateway --internet-gateway-id "$I"; done
  o ec2 delete-vpc --vpc-id "$V"
done
```

Expected: không lỗi. Nếu `delete-vpc` báo `DependencyViolation`, còn ENI hoặc resource nào đó bám vào — bỏ qua, VPC không tốn phí.

> Bỏ qua step này hoàn toàn cũng được. Nó không phải điều kiện của bất kỳ task nào.

- [ ] **Step 7: Viết `docs/terraform-runbook.md`**

```markdown
# Terraform Runbook — HushStore Phase 1

Vận hành hạ tầng AWS của HushStore. Spec kiến trúc:
[docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md](superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md).

## Yêu cầu

- Terraform ≥ 1.10 (backend dùng `use_lockfile`)
- AWS CLI v2 với profile `hushstore` (IAM Identity Center / SSO, region `ap-southeast-1`). Đầu mỗi phiên: `aws sso login --profile hushstore`
- Docker (để build image)
- `jq`

Thư mục làm việc: `infra/tf/envs/prod`. File `terraform.tfvars` **không commit** —
tạo từ `terraform.tfvars.example`.

## Toggle chi phí

| Biến | Ý nghĩa | Chi phí khi bật |
|---|---|---|
| `enable_nat` | NAT Gateway cho egress (pull ECR, SSM) | **$0.045/giờ** + $0.045/GB |
| `enable_alb` | Serving stack: ALB + 2 target group + listener + 2 ECS service | **$0.0225/giờ** |
| `instance_count` | 0 hoặc 1 EC2 container instance | free tier 750h/tháng |
| `enable_flow_logs` | VPC Flow Logs (chỉ REJECT) | phí ingest CloudWatch |
| `enable_deny_demo` | NACL rule 50 DENY `my_ip` | $0 |

Cluster, capacity provider, ASG, task definition, RDS, VPC, NACL, Security Group,
ECR, S3 và ACM cert **không** bị gate — chúng miễn phí hoặc nằm trong free tier.

## Bật hệ thống

```bash
cd infra/tf/envs/prod
aws rds start-db-instance --db-instance-identifier hushstore-db-tf --profile hushstore
aws rds wait db-instance-available --db-instance-identifier hushstore-db-tf --profile hushstore

sed -i '' 's|^enable_nat = .*|enable_nat = true|'      terraform.tfvars
sed -i '' 's|^instance_count = .*|instance_count = 1|' terraform.tfvars
sed -i '' 's|^enable_alb = .*|enable_alb = true|'      terraform.tfvars
terraform apply

aws ecs wait services-stable --cluster hushstore \
  --services hushstore-web hushstore-api --profile hushstore
```

Thứ tự quan trọng: NAT phải bật trước khi instance lên, vì ECS agent cần gọi
ECS control plane và ECR.

## Tắt hệ thống

```bash
cd infra/tf/envs/prod
sed -i '' 's|^enable_alb = .*|enable_alb = false|'     terraform.tfvars
sed -i '' 's|^instance_count = .*|instance_count = 0|' terraform.tfvars
sed -i '' 's|^enable_nat = .*|enable_nat = false|'     terraform.tfvars
terraform apply
aws rds stop-db-instance --db-instance-identifier hushstore-db-tf --profile hushstore
```

Thứ tự ngược lại: service phải xuống trước instance, nếu không ECS sẽ liên tục
thử reschedule task.

Sau khi tắt, `terraform plan` phải in `No changes` — nếu lệch thì state có vấn đề.

> Phase 3 sẽ gói 2 quy trình này thành `infra/tf/scripts/up.sh` và `down.sh`,
> cộng thêm Lambda cost-guard tự tắt hằng đêm.

## Xoá toàn bộ

```bash
cd infra/tf/envs/prod
terraform destroy
```

Hai resource có `prevent_destroy` và sẽ chặn `destroy`, **cố ý**:
- `module.storage.aws_s3_bucket.assets` — chứa ảnh sản phẩm thật
- state bucket trong `infra/tf/bootstrap`

Muốn xoá thật thì bỏ block `lifecycle { prevent_destroy = true }` rồi apply trước.

## Deploy phiên bản mới (Phase 1 — làm tay)

```bash
SHA=$(git rev-parse HEAD)
cd infra/tf/envs/prod
REG=$(terraform output -json ecr_urls | jq -r .api | cut -d/ -f1)
cd "$(git rev-parse --show-toplevel)"

aws ecr get-login-password --region ap-southeast-1 --profile hushstore \
  | docker login --username AWS --password-stdin "$REG"

for t in api web migrator; do
  case $t in
    api)      F=Dockerfile ;;
    web)      F=src/Client/Dockerfile ;;
    migrator) F=src/Infrastructure/Dockerfile.migrator ;;
  esac
  docker build -f "$F" -t "${REG}/hushstore-${t}:${SHA}" .
  docker push "${REG}/hushstore-${t}:${SHA}"
done

cd infra/tf/envs/prod
sed -i '' "s|^image_tag = .*|image_tag = \"${SHA}\"|" terraform.tfvars

# Migration TRƯỚC, chỉ deploy khi exit code = 0
terraform apply -target=module.ecs.aws_ecs_task_definition.migrator
TASK=$(aws ecs run-task --cluster hushstore --task-definition hushstore-migrator \
  --capacity-provider-strategy capacityProvider=hushstore-cp,weight=1 \
  --query 'tasks[0].taskArn' --output text --profile hushstore)
aws ecs wait tasks-stopped --cluster hushstore --tasks "$TASK" --profile hushstore
CODE=$(aws ecs describe-tasks --cluster hushstore --tasks "$TASK" \
  --query 'tasks[0].containers[0].exitCode' --output text --profile hushstore)
[ "$CODE" = "0" ] || { echo "Migration THẤT BẠI (exit $CODE) — KHÔNG deploy"; exit 1; }

terraform apply
aws ecs wait services-stable --cluster hushstore \
  --services hushstore-web hushstore-api --profile hushstore
```

Phase 2 sẽ tự động hoá đúng luồng này trong `.github/workflows/deploy.yml`.

## Rollback

```bash
aws ecs describe-task-definition --task-definition hushstore-api \
  --query 'taskDefinition.revision' --profile hushstore
aws ecs update-service --cluster hushstore --service hushstore-api \
  --task-definition hushstore-api:<revision-cũ> --profile hushstore
```

Image tag là git SHA và ECR đặt `IMMUTABLE`, nên revision cũ chắc chắn trỏ
đúng nội dung ban đầu. Rollback DB thì restore từ snapshot — migration là
**forward-only**, không dùng down-migration.

## Vào hệ thống để chẩn đoán

**Không có SSH.** Không có key pair, không có SG rule nào mở port 22.

```bash
# Vào EC2 host
INSTANCE=$(aws ecs list-container-instances --cluster hushstore \
  --query 'containerInstanceArns[0]' --output text --profile hushstore \
  | xargs -I{} aws ecs describe-container-instances --cluster hushstore \
      --container-instances {} --query 'containerInstances[0].ec2InstanceId' \
      --output text --profile hushstore)
aws ssm start-session --target "$INSTANCE" --profile hushstore

# Vào trong container API
TASK=$(aws ecs list-tasks --cluster hushstore --service-name hushstore-api \
  --query 'taskArns[0]' --output text --profile hushstore)
aws ecs execute-command --cluster hushstore --task "$TASK" \
  --container api --interactive --command "/bin/sh" --profile hushstore

# Log
aws logs tail /ecs/hushstore-api      --since 15m --follow --profile hushstore
aws logs tail /ecs/hushstore-migrator --since 1h            --profile hushstore
```

Cả hai đều cần `enable_nat = true`.

## Seed lại dữ liệu

```bash
ARTIFACTS=$(cd infra/tf/envs/prod && terraform output -raw artifacts_bucket)
RDS=$(cd infra/tf/envs/prod && terraform output -raw rds_endpoint)
aws s3 cp Infrastructure/db/seed_data.sql         "s3://${ARTIFACTS}/seed/" --profile hushstore
aws s3 cp Infrastructure/db/seed_product_data.sql "s3://${ARTIFACTS}/seed/" --profile hushstore
aws s3 cp infra/tf/scripts/seed-db.sh             "s3://${ARTIFACTS}/scripts/" --profile hushstore
```

Rồi vào SSM session và chạy:

```bash
aws s3 cp s3://<artifacts>/scripts/seed-db.sh . --region ap-southeast-1
bash seed-db.sh <rds-endpoint> <artifacts-bucket>
```

RDS nằm trong db subnet isolated nên không seed được từ laptop.

## Sự cố thường gặp

| Hiện tượng | Nguyên nhân | Xử lý |
|---|---|---|
| ECS task `RESOURCE:MEMORY` | t3.micro 1GB hết RAM | Giảm `api_memory_hard` xuống 448, hoặc đặt `instance_type = "t3.small"` (ngoài free tier) |
| `CannotPullContainerError` | `image_tag` không có trên ECR, hoặc `enable_nat = false` | Kiểm tra `aws ecr describe-images`; bật NAT |
| `tg-api` unhealthy | API không tới được RDS | Kiểm tra RDS `available`, `sg-rds` ingress, NACL db |
| SSM `TargetNotConnected` | NAT tắt hoặc SSM Agent chưa đăng ký | Bật NAT, đợi 2 phút |
| ACM cert `PENDING_VALIDATION` | CNAME sai tên, hoặc còn bật proxy mây vàng | `dig +short CNAME <record>`; đặt DNS only |
| `terraform apply` treo ở capacity provider | `managed_termination_protection` bị bật | Phải `DISABLED` |
| Deploy có downtime ~30s | Static host port + 1 instance, đúng như thiết kế | Xem mục đánh đổi trong spec |

## Chi phí

| | 24/7 | Bật ~3h/ngày |
|---|---|---|
| NAT Gateway | $32.90/mo | ~$4.10/mo |
| ALB | $16.40/mo | ~$2.05/mo |
| EC2 + EBS + RDS | free tier | free tier |
| ECR + S3 + CloudWatch + phần còn lại | ~$0.85/mo | ~$0.65/mo |
| **Tổng** | **~$50/mo** | **~$6.8/mo** |

NAT Gateway là khoản đắt nhất và không có bậc free tier. Tắt khi không dùng.
```

- [ ] **Step 8: Cập nhật phần deploy trong `README.md`**

Thay mục hướng dẫn deploy EC2 thủ công hiện có bằng:

```markdown
## Deploy

Hạ tầng AWS được dựng bằng Terraform ở [infra/tf/](infra/tf/). Ứng dụng chạy trên
ECS EC2 launch type: 2 container (nginx + Blazor WASM, và .NET API) sau một
Application Load Balancer, RDS SQL Server Express trong subnet isolated.

- **Kiến trúc:** [docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md](docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md)
- **Vận hành:** [docs/terraform-runbook.md](docs/terraform-runbook.md) — bật/tắt tiết kiệm chi phí, deploy, rollback, chẩn đoán

**Không có SSH.** Hệ thống không có key pair nào và không có Security Group rule
nào mở port 22. Vào EC2 host bằng SSM Session Manager, vào container bằng ECS Exec
— xem runbook.

Script AWS CLI cũ nằm ở [infra/legacy-cli/](infra/legacy-cli/) chỉ để tham chiếu,
**không chạy nữa** (sẽ tạo resource nằm ngoài Terraform state).
```

- [ ] **Step 9: Hạ chi phí và commit**

```bash
cd infra/tf/envs/prod
sed -i '' 's|^enable_alb = .*|enable_alb = false|'     terraform.tfvars
sed -i '' 's|^instance_count = .*|instance_count = 0|' terraform.tfvars
sed -i '' 's|^enable_nat = .*|enable_nat = false|'     terraform.tfvars
terraform apply
aws rds stop-db-instance --db-instance-identifier hushstore-db-tf --profile hushstore --no-cli-pager > /dev/null

cd "$(git rev-parse --show-toplevel)"
git add docs/terraform-runbook.md README.md infra/tf
git commit -m "docs(infra): runbook Terraform + cat DNS sang ALB

DNS hushstore.io.vn va api.hushstore.io.vn da tro CNAME sang ALB (Cloudflare
proxied, SSL Full strict — ACM cert hop le cho ca 2 hostname nen dung duoc may
vang). Da verify cert that khong can curl -k.

Cat DNS la dieu kien de kiem chung tren trinh duyet: AllowedOrigins cua API dat
la https://hushstore.io.vn nen truy cap qua ALB DNS name se bi CORS chan. curl
khong bi anh huong vi khong gui Origin.

Account chi chua dung stack Terraform: EC2 khong public IP, key pair rong,
khong EIP thua. Key pair rong la nua dau bang chung cho kich ban kiem thu so 5;
nua con lai la khong SG rule nao mo port 22.

runbook phu: toggle chi phi, thu tu bat/tat (NAT truoc instance khi bat, service
truoc instance khi tat), deploy tay voi migration gate, rollback, vao he thong
bang SSM/ECS Exec, seed lai du lieu, 7 su co thuong gap."
```

- [ ] **Step 10: Xác nhận Phase 1 hoàn thành**

```bash
cd infra/tf/envs/prod
terraform plan
echo "--- test toàn bộ module ---"
for m in network security storage data ecs alb; do
  echo "=== $m ==="
  (cd ../../modules/$m && terraform test 2>&1 | tail -2)
done
```

Expected: `terraform plan` in `No changes`; cả 6 module đều `passed, 0 failed`. Tổng cộng **46 assertion** phủ các bất biến về bảo mật và chi phí.

---

## Self-Review

Đối chiếu plan với spec, với 3 điểm cần ghi nhận:

**1. Spec coverage** — mọi mục Phase 1 của spec đều có task tương ứng: mạng (T3), NACL (T4), Security Group (T5), ECR + S3 (T6), RDS + Parameter Store (T7), 4 điểm app code (T8), dockerize client (T9), migrator (T10), IAM (T11), cluster/ASG (T12), task definition (T13), ALB/ACM (T14), service (T15), seed + kiểm chứng (T16), cắt DNS + teardown + runbook (T17).

**2. Ba chỗ plan lệch khỏi spec, có chủ ý:**

- **Bỏ hoàn toàn phần teardown/migrate stack cũ.** Spec giả định stack cũ đang chạy trên account `408194747451`. Kiểm tra thực tế cho thấy account đó đã bị xoá sạch tài nguyên sau báo cáo kỳ trước và đã hết free tier, nên kỳ này dùng account mới với IAM Identity Center. Không có gì để stop, snapshot hay teardown; Task 1 chuyển thành thiết lập danh tính SSO.
- **Bỏ `import` block cho bucket ảnh sản phẩm.** Spec gọi đây là "ngoại lệ duy nhất" của stack greenfield, nhưng bucket `hushstore-public-assets` không còn tồn tại (account cũ không còn S3 bucket nào) và DB kỳ này seed từ đầu nên không có URL ảnh cũ nào để giữ. Cả 3 bucket đều tạo mới; `hushstore-artifacts` và `hushstore-alb-logs` thêm hậu tố account ID vì tên bucket S3 duy nhất toàn cầu.
- **Spec nói 3 IAM role, plan làm 4.** Thêm `task-migrator-role` (rỗng, chỉ có trust policy) để migrator không phải dùng chung role với API — migrator chỉ cần TCP 1433, không cần quyền AWS API nào.
- **Spec nói `enable_alb` gate ALB; plan gate cả target group và 2 ECS service.** Bắt buộc: ECS `CreateService` fail nếu target group chưa gắn vào load balancer. ACM cert thì *không* gate — cert miễn phí và DNS validation mất thời gian.

**3. Hai ràng buộc phát hiện thêm khi viết plan, đã đưa vào task:**

- **Task definition migrator phải inject cả `JwtSettings__SecretKey`**, không chỉ connection string. `efbundle` chạy lại entry point của API để dựng `DbContext`, và `Program.cs` throw nếu thiếu JWT secret ở dòng nằm trước `builder.Build()`. Task 10 Step 3 kiểm chứng bằng thực nghiệm, Task 13 có assertion cho nó.
- **`.dockerignore` phải bỏ dòng `src/Client/`.** Image web build từ cùng build context (repo root), nếu vẫn loại trừ thì `dotnet publish src/Client` không tìm thấy file. Đã đưa vào Task 8 Step 10.

Ngoài ra `KnownNetworks`/`KnownProxies` phải `Clear()` chứ không whitelist VPC CIDR: `bridge` network mode làm app thấy source IP là gateway của docker bridge (`172.17.0.1`), không phải IP của ALB. An toàn vì `sg-web` chỉ nhận traffic từ `sg-alb`.
