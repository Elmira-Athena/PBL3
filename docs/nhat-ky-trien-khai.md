# Nhật ký triển khai hệ thống — HushStore trên AWS

> **Tài liệu này kể lại việc đã làm, theo thứ tự, và vì sao mỗi bước phải làm
> như vậy.** Không có code. Mục đích là để người đọc hiểu *quy trình* — thứ mà
> đọc file cấu hình không thấy được — và biết trước những chỗ dễ sai.
>
> Đọc kèm [thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md) (giải thích *cái
> gì* và *vì sao thiết kế vậy*). Tài liệu này giải thích *làm thế nào*.
>
> Chưa từng dùng AWS thì làm workshop tay trước cho có cảm giác về resource:
> xem **Phần VI — Lộ trình học** trong tài liệu thiết kế, có bản đồ từ mỗi thành
> phần của hệ thống sang workshop tiếng Việt tương ứng.

---

# Phần I — Doanh nghiệp triển khai hạ tầng như thế nào

Trước khi vào nhật ký, cần hiểu bối cảnh: vì sao một công ty không làm theo cách
đơn giản nhất.

## Cách "tự nhiên" và vì sao nó sụp đổ

Cách đầu tiên ai cũng nghĩ tới: mở console AWS, bấm tạo máy chủ, SSH vào, cài
phần mềm, copy code lên, chạy. Nhanh, và với một người thì hoạt động tốt.

Nó sụp đổ ở bốn điểm, và luôn sụp theo đúng thứ tự này:

**Tháng thứ nhất — không ai biết hệ thống có những gì.** Bạn đã bấm gì, mở port
nào, cấp quyền gì? Không có ghi chép. Người mới vào phải đi đọc từng trang
console để dựng lại bức tranh.

**Tháng thứ hai — không dựng lại được.** Máy chủ chết, hoặc cần thêm môi trường
staging. Bạn phải bấm lại từ đầu, và chắc chắn sẽ khác bản gốc ở đâu đó. Sự khác
biệt đó chính là nguồn của loại bug tệ nhất: "chạy được trên production nhưng
không chạy được trên staging".

**Tháng thứ ba — không dám sửa.** Không ai biết cái Security Group đó mở port
8080 để làm gì, nên không ai dám đóng. Rule cứ tích tụ, chỉ thêm không bớt. Đến
lúc chấm bảo mật thì hệ thống mở rộng hơn cần thiết rất nhiều — mà không ai cố
ý làm vậy.

**Tháng thứ tư — deploy thành nghi lễ.** Chỉ một người biết cách deploy. Người
đó nghỉ phép thì cả nhóm đứng.

## Cách doanh nghiệp làm, và vì sao

Bảy nguyên tắc dưới đây không phải để cho "chuẩn". Mỗi cái sinh ra để chữa đúng
một trong bốn cái chết trên.

**1. Hạ tầng viết thành code (IaC).** Toàn bộ hạ tầng nằm trong file văn bản
trong Git. Muốn biết hệ thống có gì thì đọc file, không phải đi soi console. Muốn
biết ai đổi gì thì xem `git log`. Muốn dựng lại thì chạy lại. Sửa thì mở pull
request và có người review — **rule mở quá rộng bị bắt trước khi nó tồn tại**,
không phải sau khi bị tấn công.

**2. Xem trước khi làm.** Terraform có `plan`: nó nói trước "sẽ tạo 3, sửa 1,
xoá 2" và cụ thể là những gì. Không bao giờ thay đổi hạ tầng mà chưa đọc plan.
Đây là điều console không cho bạn.

**3. State dùng chung và có lock.** Terraform ghi lại nó đã tạo gì. File đó phải
nằm ở chỗ chung (S3), không nằm trên laptop một người. Và phải có **lock**: hai
người apply cùng lúc thì người thứ hai bị chặn. Không có lock thì hai tiến trình
ghi lên cùng một state và hạ tầng lệch khỏi mô tả — trạng thái khó chữa nhất.

**4. Không có credential dài hạn.** Không SSH key, không access key ghi trong
file. Máy chủ và pipeline dùng **role** — danh tính không có mật khẩu, credential
được cấp tạm thời và tự hết hạn. Lý do đơn giản: bí mật dài hạn thì cuối cùng sẽ
bị commit vào Git, dán vào chat, hoặc để trong máy của người đã nghỉ.

**5. Image bất biến, gắn theo phiên bản.** Mỗi lần build ra một image gắn nhãn
bằng mã commit, và **không bao giờ ghi đè**. Nhãn `latest` bị cấm vì nó nói dối:
"latest" hôm nay và hôm qua là hai thứ khác nhau, nên rollback không có nghĩa gì.
Có nhãn cố định thì rollback là trỏ về nhãn cũ — một lệnh, chắc chắn đúng.

**6. Thay đổi database là cửa kiểm soát, không phải hiệu ứng phụ.** Cập nhật
schema chạy như một bước riêng, và **nếu nó thất bại thì không deploy** — bản cũ
vẫn phục vụ. Điều tuyệt đối không làm: để app tự cập nhật schema lúc khởi động
(phần "Issue #7" giải thích vì sao).

**7. Chi phí là một yêu cầu, không phải chuyện của kế toán.** Ai dựng hạ tầng
thì người đó chịu trách nhiệm về hoá đơn. Nên phải có cách tắt, có cảnh báo
ngân sách, và có người hiểu từng đồng đi đâu.

Bảy nguyên tắc này là khung của toàn bộ nhật ký dưới đây.

---

# Phần II — Nhật ký triển khai

Phase 1 gồm 17 việc, nhóm thành 10 giai đoạn. Mỗi giai đoạn ghi bốn mục:
**resource nào được dựng**, **cấu hình cụ thể từng cái và vì sao chọn giá trị
đó**, **vì sao phải theo thứ tự này**, và **chỗ dễ sai**.

Các bảng cấu hình dưới đây là giá trị **thật đang chạy**, không phải ví dụ. Cột
"vì sao" là phần đáng đọc — một thiết lập không giải thích được thì thường là
một thiết lập sai.

---

## Giai đoạn 0 — Khảo sát hiện trạng và quyết định làm lại

**Làm gì.** Đọc lại hệ thống đang có: một bộ script bash gọi AWS CLI, 310 dòng,
11 bước, dựng một EC2 với nginx cài trực tiếp trên máy. Đối chiếu với yêu cầu đề
bài thì thiếu 3 trên 5 thành phần bắt buộc: **không có Network ACL, không có
Application Load Balancer, không có kịch bản kiểm thử**. Ngoài ra deploy đang
dùng SSH key dài hạn, và schema database được cập nhật ngầm lúc app khởi động.

**Quyết định: viết lại từ đầu bằng Terraform**, giữ bộ script cũ làm tài liệu
tham chiếu (đọc để biết hệ thống cũ làm gì, nhưng **không chạy lại** — nó tạo
resource nằm ngoài Terraform state, và nó mở port 22).

**Vì sao không sửa dần.** Vì thiếu tới 3/5 thành phần và cả cách deploy đều phải
đổi. Sửa dần trong trường hợp này tốn hơn làm lại, và nguy hiểm hơn: một hạ tầng
nửa-Terraform-nửa-thủ-công là thứ không ai dám chạm.

**Chỗ dễ sai.** Xoá hệ thống cũ trước khi hệ thống mới chạy được. Cách đúng: giữ
song song, dùng dải IP khác (`10.20.0.0/16` thay vì `10.0.0.0/16`) để hai mạng
không đụng nhau, và chỉ chuyển tên miền khi bản mới đã verify xong.

---

## Giai đoạn 1 — Nơi lưu state

**Resource:** 1 S3 bucket.

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Tên bucket | `hushstore-tfstate` | Tên S3 là **toàn cầu duy nhất** — trùng với bất kỳ ai trên thế giới là tạo thất bại |
| Versioning | **bật** | State hỏng thì quay lại bản trước. Đây là lưới an toàn duy nhất cho file quan trọng nhất của hạ tầng |
| Block public access | **bật toàn bộ** | State chứa mọi ID resource và có thể chứa giá trị nhạy cảm. Public là thảm hoạ |
| Mã hoá | bật (SSE-S3) | Miễn phí, không có lý do để tắt |
| Backend lock | `use_lockfile = true` | Terraform ≥ 1.10 lock ngay trên S3, **không cần bảng DynamoDB** như hướng dẫn cũ — bớt một resource phải quản |

**Vì sao đây là việc đầu tiên.** Vì mọi thứ sau đó đều ghi vào state. Nếu state
nằm trên laptop thì chỉ một người dựng được hạ tầng; nếu nó mất thì Terraform
"quên" hết những gì đã tạo — và lần apply sau nó sẽ cố tạo lại tất cả, trong khi
resource cũ vẫn còn.

**Vì sao bucket này dựng bằng backend local.** Không thể dùng S3 backend để tạo
chính cái bucket chứa backend — vòng lặp. Nên thư mục `bootstrap/` chạy một lần
với state trên máy, sau đó mọi thứ khác dùng S3.

**Chỗ dễ sai.** Bỏ qua lock vì "chỉ mình mình dùng". Nhưng lock không chỉ chống
hai người — nó còn chống **một người apply hai lần** (mở hai terminal, hoặc apply
lại khi lần trước chưa xong). Đó là tình huống thật, và nó làm state lệch.

---

## Giai đoạn 2 — Mạng

**Resource:** 1 VPC · 6 subnet · 1 Internet Gateway · 3 route table · 1 NAT
Gateway + 1 Elastic IP · 1 S3 Gateway Endpoint · 3 Network ACL · (tuỳ chọn) Flow
Logs.

### VPC

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `cidr_block` | `10.20.0.0/16` | 65.536 địa chỉ, thừa sức. Chọn `10.20` để **không trùng** `10.0.0.0/16` của hạ tầng cũ đang chạy song song |
| `enable_dns_support` | `true` | Cho phép dùng DNS resolver của AWS trong VPC |
| `enable_dns_hostnames` | `true` | **Bắt buộc.** Không bật thì endpoint của RDS không phân giải được thành IP private, và app không kết nối được database |

### Subnet — 6 cái, và một thiết lập quyết định toàn bộ bảo mật

| Subnet | CIDR | AZ | `map_public_ip_on_launch` |
|---|---|---|---|
| `public-a` / `public-b` | `10.20.0.0/24` / `10.20.1.0/24` | 1a / 1b | **`true`** |
| `app-a` / `app-b` | `10.20.10.0/24` / `10.20.11.0/24` | 1a / 1b | **`false`** |
| `db-a` / `db-b` | `10.20.20.0/24` / `10.20.21.0/24` | 1a / 1b | **`false`** |

`map_public_ip_on_launch = false` ở app và db tier là **thiết lập bảo mật quan
trọng nhất của toàn hệ thống**. Nó nghĩa là máy tạo trong subnet đó **không có
địa chỉ Internet**. Không có địa chỉ thì không ai gõ cửa được — mạnh hơn mọi
firewall, vì firewall còn có thể cấu hình sai.

Mỗi subnet cũng được gắn tag `Tier = public|app|db`. Tag không ảnh hưởng chức
năng nhưng là cách duy nhất để sau này lọc resource theo tầng khi đọc hoá đơn
hoặc viết script.

**Vì sao dùng công thức tính CIDR thay vì gõ tay.** Ba dải tier trong NACL được
tính bằng hàm (`10.20.0.0/23`, `10.20.10.0/23`, `10.20.20.0/23`) — mỗi dải `/23`
gộp đúng hai subnet `/24` của tier đó. Gõ tay thì thêm một AZ là phải sửa nhiều
chỗ và chắc chắn sẽ quên một chỗ.

### Internet Gateway và route table

| Route table | Gắn vào | Dòng route | Ý nghĩa |
|---|---|---|---|
| public | 2 subnet public | `0.0.0.0/0` → **IGW** | Có đường vào **và** ra |
| private (app) | 2 subnet app | `0.0.0.0/0` → **NAT** *(chỉ khi bật NAT)* | **Chỉ** có đường ra |
| db | 2 subnet db | **không có dòng `0.0.0.0/0` nào** | Không đường ra, không đường vào |

Route table của db tier là ví dụ đẹp nhất của nguyên tắc tối thiểu: bảo mật ở đây
đến từ **việc thiếu một dòng cấu hình**, không phải từ việc thêm một rule.

### NAT Gateway

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Vị trí | `public-a` | NAT **phải** nằm ở public subnet — nó cần đường ra IGW để làm việc của mình |
| Elastic IP | 1 cái, gắn kèm | NAT bắt buộc có IP public tĩnh |
| Số lượng | **1** cho cả 2 AZ | NAT tính phí theo giờ. Mỗi AZ một NAT chỉ cần khi thật sự chạy nhiều máy ở nhiều AZ; ta `max_size = 1` nên chỉ có một máy tại một thời điểm |
| Công tắc | `count = enable_nat ? 1 : 0` | Đặt **ngay từ ngày đầu**, không để cuối dự án |

**Lưu ý đáng ghi vào báo cáo:** NAT Gateway **không gắn được Security Group**
(khác NAT instance là một EC2 tự dựng). Nên toàn bộ việc kiểm soát chiều ra phải
làm ở `sg-web` và ở NACL của app tier.

### S3 Gateway Endpoint

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Loại | **Gateway** (không phải Interface) | Gateway endpoint **miễn phí**; Interface endpoint tốn ~$7/tháng mỗi cái |
| Gắn vào | route table của app tier | Traffic S3 đi đường riêng, không qua NAT |

Hai lợi ích: không bị tính phí data qua NAT ($0.045/GB), và **vẫn hoạt động khi
NAT đã tắt**.

### Network ACL — 3 cái

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Số lượng | 3, mỗi tier một cái | Gắn NACL vào **subnet**, nên tách tier là điều kiện để viết rule khác nhau cho mỗi tier |
| `subnet_ids` | cả 2 subnet của tier | Hai subnet cùng tier phải có cùng rule, nếu không thì hành vi phụ thuộc AZ — cực khó chẩn đoán |
| Rule number | cách nhau 5–10 | Chừa khoảng trống để chèn rule mới vào giữa mà không phải đánh số lại |
| Rule mặc định `*` | **deny** | AWS tự thêm, không xoá được. Đây là lý do NACL "an toàn theo mặc định" |

Bảng rule đầy đủ và lý do từng rule nằm ở **Phần III của
[tài liệu thiết kế](thiet-ke-he-thong-aws.md)**. Ở đây chỉ nhắc điều quan trọng
nhất: rule 90, 95 và 115 là rule **DENY**, và chúng tồn tại **vì** rule 120 —
không phải vì thừa.

### VPC Flow Logs (tuỳ chọn)

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `traffic_type` | **`REJECT`** | Chỉ ghi kết nối **bị chặn**. Ghi cả `ALL` thì lượng log gấp hàng chục lần và tốn phí ingest, mà phần cần cho báo cáo chỉ là phần bị chặn |
| Retention | 1 ngày | Chỉ cần trong lúc thu bằng chứng |
| `max_aggregation_interval` | 600 giây | Gom bản ghi 10 phút một lần thay vì 1 phút → ít bản ghi hơn, ít tiền hơn |
| Mặc định | **tắt** | Tính phí theo lượng log ghi vào CloudWatch |

**Vì sao mạng trước tiên.** Vì mọi thứ khác đều phải *nằm trong* một subnet nào
đó. Security Group cần biết VPC nào; RDS cần biết subnet nào; ALB cần biết subnet
nào.

**Chỗ dễ sai — quan trọng nhất của giai đoạn này.** Viết NACL mà không nghĩ tới
stateless. Người mới thường viết rule cho chiều vào rồi tưởng xong. Kết quả: kết
nối ra Internet không hoạt động, vì câu trả lời quay về bị chặn ở chiều vào. Chữa
thì phải mở dải ephemeral 1024–65535 — nhưng mở xong lại vô tình mở luôn port
1433 và 8080 ra Internet, vì hai port đó nằm trong dải. Phải chặn chúng bằng rule
**số nhỏ hơn**.

---

## Giai đoạn 3 — Security Group

**Resource:** 3 Security Group · 5 rule vào · 4 rule ra.

| SG | `description` | Rule vào | Rule ra |
|---|---|---|---|
| `hushstore-alb-sg` | ALB nhận 80/443 từ internet | 80 ← `0.0.0.0/0`<br>443 ← `0.0.0.0/0` | 80 → `sg-web`<br>8080 → `sg-web` |
| `hushstore-web-sg` | container instance, không port 22 | 80 ← `sg-alb`<br>8080 ← `sg-alb` | 1433 → `sg-rds`<br>80 → `0.0.0.0/0`<br>443 → `0.0.0.0/0` |
| `hushstore-rds-sg` | chỉ nhận 1433 từ sg-web | 1433 ← `sg-web` | **không có rule nào** |

Thiết lập kỹ thuật cần biết:

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Nguồn/đích của rule | **ID của SG khác**, không phải dải IP | IP của EC2 đổi mỗi lần máy được tạo lại; SG thì không. Rule tự đúng mãi |
| `description` mỗi rule | bắt buộc điền | Sáu tháng sau, đây là thứ duy nhất cho biết rule đó mở để làm gì. Rule không có mô tả là rule không ai dám xoá |
| `lifecycle create_before_destroy` | `true` | SG đang được dùng thì không xoá được. Tạo cái mới trước rồi mới bỏ cái cũ |
| Rule khai ở đâu | resource **riêng**, ngoài định nghĩa SG | Xem "chỗ dễ sai" |
| Egress mặc định | Terraform **không** tự thêm "cho phép tất cả" | Khác console: console tạo SG là tự thêm egress allow-all. Terraform không, nên mỗi đường ra đều là một quyết định có ý thức |

**Vì sao egress của `sg-rds` rỗng hoàn toàn.** Database không bao giờ cần chủ
động gọi ra ngoài. SG là stateful nên câu trả lời cho app vẫn đi được bình
thường. Egress rỗng nghĩa là nếu kẻ tấn công chiếm được database, nó **không gửi
được dữ liệu ra ngoài**. Đây là chặn đường rút, không phải chặn đường vào — và
đó là loại rule người mới hầu như không bao giờ nghĩ tới.

**Chỗ dễ sai.** Tham chiếu vòng. `sg-alb` cần trỏ tới `sg-web`, và `sg-web` cần
trỏ tới `sg-alb`. Nếu khai rule *bên trong* định nghĩa SG thì Terraform gặp vòng
lặp và không dựng được. Cách đúng: tạo ba SG rỗng trước, rồi khai rule thành
resource riêng bên ngoài — đúng như bảng trên.

---

## Giai đoạn 4 — Kho chứa: ECR và S3

**Resource:** 4 ECR repository · 3 S3 bucket.

### ECR — 4 repository (api, web, migrator, seeder)

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `image_tag_mutability` | **`IMMUTABLE`** | Một nhãn đã push **không thể ghi đè**. Đây là điều kiện để rollback có nghĩa: trỏ về nhãn cũ chắc chắn lấy đúng bản đã chạy tốt |
| `scan_on_push` | `true` | AWS tự quét lỗ hổng mỗi lần push. Miễn phí ở mức cơ bản |
| Lifecycle policy | giữ **5** image gần nhất | Image ~700MB/cái. Không có policy thì kho phình vô hạn và tốn tiền lưu trữ |
| `force_delete` | `true` | Cho phép xoá repo còn image khi destroy. An toàn vì image build lại được từ Git |
| Xác thực | **IAM role** | Không có mật khẩu registry nào phải lưu ở đâu |

Nhãn image dùng **mã commit Git**, không dùng `latest`. Lý do: `latest` nói dối —
"latest" hôm nay và hôm qua là hai thứ khác nhau, nên rollback không có nghĩa gì.

### S3 — 3 bucket, ba mục đích, ba cấu hình khác nhau

| Bucket | `force_destroy` | Lifecycle | Vì sao cấu hình vậy |
|---|---|---|---|
| `hushstore-public-assets` (ảnh sản phẩm) | **`false`** | không | **Chứa dữ liệu thật.** URL của những ảnh này đang nằm trong database. `false` nghĩa là `terraform destroy` sẽ **thất bại** ở bucket này nếu nó còn file — đó là **lưới an toàn có chủ ý**, không phải lỗi |
| `hushstore-artifacts` | `true` | xoá sau 30 ngày | Chứa file SQL migration và file tạm. Dựng lại được từ Git |
| `hushstore-alb-logs` | `true` | xoá sau 7 ngày | Log của ALB. Chỉ cần trong lúc chẩn đoán |

Cả ba bucket đều bật: block public access, mã hoá, và chặn ACL.

**Riêng bucket ảnh là ngoại lệ duy nhất của toàn bộ hạ tầng greenfield:** nó
được **nhập** (import) vào Terraform quản lý thay vì tạo mới. Tạo lại là làm chết
toàn bộ ảnh sản phẩm hiện có.

**Chỗ dễ sai.** Chạy `terraform destroy` để "dọn cho sạch" mà không biết nó kéo
theo bucket dữ liệu thật. Đây là lý do `force_destroy = false` ở bucket ảnh, và
lý do script `nuke.sh` in cảnh báo riêng về nó trước khi hỏi xác nhận.

---

## Giai đoạn 5 — Database và bí mật

**Resource:** 1 DB subnet group · 1 RDS instance · 2 mật khẩu sinh tự động · 3
SSM Parameter.

### DB subnet group

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Subnet | 2 cái, ở **2 AZ khác nhau** | RDS **bắt buộc** tối thiểu 2 AZ, kể cả khi chạy single-AZ. Đây là lý do phải có `db-b` dù nó không chứa gì |

### RDS instance

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `engine` | `sqlserver-ex` (Express) | Bản miễn phí license của SQL Server. Giới hạn 10GB/database, đủ cho đồ án |
| `license_model` | `license-included` | Express không phải trả phí license, nhưng AWS vẫn đòi khai rõ |
| `instance_class` | `db.t3.micro` | Nhỏ nhất. **Nhưng xem Issue #11** — lớp `t3` có bẫy chi phí |
| `allocated_storage` | 20 GB, `gp2` | Mức tối thiểu của SQL Server trên RDS |
| `storage_encrypted` | **`true`** | Mã hoã ổ đĩa, miễn phí. **Chỉ đặt được lúc tạo** — sau này muốn bật phải tạo lại database |
| `publicly_accessible` | **`false`** | Endpoint phân giải ra IP **private**. Kẻ tấn công tra được tên nhưng nhận về địa chỉ không tồn tại trên Internet |
| `multi_az` | `false` | SQL Server Express **không hỗ trợ** Multi-AZ |
| `vpc_security_group_ids` | chỉ `sg-rds` | Lớp bảo vệ thứ hai sau việc không có route |
| `backup_retention_period` | 7 ngày | Miễn phí tới bằng dung lượng đã cấp. Cho phép khôi phục về một thời điểm bất kỳ trong 7 ngày |
| `auto_minor_version_upgrade` | `true` | Vá lỗi bảo mật tự động trong cửa sổ bảo trì |
| `deletion_protection` | `false` | Đồ án cần xoá/dựng lại nhiều lần. **Production phải là `true`** |
| `skip_final_snapshot` | `true` | Destroy nhanh. **Đây là bẫy** — xoá là mất dữ liệu, không có snapshot cuối. `nuke.sh` cảnh báo riêng về nó |
| `performance_insights_enabled` | `false` | Tốn phí, và ta đã có CloudWatch metrics |

### Mật khẩu — sinh tự động, không ai biết

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Mật khẩu DB | 32 ký tự, tối thiểu 2 chữ hoa / 2 chữ thường / 2 số / 2 ký tự đặc biệt | Đủ mạnh, và ràng buộc từng loại để chắc chắn thoả yêu cầu của SQL Server |
| Ký tự đặc biệt | **danh sách chỉ định**, không dùng mặc định | Vài ký tự (`;` `'` `"` `/` `@`) **phá vỡ connection string** hoặc URL. Đây là lỗi rất khó chẩn đoán vì nó chỉ xảy ra với một số mật khẩu sinh ra |
| Khoá JWT | 64 ký tự, **không** ký tự đặc biệt | Cần trên 256-bit entropy. Bỏ ký tự đặc biệt để tránh rắc rối khi đi qua biến môi trường |

### SSM Parameter Store — 3 bí mật

| Tên | Loại | Nội dung |
|---|---|---|
| `/hushstore/prod/db-password` | `SecureString` | Mật khẩu master, **chỉ** seeder đọc được |
| `/hushstore/prod/connection-string` | `SecureString` | Chuỗi kết nối đầy đủ, có `Encrypt=True` và `TrustServerCertificate=False` |
| `/hushstore/prod/jwt-secret` | `SecureString` | Khoá ký JWT |

`SecureString` nghĩa là AWS mã hoá giá trị bằng KMS. Đọc được hay không do IAM
quyết định — và đó là chỗ Deny tường minh ở Giai đoạn 7 phát huy tác dụng.

Chú ý `TrustServerCertificate=False` trong connection string: nó buộc app
**thật sự kiểm chứng** chứng chỉ TLS của RDS. Đặt `True` là bỏ qua kiểm tra — tức
mã hoá mà không xác thực, và khi đó mã hoá gần như vô nghĩa. Đây là lý do image
phải có sẵn chứng chỉ gốc của Amazon RDS.

**Vì sao database tạo rỗng.** Vì schema là việc của migration, dữ liệu là việc
của seeder. Cả hai phải chạy được nhiều lần cho cùng kết quả và phải nằm trong
Git. Dựng database bằng cách khôi phục một bản backup nào đó thì không ai biết
chính xác trong đó có gì.

**Chỗ dễ sai.** Đặt `publicly_accessible = true` cho tiện kết nối từ máy cá nhân
lúc phát triển. Đó là lỗ hổng lớn nhất người mới hay tạo ra: database mở ra
Internet, chỉ còn mật khẩu bảo vệ. Cách đúng là vào bằng SSM, hoặc chạy việc cần
làm như một task bên trong VPC.

---

## Giai đoạn 6 — Đóng gói ứng dụng và sửa 4 điểm trong code

**Resource:** 4 container image đẩy lên ECR.

| Image | Nội dung | Điểm cấu hình đáng chú ý |
|---|---|---|
| `web` | Blazor WASM build sẵn + nginx | Bản build frontend **nướng vào image**, không copy file lên server. nginx chỉ `listen 80`, không SSL (ALB đã bóc TLS), không proxy (ALB tự định tuyến `api.*`) |
| `api` | .NET 10 | Nghe `http://+:8080`. Không tự chạy migration |
| `migrator` | EF Core migration bundle | Đóng gói toàn bộ migration thành **một chương trình chạy được**, chạy xong thoát. Có chứng chỉ gốc RDS |
| `seeder` | công cụ dòng lệnh SQL + 2 file `.sql` | Nhận mật khẩu qua biến môi trường do ECS tiêm, **không** đọc từ file |

Bốn thay đổi trong code ứng dụng:

| Sửa gì | Vì sao |
|---|---|
| Thêm health check **có kiểm tra database** | Endpoint cũ trả 200 mà không chạm DB → ALB báo healthy dù database đã chết |
| Đọc header `X-Forwarded-*` | ALB bóc TLS rồi chuyển HTTP vào; không đọc header này thì app tưởng request là HTTP và có thể tạo vòng lặp redirect |
| Bỏ access key ghi cứng, dùng role | Loại bỏ credential dài hạn cuối cùng còn trong code. SDK tự lấy credential tạm từ task role — **chỉ cần xoá dòng cũ**, không phải viết thêm gì |
| **Bỏ cập nhật schema lúc app khởi động** | Xem Issue #7 — quan trọng nhất trong bốn cái |

**Chỗ dễ sai.** Build image trên máy Mac chip ARM rồi push lên, trong khi EC2 là
x86. Container sẽ không chạy với lỗi rất khó hiểu. Phải chỉ định rõ kiến trúc
đích khi build.

---

## Giai đoạn 7 — IAM, cluster và máy chủ

**Resource:** 4 IAM role · 1 instance profile · 1 launch template · 1 ASG · 1 ECS
cluster · 1 capacity provider · 4 CloudWatch log group.

### IAM — 4 role

| Role | Trust policy cho phép ai assume | Quyền |
|---|---|---|
| `container-instance-role` | **đúng** `ec2.amazonaws.com` | 2 managed policy (tham gia ECS + dùng SSM), đọc bucket artifacts, **cộng một statement Deny tường minh** |
| `task-execution-role` | **đúng** `ecs-tasks.amazonaws.com` | Pull ECR, ghi log, đọc **đúng 2** parameter |
| `task-app-role` | **đúng** `ecs-tasks.amazonaws.com` | **Chỉ** S3 trên bucket ảnh + `ssmmessages` cho ECS Exec |
| `task-execution-seeder-role` | **đúng** `ecs-tasks.amazonaws.com` | Đọc **đúng 1** parameter: mật khẩu DB |

Bốn điểm cấu hình đáng ghi:

| Thiết lập | Vì sao |
|---|---|
| Trust policy khai **đúng một** service principal | So khớp chính xác, không dùng danh sách. Trust policy rộng là đường leo thang quyền |
| Statement **Deny** `ssm:GetParameter*` trên `/hushstore/*` cho role máy EC2 | Managed policy dùng để có SSM **kèm theo** quyền đọc parameter. Deny bịt lại. Trong IAM, **Deny luôn thắng Allow** |
| Deny phủ **đủ 4** action: `GetParameter`, `GetParameters`, `GetParameterHistory`, `GetParametersByPath` | Thiếu một cái là còn một đường đọc. Đặc biệt `GetParameterHistory` — nó trả về **các version cũ** của giá trị, tức vẫn đọc được mật khẩu |
| `kms:Decrypt` kèm điều kiện `kms:ViaService` | Tài nguyên phải là `*` (ARN của khoá mặc định không cố định), nên **điều kiện là thứ duy nhất** giới hạn: khoá chỉ dùng được qua SSM, không dùng để giải mã thứ khác |

**Hai tập bí mật giao nhau bằng rỗng.** `api/web/migrator` đọc connection string
+ khoá JWT nhưng **không** đọc mật khẩu DB. `seeder` đọc mật khẩu DB nhưng
**không** đọc hai cái kia. Dùng chung một role thì cả bốn task đều thấy cả ba bí
mật.

### Launch Template — cấu hình của máy chủ

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `image_id` | đọc từ **SSM public parameter** của AWS | Luôn lấy bản AMI ECS-optimized mới nhất. **Không hardcode AMI ID** — nó khác nhau theo region và cũ đi theo thời gian |
| `instance_type` | `t3.micro` | Nhỏ nhất chạy được. 1 GB RAM là **chật** — xem Issue bên dưới |
| `iam_instance_profile` | `container-instance-role` | Cách gắn IAM role vào một EC2 |
| `vpc_security_group_ids` | chỉ `sg-web` | |
| **`metadata_options.http_tokens`** | **`required`** | Bắt buộc dùng **IMDSv2**. Đây là biện pháp chặn tấn công **SSRF** — với IMDSv1, một lỗ hổng trong app cho phép kẻ tấn công đọc credential của máy qua một request HTTP đơn giản. `required` bắt phải có token, và token không lấy được qua SSRF |
| `metadata_options.http_put_response_hop_limit` | `1` | Gói tin lấy token không đi được quá 1 chặng — tức **container không thò tay ra lấy credential của host** được |
| Ổ đĩa | `gp3`, mã hoá, **`delete_on_termination = true`** | `gp3` rẻ và nhanh hơn `gp2`. Xoá cùng máy là điều kiện để "tắt về $0" đúng nghĩa |
| `user_data` | **chỉ** ghi tên cluster vào file cấu hình + tạo 2 GB swap | **Không** ra lệnh khởi động dịch vụ — xem Issue #2. Swap bù cho việc chỉ có 1 GB RAM |
| `lifecycle create_before_destroy` | `true` | ASG đang dùng template thì không xoá được |

### Auto Scaling Group

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `min_size` / `max_size` | **0** / **1** | `min = 0` là điều kiện để tắt về $0 thật. `max = 1` vì chi phí, RAM, và vì rate limiter đếm trong bộ nhớ |
| `desired_capacity` | biến `instance_count` | Công tắc bật/tắt |
| `vpc_zone_identifier` | 2 subnet **app** | Máy nằm ở tier không có IP public |
| `health_check_type` | `EC2` | **Đây là nguồn của Issue #1.** Nó chỉ hỏi "máy có running không", **không** hỏi "ECS agent có đăng ký chưa" |
| `health_check_grace_period` | 180 giây | Cho máy thời gian boot trước khi bị đánh giá |
| `wait_for_capacity_timeout` | 10 phút | Terraform chờ máy vào phục vụ trước khi apply trả về |
| `instance_refresh.min_healthy_percentage` | **0** | `max_size = 1` nên **không thể** giữ máy nào healthy trong lúc thay máy. Đặt khác 0 là refresh treo mãi |
| Tag `propagate_at_launch` | `true` | Máy do ASG tạo mới có tag. Không bật thì máy không có tag, và mọi script lọc theo tag đều bỏ sót nó |

### ECS cluster và capacity provider

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `containerInsights` | **`disabled`** | Tính phí theo metric. Bật khi cần chẩn đoán sâu, không bật mặc định |
| `managed_scaling` | **`DISABLED`** | Nếu bật, ECS tự tạo một chính sách scaling và **tranh quyền với Terraform** về số máy: Terraform đặt 0, chính sách đặt lại 1, lặp vô hạn. Tắt nó để có **một nguồn sự thật duy nhất** |
| `managed_termination_protection` | **`DISABLED`** | Bật thì capacity provider không cho xoá máy, và `nuke.sh` treo |

### CloudWatch log group — 4 cái

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Tên | `/ecs/hushstore-{api,web,migrator,seeder}` | Mỗi container một nhóm, tìm log dễ |
| Retention | **3 ngày** | Log chiếm tiền. 3 ngày đủ để chẩn đoán. **Không đặt retention là giữ vĩnh viễn và trả tiền vĩnh viễn** — lỗi phổ biến |
| Tạo bằng Terraform, không để ECS tự tạo | | Log group do ECS tự tạo **không có retention**, tức giữ mãi |

**Vì sao ASG có `min = 0` mà vẫn an toàn.** Vì **không có state nào nằm trên
máy** — mọi thứ nằm trong image và Parameter Store. Điều này đã được kiểm chứng:
hạ về 0 rồi bật lại, máy mới và container mới hoàn toàn tự lên đủ, không cần thao
tác tay nào.

**Chỗ dễ sai.** Bật `managed_scaling` vì tưởng "cho nó tự động thì tốt hơn". Nó
tạo ra cuộc tranh chấp âm thầm với Terraform mà triệu chứng là "số máy tự đổi
không rõ lý do".

---

## Giai đoạn 8 — Task definition, ALB, và service

**Resource:** 4 task definition · 1 ACM certificate · 1 ALB · 2 target group · 2
listener · 2 listener rule · 2 ECS service.

### Task definition — lấy `api` làm ví dụ đầy đủ

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `requires_compatibilities` | `["EC2"]` | Chạy trên EC2 launch type, không phải Fargate — đúng yêu cầu đề bài |
| `network_mode` | **`bridge`** | Container dùng mạng của host qua cầu nối. Cho phép **port cố định** ở host |
| `execution_role_arn` | `task-execution-role` | Dùng **trước** khi container chạy: pull image, lấy bí mật, mở log |
| `task_role_arn` | `task-app-role` | Dùng **trong khi** container chạy: code gọi API AWS |
| `skip_destroy` | **`true`** | Revision cũ vẫn **ACTIVE** sau khi tạo revision mới → rollback là trỏ về revision cũ. Không bật thì Terraform xoá revision cũ và **rollback không còn đường về** |
| `memory` (giới hạn cứng) | 512 MB | Vượt là container bị giết. Bảo vệ host khỏi một container ăn hết RAM |
| `memoryReservation` (mềm) | 384 MB | Mức ECS **đảm bảo**. Có cả hai thì container mượn thêm được lúc cao điểm mà vẫn có sàn |
| `portMappings` | `8080` → `8080` | Cố định. Đây là **lý do SG chỉ mở 2 port** thay vì dải 32768–65535 |
| `environment` | 5 biến không nhạy cảm | Region, tên bucket, origin CORS, môi trường, URL nghe |
| **`secrets`** | 2 mục, trỏ tới **ARN của SSM parameter** | Điểm quan trọng: giá trị **không nằm trong task definition**. ECS đọc lúc khởi động và tiêm vào. Ai xem task definition **không thấy bí mật** |
| `linuxParameters.maxSwap` / `swappiness` | 1024 / 60 | Cho container dùng swap. **Chỉ EC2 launch type hỗ trợ**, Fargate không |
| `linuxParameters.initProcessEnabled` | `true` | Cần cho **ECS Exec** — đường vào trong container để chẩn đoán |
| `logConfiguration` | `awslogs` → log group tương ứng | |
| Container-level `healthCheck` | **không đặt** | Image `.NET` không có `curl`. Việc kiểm tra sức khoẻ để target group của ALB làm |

`migrator` và `seeder` khác ở ba chỗ: **không** `portMappings`, **không** có
service, và `seeder` dùng **execution role riêng**.

### ACM certificate

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| Tên miền | `hushstore.io.vn` + SAN `api.hushstore.io.vn` | Một chứng chỉ phục vụ cả hai hostname |
| Phương thức xác thực | **DNS** | Tự động gia hạn được. Xác thực bằng email thì phải làm tay mỗi lần |
| `lifecycle create_before_destroy` | `true` | Chứng chỉ đang gắn vào listener thì không xoá được |

Bản ghi DNS xác thực **phải giữ mãi** — ACM đọc lại nó mỗi lần gia hạn. Xoá đi
thì HTTPS chết âm thầm sau 13 tháng.

### ALB

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `internal` | `false` | Internet-facing |
| `subnets` | 2 public subnet, 2 AZ | AWS **bắt buộc** tối thiểu 2 AZ |
| `security_groups` | `sg-alb` | |
| `enable_deletion_protection` | `false` | Ta xoá ALB mỗi lần tắt để tiết kiệm |
| `access_logs` | bật, ghi vào `hushstore-alb-logs` | Nguồn bằng chứng chính cho báo cáo bảo mật khi Flow Logs đang tắt |
| **`drop_invalid_header_fields`** | **`true`** | Bỏ header không đúng chuẩn HTTP trước khi chuyển vào. Chặn một lớp tấn công **request smuggling**. *Lưu ý: nó **không** chặn giả mạo `X-Forwarded-*`, vì ALB **thêm vào** chứ không thay thế — chặn cái đó là việc của `ForwardLimit` trong app* |

### Target group — 2 cái

| Thiết lập | `tg-web` | `tg-api` | Vì sao |
|---|---|---|---|
| Port | 80 | 8080 | Khớp `hostPort` trong task definition |
| `target_type` | `instance` | `instance` | Vì `bridge` mode đăng ký theo máy. Fargate/`awsvpc` sẽ là `ip` |
| Health check path | `/healthz` | **`/health/ready`** | `/healthz` chỉ kiểm nginx sống. `/health/ready` **có mở kết nối database** — nguồn của Issue #3 |
| `interval` / `timeout` | 15 / 5 giây | 15 / 5 | |
| `healthy_threshold` | 2 | 2 | Đúng 2 lần liên tiếp là được nhận traffic → lên nhanh |
| `unhealthy_threshold` | 3 | 3 | 3 × 15 = **45 giây** để kết luận chết. Đây là con số làm nên vòng lặp chết ở Issue #3 |
| `matcher` | `200` | `200` | Chỉ 200 là healthy. Không nhận 3xx/4xx |
| `deregistration_delay` | **5 giây** | 5 | Mặc định AWS là 300 giây. Giảm xuống 5 nên deploy và tắt nhanh hơn nhiều. Chấp nhận được vì request của ta ngắn |

### Listener và rule

| Listener | Cấu hình | Vì sao |
|---|---|---|
| `:80` | `redirect` **301** sang HTTPS, giữ nguyên host/path/query | Không phục vụ gì trên HTTP. 301 là vĩnh viễn, browser tự nhớ |
| `:443` | chứng chỉ ACM, `ssl_policy = ELBSecurityPolicy-TLS13-1-2-2021-06` | Policy này **chỉ cho TLS 1.2 và 1.3**. Loại bỏ TLS 1.0/1.1 đã có lỗ hổng |
| `:443` default action | **`fixed-response` 403** | **Không phải `forward`.** Tên miền không có trong danh sách nhận 403 và **không đi đến đâu cả** |
| Rule priority 100 | `host_header = api.hushstore.io.vn` → `tg-api` | Số nhỏ xét trước |
| Rule priority 200 | `host_header = hushstore.io.vn` → `tg-web` | |

Đây là **allowlist Host header**. Nó chặn tấn công Host header injection và chặn
việc người khác trỏ tên miền của họ vào hạ tầng của ta.

### ECS service — 2 cái

| Thiết lập | Giá trị | Vì sao |
|---|---|---|
| `capacity_provider_strategy` | dùng capacity provider, `weight = 1` | Cách nói "xếp task lên máy của ASG này" |
| `deployment_minimum_healthy_percent` | **0** | `max_size = 1` + port cố định → **không thể** chạy hai bản song song. Đặt khác 0 là deploy treo mãi |
| `deployment_maximum_percent` | **100** | Cùng lý do |
| `load_balancer` | trỏ target group + tên container + port | Cách ECS tự đăng ký/rút container khỏi target group |
| `health_check_grace_period_seconds` | web **60** / api **120** | Thời gian ALB bỏ qua health check lúc container mới lên. API cần lâu hơn vì .NET khởi động chậm hơn nginx. **Không nâng lên 600 để chữa Issue #3** — xem lý do ở đó |
| `enable_execute_command` | `true` (chỉ `api`) | Bật ECS Exec để vào trong container chẩn đoán. Cần cả `initProcessEnabled` ở task definition và quyền `ssmmessages` ở task role |
| `depends_on` capacity providers | có | Không có thì Terraform tạo service trước khi cluster biết lấy máy ở đâu |

**Vì sao service phải dựng sau ALB.** Vì service cần đăng ký container vào target
group, và AWS **từ chối tạo service nếu target group chưa gắn vào một load
balancer nào**. Đây là ràng buộc cứng của AWS — nên hai thứ này bật/tắt cùng
nhau bằng một công tắc.

**Chỗ dễ sai.** Xem Issue #4 và #5 — cả hai đều ở giai đoạn này và cả hai đều
làm mất nhiều giờ.

---

## Giai đoạn 9 — Dựng schema, nạp dữ liệu, và verify

**Resource:** không tạo resource mới. Chạy 2 one-off task.

| Bước | Cách chạy | Điều kiện đi tiếp |
|---|---|---|
| Migration | one-off ECS task, task definition `migrator`, chỉ định capacity provider | **mã thoát = 0**. Khác 0 là dừng, bản cũ vẫn phục vụ |
| Seed | one-off ECS task, task definition `seeder` | mã thoát = 0, rồi đếm số bản ghi từng bảng |

**Ba tiêu chí nghiệm thu, và vì sao đúng ba cái này:**

1. **Website mở được bằng HTTPS trên tên miền thật** — chứng minh chuỗi
   DNS → Cloudflare → ALB → target group → container hoạt động hoàn chỉnh. Kiểm
   **không dùng cờ bỏ qua chứng chỉ**, vì dùng cờ đó là bỏ qua đúng thứ cần kiểm.
2. **Đăng nhập lấy được JWT** — chứng minh API nói chuyện được với database.
   Kiểm luôn nội dung token: không có mật khẩu, không có dữ liệu nhạy cảm, vì ai
   cũng giải mã được phần thân của JWT.
3. **Upload ảnh lên S3 trả về URL thật, tải lại được** — **quan trọng nhất và dễ
   bị bỏ sót nhất.** Đây là bằng chứng duy nhất cho **đường ghi**: rằng IAM role
   của container thật sự thay thế được access key ghi cứng. Hai tiêu chí đầu chỉ
   chứng minh đường đọc. Kiểm cả chiều ngược: gọi mà **không** có token phải
   nhận 401.

**Vì sao seed chạy như task trong VPC, không phải từ laptop.** Vì database không
có đường ra Internet nên laptop không kết nối tới được — đúng như thiết kế. Chạy
như task bên trong VPC là cách duy nhất, và có lợi ích kèm theo: mật khẩu đi qua
đường bí mật của ECS, **không bao giờ xuất hiện trên terminal hay trong shell
history**.

**Chỗ dễ sai.** Verify trên database rỗng rồi kết luận "hệ thống chạy được". Lần
verify đầu chạy khi chưa seed, nên nó chỉ chứng minh app không crash. Phải verify
**lại** sau khi có dữ liệu thật — đó mới là lúc biết seed, app và database đồng ý
với nhau.

---

## Giai đoạn 10 — Kiểm thử bảo mật và viết báo cáo

**Resource:** bật `enable_flow_logs` và `enable_deny_demo` tạm thời.

11 kịch bản tấn công từ laptop. Với mỗi kịch bản: lưu **nguyên lệnh và nguyên
output** vào một file bằng chứng, rồi báo cáo **chỉ trích số từ đó** — không viết
tay số nào.

| Cần chứng minh | Cách làm | Kết quả đúng |
|---|---|---|
| Chỉ 80/443 mở | quét cổng vào ALB | 80, 443 mở; 22, 1433, 8080, 3389… **filtered** |
| Database không tới được | mở socket tới endpoint RDS :1433 | **timeout**, không phải "từ chối" |
| Không có SSH | thử kết nối port 22 | không có đường tới |
| Chống dò mật khẩu | gọi API đăng nhập 20 lần | từ lần thứ 6 trả **429** |
| Allowlist Host | gọi ALB với Host lạ | **403** |
| **NACL chặn theo IP** | bật `enable_deny_demo`, rồi gọi **hai đường** | trực tiếp vào ALB → **timeout**; qua tên miền → **200** |
| Blast radius của IAM | dùng công cụ mô phỏng policy | role app: S3 `allowed`, RDS `implicitDeny`; role máy: bí mật `explicitDeny` |
| Bằng chứng tầng mạng | bật Flow Logs, đọc bản ghi `REJECT` | khớp từng kịch bản |

**Mẹo đáng nhớ.** Để chứng minh rule chặn-theo-IP hoạt động cần *hai* điểm quan
sát: một máy bị chặn và một máy không. Nhưng **không cần dựng máy thứ hai** —
proxy của Cloudflare chính là điểm quan sát thứ hai. Gọi trực tiếp vào ALB thì
nguồn là IP laptop (bị chặn); gọi qua tên miền thì nguồn là IP Cloudflare (không
bị chặn). Cùng một lệnh, khác biệt duy nhất là địa chỉ nguồn.

**Vì sao thu bằng chứng ngay trong cùng cửa sổ đã bật.** Vì hạ tầng đang tính
tiền theo giờ. Mở một cửa sổ riêng chỉ để test là trả tiền hai lần cho cùng một
việc.

**Chỗ dễ sai.** Quên tắt `enable_deny_demo` — xem Issue #12.

---

# Phần III — Các issue chính

Đây là những lỗi **cấu trúc** — loại mà bất cứ ai làm hệ thống tương tự cũng sẽ
gặp. Mỗi cái ghi: hiện tượng, nguyên nhân thật, và bài học.

## Issue #1 — `terraform apply` báo thành công nhưng hệ thống không hoạt động

**Hiện tượng.** Apply xanh, Auto Scaling Group báo máy Healthy, nhưng ECS cluster
**không có máy nào** và không container nào chạy.

**Nguyên nhân.** ASG kiểm tra sức khoẻ ở mức "EC2 có đang running không". Nó
**không** kiểm tra "ECS agent trên máy đó có đăng ký vào cluster chưa". Một máy
boot lên mà agent chết vẫn làm apply xanh **và** ASG báo Healthy. Không có tín
hiệu nào ở tầng Terraform cho biết điều đó.

**Bài học — và đây là bài học lớn nhất của cả dự án.** *Apply thành công không
đồng nghĩa với hệ thống dùng được.* Hai chuyện khác nhau: Terraform chỉ đảm bảo
"AWS đã nhận lệnh tạo resource"; nó không biết gì về việc phần mềm bên trong
resource đó có hoạt động không. Với mọi hạ tầng, phải có một **bước kiểm tra sức
khoẻ thật sự** sau khi apply. Ta viết một script riêng cho việc đó, và nó kiểm cả
trạng thái kết nối của agent chứ không chỉ sự tồn tại của máy.

## Issue #2 — Đoạn khởi tạo máy tự treo chính nó

**Hiện tượng.** Máy boot xong nhưng không bao giờ tham gia cluster. Vào kiểm thì
thấy tiến trình khởi tạo vẫn đang "running" sau 20 phút.

**Nguyên nhân.** Script khởi tạo máy có một dòng ra lệnh khởi động dịch vụ ECS
ngay lập tức. Nhưng script đó *đang được chạy bởi* một dịch vụ hệ thống khác, và
lệnh khởi động kia lại chờ dịch vụ đó xong. Hai bên chờ nhau — **deadlock**.

**Bài học.** Trong script khởi tạo máy, chỉ nên **ghi cấu hình**, không nên ra
lệnh khởi động dịch vụ. Hệ thống sẽ tự khởi động đúng thứ tự sau đó. Đây là lỗi
kinh điển và rất khó chẩn đoán, vì hiện tượng là "không có gì xảy ra" chứ không
phải một thông báo lỗi.

## Issue #3 — Bật database và ứng dụng cùng lúc gây vòng lặp chết

**Hiện tượng.** Container API bị giết và tạo lại liên tục trong khoảng 8 phút,
log đầy lỗi kết nối. Sau đó tự nhiên bình thường.

**Nguyên nhân.** Health check của API **có mở kết nối tới database** (đúng thiết
kế — một API không nối được DB thì chưa thực sự sẵn sàng). ALB kết luận unhealthy
sau 45 giây và yêu cầu ECS thay container. Nhưng RDS SQL Server mất **5–10 phút**
để chuyển từ "starting" sang nhận được kết nối. Nên trong 5–10 phút đó, mọi
container API đều bị giết ngay khi vừa lên.

**Cách chữa sai.** Nâng thời gian ân hạn lên 10 phút. Làm vậy thì một API **thật
sự chết** cũng mất 10 phút mới bị phát hiện. Thời gian ân hạn là công cụ sai cho
vấn đề thứ tự.

**Cách chữa đúng.** Bắt buộc thứ tự: database phải nhận được kết nối **trước
khi** có chỗ cho container chạy. Trong quy trình bật, đây là một cửa chặn thật
sự, không phải khuyến nghị.

**Bài học.** Health check chạm database là quyết định đúng, nhưng nó tạo ra một
ràng buộc thứ tự khởi động. Ràng buộc đó phải được viết vào quy trình, vì nó
không hiển hiện ở bất cứ đâu trong cấu hình.

## Issue #4 — Tên của load balancer đổi mỗi lần dựng lại

**Hiện tượng.** Tắt hệ thống rồi bật lại, tên miền không hoạt động nữa dù mọi
thứ đều xanh.

**Nguyên nhân.** Tên DNS của ALB có một phần **ngẫu nhiên do AWS sinh lúc tạo**.
Vì ta xoá ALB mỗi lần tắt (để không tốn $18/tháng), mỗi lần bật lại là một tên
mới. Hai bản ghi DNS trỏ vào tên cũ đều chết.

**Cách chữa.** Thêm một **lớp trung gian** trong DNS: một bản ghi phụ trỏ vào
ALB, và hai bản ghi chính trỏ vào bản ghi phụ. Bật lại thì chỉ sửa **một** chỗ.

**Bài học.** Khi một thứ do nhà cung cấp sinh ra và có thể đổi, đừng để nhiều nơi
trỏ trực tiếp vào nó. Thêm một lớp gián tiếp mà bạn kiểm soát được.

## Issue #5 — Chứng chỉ TLS đứng mãi ở trạng thái chờ

**Hiện tượng.** Terraform treo 30 phút rồi báo lỗi timeout khi chờ chứng chỉ.

**Nguyên nhân.** ACM cấp chứng chỉ sau khi đọc được một bản ghi DNS xác thực.
Nhưng bản ghi đó đang bật chế độ proxy của Cloudflare — nghĩa là Cloudflare trả
về giá trị của *nó* thay vì giá trị thật. ACM không đọc được, nên chờ vô hạn.

**Cách chữa.** Bản ghi xác thực phải để chế độ **DNS thuần** (không proxy).

**Bài học.** Khi đặt một CDN/proxy trước hạ tầng, cần biết bản ghi nào được phép
proxy và bản ghi nào không. Bản ghi phục vụ *người dùng* thì proxy được; bản ghi
phục vụ *máy móc xác thực* thì không.

## Issue #6 — Máy không chịu chết khi tắt hệ thống

**Hiện tượng.** Hạ số máy về 0, nhưng máy nằm mãi ở trạng thái "đang chờ kết
thúc" và vẫn tính tiền.

**Nguyên nhân.** ECS tự tạo một *hook* trên Auto Scaling Group để có thời gian
chuyển việc khỏi máy trước khi máy chết. Thời gian chờ mặc định là **một tiếng**.
Hook này **không do Terraform quản lý** và thời gian chờ không phải tham số cấu
hình được, nên không sửa được bằng code.

**Cách chữa.** Xử lý ở tầng vận hành: khi thấy máy vào trạng thái chờ thì gửi
tín hiệu "xong rồi, cho chết đi". Việc này đã được tự động hoá trong script tắt.

**Bài học.** Không phải mọi thứ trong hạ tầng đều do IaC quản. Có những thứ dịch
vụ tự tạo ra, và phải biết chúng tồn tại. Bỏ qua cái này thì mỗi lần tắt vẫn tốn
thêm một tiếng tiền máy.

## Issue #7 — App tự cập nhật schema lúc khởi động

**Hiện tượng.** Không có hiện tượng — cho tới lúc có sự cố, và khi đó nó rất tệ.

**Nguyên nhân.** Ứng dụng ban đầu tự chạy cập nhật schema khi khởi động. Ba vấn
đề, theo thứ tự tăng dần độ nghiêm trọng:

1. **Không ai chặn được.** Cập nhật lỗi thì app crash, khởi động lại, lỗi lại —
   vòng lặp, và website chết trong suốt thời gian đó.
2. **Không biết nó thất bại.** Lỗi nằm lẫn trong log khởi động, không có ai báo.
3. **Tranh chấp.** Hai container API cùng lên sẽ cùng cố sửa schema một lúc.

**Cách chữa.** Bỏ hẳn khỏi app. Cập nhật schema đóng gói thành một chương trình
riêng, chạy như một task **một lần**, và **mã thoát của nó là điều kiện để deploy
tiếp tục**. Thất bại thì dừng — bản cũ vẫn đang phục vụ bình thường.

**Bài học.** Đây là mẫu thiết kế mà mọi hệ thống có database nên theo: **thay đổi
schema là một bước có cửa kiểm soát, không phải một hiệu ứng phụ của việc khởi
động ứng dụng.** Và chính sách phải là *chỉ tiến, không lùi*: không dùng
migration ngược; điểm quay về là bản snapshot chụp trước khi cập nhật.

## Issue #8 — Gọi load balancer nhận về 403 và tưởng là lỗi

**Hiện tượng.** Gọi ALB bằng tên DNS thô → HTTP 403. Trông như cấu hình sai.

**Nguyên nhân.** Đó là **đúng thiết kế**. ALB có danh sách tên miền cho phép, và
hành động mặc định cho tên miền lạ là trả 403. Gọi bằng tên thô của ALB thì tên
đó không nằm trong danh sách.

**Điểm cần phân biệt, và nó quan trọng:**
- Trả **403** = allowlist đang hoạt động → **đúng**
- Trả **503** = allowlist *chưa* có hiệu lực, request vẫn được chuyển tiếp và
  thất bại ở phía sau → **đây mới là lỗi**

**Bài học.** Khi thiết kế một rule từ chối, phải ghi lại rõ *phản hồi đúng khi
rule hoạt động là gì*. Không ghi thì lần sau chính nhóm mình sẽ nhìn thấy nó và
đi sửa một thứ không hỏng.

## Issue #9 — Dò cổng bị timeout và tưởng công cụ lỗi

**Hiện tượng.** Quét toàn bộ 65535 cổng của ALB, chạy 15 phút không ra kết quả
nào rồi bị cắt.

**Nguyên nhân.** Security Group **im lặng bỏ gói tin** thay vì trả lời "cổng
đóng". Nên với mỗi cổng đóng, công cụ quét phải chờ hết thời gian timeout rồi mới
kết luận. 65533 cổng × vài giây = không bao giờ xong.

**Cách nhìn lại.** Việc quét không xong **chính là bằng chứng**, không phải thất
bại: nếu firewall trả lời "cổng đóng" thì cùng lệnh đó xong trong vài giây. Việc
nó không xong chứng minh cấu hình đang che giấu hệ thống — kẻ tấn công không phân
biệt được "cổng đóng" với "máy không tồn tại".

**Bài học.** Trong kiểm thử bảo mật, phải hiểu *ý nghĩa* của kết quả, không chỉ
ghi lại kết quả. Và khi quét thì giới hạn phạm vi (danh sách cổng nguy hiểm +
1000 cổng phổ biến) thay vì quét tất cả.

## Issue #10 — Managed policy của AWS rộng hơn mình nghĩ

**Hiện tượng.** Không có hiện tượng. Phát hiện khi ngồi rà lại quyền.

**Nguyên nhân.** Role của máy EC2 dùng một managed policy sẵn của AWS để có thể
vào máy bằng SSM. Nhưng policy đó **kèm theo quyền đọc SSM Parameter**. Nghĩa là
ai vào được máy sẽ đọc được mật khẩu database — dù ta chưa bao giờ cấp quyền đó.

**Cách chữa.** Thêm một statement **Deny tường minh** cho quyền đọc bí mật trên
đường dẫn của dự án. Trong IAM, Deny luôn thắng Allow.

**Bài học.** Managed policy tiện nhưng là **hộp đen**. Phải đọc xem nó thật sự
cấp những gì. Và khi chỉ cần một phần, cách sạch là dùng managed policy rồi
**Deny lại phần không muốn** — vì Deny thắng, kết quả chắc chắn đúng bất kể AWS
sau này có thêm quyền gì vào policy đó.

## Issue #11 — Hiểu sai chi phí: tưởng cái gì miễn phí thì nó miễn phí

**Hiện tượng.** Suốt dự án, nhóm cẩn thận tắt NAT Gateway và ALB vì "chúng đắt",
nhưng để database chạy thoải mái vì "nó nằm trong free tier". Đến lúc soi hoá đơn
thì **database là khoản đắt nhất** — và riêng một dòng phí trong đó đã xấp xỉ
bằng cả NAT + ALB cộng lại.

**Nguyên nhân.** Hai sai lầm chồng lên nhau. Thứ nhất, `db.t3.micro` là loại máy
*burstable*: chỉ được dùng miễn phí 10% CPU, vượt lên thì **bị tính tiền phần
vượt**. SQL Server Express không tải vẫn ngồi ở ~36% CPU, nên nó vượt hạn mức
liên tục. Thứ hai, account này không hề có free tier — nó thuộc mô hình credit
trả trước, nên mọi thứ đều tính tiền, kể cả EC2.

**Bài học — hai cái, cả hai đều tổng quát:**

1. **Đừng suy diễn chi phí, hãy đo.** Bảng giá không cho biết bạn *thật sự* đang
   dùng cái gì. Phải bóc hoá đơn theo từng loại sử dụng. Trong trường hợp này,
   dòng phí lớn nhất không có tên nào trong sơ đồ kiến trúc.
2. **Đừng tin nhãn "miễn phí".** Nó luôn kèm điều kiện, và điều kiện thường là
   một hạn mức mà bạn không hề đo. Ở đây điều kiện là "10% CPU".

## Issue #12 — Bật rule demo rồi để quên

**Hiện tượng.** Bật lại hệ thống, mọi thứ báo healthy, nhưng browser timeout với
mọi URL. Trông y như hạ tầng bị lỗi nặng.

**Nguyên nhân.** Rule NACL chặn theo IP (bật lúc demo) vẫn còn bật, và nó đang
chặn đúng IP của máy mình ở tầng mạng. Hệ thống hoạt động hoàn hảo — chỉ riêng
mình không vào được.

**Cách chữa.** Script tắt tự đặt lại rule đó về false, và bảng trạng thái in cảnh
báo riêng nếu thấy nó đang bật.

**Bài học.** Bất cứ công tắc nào tạo ra hành vi "hệ thống trông như bị hỏng" đều
phải **tự động reset** khi kết thúc phiên làm việc, và phải **hiển thị rõ ràng**
khi đang bật. Không dựa vào việc con người nhớ.

---

# Phần IV — Vận hành hằng ngày

Sau khi hạ tầng đã xong, công việc thường ngày chỉ còn ba lệnh:

| Lệnh | Làm gì | Mất bao lâu |
|---|---|---|
| `up.sh` | Bật đủ để mở browser | 8–12 phút |
| `status.sh` | Xem đang chạy gì, bao lâu, tốn bao nhiêu | ngay |
| `down.sh` | Tắt sạch rồi tự kiểm chứng | 6–8 phút |

**Vì sao cần một bảng trạng thái riêng.** Vì bật/tắt mất nhiều phút, và như
Issue #1 đã chỉ ra, "apply xanh" không có nghĩa là dùng được. Bảng trạng thái in
**ý muốn** (giá trị trong cấu hình) cạnh **thực tế** (đọc trực tiếp từ AWS) — hai
cột lệch nhau là dấu hiệu có việc chạy dở. Thời gian lấy từ dấu thời gian của
AWS chứ không phải đồng hồ của script, nên tắt máy rồi mở lại vẫn đúng.

**Vì sao script tắt tự kiểm chứng lại bằng AWS.** Vì tin Terraform là chưa đủ:
nếu apply thất bại giữa đường, state có thể nói "đã xoá" trong khi resource vẫn
sống và vẫn tính tiền. Script gọi thẳng AWS đếm số load balancer, NAT gateway,
địa chỉ IP, máy chủ — và chỉ báo thành công khi tất cả bằng 0.

**Rủi ro lớn nhất còn lại, và nó không phải đơn giá.** AWS **tự khởi động lại**
một database đang dừng sau 7 ngày. Ở giá $0.098/giờ, một tuần không ai để ý là
$16.5 bay âm thầm. Đơn giá thì có chặn trên; cái này thì không. Đó là lý do việc
còn lại trong kế hoạch — một tác vụ tự tắt hằng đêm — là **bắt buộc**, không phải
tuỳ chọn.

---

# Phần V — Nếu làm lại từ đầu

Năm điều sẽ làm khác, xếp theo giá trị:

1. **Viết cách tắt trước khi viết cách bật.** Ta làm gần đúng (NAT có công tắc từ
   ngày đầu) nhưng bộ script tắt/bật hoàn chỉnh thì tới cuối mới có. Trong lúc
   đó, mọi lần tắt là một chuỗi lệnh tay, và mỗi lần là một cơ hội để quên một
   bước.

2. **Đo chi phí ngay tuần đầu, đừng đợi thấy hoá đơn.** Bóc hoá đơn theo loại sử
   dụng ngay sau phiên làm việc đầu tiên. Nếu làm vậy, ta đã biết database là
   khoản đắt nhất từ tuần một chứ không phải tuần cuối — và có thể đã chọn hệ
   quản trị nhẹ hơn ngay từ đầu, thay vì phải đổi về sau.

3. **Coi "apply xanh" là chưa xong.** Mỗi giai đoạn nên có một bước kiểm tra sức
   khoẻ thật sự ngay từ khi viết, không phải thêm vào sau khi bị lừa hai lần.

4. **Ghi lại phản hồi đúng của mỗi rule từ chối.** Issue #8 (403 đúng, 503 sai)
   là loại kiến thức bị mất ngay khi người viết rule quên nó.

5. **Test đường ghi sớm hơn.** Tiêu chí upload ảnh là tiêu chí duy nhất chứng
   minh IAM role thay được access key, mà nó lại được kiểm ở việc gần cuối cùng.
   Nếu nó thất bại, ta sẽ phát hiện quá muộn.
