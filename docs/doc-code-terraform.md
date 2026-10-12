# Đọc code Terraform của dự án này — cho người chưa đọc Terraform bao giờ

`infra/tf/` có **89 file, khoảng 12.800 dòng**. Đọc từ đầu tới cuối thì không ai
đọc nổi, và cũng không cần. Tài liệu này giúp bạn ba việc:

1. **Hiểu đủ cú pháp** để tự đọc được bất kỳ file `.tf` nào trong dự án (§2).
2. **Biết file nào quan trọng**, mở file nào trước (§3–§5).
3. **Không hiểu sai** những chỗ mà người mới gần như chắc chắn hiểu sai (§8).

> **Bạn không cần biết AWS hay Terraform trước.** Gặp từ lạ như *subnet*, *NAT*,
> *security group* thì tra **§11 — Từ điển**. Mọi ví dụ trong tài liệu đều chép
> từ code thật của dự án, kèm đường dẫn file để bạn mở ra đối chiếu.

**Đọc theo lượng thời gian bạn có:**

| Bạn có | Đọc |
|---|---|
| 20 phút | §1 + §2 — đủ để mở một file `.tf` mà không bị ngợp |
| 1 tiếng | thêm §3 → §6 — biết hệ thống gồm những gì và một cấu hình chạy xuyên qua nó ra sao |
| 2 tiếng | đọc hết, rồi mở 3 file ở §5 ra đọc thật |

---

## 1. Terraform làm gì — bức tranh trong 5 phút

### Không có Terraform thì sao?

Muốn có một mạng riêng trên AWS, bạn vào web AWS (gọi là *Console*), bấm "Create
VPC", điền tên, điền dải IP, bấm OK. Rồi tạo subnet, bấm tiếp. Rồi route table…
Hệ thống này có **hơn 100 thứ** như vậy. Làm bằng tay thì:

- Không ai nhớ đã bấm gì, nên không làm lại được y hệt.
- Không ai review được (không có gì để đọc).
- Ai đó sửa một ô trên web thì không ai biết.

### Terraform đổi cách làm

Bạn **viết ra file** "tôi muốn có một VPC, dải IP này, tên này". Terraform đọc file
rồi **tự đi bấm** giúp bạn (thực ra là gọi API của AWS).

Giống như **bản vẽ nhà**: bạn không tự xây, bạn vẽ bản vẽ, thợ (Terraform) nhìn bản
vẽ mà xây. Muốn sửa nhà thì sửa bản vẽ, thợ tự tìm ra chỗ khác nhau và sửa đúng chỗ
đó.

### Ba thứ cần phân biệt

```
   CODE (.tf)                STATE                     AWS THẬT
   "tôi MUỐN có gì"    "sổ ghi: dòng code nào     "đang THẬT SỰ có gì"
                        ứng với thứ nào trên AWS"
        │                        │                         │
        └──────── terraform plan so sánh cả ba ────────────┘
                                 │
                 terraform apply làm cho AWS khớp với code
```

- **Code** — các file `.tf` trong repo. Đây là thứ bạn đọc.
- **AWS thật** — những thứ đang chạy và đang tính tiền.
- **State** — một file JSON Terraform tự ghi, kiểu sổ sách: "resource `aws_vpc.this`
  trong code chính là VPC có ID `vpc-0abc…` trên AWS". Không có sổ này thì Terraform
  không biết VPC nào trên AWS là "của mình". Xem thêm ở §9.

### Hai lệnh chính

| Lệnh | Làm gì | Nguy hiểm không |
|---|---|---|
| `terraform plan` | So code với thực tế, **in ra** sẽ thêm / sửa / xoá gì | **Không** — chỉ xem, không đổi gì |
| `terraform apply` | **Làm thật** những thay đổi đó trên AWS | **Có** — tạo, sửa, xoá thứ thật, tốn tiền thật |

> Nhóm đang **chỉ đọc code**. Bạn không cần chạy lệnh nào, và **tuyệt đối không
> chạy `terraform apply` hay `terraform destroy`**.

---

## 2. Cú pháp HCL — đủ để đọc mọi file trong dự án

Ngôn ngữ trong file `.tf` gọi là **HCL**. Nó nhỏ hơn bạn tưởng: cả dự án chỉ dùng
khoảng chục cấu trúc lặp đi lặp lại.

### 2.1. Mọi thứ là một "khối"

Mọi file `.tf` là một chuỗi **khối** (block) nối tiếp nhau. Một khối trông thế này
(chép từ [`modules/network/vpc.tf`](../infra/tf/modules/network/vpc.tf)):

```hcl
resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "${var.project}-igw" }
}
```

Tách từng phần ra:

```
resource   "aws_internet_gateway"   "this"   {  ...  }
   │               │                   │         │
   │               │                   │         └─ thân khối: các dòng  tên = giá trị
   │               │                   └─ TÊN do mình đặt, để chỗ khác gọi tới
   │               └─ LOẠI tài nguyên trên AWS (do AWS/Terraform quy định sẵn)
   └─ từ khoá: "hãy TẠO một thứ"
```

Đọc thành câu: *"Tạo một Internet Gateway, đặt tên trong code là `this`, gắn vào
VPC `aws_vpc.this`, và gắn nhãn Name = `<tên dự án>-igw` trên AWS."*

Hai điều người mới hay thắc mắc:

- **Tên `this` không có nghĩa đặc biệt.** Đó chỉ là quy ước khi module chỉ có một
  cái thuộc loại đó. Có nhiều cái thì đặt tên khác: `aws_subnet.public`,
  `aws_subnet.app`, `aws_subnet.db`.
- **Thứ tự các khối trong file không quan trọng.** Terraform tự tìm thứ tự (xem
  §2.3). Bạn có thể đọc khối nào trước cũng được.

### 2.2. Bảy loại khối

Cả dự án chỉ có bảy loại khối. Đọc được bảy loại này là đọc được mọi file.

| Khối | Nghĩa | Ví dụ trong dự án | So với lập trình thường |
|---|---|---|---|
| `resource` | **Tạo** một thứ trên AWS | `resource "aws_vpc" "this"` | tạo object |
| `data` | **Đọc** một thứ đã có sẵn, không tạo gì | `data "aws_caller_identity" "current"` (đọc account ID) | gọi hàm chỉ đọc |
| `variable` | Tham số đầu vào | `variable "enable_nat"` | tham số của hàm |
| `output` | Giá trị trả ra cho bên ngoài | `output "vpc_id"` | `return` |
| `locals` | Biến tạm tính sẵn, dùng lại trong module | `locals { nat_count = ... }` | biến cục bộ |
| `module` | Gọi một module khác (một thư mục `.tf` khác) | `module "network"` | gọi hàm |
| `terraform` | Cấu hình của chính Terraform: phiên bản, nơi để state | [`envs/prod/backend.tf`](../infra/tf/envs/prod/backend.tf) | file cấu hình build |

Hiểu **module = hàm** là chìa khoá: một thư mục trong `modules/` là một hàm,
`variables.tf` là danh sách tham số, `outputs.tf` là giá trị trả về, và
`envs/prod/main.tf` là chương trình chính gọi các hàm đó.

Một khối `variable` đầy đủ trông thế này
([`modules/network/variables.tf`](../infra/tf/modules/network/variables.tf)):

```hcl
variable "enable_nat" {
  description = "Tạo NAT Gateway. $0,059/giờ MỖI cái + $0,045/GB — chỉ bật khi cần egress (pull ECR, SSM)"
  type        = bool       # chỉ nhận true / false
  default     = false      # không ai truyền vào thì lấy giá trị này
}
```

Có thể kèm khối `validation` để **từ chối giá trị sai** ngay từ đầu:

```hcl
variable "nat_gateway_count" {
  type    = number
  default = 1

  validation {
    condition     = var.nat_gateway_count >= 1 && var.nat_gateway_count <= length(var.azs)
    ...
  }
}
```

Đọc thành câu: *"Số NAT phải từ 1 tới số AZ. Truyền 3 trong khi chỉ có 2 AZ thì
Terraform báo lỗi, không tạo gì."*

### 2.3. Gọi tới thứ khác — năm kiểu tên

Đây là phần quan trọng nhất của cú pháp. Gặp một cái tên có dấu chấm, hãy nhìn
**chữ đầu tiên** để biết nó trỏ tới đâu:

| Viết | Nghĩa | Tìm nó ở đâu |
|---|---|---|
| `var.enable_nat` | giá trị của biến đầu vào `enable_nat` | `variables.tf` của **cùng module** |
| `local.nat_count` | biến tạm `nat_count` | khối `locals { }` trong **cùng module** |
| `aws_vpc.this.id` | thuộc tính `id` của resource `aws_vpc` tên `this` | một file `.tf` trong cùng module |
| `data.aws_caller_identity.current.account_id` | thuộc tính của một khối `data` | cùng module |
| `module.network.vpc_id` | output `vpc_id` của module `network` | `modules/network/outputs.tf` |

Một điều rất hay: **những tham chiếu này chính là thứ quyết định thứ tự tạo**.
Không file nào viết "tạo VPC trước, tạo Internet Gateway sau". Terraform thấy khối
Internet Gateway có `vpc_id = aws_vpc.this.id`, tức cần ID của VPC, nên tự hiểu
VPC phải có trước.

Hiếm khi Terraform không tự suy ra được, code mới phải nói thẳng bằng
`depends_on = [...]` (ví dụ ở §2.6).

### 2.4. Giá trị và kiểu dữ liệu

| Kiểu | Ví dụ trong dự án | Ghi chú |
|---|---|---|
| chuỗi (`string`) | `"10.20.0.0/16"` | luôn trong ngoặc kép |
| số (`number`) | `5432` | |
| đúng/sai (`bool`) | `true`, `false` | |
| danh sách (`list`) | `["api", "web", "migrator", "seeder"]` | đánh chỉ số từ 0: `[0]` là phần tử đầu |
| bảng (`map` / `object`) | `{ no = 100, proto = "tcp", action = "allow" }` | lấy giá trị bằng dấu chấm: `.no`, `.proto` |

**Ghép chuỗi** bằng `${ }`:

```hcl
tags = { Name = "${var.project}-igw" }    # var.project = "hushstore"  →  "hushstore-igw"
```

**Ghi chú** (comment) bắt đầu bằng `#`. Code dự án này có **rất nhiều** comment tiếng
Việt giải thích *vì sao*, và chúng thường là phần đáng đọc nhất. Đừng bỏ qua.

### 2.5. Các biểu thức hay gặp

Bảy dạng dưới đây chiếm gần hết phần "logic" trong code.

**① Chọn một trong hai: `điều_kiện ? nếu_đúng : nếu_sai`**

```hcl
nat_count = var.enable_nat ? var.nat_gateway_count : 0
```

*"Nếu bật NAT thì số NAT bằng `nat_gateway_count`, không thì bằng 0."*

**② Đếm: `length(...)`**

```hcl
count = length(var.public_subnet_cidrs)    # có 2 dải IP → tạo 2 subnet
```

**③ Lấy tất cả: `[*]`**

```hcl
subnet_ids = aws_subnet.public[*].id       # danh sách ID của MỌI public subnet
```

**④ Biến đổi danh sách: `[for x in danh_sách : biểu_thức]`**

```hcl
[for s in aws_subnet.app : s.map_public_ip_on_launch == false]
```

*"Với mỗi app subnet, kiểm xem nó có tắt tự gán IP công khai không"* → ra một
danh sách `true`/`false`. Hay đi kèm `alltrue(...)`: *"tất cả đều đúng chứ?"*.

**⑤ Biến danh sách thành bảng: `{for x in danh_sách : khoá => giá_trị}`**

```hcl
{ for r in local.public_ingress : tostring(r.no) => r }
```

*"Lấy danh sách rule, biến thành bảng mà khoá là số thứ tự rule"*, ví dụ
`{ "100" = {...}, "110" = {...}, "120" = {...} }`. Dạng này luôn đi cùng
`for_each` (§2.6).

**⑥ Hàm có sẵn.** Gặp hàm lạ thì tra tên trên trang hàm của Terraform. Dự án dùng
nhiều nhất:

| Hàm | Ví dụ | Kết quả |
|---|---|---|
| `length(x)` | `length(["a","b"])` | `2` |
| `min(a, b)` | `min(1, 0)` | `0` |
| `substr(s, vị_trí, độ_dài)` | `substr("ap-southeast-1a", -1, 1)` | `"a"` (ký tự cuối) |
| `cidrsubnet(dải, thêm_bit, số)` | `cidrsubnet("10.20.0.0/16", 7, 5)` | `"10.20.10.0/23"` |
| `toset(list)` | `toset(["api","web"])` | tập hợp, để dùng với `for_each` |
| `tostring(x)` | `tostring(100)` | `"100"` |

**⑦ Đọc thuộc tính lồng nhau:** `each.value.no`, `aws_nat_gateway.this[0].tags["Name"]`.
Cứ đọc từ trái sang phải, mỗi dấu chấm hoặc `[ ]` là đi sâu vào một tầng.

### 2.6. Bốn "meta-argument" — thứ điều khiển cách tạo

Đây là các dòng đặc biệt có thể đặt trong **bất kỳ** khối `resource` nào. Chúng
không phải thuộc tính của AWS, mà là lệnh cho Terraform.

#### `count` — tạo N bản giống nhau

```hcl
resource "aws_nat_gateway" "this" {
  count = local.nat_count                                  # 0, 1 hoặc 2

  allocation_id = aws_eip.nat[count.index].id              # NAT thứ i dùng IP thứ i
  subnet_id     = aws_subnet.public[count.index].id        # và nằm ở subnet thứ i

  tags = { Name = "${var.project}-natgw-${substr(var.azs[count.index], -1, 1)}" }

  depends_on = [aws_internet_gateway.this]
}
```

- `count.index` là số thứ tự của bản đang tạo: `0`, `1`, ….
- Với `nat_count = 2`, Terraform tạo `aws_nat_gateway.this[0]` (tên
  `hushstore-natgw-a`) và `aws_nat_gateway.this[1]` (tên `hushstore-natgw-b`).
- **Có `count` thì resource thành một danh sách.** Muốn gọi tới nó phải ghi chỉ số:
  `aws_nat_gateway.this[0].id`, không được ghi `aws_nat_gateway.this.id`.

> 🚨 **Chỗ người mới hiểu sai nhiều nhất: `count = 0` KHÔNG phải "tắt", mà là XOÁ.**
> Đổi `enable_nat` từ `true` về `false` rồi `apply` thì Terraform **xoá hẳn** NAT
> Gateway trên AWS, không phải cho nó "ngủ". Đó chính là cách dự án đưa chi phí NAT
> về $0 thật — và cũng là lý do bật lại mất vài phút (phải tạo mới) chứ không phải
> vài giây.

#### `for_each` — tạo một bản cho mỗi phần tử của bảng

```hcl
# modules/network/nacl.tf
resource "aws_network_acl_rule" "public_ingress" {
  for_each = { for r in local.public_ingress : tostring(r.no) => r }

  network_acl_id = aws_network_acl.public.id
  rule_number    = each.value.no        # each.value = phần tử đang xét
  protocol       = each.value.proto
  rule_action    = each.value.action
  cidr_block     = each.value.cidr
  from_port      = each.value.from
  to_port        = each.value.to
  egress         = false
}
```

`local.public_ingress` là danh sách 3 rule (số 100, 110, 120), nên khối này tạo **3**
rule NACL. `each.key` là khoá (`"100"`), `each.value` là cả object của rule đó.

**Vì sao không dùng `count`?** Với `count`, rule được đánh số theo vị trí 0, 1, 2. Chèn
một rule vào giữa thì mọi rule phía sau bị lệch vị trí, Terraform tưởng chúng đã đổi
và tạo lại hết. Với `for_each`, mỗi rule gắn với khoá cố định là số rule, nên chèn
thêm không ảnh hưởng ai. Dự án dùng `for_each` cho 4 ECR repo và mọi rule NACL, SG.

#### `lifecycle` — đổi cách Terraform xử lý thay đổi

```hcl
lifecycle {
  prevent_destroy = true                 # cấm xoá — có lệnh xoá thì báo lỗi
}

lifecycle {
  ignore_changes = [task_definition]     # trường này có ai sửa ngoài Terraform thì kệ
}
```

Dự án dùng `prevent_destroy` cho bucket chứa state (§9), và `ignore_changes` cho ECS
service (§8 ②).

#### `depends_on` — nói thẳng thứ tự khi Terraform không tự đoán được

Ví dụ khối NAT ở trên có `depends_on = [aws_internet_gateway.this]`: NAT không dùng
trực tiếp thuộc tính nào của Internet Gateway, nhưng thiếu Internet Gateway thì NAT
không có đường ra internet.

#### Bonus: khối `moved`

```hcl
moved {
  from = aws_route_table.private
  to   = aws_route_table.private[0]
}
```

Khi code đổi tên hoặc thêm `count` vào một resource **đã tồn tại**, khối này báo
Terraform: *"cái cũ chính là cái mới, đừng xoá đi tạo lại"*. Nó chỉ đổi sổ sách
(state), không đụng gì trên AWS.

### 2.7. Tập đọc: dịch một khối ra tiếng Việt

Cách học nhanh nhất là tự dịch từng dòng thành một câu. Thử với khối này trong
[`modules/network/vpc.tf`](../infra/tf/modules/network/vpc.tf):

```hcl
resource "aws_route" "private_nat" {
  count = local.nat_count > 0 ? length(aws_route_table.private) : 0

  route_table_id         = aws_route_table.private[count.index].id
  destination_cidr_block = "0.0.0.0/0"
  nat_gateway_id         = aws_nat_gateway.this[min(count.index, local.nat_count - 1)].id
}
```

| Dòng | Đọc thành |
|---|---|
| `resource "aws_route" "private_nat"` | Tạo các **đường đi** (route), đặt tên trong code là `private_nat`. |
| `count = local.nat_count > 0 ? length(...) : 0` | Có NAT thì tạo mỗi private route table một đường (2 cái). Không có NAT thì không tạo đường nào. |
| `route_table_id = ...private[count.index].id` | Đường thứ i nằm trong route table thứ i. |
| `destination_cidr_block = "0.0.0.0/0"` | Đường này áp dụng cho **mọi** địa chỉ đích, tức là "ra internet". |
| `nat_gateway_id = ...this[min(count.index, local.nat_count - 1)].id` | Đi ra qua NAT nào? Có 2 NAT thì `min(0,1)=0`, `min(1,1)=1`: mỗi AZ dùng NAT của mình. Có 1 NAT thì `min(0,0)=0`, `min(1,0)=0`: cả hai AZ dùng chung NAT số 0. |

Cả khối gói lại: *"Khi bật NAT, cho app subnet ra internet qua NAT — mỗi AZ qua NAT
của chính nó nếu có đủ NAT."*

Ngay trên khối này trong file có một đoạn comment giải thích đúng công thức `min`,
kèm cái giá phải trả khi hai AZ dùng chung một NAT. Code dự án này hầu như luôn có
comment như vậy ở chỗ khó — **đọc comment trước, đọc code sau**.

---

## 3. Ba tầng thư mục

```
infra/tf/
├── bootstrap/     ← tạo chỗ chứa state. Chạy MỘT LẦN duy nhất. Đọc sau cùng (§9).
├── envs/prod/     ← "chương trình chính". Không tự tạo resource, chỉ gọi 8 module.
├── modules/       ← 8 module, mỗi module một mảng hạ tầng. Chi tiết thật nằm ở đây.
└── scripts/       ← 6 script bash bật/tắt/xem trạng thái. Không phải Terraform.
```

### `envs/prod/` — nơi bắt đầu

| File | Chứa | Dòng |
|---|---|---|
| [`main.tf`](../infra/tf/envs/prod/main.tf) | gọi 8 module, nối output module này vào input module kia | 195 |
| [`variables.tf`](../infra/tf/envs/prod/variables.tf) | khai báo biến của môi trường prod, **kèm default của prod** | 225 |
| [`outputs.tf`](../infra/tf/envs/prod/outputs.tf) | 28 giá trị in ra sau khi apply: DNS của ALB, endpoint RDS, URL ECR… | 152 |
| [`backend.tf`](../infra/tf/envs/prod/backend.tf) | phiên bản Terraform/provider, nơi để state | 30 |
| `providers.tf` | dùng AWS ở region nào, bằng tài khoản nào | 12 |
| `terraform.tfvars` | giá trị thật của biến cho prod. **Không có trong git**, mỗi máy tự có | — |

Mở `main.tf` ra là thấy ngay hình dạng của cả hệ thống, vì mỗi module ghi rõ nó cần
gì từ module nào:

```hcl
module "data" {
  source = "../../modules/data"            # code của module nằm ở thư mục này

  project       = local.name
  db_subnet_ids = module.network.db_subnet_ids   # ← cần subnet từ network
  rds_sg_id     = module.security.rds_sg_id      # ← cần security group từ security
  ...
}
```

Đọc thành câu: *"Gọi module `data`, đưa cho nó danh sách DB subnet mà module
`network` tạo ra, và security group RDS mà module `security` tạo ra."* Vì dùng
output của hai module kia, `data` tự động được tạo **sau** hai module đó.

### `modules/<tên>/` — mọi module có cùng bộ file

Đây là quy ước. Biết quy ước thì không phải đoán file nào chứa gì:

| File | Chứa |
|---|---|
| `main.tf`, hoặc tên theo chủ đề (`vpc.tf`, `nacl.tf`, `s3.tf`, `iam.tf`…) | các khối `resource` — **phần chính** |
| `variables.tf` | đầu vào của module, kèm `default` |
| `outputs.tf` | đầu ra để module khác dùng |
| `versions.tf` | yêu cầu phiên bản provider |
| `tests/*.tftest.hcl` | test tự động (§7) |

---

## 4. Bản đồ 8 module

Cột cuối là cột nên dùng nhất: **mở file nào trước**.

| Module | Tạo gì | Dòng (không tính test) | Mở file này trước |
|---|---|---|---|
| [`network`](../infra/tf/modules/network/) | VPC, 6 subnet (3 tầng × 2 AZ), Internet Gateway, 0–2 NAT, 4 route table, **3 Network ACL**, S3 endpoint, ECR endpoint tuỳ chọn, Flow Logs tuỳ chọn | 759 | [`nacl.tf`](../infra/tf/modules/network/nacl.tf) — 178 dòng, phần kỹ thuật đáng giá nhất cả đồ án |
| [`security`](../infra/tf/modules/security/) | 3 Security Group: `alb`, `web`, `rds` | 247 | [`main.tf`](../infra/tf/modules/security/main.tf) — cả module chỉ một file chính |
| [`storage`](../infra/tf/modules/storage/) | 4 ECR repo (`api`, `web`, `migrator`, `seeder`), 3 S3 bucket + policy/lifecycle/CORS | 392 | [`s3.tf`](../infra/tf/modules/storage/s3.tf) |
| [`data`](../infra/tf/modules/data/) | RDS PostgreSQL 17, subnet group, read replica tuỳ chọn, mật khẩu ngẫu nhiên, 4 tham số SSM | 498 | [`main.tf`](../infra/tf/modules/data/main.tf) |
| [`ecs`](../infra/tf/modules/ecs/) | cluster, Auto Scaling Group, launch template, 4 task definition, 2 service, **5 IAM role** | 1380 | [`iam.tf`](../infra/tf/modules/ecs/iam.tf) — có chữ `Deny` tường minh, xem §5 |
| [`alb`](../infra/tf/modules/alb/) | ALB, 2 target group, 2 listener + 2 rule, chứng chỉ HTTPS (ACM) | 393 | [`alb.tf`](../infra/tf/modules/alb/alb.tf) |
| [`cicd`](../infra/tf/modules/cicd/) | kết nối GitHub ↔ AWS bằng OIDC, 2 IAM role (deploy + plan) | 629 | [`policy.tf`](../infra/tf/modules/cicd/policy.tf) — 282 dòng, chỗ chặn pipeline tự bật hạ tầng |
| [`costguard`](../infra/tf/modules/costguard/) | Lambda tự tắt hạ tầng theo lịch, EventBridge Scheduler, Budgets, SNS | 1521 | [`src/cost_guard.py`](../infra/tf/modules/costguard/src/cost_guard.py) — 581 dòng Python, không phải Terraform |

Hình dung hệ thống từ ngoài vào trong:

```
 Người dùng ──► ALB (public subnet) ──► container trên EC2 (app subnet) ──► RDS (db subnet)
                 │ module alb                │ module ecs                     │ module data
                 └────── tất cả nằm trong VPC + subnet + NACL của module network ──────┘
                         và được canh cổng bởi 3 security group của module security
 Image container lấy từ ECR, file ảnh để ở S3            ← module storage
 GitHub Actions deploy bằng IAM role                      ← module cicd
 Tối tự tắt để khỏi tốn tiền                              ← module costguard
```

---

## 5. Đọc theo thứ tự nào

Đọc theo đúng thứ tự phụ thuộc, vì module sau dùng output của module trước:

```
1. network    →  VPC và subnet phải có trước mọi thứ
2. security   →  cần vpc_id
3. storage    →  độc lập, đọc lúc nào cũng được
4. data       →  cần db_subnet_ids + rds_sg_id
5. ecs        →  cần gần như tất cả những cái trên
6. alb        →  cần vpc, public subnet, alb_sg, bucket log
7. cicd       →  độc lập với phần chạy app, chỉ cần tên/ARN để cấp quyền
8. costguard  →  cần tên cluster / ASG / RDS để biết tắt cái gì
```

**Nếu chỉ có 1 tiếng**, đọc đúng ba file này. Chúng chứa phần lớn thứ đáng trình bày
trước hội đồng:

1. [`modules/network/nacl.tf`](../infra/tf/modules/network/nacl.tf) — Network ACL.
   Đọc khối `locals` ở đầu file: 6 danh sách rule, mỗi rule một dòng. Chú ý comment
   giải thích **vì sao rule 90/95/115 (DENY) phải có số nhỏ hơn rule 120 (ALLOW)**:
   NACL xét rule từ số nhỏ tới số lớn và dừng ở rule khớp đầu tiên. Rule 120 phải mở
   cả dải cổng 1024–65535 để nhận phản hồi, mà 5432 (cổng PostgreSQL) và 8080 nằm
   trong dải đó. Rule 95 chặn 5432 **trước**, nên rule 120 không vô tình mở cổng DB.
2. [`modules/security/main.tf`](../infra/tf/modules/security/main.tf) — 3 Security
   Group. Chú ý: chỉ `alb` nhận traffic từ internet (`0.0.0.0/0`). `web` chỉ nhận từ
   **security group** `alb`, và `rds` chỉ nhận từ **security group** `web` — chúng gọi
   nhau bằng ID của security group (`source_sg = aws_security_group.alb.id`), không
   bằng dải IP.
3. [`modules/ecs/iam.tf`](../infra/tf/modules/ecs/iam.tf) — 5 IAM role. Tìm chữ
   `effect = "Deny"`: máy EC2 bị **cấm tường minh** đọc secret của dự án, và comment
   giải thích vì sao phải liệt kê đủ **bốn** action (`GetParameter`, `GetParameters`,
   `GetParameterHistory`, `GetParametersByPath`) chứ không phải ba.

---

## 6. Đi theo một biến: `enable_nat` chạy xuyên hệ thống thế nào

Ví dụ này đáng theo hết, vì bật/tắt NAT là cơ chế tiết kiệm tiền trung tâm của dự án,
và nó cho thấy các tầng ghép với nhau ra sao. Một giá trị đi qua **năm chặng**:

```
terraform.tfvars ──► envs/prod/variables.tf ──► envs/prod/main.tf ──► modules/network/variables.tf ──► modules/network/vpc.tf
 (giá trị thật)        (khai báo + default)      (truyền vào module)    (module nhận)                     (dùng làm count)
```

**Chặng 1 — giá trị thật** nằm trong `envs/prod/terraform.tfvars` (file không có trong
git):

```hcl
enable_nat = false
```

**Chặng 2 — môi trường prod khai báo biến** ([`envs/prod/variables.tf`](../infra/tf/envs/prod/variables.tf)):

```hcl
variable "enable_nat" {
  type    = bool
  default = false
}

variable "nat_gateway_count" {
  type    = number
  default = 2          # ← prod GHIM 2: mỗi AZ một NAT
}
```

**Chặng 3 — truyền vào module** ([`envs/prod/main.tf`](../infra/tf/envs/prod/main.tf)):

```hcl
module "network" {
  source            = "../../modules/network"
  enable_nat        = var.enable_nat
  nat_gateway_count = var.nat_gateway_count
  ...
}
```

Vế trái (`enable_nat`) là tên biến **bên trong module**, vế phải (`var.enable_nat`) là
biến **của prod**. Hai cái trùng tên cho dễ đọc, nhưng là hai biến khác nhau ở hai
nơi.

**Chặng 4 — module nhận** ([`modules/network/variables.tf`](../infra/tf/modules/network/variables.tf)):

```hcl
variable "nat_gateway_count" {
  type    = number
  default = 1          # ← module mặc định 1: rẻ nhất
}
```

**Chặng 5 — module dùng nó** ([`modules/network/vpc.tf`](../infra/tf/modules/network/vpc.tf)):

```hcl
locals {
  nat_count = var.enable_nat ? var.nat_gateway_count : 0
}

resource "aws_eip" "nat"           { count = local.nat_count  ... }
resource "aws_nat_gateway" "this"  { count = local.nat_count  ... }
resource "aws_route" "private_nat" { count = local.nat_count > 0 ? length(aws_route_table.private) : 0  ... }
```

Kết quả:

| `enable_nat` | `nat_gateway_count` | Tạo ra |
|---|---|---|
| `false` | bất kỳ | 0 NAT, 0 IP tĩnh, 0 route ra internet — $0 |
| `true` | 1 | 1 NAT, cả hai AZ đi chung |
| `true` | 2 | 2 NAT, mỗi AZ một cái — prod dùng cấu hình này khi bật |

### Giá trị cuối cùng lấy từ đâu?

Terraform lấy theo thứ tự ưu tiên: **giá trị trong `terraform.tfvars`** thắng; không
có thì lấy **`default`** trong `variables.tf`.

Chú ý `nat_gateway_count` có **hai default khác nhau**: module ghi `1`, prod ghi `2`.
Cả hai đều đúng, vì chúng trả lời hai câu hỏi khác nhau:

- **Default của module** = "ai dùng lại module này mà không nói gì thì nên được cấu
  hình an toàn và rẻ nhất".
- **Default của prod** = "môi trường thật của dự án chọn gì".

Prod truyền `2` vào, nên `1` của module không bao giờ được dùng ở prod.

> ⚠️ Muốn biết prod **thật sự** chạy cấu hình gì, phải đọc **cả hai tầng**
> (`modules/.../variables.tf` lẫn `envs/prod/variables.tf`). Đọc một tầng rồi kết
> luận là cách hiểu sai phổ biến nhất trong repo này — nó từng xảy ra với chính
> `enable_multi_az` (module `false`, prod `true`).

### Và có test canh

Trong [`modules/network/tests/vpc.tftest.hcl`](../infra/tf/modules/network/tests/vpc.tftest.hcl):

```hcl
run "private_route_table_khong_co_route_ra_igw" {
  command = plan

  assert {
    condition     = length(aws_nat_gateway.this) == 0
    error_message = "Khi enable_nat = false thì không được tạo NAT Gateway (tốn $0,059/giờ mỗi cái)."
  }
}
```

Điều rút ra: **một chữ `true`/`false` trong file text quyết định hoá đơn AWS**, và có
test tự động canh để không ai vô tình đảo nó. "Hạ tầng như code" trong thực tế nghĩa
là vậy.

---

## 7. Đọc file test (`.tftest.hcl`)

Mỗi module có thư mục `tests/`. Cả dự án có **12 file test, 109 khối `run`**. Test
giúp bạn đọc code nhanh hơn, vì mỗi `error_message` là một câu tiếng Việt nói thẳng
**điều code phải đảm bảo**.

### Cấu trúc một file test

```hcl
# 1. Giá trị đầu vào giả để chạy thử module
variables {
  project    = "hushstore-tftest"
  vpc_cidr   = "10.20.0.0/16"
  enable_nat = false
  ...
}

# 2. Mỗi "run" là một ca test
run "bat_nat_mac_dinh_1_gateway_nhung_2_route_table" {
  command = plan                 # chỉ LÊN KẾ HOẠCH, không tạo gì thật → $0

  variables {
    enable_nat = true            # ghi đè riêng cho ca này
  }

  # 3. Mỗi assert là một điều phải đúng
  assert {
    condition     = length(aws_nat_gateway.this) == 1
    error_message = "Mặc định nat_gateway_count = 1: chỉ 1 NAT Gateway ($0,059/giờ). Muốn 2 phải khai tường minh."
  }
}
```

Đọc thành câu: *"Bật NAT mà không nói số lượng thì phải ra đúng 1 NAT. Sai thì in câu
báo lỗi kia."*

### Ca test "phải bị từ chối"

```hcl
run "nat_nhieu_hon_so_az_thi_bi_chan" {
  command = plan
  variables {
    enable_nat        = true
    nat_gateway_count = 3
  }
  expect_failures = [var.nat_gateway_count]
}
```

`expect_failures` đảo ngược ý nghĩa: test **đạt** khi Terraform **từ chối** giá trị
này. Nó kiểm khối `validation` ở §2.2 thật sự hoạt động.

### Cách đọc nhanh một module bằng test

Mở file test, đọc lướt **tên các `run`** và **các `error_message`**. Chỉ vài phút bạn
đã có danh sách "module này hứa những gì". Ví dụ tên các ca trong
[`sg.tftest.hcl`](../infra/tf/modules/security/tests/sg.tftest.hcl):
`khong_co_bat_ky_rule_nao_mo_port_22`, `khong_co_ingress_rule_nao_mo_0000_ngoai_alb`,
`alb_chi_mo_dung_2_rule_80_va_443`… Đọc tên là biết luật.

Giới hạn của test chạy ở `command = plan` có ở §8 ③.

---

## 8. Bốn chỗ dễ hiểu sai

### ① `aws_default_security_group` KHÔNG tạo security group mới

Ở [`modules/network/vpc.tf`](../infra/tf/modules/network/vpc.tf):

```hcl
resource "aws_default_security_group" "this" {
  vpc_id = aws_vpc.this.id

  # Cố ý KHÔNG có ingress và egress. Đây là toàn bộ mục đích của resource này.

  tags = { Name = "${var.project}-default-KHONG-DUNG" }
}
```

Người mới nhìn sẽ tưởng: *"tạo một security group rỗng để làm gì?"*

Thật ra: AWS **tự** tạo một security group tên `default` cho **mọi** VPC, **không cho
xoá**, và mặc định nó cho các máy cùng nằm trong nó nói chuyện với nhau ở **mọi cổng**,
kể cả 22 (SSH). Khối này không tạo gì mới. Nó **nhận quản lý** cái AWS đã tạo, và vì
không có `ingress`/`egress` nên **xoá sạch rule** của nó.

Khác biệt tinh tế: **để trống** khối `ingress`/`egress` nghĩa là "không có rule nào".
Còn **bỏ hẳn cả resource** thì Terraform không quản default SG nữa, và nó giữ nguyên
mặc định mở của AWS.

### ② `ignore_changes = [task_definition]` không phải để cho tiện

Ở [`modules/ecs/service.tf`](../infra/tf/modules/ecs/service.tf). Bối cảnh: có **hai**
bên cùng muốn quyết định service đang chạy phiên bản nào:

- **Terraform**: trong code ghi image tag nào thì muốn service chạy tag đó.
- **Pipeline GitHub Actions**: mỗi lần merge code, nó build image mới và cập nhật
  service sang bản mới.

Không có `ignore_changes`, lần `terraform apply` kế tiếp sẽ thấy service "khác code"
và **kéo về bản cũ** — tức âm thầm rollback bản pipeline vừa deploy, không cảnh báo
gì. Dòng này là cách khai báo: *"phiên bản đang chạy là việc của pipeline, Terraform
đừng đụng."*

### ③ Test chạy ở `command = plan`, nên có thứ nó KHÔNG kiểm được

`plan` không tạo gì thật, nên chạy test tốn $0. Cái giá: giá trị nào **chỉ biết sau
khi tạo thật** thì test không kiểm được. Lúc plan, Terraform chỉ biết nó là *"known
after apply"* (sẽ biết sau).

Ví dụ rõ nhất, có comment dài trong
[`vpc.tftest.hcl`](../infra/tf/modules/network/tests/vpc.tftest.hcl): không thể viết
`length(aws_default_security_group.this.ingress) == 0`. Lúc plan, danh sách rule của
default SG là "sẽ biết sau", vì provider phải hỏi AWS mới biết.

Nên test đó chỉ chứng minh được **resource có được khai báo**, chưa chứng minh
**rule đã rỗng**. Điều đó chỉ kiểm được sau khi apply, bằng lệnh:

```bash
aws ec2 describe-security-group-rules \
  --filters Name=group-id,Values=<default sg của VPC> \
  --query 'length(SecurityGroupRules)' --profile hushstore   # phải ra 0
```

Vậy test đó có ích gì? Nó **chống xoá**: ai lỡ xoá resource khỏi `vpc.tf` thì test đỏ
ngay. Phân biệt được "test **chứng minh** X đúng" với "test **chặn** không cho ai xoá
X" là một bước tiến lớn khi đọc code.

### ④ Một resource có `count` thì phải gọi bằng chỉ số

Đã nói ở §2.6 nhưng đáng nhắc lại vì nó gây lỗi thật: `aws_nat_gateway.this` là một
**danh sách**, nên phải viết `aws_nat_gateway.this[0].id`. Khi thêm `count` vào một
resource đang tồn tại, **mọi** chỗ gọi tới nó phải sửa theo, và resource cũ phải được
"dời" sang `[0]` bằng khối `moved` (§2.6) — nếu không Terraform sẽ xoá đi tạo lại.

---

## 9. State ở đâu, và vì sao `bootstrap/` phải tách riêng

Nhắc lại §1: **state** là cuốn sổ ghi "resource nào trong code ứng với resource nào
trên AWS". State của prod nằm trên S3, cấu hình ở
[`envs/prod/backend.tf`](../infra/tf/envs/prod/backend.tf):

```hcl
backend "s3" {
  bucket       = "hushstore-tfstate-551897327153"   # bucket chứa sổ
  key          = "prod/terraform.tfstate"           # đường dẫn file sổ trong bucket
  region       = "ap-southeast-1"
  encrypt      = true                               # mã hoá file trên S3
  use_lockfile = true                               # khoá khi có người đang apply
}
```

**`use_lockfile = true`** chặn hai người `apply` cùng lúc. Người thứ hai sẽ thấy báo
"đang bị khoá" thay vì hai người cùng sửa sổ rồi làm hỏng nó. Trước Terraform 1.10,
muốn khoá phải dùng thêm một bảng DynamoDB; từ 1.10 S3 tự làm được, nên dự án
**không dùng DynamoDB**.

**Vì sao cần thư mục `bootstrap/` riêng?** Vì bài toán con gà – quả trứng:

- State của prod phải nằm trong bucket S3.
- Bucket S3 đó cũng phải do Terraform tạo.
- Vậy state **của chính việc tạo bucket** để ở đâu? Không thể để trong bucket chưa
  tồn tại.

Cách giải: [`bootstrap/main.tf`](../infra/tf/bootstrap/main.tf) là một dự án Terraform
nhỏ riêng, **không có khối `backend`**, nên state của nó nằm ngay trên máy (file
`terraform.tfstate` cạnh code, không đưa lên git). Nó chỉ tạo đúng một bucket
(`hushstore-tfstate-<account ID>`), bật versioning để giữ các bản cũ của state, mã
hoá, chặn truy cập công khai, xoá bản cũ sau 90 ngày, và có
`prevent_destroy = true` để không ai xoá nhầm. Chạy một lần rồi thôi.

---

## 10. Lệnh tra nhanh (cho người có quyền chạy)

> Người chỉ đọc code **không cần** chạy phần này. Các lệnh `plan`, `test`, `state`
> cần quyền AWS của dự án. **Không bao giờ chạy `apply` hay `destroy`** nếu không được
> giao việc đó.

```bash
cd infra/tf/envs/prod

terraform fmt -check -recursive    # kiểm định dạng code — CI cũng chạy lệnh này
terraform validate                 # kiểm cú pháp và kiểu dữ liệu
terraform plan                     # in ra SẼ đổi gì. Không đổi gì thật.
terraform output                   # 28 giá trị: DNS của ALB, endpoint RDS, URL ECR…

# Chạy test của một module — chỉ plan, không tạo resource, $0
cd ../../modules/network && terraform init && terraform test

# Xem sổ state
terraform state list | grep nacl
terraform state show 'module.network.aws_network_acl.app'
```

> Sau khi `apply` xong, chạy lại `terraform plan` mà nó in **"No changes"** là một phép
> kiểm quan trọng: code và thực tế đã khớp nhau. Nếu plan vẫn thấy thay đổi, nghĩa là
> có ai đó (hoặc cái gì đó) đã sửa trên AWS mà không qua Terraform — gọi là **drift**.

---

## 11. Từ điển — thuật ngữ AWS gặp trong code

Xếp theo thứ tự gói tin đi từ người dùng vào database.

| Thuật ngữ | Giải thích ngắn | Trong code |
|---|---|---|
| **Region** | Một khu vực trung tâm dữ liệu của AWS. Dự án dùng Singapore. | `ap-southeast-1` |
| **AZ** (Availability Zone) | Một toà trung tâm dữ liệu riêng trong region. Dự án dùng 2 AZ để một toà sập thì vẫn chạy. | `ap-southeast-1a`, `-1b` |
| **VPC** | Mạng riêng ảo của mình trên AWS, cách ly với người khác. | `aws_vpc` |
| **CIDR** | Cách viết một dải địa chỉ IP. `/16` = 65.536 địa chỉ, `/24` = 256, `/23` = 512. `0.0.0.0/0` = mọi địa chỉ, tức "cả internet". | `10.20.0.0/16` |
| **Subnet** | Một khúc nhỏ của VPC, nằm trong đúng một AZ. Dự án có 3 tầng × 2 AZ = 6 subnet: **public** (ALB, NAT), **app** (máy chạy container), **db** (database). | `aws_subnet` |
| **Internet Gateway (IGW)** | Cửa nối VPC với internet. Subnet nào có đường tới nó thì gọi là public. | `aws_internet_gateway` |
| **NAT Gateway** | Cho máy trong subnet riêng **đi ra** internet (tải image, cập nhật), nhưng internet không gọi vào được. Tính tiền theo giờ, nên dự án hay tắt. | `aws_nat_gateway` |
| **EIP** (Elastic IP) | Địa chỉ IP công khai cố định. Mỗi NAT cần một cái. | `aws_eip` |
| **Route table** | Bảng chỉ đường: "đi tới địa chỉ X thì qua cửa Y". Dự án có 4: public, private-a, private-b, db. | `aws_route_table`, `aws_route` |
| **VPC Endpoint** | Đường tắt từ VPC tới dịch vụ AWS (S3, ECR) mà không phải đi qua internet/NAT. | `aws_vpc_endpoint` |
| **NACL** (Network ACL) | Tường lửa ở **cấp subnet**. **Không nhớ** kết nối (*stateless*): chiều đi và chiều về phải mở riêng. Rule có số thứ tự, xét từ nhỏ tới lớn, dừng ở rule khớp đầu tiên. Có cả ALLOW lẫn DENY. | `aws_network_acl`, `aws_network_acl_rule` |
| **Cổng ephemeral** | Cổng ngẫu nhiên (1024–65535) mà máy dùng để **nhận phản hồi**. Vì NACL không nhớ kết nối, phải mở dải này cho chiều về. | rule 120/130 trong `nacl.tf` |
| **Security Group (SG)** | Tường lửa ở **cấp từng máy**. **Có nhớ** kết nối (*stateful*): cho vào thì tự cho phản hồi đi ra. Chỉ có ALLOW. Có thể cho phép theo "đến từ SG khác" thay vì theo IP. | `aws_security_group` |
| **ALB** | Bộ cân bằng tải, cửa ngõ duy nhất người dùng gọi vào. Chia request cho các container phía sau. | `aws_lb` |
| **Target group** | Nhóm đích mà ALB chuyển request tới (ở đây: nhóm web và nhóm api). | `aws_lb_target_group` |
| **Listener / rule** | ALB nghe ở cổng nào (80, 443) và chuyển request theo luật nào (theo tên miền). | `aws_lb_listener` |
| **ACM** | Dịch vụ cấp chứng chỉ HTTPS miễn phí. | `aws_acm_certificate` |
| **ECS** | Dịch vụ chạy container của AWS. | `aws_ecs_cluster` |
| **Task definition** | "Công thức" chạy một container: image nào, bao nhiêu RAM, biến môi trường, secret. | `aws_ecs_task_definition` |
| **Service** | Giữ cho luôn có đủ N container theo một task definition đang chạy. | `aws_ecs_service` |
| **ASG** (Auto Scaling Group) | Nhóm máy EC2 tự giữ đủ số lượng. Container chạy trên các máy này. | `aws_autoscaling_group` |
| **ECR** | Kho chứa image Docker của dự án. | `aws_ecr_repository` |
| **S3 / bucket** | Kho chứa file. Một bucket là một "thùng" có tên duy nhất toàn cầu. | `aws_s3_bucket` |
| **RDS** | Database do AWS quản lý (ở đây là PostgreSQL 17). | `aws_db_instance` |
| **Multi-AZ** | RDS có thêm một bản dự phòng ở AZ khác, tự chuyển sang khi bản chính hỏng. | `multi_az` |
| **Read replica** | Bản sao chỉ đọc của database, để chia tải đọc. Dự án mặc định tắt. | `aws_db_instance.replica` |
| **SSM Parameter Store** | Chỗ cất cấu hình và bí mật. `SecureString` là loại được mã hoá — dùng cho mật khẩu DB, JWT secret. | `aws_ssm_parameter` |
| **IAM role / policy** | "Ai được làm gì". Role là danh tính, policy là danh sách quyền. `Allow` cho phép, `Deny` cấm và **luôn thắng** `Allow`. | `aws_iam_role` |
| **ARN** | Mã định danh đầy đủ của một thứ trên AWS, dạng `arn:aws:dịch-vụ:region:account:tên`. | `..._arn` |
| **OIDC** | Cách để GitHub Actions đăng nhập AWS mà không cần lưu key bí mật trong GitHub. | `aws_iam_openid_connect_provider` |
| **Provider** | Plugin giúp Terraform nói chuyện với một nền tảng. Dự án dùng `aws`, `random` (sinh mật khẩu), `archive` (đóng gói code Lambda). | `required_providers` |

---

## 12. Bị kẹt thì hỏi AI thế nào

AI giải thích code Terraform rất tốt, **miễn là bạn đưa đúng file**.

**Nên đính kèm:**

- File `.tf` đang đọc, **cộng** `variables.tf` của cùng module (để AI biết giá trị
  mặc định).
- File test `tests/*.tftest.hcl` của module đó — các `error_message` cho AI biết ý
  định của code.
- Tài liệu này.

**Tuyệt đối KHÔNG đính kèm:**

- `*.tfstate` (có trong `infra/tf/bootstrap/`) — chứa ID, ARN thật của tài khoản AWS.
- `*.tfvars` — chứa email và cấu hình riêng.
- Thư mục `infra/tf/.local/`.
- Cả thư mục `infra/` một lần — có `legacy-cli/` cũ gây nhiễu, AI sẽ trộn lẫn.

**Mẫu câu hỏi hiệu quả:**

```
Tôi là người mới, chưa đọc Terraform bao giờ. Giải thích càng dễ hiểu càng tốt.
Đính kèm: nacl.tf, variables.tf, nacl.tftest.hcl.
Hãy dịch khối `app_ingress` sang tiếng Việt, mỗi rule một câu, rồi giải thích
vì sao rule 95 phải có số nhỏ hơn rule 120. Chỗ nào bạn đoán chứ không đọc
thấy trong file thì nói rõ là đoán.
```

Dòng cuối quan trọng: AI hay "bịa cho trơn". Bắt nó tách **đọc thấy trong file** với
**tự suy ra**, rồi tự mở file đối chiếu những chỗ nó đoán.

---

## 13. Đọc tiếp

| Cần gì | Mở |
|---|---|
| Sơ đồ hạ tầng dạng hình | [diagrams/hushstore-aws-2026.drawio](diagrams/hushstore-aws-2026.drawio) |
| Vì sao thiết kế như vậy | [thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md) |
| Học Terraform từ đầu, nguồn tiếng Việt | cùng file trên, **Phần VI** |
| Từng quyết định đến từ đâu, vấp ở đâu | [nhat-ky-trien-khai.md](nhat-ky-trien-khai.md) |
| Chạy thật: bật, tắt, deploy, sự cố | [terraform-runbook.md](terraform-runbook.md) |
| CI/CD giải thích từ đầu | [cicd-cho-nguoi-moi.md](cicd-cho-nguoi-moi.md) |
