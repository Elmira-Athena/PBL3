# Đọc code Terraform của dự án này

`infra/tf/` có **68 file** và **9713 dòng**. Tài liệu này là bản đồ để bạn không
phải mở từng file ra xem nó là gì.

Nó **không** dạy Terraform từ đầu — muốn học từ đầu thì xem
[thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md) **Phần VI**, ở đó có lộ trình
3 tuần với nguồn tiếng Việt. Tài liệu này giả định bạn biết Terraform là công cụ
mô tả hạ tầng bằng file, và trả lời đúng một câu: **code của dự án NÀY nằm ở đâu,
và đọc theo thứ tự nào.**

---

## 1. Ba tầng, và chỉ một tầng bạn thật sự cần đọc

```
infra/tf/
├── bootstrap/     ← chạy MỘT LẦN, tạo chỗ chứa state. Đọc sau cùng, hoặc không đọc.
├── envs/prod/     ← 175 dòng. ĐÂY LÀ CHỖ BẮT ĐẦU. Nó chỉ nối 8 module lại.
├── modules/       ← 8 module. Chi tiết thật nằm ở đây.
└── scripts/       ← 6 script bash bật/tắt. Không phải Terraform.
```

Mở [`envs/prod/main.tf`](../infra/tf/envs/prod/main.tf) trước. Nó **không tạo
resource nào**, chỉ gọi 8 module và nối output của module này vào input của module
kia. Đọc 175 dòng đó là thấy được toàn bộ hình dạng hệ thống — và thấy luôn thứ tự
phụ thuộc:

```hcl
module "security" {
  source  = "../../modules/security"
  project = local.name
  vpc_id  = module.network.vpc_id      # ← security PHỤ THUỘC network
}
```

Terraform tự suy ra thứ tự tạo resource từ chính những tham chiếu như vậy. Không
có file nào khai báo "tạo VPC trước, tạo SG sau" — điều đó suy ra từ
`module.network.vpc_id`.

---

## 2. Cú pháp HCL tối thiểu — bảy khối, hết

Đọc được bảy khối này là đọc được toàn bộ 9713 dòng.

| Khối | Nghĩa | Ví dụ trong dự án |
|---|---|---|
| `resource` | **Tạo** một thứ trên AWS | `resource "aws_vpc" "this"` |
| `data` | **Đọc** một thứ đã tồn tại, không tạo gì | `data "aws_region" "current"` |
| `variable` | Tham số đầu vào của module | `variable "enable_nat"` |
| `output` | Giá trị module trả ra cho bên ngoài dùng | `output "vpc_id"` |
| `module` | Gọi một module khác | `module "network"` |
| `locals` | Giá trị tính sẵn, dùng lại trong file | `locals { app_tier_cidr = cidrsubnet(...) }` |
| `terraform` | Cấu hình chính Terraform (backend, phiên bản provider) | [`envs/prod/backend.tf`](../infra/tf/envs/prod/backend.tf) |

Và ba **meta-argument** xuất hiện khắp nơi:

| | Nghĩa | Dự án này dùng để |
|---|---|---|
| `count` | Tạo N bản. `count = 0` nghĩa là **không tạo gì** | Toàn bộ cơ chế bật/tắt: `count = var.enable_nat ? 1 : 0` |
| `for_each` | Tạo một bản cho mỗi phần tử trong tập | 4 ECR repo, và mọi rule NACL |
| `lifecycle` | Đổi cách Terraform xử lý thay đổi | `ignore_changes = [task_definition]` để pipeline đổi revision mà Terraform không kéo về |

> **Chỗ dễ nhầm nhất cho người mới:** `count = 0` **không** phải "tắt". Nó là
> **xoá**. `terraform apply` với `enable_nat = false` sẽ **destroy** NAT Gateway,
> không phải để đó ở trạng thái nghỉ. Đó chính là lý do cơ chế này đưa chi phí về
> $0 thật — và cũng là lý do bật lại mất vài phút chứ không phải vài giây.

---

## 3. Bản đồ 8 module

Cột cuối là cột đáng dùng: **file nào là file đáng đọc nhất của module đó**.

| Module | Tạo gì | Dòng | Mở file này trước |
|---|---|---|---|
| [`network`](../infra/tf/modules/network/) | VPC, 6 subnet, IGW, NAT, 3 route table, **3 Network ACL**, S3 endpoint, Flow Logs | ~470 | [`nacl.tf`](../infra/tf/modules/network/nacl.tf) — 178 dòng, phần kỹ thuật đáng nhất của cả đồ án |
| [`security`](../infra/tf/modules/security/) | 3 Security Group | 188 | [`main.tf`](../infra/tf/modules/security/main.tf) — cả module chỉ 1 file |
| [`storage`](../infra/tf/modules/storage/) | 4 ECR repo, 3 S3 bucket + policy/lifecycle/CORS | ~285 | [`s3.tf`](../infra/tf/modules/storage/s3.tf) |
| [`data`](../infra/tf/modules/data/) | RDS PostgreSQL 17 (Multi-AZ), subnet group, read replica tuỳ chọn, 4 SSM SecureString | ~290 | [`main.tf`](../infra/tf/modules/data/main.tf) |
| [`ecs`](../infra/tf/modules/ecs/) | Cluster, capacity provider, ASG, launch template, **4 task definition**, 2 service, **5 IAM role** | ~810 | [`iam.tf`](../infra/tf/modules/ecs/iam.tf) — chứa chữ `Deny` tường minh, xem [§6](#6-ba-chỗ-dễ-hiểu-sai) |
| [`alb`](../infra/tf/modules/alb/) | ALB, 2 target group, 2 listener + rule, ACM cert + validation | 219 | [`alb.tf`](../infra/tf/modules/alb/alb.tf) |
| [`cicd`](../infra/tf/modules/cicd/) | OIDC provider của GitHub, 2 IAM role (deploy + plan) | ~490 | [`policy.tf`](../infra/tf/modules/cicd/policy.tf) — 282 dòng, đây là chỗ chặn pipeline tự bật hạ tầng |
| [`costguard`](../infra/tf/modules/costguard/) | Lambda + EventBridge Scheduler + Budgets + SNS | ~710 | [`src/cost_guard.py`](../infra/tf/modules/costguard/src/cost_guard.py) — 517 dòng Python, không phải Terraform |

Mọi module có cùng bộ file, và đó là quy ước — biết quy ước thì không phải đoán:

| File | Chứa |
|---|---|
| `main.tf` hoặc `<chủ đề>.tf` | Các `resource` |
| `variables.tf` | Đầu vào |
| `outputs.tf` | Đầu ra |
| `versions.tf` | Ràng buộc phiên bản provider |
| `tests/*.tftest.hcl` | Test tự động |

---

## 4. Đọc theo thứ tự nào

Đúng thứ tự phụ thuộc, vì mỗi module sau dùng output của module trước:

```
1. network   →  VPC và subnet phải có trước mọi thứ
2. security  →  cần vpc_id
3. storage   →  độc lập, đọc lúc nào cũng được
4. data      →  cần db_subnet_ids + rds_sg_id
5. ecs       →  cần gần như tất cả những cái trên
6. alb       →  cần target group của ecs
7. cicd      →  độc lập với data plane
8. costguard →  cần tên cluster/service/RDS để mà tắt
```

**Nếu chỉ có 1 tiếng**, đọc đúng ba file này — chúng chứa phần lớn nội dung đáng
bảo vệ trước hội đồng:

1. [`modules/network/nacl.tf`](../infra/tf/modules/network/nacl.tf) — Network ACL
   là thành phần đề bài yêu cầu mà workshop tiếng Việt không dạy. Đọc khối `locals`
   ở đầu file: 6 danh sách rule, mỗi rule một dòng, có comment giải thích **vì sao
   rule 90/95/115 phải đứng trước rule 120**.
2. [`modules/security/main.tf`](../infra/tf/modules/security/main.tf) — 3 SG, và
   chú ý chúng tham chiếu **nhau bằng SG ID**, không bằng CIDR.
3. [`modules/ecs/iam.tf`](../infra/tf/modules/ecs/iam.tf) — 5 role, và chữ `Deny`
   tường minh với comment dài giải thích vì sao phải liệt kê đủ **bốn** action.

---

## 5. Một biến toggle chạy xuyên hệ thống như thế nào

Ví dụ này đáng theo dõi hết, vì nó là cơ chế trung tâm của dự án — và vì nó cho
thấy Terraform ghép các tầng lại ra sao.

**Bước 1 — giá trị nằm trong file cấu hình:**

```hcl
# envs/prod/terraform.tfvars
enable_nat = false
```

**Bước 2 — env khai báo biến rồi truyền vào module:**

```hcl
# envs/prod/variables.tf
variable "enable_nat" { type = bool }

# envs/prod/main.tf
module "network" {
  enable_nat = var.enable_nat
}
```

**Bước 3 — module dùng nó làm `count`:**

```hcl
# modules/network/vpc.tf
resource "aws_nat_gateway" "this" {
  count         = var.enable_nat ? 1 : 0
  allocation_id = aws_eip.nat[0].id
  subnet_id     = aws_subnet.public[0].id
}

resource "aws_route" "private_nat" {
  count          = var.enable_nat ? 1 : 0
  route_table_id = aws_route_table.private.id
  nat_gateway_id = aws_nat_gateway.this[0].id
}
```

**Bước 4 — test canh cho nó:**

```hcl
# modules/network/tests/vpc.tftest.hcl
run "private_route_table_khong_co_route_ra_igw" {
  command = plan
  assert {
    condition     = length(aws_nat_gateway.this) == 0
    error_message = "Khi enable_nat = false thì không được tạo NAT Gateway (tốn $0.045/giờ)."
  }
}
```

Đọc hết bốn bước là thấy được điều quan trọng: **một cờ boolean trong file text
quyết định hoá đơn AWS**, và có một test tự động canh không cho ai vô tình đảo nó.
Đó là "hạ tầng như code" nghĩa là gì trong thực tế.

Chú ý `aws_nat_gateway.this[0]` — có `count` thì resource thành **một danh sách**,
nên phải đánh chỉ số. Đây là nguồn lỗi phổ biến nhất khi người ta thêm `count` vào
một resource đã tồn tại: **mọi** chỗ tham chiếu tới nó đều phải sửa theo.

---

## 6. Ba chỗ dễ hiểu sai

**① `aws_default_security_group` KHÔNG tạo security group mới.**
Ở [`modules/network/vpc.tf`](../infra/tf/modules/network/vpc.tf) có khối này với
`ingress`/`egress` **để trống**. Người mới sẽ tưởng nó tạo một SG rỗng vô dụng.
Thật ra: AWS tự tạo một SG tên `default` cho **mọi** VPC và **không cho xoá**, mặc
định nó cho phép mọi port giữa các resource cùng nằm trong nó — kể cả 22. Khai báo
resource này là **adopt** cái AWS đã tạo rồi **xoá sạch rule** của nó. Bỏ hẳn khối
`ingress`/`egress` đi thì khác hoàn toàn: Terraform không quản rule và giữ nguyên
mặc định mở của AWS.

**② `lifecycle { ignore_changes = [task_definition] }` không phải để cho tiện.**
Ở [`modules/ecs/service.tf`](../infra/tf/modules/ecs/service.tf). Không có nó, mỗi
`terraform apply` sẽ kéo service về đúng revision ứng với `image_tag` trong
`terraform.tfvars` — tức **rollback ngược** bản mà pipeline vừa deploy. Terraform
và pipeline cùng muốn sở hữu một trường; `ignore_changes` là cách khai ai sở hữu.

**③ Test chạy ở `command = plan`, nên có thứ nó KHÔNG kiểm được.**
106 test đều không tạo resource thật → $0, nhưng cái giá là: giá trị nào chỉ biết
được **sau khi apply** thì không assert được. Ví dụ rõ nhất có comment dài ở
[`modules/network/tests/vpc.tftest.hcl`](../infra/tf/modules/network/tests/vpc.tftest.hcl):
không thể assert `length(aws_default_security_group.this.ingress) == 0`, vì hai
thuộc tính đó là *Optional + Computed* nên ở plan-time chúng là "known after apply".

Nên test đó chỉ chứng minh được **resource có được khai báo**, không chứng minh
rule đã rỗng. Điều đó phải kiểm bằng lệnh sau khi apply:

```bash
aws ec2 describe-security-group-rules \
  --filters Name=group-id,Values=<default sg của VPC> \
  --query 'length(SecurityGroupRules)' --profile hushstore   # phải ra 0
```

Giá trị thật của test đó là **chống xoá**: ai bỏ resource khỏi `vpc.tf` thì test đỏ
ngay. Nhận ra khác biệt giữa "test chứng minh điều X đúng" và "test chặn không cho
ai xoá điều X" là một bước đọc code quan trọng.

---

## 7. State ở đâu, và vì sao `bootstrap/` phải riêng

Terraform ghi **state** — bảng đối chiếu "resource nào trong code ứng với resource
nào trên AWS". State của dự án này nằm trên S3:

```hcl
# envs/prod/backend.tf
backend "s3" {
  bucket       = "hushstore-tfstate-551897327153"
  key          = "prod/terraform.tfstate"
  use_lockfile = true      # khoá bằng file trên chính S3, cần Terraform >= 1.10
}
```

`use_lockfile = true` chặn hai người `apply` cùng lúc. Trước Terraform 1.10 việc
này phải dùng thêm một bảng DynamoDB; giờ S3 làm được nên **không cần DynamoDB**.

Và đây là lý do `bootstrap/` tồn tại như một thư mục riêng: nó tạo **chính cái
bucket chứa state**. Nó không thể lưu state của mình vào một bucket mà nó chưa tạo
— nên `bootstrap/` dùng backend **local**, chạy đúng một lần, rồi không ai chạm
nữa. Đây là bài toán con-gà-quả-trứng kinh điển của Terraform, và cách giải luôn là
tách ra một stack riêng chạy trước.

---

## 8. Lệnh tra nhanh

```bash
cd infra/tf/envs/prod

terraform fmt -check -recursive     # định dạng — CI cũng chạy đúng lệnh này
terraform validate                 # cú pháp và kiểu
terraform plan                     # SẼ đổi gì. Không đổi gì thật.
terraform output                   # 21 giá trị: DNS của ALB, endpoint RDS, URL ECR...

# Chạy test — KHÔNG tạo resource, $0
cd ../../modules/network && terraform init && terraform test

# Xem một resource cụ thể trong state
terraform state list | grep nacl
terraform state show 'module.network.aws_network_acl.app'
```

> `terraform plan` in "No changes" **sau** khi apply là một phép kiểm quan trọng:
> nó chứng minh code và thực tế đã khớp, tức **không có drift**. Nếu plan vẫn thấy
> thay đổi sau khi apply xong, nghĩa là có gì đó đang bị sửa ngoài Terraform.

---

## 9. Đọc tiếp

| Cần gì | Mở |
|---|---|
| Sơ đồ hạ tầng dạng hình | [diagrams/hushstore-aws-2026.drawio](diagrams/hushstore-aws-2026.drawio) |
| Vì sao thiết kế như vậy | [thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md) |
| Học Terraform từ đầu, nguồn tiếng Việt | cùng file trên, **Phần VI** |
| Chạy thật: bật, tắt, deploy, sự cố | [terraform-runbook.md](terraform-runbook.md) |
| Từng quyết định đến từ đâu, vấp ở đâu | [nhat-ky-trien-khai.md](nhat-ky-trien-khai.md) |
| CI/CD giải thích từ đầu | [cicd-cho-nguoi-moi.md](cicd-cho-nguoi-moi.md) |
