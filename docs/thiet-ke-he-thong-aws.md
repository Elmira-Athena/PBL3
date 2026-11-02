# Thiết kế hệ thống AWS — HushStore

**Bổ sung WAF 2026-10-28:** chọn [Cloudflare Free](waf-cloudflare.md), với root `infra/cloudflare/` riêng. Khi bật `enable_cloudflare_origin_lockdown`, SG ALB chỉ nhận IP Cloudflare và listener yêu cầu Host **cùng** header bí mật. Flag mặc định false để chuyển NACL an toàn; các bảng SG Internet phía dưới là cấu hình trước lockdown. **Chưa apply hoặc kiểm thử runtime WAF.**

> **Tài liệu này dành cho ai:** thành viên trong nhóm chưa từng dùng AWS và chưa
> vững mạng máy tính, nhưng cần hiểu **bản chất** hệ thống để bảo vệ đồ án.
> Không cần đọc code. Mỗi phần trả lời hai câu: *cái này là gì* và *vì sao ta
> chọn như vậy*.
>
> Đọc theo thứ tự. Phần I và II là kiến thức nền — nếu đã biết thì nhảy sang
> Phần III. Trong Phần III, mục **"Linux — hệ điều hành chạy bên dưới tất cả"**
> trả lời gạch đầu dòng đầu tiên của đề bài và đọc được độc lập.

**Thiết kế hiện tại trong mã:** bỏ ba custom NACL, sáu subnet dùng default NACL
allow-all do AWS tạo và không do Terraform quản lý. Security Group là lớp lọc
mạng chính. Thay đổi này **chưa apply lên AWS và chưa kiểm chứng runtime**; xem
[hướng dẫn chuyển đổi](nacl-to-sg-migration.md) trước khi triển khai.

**Lịch sử 2026-09-06 — ba thay đổi đã triển khai ở thời điểm đó:**

| | Trước | Nay |
|---|---|---|
| Database | SQL Server Express, single-AZ | **PostgreSQL 17, Multi-AZ** (`db.t4g.micro`, `gp3`) |
| NAT Gateway | 1 cái, hai AZ đi chung | **2 cái, mỗi AZ một cái** + route table tách theo AZ |
| Read replica | không làm được (đòi SQL Server Enterprise ≥ 4 vCPU) | hạ tầng đã có, **mặc định tắt** |

Cả ba đã **apply thật lên AWS** ngày 2026-09-06 và `terraform plan` sau đó trả
`No changes`. Trước mốc này toàn bộ hạ tầng mới chỉ tồn tại dưới dạng mã và test.

⚠️ **Một điều tài liệu này KHÔNG khẳng định:** hệ thống chưa chạy đủ lâu để đo
chi phí thật sau khi đổi engine. Mọi con số tiền bên dưới là **giá niêm yết**,
cộng một số đo cũ trên SQL Server được giữ lại làm **cận trên** — chỗ nào như vậy
đều có ghi rõ.

---

# Phần I — Kiến thức mạng tối thiểu

Không nắm 8 khái niệm này thì phần thiết kế bên dưới sẽ chỉ là danh sách tên
riêng. Nắm rồi thì mọi quyết định trong hệ thống đều tự giải thích.

## 1. Địa chỉ IP và CIDR

Mỗi máy trong mạng có một địa chỉ IP, ví dụ `10.20.10.37`. Bốn số, mỗi số 0–255.

**CIDR** là cách viết gọn một *dải* địa chỉ: `10.20.0.0/16`.

Con số sau dấu `/` cho biết **bao nhiêu bit đầu bị cố định**. IP có 32 bit, nên
**số địa chỉ = 2^(số bit tự do)**, với *bit tự do = 32 − số sau dấu `/`*:

| CIDR | Bit cố định | Bit tự do | Số địa chỉ | Nghĩa |
|---|---|---|---|---|
| `10.20.0.0/16` | 16 | 16 | 65.536 | mọi IP bắt đầu bằng `10.20.` |
| `10.20.10.0/24` | 24 | 8 | 256 | mọi IP bắt đầu bằng `10.20.10.` |
| `10.20.0.0/23` | 23 | 9 | 512 | gộp `10.20.0.x` và `10.20.1.x` |
| `42.1.89.156/32` | 32 | 0 | 1 | **đúng một** máy |

Số sau `/` càng lớn thì dải càng **nhỏ**. `/32` là một IP duy nhất — ta dùng nó
để chỉ đúng máy laptop khi viết rule tấn công/chặn.

`0.0.0.0/0` = **mọi địa chỉ trên Internet**. Thấy nó trong một rule cho phép,
nghĩa là "cho cả thế giới vào". Đây là dấu hiệu cần soi kỹ nhất khi chấm bảo mật.

**IP private vs public.** Ba dải `10.x.x.x`, `172.16–31.x.x`, `192.168.x.x` là
IP *riêng* — không tồn tại trên Internet, ai cũng được dùng trong mạng nội bộ
của mình. Máy có IP private **không thể bị gọi trực tiếp từ Internet**. Đó là
lớp bảo vệ đầu tiên và mạnh nhất, mạnh hơn mọi firewall: kẻ tấn công không thể
gõ cửa một địa chỉ không tồn tại.

## 2. Subnet — và vì sao phải chia

**Subnet** là một dải IP con trong mạng lớn, gắn với một vùng vật lý. Chia mạng
`10.20.0.0/16` thành nhiều subnet nhỏ giống như chia một toà nhà thành các phòng
có cửa riêng.

Vì sao không để một mạng phẳng? Vì **rule chỉ chặt được khi có biên giới để
đặt rule lên**. Nếu web server và database nằm cùng một subnet, không có chỗ nào
để viết "chặn mọi thứ đi vào database trừ web server" ở tầng subnet — tất cả đã
ở cùng một phòng. Chia tách chính là điều kiện tiên quyết của nguyên tắc tối
thiểu.

## 3. Route table — bảng chỉ đường

Khi một máy muốn gửi gói tin tới `142.250.x.x` (ngoài Internet), nó tra **route
table**: "địa chỉ này thuộc dải nào, thì gửi ra cửa nào".

Một dòng đặc biệt: `0.0.0.0/0` → *cửa mặc định*. Nghĩa là "mọi thứ tôi không
biết đường thì đẩy ra đây".

Điểm cốt tử: **một subnet không có dòng `0.0.0.0/0` trỏ ra Internet thì máy
trong đó không thể ra Internet, và Internet cũng không có đường vào.** Không cần
firewall nào cả. Đây là cách **db tier** của ta được bảo vệ.

⚠️ **Đọc kỹ chữ "trỏ ra Internet" — nó là chỗ dễ hiểu nhầm nhất của cả mục này.**
Có ba trạng thái, không phải hai:

| Route table của subnet | Máy trong đó | Internet vào được? |
|---|---|---|
| `0.0.0.0/0` → **Internet Gateway** | ra được Internet | **được** (nếu máy có IP public) |
| `0.0.0.0/0` → **NAT Gateway** | ra được Internet | **KHÔNG** — xem mục 6 |
| **không có** dòng `0.0.0.0/0` nào | **không ra được** | không |

App tier của ta nằm ở **hàng giữa**: nó *có* một dòng `0.0.0.0/0`, nhưng trỏ vào
NAT chứ không phải Internet Gateway — nên nó đi ra được mà không ai vào được. Db
tier nằm ở hàng cuối. Mục 6 giải thích vì sao NAT tạo ra được sự bất đối xứng đó.

## 4. Cổng (port) và TCP

Một IP là địa chỉ toà nhà; **port** là số phòng. Máy chủ nghe trên một port, và
mỗi port thường ứng với một loại dịch vụ:

| Port | Dịch vụ | Ghi chú trong hệ thống này |
|---|---|---|
| 22 | SSH — điều khiển máy Linux từ xa | **cố ý đóng hoàn toàn** |
| 80 | HTTP — web không mã hoá | chỉ để redirect sang 443 |
| 443 | HTTPS — web có mã hoá | cửa chính duy nhất |
| 5432 | PostgreSQL | chỉ mở giữa app tier và db tier |
| 8080 | API .NET | chỉ mở giữa ALB và container |

**Ephemeral port (cổng tạm).** Khi máy A gọi máy B ở port 443, máy A tự mở một
port ngẫu nhiên trong dải **1024–65535** để nhận câu trả lời. Nghe lạ nhưng đây
là cổng đích của gói phản hồi. SG stateful tự cho phép traffic trả lời; cấu hình
hiện tại không cần rule riêng cho dải cổng này.

## 5. Stateful vs stateless — phân biệt quan trọng nhất tài liệu này

Hình dung một người bảo vệ ở cổng.

**Stateful** (có ghi nhớ): bảo vệ ghi sổ "ông A vừa vào lúc 9h". Khi ông A đi ra,
bảo vệ nhìn sổ và cho ra luôn, không cần rule riêng cho chiều ra. **Security
Group hoạt động như vậy** — chỉ cần cho phép chiều vào, chiều trả lời tự động
được phép.

**Stateless** (không ghi nhớ): bảo vệ không có sổ. Mỗi gói tin, dù đi vào hay đi
ra, đều bị xét lại từ đầu như thể chưa từng thấy. **Network ACL hoạt động như
vậy** — phải viết rule cho **cả hai chiều**, kể cả chiều trả lời.

Nếu dùng custom NACL hạn chế traffic thì phải xét cả chiều đi lẫn chiều về,
bao gồm dải cổng tạm phù hợp với client và dịch vụ. Đó là nguồn phức tạp của
thiết kế cũ. NACL xét số rule từ nhỏ tới lớn, dừng ở rule khớp đầu tiên.

Trong thiết kế hiện tại, default NACL cho phép mọi traffic hai chiều; SG stateful
thực thi quan hệ ALB → web → RDS mà không cần thêm rule ephemeral. Default NACL
vẫn tồn tại vì mỗi subnet luôn phải gắn một NACL; nó không phải lớp chặn chủ động.

## 6. NAT — vì sao máy không có IP public vẫn tải được phần mềm

App tier không có IP public, nên Internet không vào được. Nhưng nó *cần đi ra*:
tải container image, gọi API của AWS, cập nhật hệ thống.

**NAT (Network Address Translation)** giải quyết chuyện đó. Máy private gửi gói
tin ra, NAT đổi địa chỉ nguồn thành IP public của chính nó, ghi nhớ, rồi khi câu
trả lời về thì dịch ngược lại và chuyển vào.

Tính chất quan trọng: **NAT chỉ hoạt động một chiều**. Nó mở đường ra, nhưng
không tạo đường vào — vì nó chỉ chuyển tiếp những gói tin *trả lời* cho kết nối
do bên trong khởi tạo. Đây chính là điều ta muốn.

## 7. DNS — dịch tên thành IP

Người dùng gõ `hushstore.io.vn`; máy tính cần một IP. **DNS** làm việc dịch đó.

Hai loại bản ghi ta dùng:
- **A record**: tên → địa chỉ IP
- **CNAME record**: tên → **một tên khác** (rồi tên đó mới ra IP)

Ta dùng CNAME vì địa chỉ của load balancer do AWS cấp và **có thể đổi**, nên
không thể ghi cứng IP.

## 8. TLS/HTTPS — mã hoá và chứng chỉ

HTTPS = HTTP + mã hoá TLS. Mã hoá giải quyết hai việc **khác nhau**, và trộn hai
việc này là chỗ hay hiểu sai:

1. **Bí mật** — người ngồi giữa (chủ quán wifi, nhà mạng) không đọc được nội dung.
2. **Danh tính** — bạn đang nói chuyện đúng với `hushstore.io.vn`, không phải với
   một máy chủ giả mạo nó.

Việc thứ nhất chỉ cần thuật toán. Việc thứ hai cần **chứng chỉ**.

### Chứng chỉ là gì

Trước hết, **cặp khoá** — nền của cả mục này. Máy chủ giữ hai khoá đi liền nhau:
**khoá riêng** giữ kín tuyệt đối, **khoá công khai** đem công bố cho cả thế giới. Thứ
khoá công khai mã hoá thì chỉ khoá riêng tương ứng giải được; ngược lại, thứ khoá riêng
**ký** thì ai cũng dùng khoá công khai để kiểm chứng được. Vì vậy công bố khoá công
khai vẫn an toàn.

Chứng chỉ là một file chứa: **tên miền**, **khoá công khai** của máy chủ, **thời
hạn**, và **chữ ký số** của một tổ chức tên là **CA** (Certificate Authority).

Cơ chế tin cậy nằm ở chữ ký đó. Trình duyệt của bạn được cài sẵn danh sách khoảng
100–150 CA mà nó tin. Khi máy chủ xuất chứng chỉ, trình duyệt kiểm:

```
chứng chỉ của hushstore.io.vn
   ├─ ký bởi CA trung gian (intermediate)
   │     └─ ký bởi CA gốc (root) — cái này CÓ trong danh sách cài sẵn
   ├─ tên miền trong chứng chỉ có khớp với tên miền đang gõ?
   └─ hôm nay còn nằm trong thời hạn?
```

Chuỗi đó gọi là **chain of trust**. Sai một mắt là trình duyệt chặn — và nó chặn
hẳn, không phải cảnh báo nhẹ, vì một chứng chỉ không kiểm được nghĩa là **không
biết đang nói chuyện với ai**.

Điểm mấu chốt: chứng chỉ **không** làm mã hoá. Nó chỉ chứng minh khoá công khai
này thuộc về tên miền này. Mã hoá là việc của bước sau, dùng chính khoá đó.

### Thời hạn — và vì sao nó đang ngắn dần rất nhanh

Mỗi chứng chỉ có ngày hết hạn. Trước 2020 thời hạn thường là **2–3 năm**. Hiện
trần là **200 ngày**, và theo lịch đã được CA/Browser Forum (nhóm gồm các nhà cấp
chứng chỉ và các hãng trình duyệt, tự thoả thuận luật chung với nhau — không phải cơ
quan nhà nước) thông qua tháng 4/2025
([ballot SC-081v3](https://cabforum.org/2025/04/11/ballot-sc081v3-introduce-schedule-of-reducing-validity-and-data-reuse-periods/)):

| Từ ngày | Thời hạn tối đa |
|---|---|
| 2026-03-15 | **200 ngày** ← đang áp dụng |
| 2027-03-15 | 100 ngày |
| 2029-03-15 | **47 ngày** |

Lý do rút ngắn không phải để bán thêm chứng chỉ (phần lớn chứng chỉ giờ miễn phí).
Lý do là **cơ chế thu hồi không hoạt động đáng tin**.

Khi khoá riêng của một máy chủ bị lộ, chứng chỉ của nó phải bị **thu hồi**. Có hai
cơ chế: **CRL** (danh sách thu hồi, trình duyệt tải về định kỳ) và **OCSP** (trình
duyệt hỏi CA từng lần). Cả hai đều có vấn đề thực tế: CRL thì cũ, OCSP thì chậm và
làm rò rỉ lịch sử duyệt web — nên phần lớn trình duyệt **bỏ qua lỗi khi không hỏi
được**. Nghĩa là một chứng chỉ đã thu hồi vẫn thường dùng được.

Kết luận của cả ngành: nếu không thu hồi được đáng tin, thì hãy để chứng chỉ
**tự hết hạn nhanh**. Thời hạn ngắn chính là cơ chế thu hồi.

Hệ quả cho người vận hành: **gia hạn tay sẽ không còn khả thi.** Tới 2029, mỗi tên
miền phải thay chứng chỉ 8 lần/năm. Nên tự động hoá gia hạn không phải tiện lợi mà
là bắt buộc — và đó là lý do thật sự để dùng ACM trong hệ thống này, không phải vì
nó miễn phí.

### "TTL" — cùng một chữ, bốn thứ khác nhau

TTL (*time to live*) nghĩa là "sống được bao lâu rồi phải làm lại". Nó bị dùng cho
quá nhiều thứ, nên khi ai đó nói "TTL", phải hỏi lại TTL của cái gì:

| Loại | Nghĩa | Ai quyết định |
|---|---|---|
| **Thời hạn chứng chỉ** | Sau ngày này trình duyệt chặn website | CA, theo trần của CA/Browser Forum |
| **TTL của bản ghi DNS** | Máy khách được cache kết quả phân giải tên bao lâu trước khi hỏi lại | Chủ tên miền |
| **Thời hạn credential/token** | Sau đó phải xin cái mới (token JWT, credential tạm của AWS) | Người viết ứng dụng |
| **Thời hạn lưu trữ** (retention) | Sau đó dữ liệu bị **xoá** — log, backup, artifact | Người viết hạ tầng |

Ba loại đầu: hết hạn thì **xin lại**. Loại thứ tư: hết hạn thì **mất**. Nhầm hai
nhóm này là cách người ta xoá mất bằng chứng của chính mình.

Bảng đầy đủ mọi giá trị TTL thật của hệ thống này — kèm triệu chứng khi từng cái
hết hạn — ở [bao-mat-he-thong.md](bao-mat-he-thong.md) mục *Lớp 2 → Chứng chỉ TLS:
vòng đời và TTL*.

### TLS termination

**TLS termination** là chỗ mã hoá được *bóc ra*. Trong hệ thống này, load balancer
bóc TLS rồi chuyển tiếp HTTP thường vào trong mạng riêng. Nhờ vậy các container
không phải quản chứng chỉ — một chỗ duy nhất lo việc đó.

Đánh đổi cần biết: đoạn từ load balancer vào container là **HTTP không mã hoá**.
Chấp nhận được ở đây vì đoạn đó nằm hoàn toàn trong VPC riêng, và Security Group
chỉ cho đúng một nguồn gọi vào. Nếu yêu cầu là mã hoá đầu-cuối (ví dụ hệ thống
thanh toán) thì phải mã hoá lại đoạn trong, và giá phải trả là mỗi container lại
cần chứng chỉ riêng.

Một hệ quả nữa của việc bóc TLS: ứng dụng bên trong nhận request HTTP, nên nếu
không đọc header `X-Forwarded-Proto` thì nó **tưởng người dùng đang dùng HTTP** và
sẽ redirect vòng vòng. Chi tiết ở Phần III.

---

# Phần II — Kiến thức AWS tối thiểu

## Region và Availability Zone

**Region** là một vùng địa lý có trung tâm dữ liệu của AWS. Ta dùng
`ap-southeast-1` (Singapore) — gần Việt Nam nhất nên độ trễ thấp.

**Availability Zone (AZ)** là một trung tâm dữ liệu riêng biệt *trong* region —
nguồn điện, mạng, làm mát độc lập. Một region có nhiều AZ. Đặt hệ thống ở 2 AZ
nghĩa là một trung tâm dữ liệu cháy thì cái kia vẫn sống.

Ta dùng `ap-southeast-1a` và `ap-southeast-1b`. Lý do bắt buộc: **Application
Load Balancer đòi tối thiểu 2 subnet ở 2 AZ khác nhau** — AWS không cho tạo nếu
chỉ có một.

## Các dịch vụ dùng trong hệ thống

| Dịch vụ | Là gì | Vai trò ở đây |
|---|---|---|
| **VPC** | Mạng riêng ảo của bạn trong AWS | Toàn bộ hệ thống nằm trong đây |
| **Subnet** | Dải IP con, gắn với 1 AZ | 6 cái, chia 3 tier × 2 AZ |
| **Internet Gateway (IGW)** | Cửa ra/vào Internet của VPC | Gắn vào VPC, phục vụ public subnet |
| **NAT Gateway** | Cho private subnet đi ra Internet | App tier tải image, gọi API AWS |
| **Security Group (SG)** | Firewall **stateful**, gắn vào từng resource | 3 cái: alb, web, rds |
| **Network ACL (NACL)** | Firewall **stateless**, gắn vào từng subnet | Default allow-all của AWS, dùng chung cho 6 subnet; không quản lý bằng Terraform |
| **Application Load Balancer (ALB)** | Bộ chia tải hiểu HTTP | Cửa vào duy nhất, bóc TLS, định tuyến theo tên miền |
| **Target Group** | Nhóm đích để ALB gửi traffic tới | 2 cái: web (:80) và api (:8080) |
| **EC2** | Máy chủ ảo | 1 máy `t3.micro` chạy container |
| **AMI** | Ảnh đĩa để tạo máy | Bản Amazon Linux đã cài sẵn ECS agent |
| **Launch Template** | Khuôn mẫu tạo EC2 | Định nghĩa máy sẽ được tạo thế nào |
| **Auto Scaling Group (ASG)** | Quản lý số lượng EC2 | Đặt 0 để tắt, 1 để bật |
| **ECS** | Bộ điều phối container | Quyết định container nào chạy ở đâu *(hiện 1 máy nên gần như không phải chọn — vai trò này rõ khi nhiều máy)* |
| **Task Definition** | Bản mô tả một container sẽ chạy thế nào | 4 cái: web, api, migrator, seeder |
| **ECS Service** | Giữ cho container luôn chạy đủ số lượng | 2 cái: web, api |
| **ECR** | Kho chứa container image | 4 repo |
| **RDS** | Database do AWS quản lý hộ | PostgreSQL 17, Multi-AZ |
| **IAM Role** | Danh tính có quyền, **không có mật khẩu** — gán cho máy hoặc container, AWS tự cấp khoá tạm và tự đổi liên tục | 4 role, mỗi role một phạm vi |
| **SSM Parameter Store** | Kho lưu bí mật, mã hoá | Mật khẩu DB, connection string, khoá JWT |
| **SSM Session Manager** | Vào máy chủ **không cần SSH** | Đường quản trị duy nhất |
| **CloudWatch Logs** | Nơi tập trung log | Log của mọi container |
| **VPC Flow Logs** | Ghi lại mọi kết nối bị chặn/cho phép | Bằng chứng tầng mạng cho báo cáo |
| **ACM** | Cấp chứng chỉ TLS miễn phí | Chứng chỉ cho ALB, tự gia hạn |
| **S3** | Lưu file | Ảnh sản phẩm, log, state của Terraform |

**Thứ tự lắp ráp — đọc xong bảng trên thì nối lại theo đường này:**

```
Internet → ALB → Target Group → EC2 ← (ASG dựng ra, theo Launch Template, từ một AMI)
                                 └─ trên EC2: ECS chạy container theo Task Definition,
                                    ECS Service lo giữ đúng số bản đang chạy
                                                   ↓
                                                  RDS
```

## Terraform — và vì sao dùng nó

Có hai cách dựng hạ tầng:

**Bấm chuột trên console.** Nhanh lúc đầu. Nhưng: không ai biết bạn đã bấm gì,
không dựng lại được y hệt, không review được, và sau ba tháng không ai còn nhớ
cái Security Group kia mở port đó để làm gì.

**Infrastructure as Code (IaC).** Viết hạ tầng thành file văn bản, lưu vào Git.
Terraform đọc file đó và gọi API của AWS để tạo đúng những thứ được mô tả.

Cái được:
- **Xem lại được** — hạ tầng nằm trong Git, có history, có review
- **Dựng lại được** — xoá sạch rồi dựng lại y nguyên
- **Xem trước được** — `terraform plan` cho biết sẽ thay đổi gì *trước khi* thay đổi
- **Tự tài liệu hoá** — file cấu hình chính là tài liệu, không bị lệch với thực tế

Ba khái niệm cần biết:

**State.** Terraform ghi lại "tôi đã tạo những gì" vào một file state. Nhờ state
nó biết cái gì cần tạo mới, cái gì cần sửa, cái gì cần xoá. State của ta nằm
trên S3 (không nằm trên máy cá nhân) để cả nhóm dùng chung và có **lock** — hai
người apply cùng lúc thì người thứ hai bị chặn, tránh hỏng hạ tầng.

**Module.** Nhóm resource liên quan thành một khối tái dùng được. Ta có 8 module:
`network`, `security`, `data`, `ecs`, `alb`, `storage`, `cicd`, `costguard`.

**Provider.** Thư viện biết cách nói chuyện với AWS. Ta dùng AWS provider 6.60.

### Hai môi trường — tách bằng THƯ MỤC, không phải workspace

```
infra/tf/envs/
├── prod/   key = "prod/terraform.tfstate"   project = hushstore       10.20.0.0/16
└── dev/    key = "dev/terraform.tfstate"    project = hushstore-dev   10.30.0.0/16
```

`envs/dev/main.tf` và `outputs.tf` là **symlink** sang `envs/prod/`. Cố ý: hai
môi trường phải dựng **cùng một kiến trúc**, khác nhau ở *giá trị* chứ không ở
*cấu trúc*. Chép 200 dòng `main.tf` sang là tạo hai nguồn sự thật — và loại lỗi
sinh ra từ đó là loại tệ nhất: dev xanh, prod đỏ, vì một `module` block chỉ được
thêm ở một bên.

Toàn bộ khác biệt nằm gọn trong `dev/variables.tf`. Thêm một `var` mới vào
`prod/main.tf` mà quên khai ở dev thì `terraform validate` của dev **đỏ** — hỏng
to tiếng, có chủ ý, và CI chạy validate cho `envs/*/` nên nó bị chặn ngay ở PR.

**Vì sao không dùng `terraform workspace`:** workspace dùng chung một backend key
và một file cấu hình, phân biệt bằng `terraform.workspace` rải khắp code. Quên
`workspace select` một lần là apply thẳng vào prod — không có ranh giới cứng nào
chặn. Thư mục thì `cd` sai là thấy ngay, và `backend.tf` khai key khác nhau nên
state không thể lẫn.

**Dev cố ý rẻ hơn prod:** `nat_gateway_count = 1` (prod 2), `enable_multi_az =
false` (prod true), `enable_budget = false` — vì dev tồn tại để thử một thay đổi
Terraform *trước khi* đụng vào prod, không phải để chứng minh tính sẵn sàng cao.

⚠️ Còn nợ: bộ script `up.sh`/`down.sh`/`status.sh` hiện hardcode prod
(`HS_PROJECT=hushstore`, `-chdir=envs/prod`), và 4 repo ECR của dev đang rỗng vì
`deploy.yml` chỉ push vào repo của prod. Dev dựng được hạ tầng, chưa deploy được
ứng dụng.

---

# Phần III — Thiết kế hệ thống HushStore

## Đường đi của một request

Người dùng mở `https://hushstore.io.vn` và bấm xem sản phẩm. Các SG kiểm soát
kết nối ở từng chặng; ALB kiểm tra TLS và Host header; ứng dụng kiểm tra quyền
truy cập dữ liệu. Default NACL cho qua cả hai chiều nên không được tính là lớp
chặn trong luồng hiện tại. Bản [bảo mật](bao-mat-he-thong.md) giải thích sáu nhóm
biện pháp phòng thủ, không dùng cách đếm 8/10/12 chốt của thiết kế custom NACL cũ.

**Trước khi đọc sơ đồ — Cloudflare là ai và vì sao nó ở đây.** Tên miền
`hushstore.io.vn` được quản lý DNS bởi **Cloudflare** (dịch vụ miễn phí, KHÔNG
thuộc AWS). Nhóm bật chế độ *proxy* của nó, nghĩa là Cloudflare đứng chắn phía
trước và chuyển tiếp request vào ALB, thay vì chỉ trả về địa chỉ của ALB. Hệ quả
phải nắm:

- **Có hai lớp TLS nối tiếp nhau:** người dùng ↔ Cloudflare dùng chứng chỉ của
  Cloudflare; Cloudflare ↔ ALB dùng chứng chỉ ACM của ta. Không phải hai lớp
  chồng lên nhau, mà hai chặng riêng biệt, mỗi chặng mã hoá độc lập.
- **Câu "ALB là cửa vào duy nhất" vẫn đúng, nhưng đúng ở phạm vi AWS:** trong VPC
  của ta không có đường nào khác đi vào. Cloudflare nằm *ngoài* AWS, ở phía trước.
- WAF/Transform Rules của Cloudflare được quản lý bằng root `infra/cloudflare/`
  riêng. DNS có sẵn vẫn do operator quản lý. Free Managed Ruleset do Cloudflare
  tự triển khai; phải xác nhận trên dashboard. Xem [runbook WAF](waf-cloudflare.md).

```
Người dùng → DNS / Cloudflare proxy (ngoài AWS)
   │ HTTPS :443
   ▼
sg-alb ingress 80/443 từ Internet
   ▼
ALB — :80 redirect sang HTTPS; :443 terminate TLS, kiểm Host
   │ hushstore.io.vn → web :80; api.hushstore.io.vn → API :8080
   │ Host khác → 403
   │ sg-alb egress 80/8080 tới sg-web
   ▼
sg-web ingress 80/8080 chỉ từ sg-alb
   ▼
EC2 không IP public — nginx :80 / API :8080
   │ API kiểm JWT, quyền dữ liệu
   │ sg-web egress 5432 tới sg-rds
   ▼
sg-rds ingress 5432 chỉ từ sg-web; egress rỗng
   ▼
RDS PostgreSQL — subnet DB không có default route Internet
```

Phản hồi trên các kết nối đã được SG cho phép tự đi về nhờ connection tracking,
kể cả khi `sg-rds` không có egress. Default NACL allow-all không cần rule trả lời
riêng. Kết nối mới vẫn phải khớp rule ở từng SG liên quan.

Các lớp bảo vệ có vai trò khác nhau: SG xác định nguồn và đích tin cậy, route và
IP quyết định đường đi, ALB xử lý TLS/Host, IAM và ứng dụng giới hạn quyền. Bỏ
custom NACL đồng nghĩa bỏ lớp deny theo CIDR ở subnet và bài demo chặn một IP;
đây là đánh đổi đã chọn để giảm rule stateless và giữ tập luật mạng dễ kiểm tra.

## Mạng — VPC và 6 subnet

VPC `10.20.0.0/16`. Chọn `10.20` chứ không phải `10.0` để không trùng với hạ
tầng cũ trong lúc chuyển đổi song song.

| Subnet | CIDR | AZ | Chứa | Có IP public? | Có đường ra Internet? |
|---|---|---|---|---|---|
| `public-a` | `10.20.0.0/24` | 1a | ALB, **NAT Gateway A** | Có | Trực tiếp qua IGW |
| `public-b` | `10.20.1.0/24` | 1b | ALB, **NAT Gateway B** | Có | Trực tiếp qua IGW |
| `app-a` | `10.20.10.0/24` | 1a | EC2 chạy container | **Không** | Chỉ đi ra, qua **NAT A** |
| `app-b` | `10.20.11.0/24` | 1b | EC2 chạy container | **Không** | Chỉ đi ra, qua **NAT B** |
| `db-a` | `10.20.20.0/24` | 1a | RDS | **Không** | **Không có** |
| `db-b` | `10.20.21.0/24` | 1b | RDS (subnet group) | **Không** | **Không có** |

*(**DB subnet group** = danh sách subnet ta khai cho RDS, để AWS biết được phép đặt
database vào những chỗ nào.)*

**Vì sao 3 tier chứ không phải 2?** Vì db tier tách riêng thì mới viết được rule
"chỉ app tier mới được nói chuyện với database" ở tầng subnet. Nếu app và db
cùng subnet, rule đó không tồn tại được. Subnet không tốn phí, nên tách là lựa
chọn miễn phí đổi lấy một lớp phòng thủ thật.

**Vì sao mỗi tier 2 AZ?** ALB bắt buộc 2 AZ. Db subnet group của RDS cũng đòi tối
thiểu 2 subnet ở 2 AZ — **và đây là chỗ dễ đọc nhầm sơ đồ**: subnet group phủ 2 AZ
là yêu cầu của AWS cho *mọi* RDS, kể cả single-AZ. Nhìn thấy "RDS ở cả hai AZ"
trên sơ đồ **không** phải bằng chứng Multi-AZ. Bằng chứng là cờ `multi_az`, và
hiện nó **bật**.

**Định tuyến — một route table cho MỖI app subnet, không dùng chung:**
- Public subnet: `0.0.0.0/0` → **Internet Gateway**. Có đường vào và ra. Cả hai
  public subnet dùng chung một route table, vì đích của chúng giống hệt nhau.
- App subnet: **hai route table riêng**. `app-a` → NAT A, `app-b` → NAT B.
- Db subnet: **không có dòng `0.0.0.0/0` nào**. Không đường ra, không đường vào.

**Vì sao app tier phải tách route table, còn public tier thì không?** Vì một route
table chỉ chứa được **một** dòng `0.0.0.0/0`. Dùng chung một route table thì hai
AZ buộc đi chung một NAT — và lúc đó dựng NAT thứ hai cũng vô nghĩa, vì không có
cách nào trỏ `app-b` sang nó. Tách route table là **điều kiện cần** của NAT theo
AZ, không phải một tuỳ chọn làm cho đẹp. Route table không tính phí.

**Vì sao hai NAT chứ không phải một?** NAT Gateway nằm *trong* một subnet, nên nó
gắn chặt với đúng một AZ và **không tự chuyển sang AZ khác** khi AZ đó hỏng. Với
một NAT:
- Mọi byte từ `app-b` đi ra Internet phải sang AZ-a trước ⇒ chịu thêm phí
  **cross-AZ $0.01/GB mỗi chiều**, cộng trên $0.045/GB xử lý của NAT. Khoản này
  không hiện ở bất kỳ dòng hoá đơn nào tên "NAT".
- Mất AZ-a là `app-b` mất luôn đường ra: không pull được image từ ECR ⇒ ECS
  **không dựng lại được task**, và SSM cũng đứt.

⚠️ **Nhưng phải nói đúng thứ NAT thứ hai mua được:** nó chỉ cứu **egress** —
deploy, ECR, SSM. Đường phục vụ người dùng là ALB → EC2 → RDS, **đi hoàn toàn
trong VPC và không chạm NAT**. Muốn hệ thống sống sót khi mất một AZ thì thứ phải
bật là **Multi-AZ của RDS**, không phải NAT. Giá: **$0.059/giờ mỗi cái**.

Đó là lý do database an toàn: không phải vì firewall giỏi, mà vì **không tồn tại
con đường nào** từ Internet tới nó.

**S3 Gateway Endpoint.** Một cửa riêng đi tới S3 ngay trong VPC, miễn phí. Nhờ
nó, traffic đọc/ghi ảnh sản phẩm không đi qua NAT Gateway — tiết kiệm phí data
($0.045/GB) và vẫn hoạt động khi NAT đã bị tắt.

**ECR Interface Endpoint ×2 — `enable_ecr_endpoints`, mặc định TẮT.** Hai cửa
riêng đi tới ECR (`ecr.api` cho auth token + manifest, `ecr.dkr` cho Docker
Registry API). SG của endpoint chỉ nhận TCP 443 từ `sg-web`, không mở theo
CIDR của app subnet; rule này nằm ở module `security` để tránh vòng phụ thuộc.
Bật thì phần đường mạng để kéo image chạy được **ngay cả khi `enable_nat =
false`**, và không gói tin nào ra Internet.

Vì sao chỉ 2 cái, không phải đủ bộ 7 như tài liệu AWS thường khuyên
(`ecr.api`, `ecr.dkr`, `ssm`, `ssmmessages`, `ec2messages`, `logs`,
`secretsmanager`) — đây là phép tính, không phải cảm tính:

| | số ENI | $/giờ |
|---|---|---|
| Đủ bộ 7 endpoint × 2 AZ | 14 | **≈ $0.14** |
| 2 NAT Gateway hiện dùng | — | $0.118 |
| Chỉ `ecr.api` + `ecr.dkr` × 2 AZ | 4 | **$0.04** |

Interface endpoint tính tiền **theo ENI**, mà mỗi endpoint đặt một ENI vào *mỗi*
subnet được khai. Ở 2 AZ, đủ bộ **đắt hơn** cả hai NAT Gateway — và vẫn **không
thay được NAT**, vì task còn cần egress cho `yum update` và cho API bên ngoài.
Nên đủ bộ là *cộng thêm* $0.14/giờ chứ không phải *thay thế*.

Hai cái ECR thì khác: phần byte lớn nhất của một lần pull là layer, mà layer nằm
trên S3 — S3 Gateway Endpoint ở trên đã gánh phần đó **miễn phí** từ trước. Hai
endpoint này gỡ nốt phần metadata + auth, đổi lấy khả năng pull image mà không
cần NAT.

> 🚨 **Đây là resource duy nhất trong stack tính tiền mà KHÔNG được
> `enable_nat` / `enable_alb` / `instance_count` che chắn.** Một `terraform
> apply` bình thường với công tắc này bật là đồng hồ chạy ngay, kể cả khi mọi
> thứ khác đã tắt. `status.sh` in một dòng riêng cho nó vì lý do đó.
>
> Và `private_dns_enabled` **bắt buộc** `true`. Đặt `false` thì endpoint vẫn
> dựng, vẫn tính tiền, mà containerd vẫn phân giải tên ECR ra IP công khai rồi
> đi qua NAT như cũ — trả tiền hai lần, không lỗi, không log. `status.sh` in một
> dòng **đỏ** khi gặp trạng thái đó.

## Security Group — firewall stateful, chỉ có danh sách cho phép

Ba SG phục vụ ứng dụng. Default SG của VPC được Terraform quản lý với ingress và
egress rỗng. Khi bật ECR endpoint có thêm SG endpoint chỉ nhận 443 từ `sg-web`.
Điểm đặc biệt: **hầu hết rule dùng SG khác làm nguồn, không dùng dải IP.**

| SG | Cho vào (ingress) | Cho ra (egress) |
|---|---|---|
| `sg-alb` | 80 ← `0.0.0.0/0`<br>443 ← `0.0.0.0/0` | 80 → `sg-web`<br>8080 → `sg-web`<br>*(không gì khác)* |
| `sg-web` | 80 ← **chỉ `sg-alb`**<br>8080 ← **chỉ `sg-alb`**<br>**không có rule port 22 nào** | 5432 → `sg-rds`<br>80, 443 → `0.0.0.0/0` *(tải image, gọi API AWS)* |
| `sg-rds` | 5432 ← **chỉ `sg-web`** | **rỗng hoàn toàn** |

**Vì sao tham chiếu SG thay vì ghi dải IP?** Vì IP của EC2 thay đổi mỗi lần máy
được tạo lại, còn SG thì không. Viết `80 ← sg-alb` nghĩa là "cho vào nếu gói tin
đến từ *bất cứ máy nào đang gắn sg-alb*" — rule tự đúng mãi, không cần cập nhật.
Đây là cách đúng, và nó cũng chặt hơn: không có cách nào giả mạo được việc "tôi
đang gắn SG đó".

**Vì sao egress của `sg-rds` rỗng?** Database không cần chủ động gọi ra ngoài
bao giờ. SG là stateful nên câu trả lời cho app vẫn đi được. Egress rỗng nghĩa
là không cho phép database chủ động mở kết nối mới. Dữ liệu vẫn có thể đi về
trên kết nối PostgreSQL hợp lệ, nên egress rỗng không bảo đảm chống rò rỉ dữ liệu.

**Vì sao không có port 22 ở đâu cả?** Vì SSH là bề mặt tấn công lớn nhất và phổ
biến nhất của một máy Linux trên Internet (Flow Logs của ta bắt được máy quét tự
động dò port 22 và 23 chỉ trong một giờ). Ta bỏ nó hoàn toàn: **không có key
pair nào, không có file `.pem` nào**. Quản trị đi bằng **SSM Session Manager** —
AWS mở kênh từ trong máy ra, nên không cần mở port nào từ ngoài vào, và mọi
phiên đều được ghi log và kiểm soát bằng IAM.

## Network ACL — dùng default allow-all của AWS

Sáu subnet public/app/db dùng default NACL do AWS tạo cùng VPC. Giữ nguyên rule
allow-all inbound/outbound của AWS và **không** khai báo `aws_default_network_acl`,
`aws_network_acl`, `aws_network_acl_rule` hay association custom trong Terraform.
Default NACL và default SG là hai đối tượng khác nhau: ACL cho phép mọi traffic,
còn default SG được quản lý với toàn bộ ingress/egress rỗng.

| Thành phần | Cấu hình hiện tại | Nơi thực thi hạn chế |
|---|---|---|
| NACL cho 6 subnet | Default allow-all, AWS quản lý | Không dùng làm lớp chặn |
| ALB | 80/443 từ Internet; 80/8080 tới web | `sg-alb` |
| Web | 80/8080 từ ALB; 5432 tới RDS; 80/443 ra Internet | `sg-web` |
| RDS | 5432 từ web; không cho mở kết nối outbound mới | `sg-rds` |
| ECR endpoint (tuỳ chọn) | 443 chỉ từ web SG | SG endpoint + rule tại module `security` |

Lý do bỏ ba custom NACL: rule stateless phải xử lý chiều phản hồi, thứ tự rule và
CIDR của từng tier. Các quan hệ truy cập cần giữ đã được mô tả trực tiếp bằng
SG stateful, nên chuyển sang một nơi kiểm soát chính giảm rủi ro vận hành khi đổi
port hoặc subnet. Route, subnet riêng, ALB, IAM và bảo mật ứng dụng vẫn cần thiết.

**Đánh đổi:** không còn deny chủ động theo IP/CIDR ở subnet, không còn lớp NACL
hạn chế bổ sung khi cấu hình SG sai. SG chỉ hỗ trợ allow; nếu gắn nhầm SG hoặc
mở rộng rule, default NACL không chặn thay. WAF ở lớp HTTP được bổ sung riêng
bằng [Cloudflare Free](waf-cloudflare.md), không thay thế SG.
Hai biến Terraform `enable_deny_demo` và `my_ip` đã bị xoá. Khi đo Flow Logs,
IP của máy probe được ghi trong bằng chứng, không phải input của module mạng.

**Lịch sử:** `nacl-public`, `nacl-app`, `nacl-db`, rule 50/90/95/115/120 và KB-08
là thiết kế/kiểm thử cũ. [Báo cáo đo](security-validation-report.md), nhật ký và
`evidence/` được giữ nguyên làm hồ sơ, không chứng minh cấu hình mới. Thứ tự
chuyển association, kiểm tra default ACL, review plan, rollback và kiểm tra sau
triển khai nằm trong [nacl-to-sg-migration.md](nacl-to-sg-migration.md).

## Application Load Balancer

ALB là cửa vào duy nhất. Nó khác một load balancer thường ở chỗ **hiểu HTTP**,
nên định tuyến được theo tên miền và đường dẫn, không chỉ theo IP/port.

**Hai listener:**
- `:80` → **redirect 301** sang `:443`. Không phục vụ gì trên HTTP.
- `:443` → có chứng chỉ ACM, bóc TLS, rồi áp rule định tuyến.

**Hai target group:**

| Target group | Cổng | Health check | Nhận traffic khi |
|---|---|---|---|
| `tg-web` | 80 | `/healthz` | Host = `hushstore.io.vn` |
| `tg-api` | 8080 | `/health/ready` | Host = `api.hushstore.io.vn` |

**Allowlist Host header.** Default action của listener là **trả 403**, không phải
chuyển tiếp. Chỉ hai tên miền trong danh sách được đi tiếp. Nghĩa là gọi ALB
bằng tên DNS thô của nó sẽ nhận **403 — và đó là đúng thiết kế**, không phải
lỗi. (Nếu nhận 503 thì mới là lỗi: chứng tỏ allowlist chưa có hiệu lực.) Rule
này chặn được tấn công Host header injection và chặn việc kẻ khác trỏ tên miền
của họ vào hạ tầng của ta.

**Health check là gì và vì sao quan trọng.** ALB gọi một URL trên mỗi target
theo chu kỳ (15 giây). Trả 200 thì target là *healthy* và được nhận traffic;
sai 3 lần liên tiếp thì bị coi là *unhealthy* và bị rút khỏi vòng phục vụ.

Chi tiết đáng chú ý: `/health/ready` của API **có mở kết nối tới database**. Đó
là chủ ý — một API không nối được DB thì không thực sự sẵn sàng, dù tiến trình
vẫn sống. Nhưng nó tạo ra một ràng buộc thứ tự khi khởi động, sẽ nói ở tài liệu
nhật ký.

**TLS hai lớp.** Cloudflare đặt chế độ Full (strict): người dùng ↔ Cloudflare mã
hoá bằng chứng chỉ của Cloudflare; Cloudflare ↔ ALB mã hoá bằng chứng chỉ ACM.
Không có đoạn nào chạy plaintext trên Internet.

## EC2 và ECS — chạy container trên máy chủ thật

Đề bài yêu cầu **triển khai website thông qua EC2 Instance**. Ta dùng
**ECS EC2 launch type**: container thật, nhưng chạy trên EC2 thật do ta quản lý
— khác Fargate (AWS quản máy hộ, ta không thấy EC2 nào).

**Vì sao container thay vì cài trực tiếp lên máy?** Vì image là **bất biến**:
cùng một image chạy ở đâu cũng như nhau. Không còn cảnh "máy tôi chạy được".
Deploy là đổi sang image mới; rollback là trỏ về image cũ. Và máy chủ trở thành
thứ dùng-rồi-bỏ: xoá máy dựng lại vẫn đúng nguyên trạng, vì không có state nào
nằm trên máy.

**Các thành phần:**

- **Launch Template** — khuôn mẫu: dùng AMI Amazon Linux đã có sẵn ECS agent,
  loại `t3.micro`, gắn `sg-web`, không gán IP public, và một đoạn `user_data`
  chạy lúc boot để khai báo máy này thuộc cluster nào + tạo 2GB swap.
- **Auto Scaling Group** — `min 0 / max 1`. Đặt về 0 là máy bị xoá **cùng ổ đĩa**
  → chi phí về 0 thật. Đặt về 1 là máy mới hoàn toàn được dựng lại.
- **ECS Cluster + Capacity Provider** — cluster là nhóm máy; capacity provider
  là cầu nối giữa cluster và ASG.
- **Task Definition** — bản mô tả container: image nào, cần bao nhiêu RAM, mở
  port nào, biến môi trường và bí mật lấy từ đâu, log gửi đi đâu. Có 4 cái:
  `web`, `api`, `migrator`, `seeder`.
- **ECS Service** — giữ container luôn chạy. Container chết thì service tự dựng
  lại. Có 2 cái: `web` và `api`. `migrator` và `seeder` **không có service** vì
  chúng là việc chạy-một-lần-rồi-thoát.

**Chọn `bridge` network mode với port cố định 80 và 8080** thay vì để ECS tự
chọn port ngẫu nhiên. Lý do là bảo mật: nếu để port động, SG phải mở dải
`32768–65535` từ `sg-alb` — hàng chục nghìn port. Với port cố định, SG chỉ mở
đúng hai port. Đánh đổi: mỗi máy chỉ chạy được một bản của mỗi container, nên
lúc deploy có downtime ~20–40 giây. Ở quy mô đồ án, đổi 30 giây downtime lấy một
bề mặt tấn công nhỏ hơn hàng nghìn lần là hợp lý — và đây là một *lựa chọn có ý
thức*, đáng nói khi báo cáo.

## Linux — hệ điều hành chạy bên dưới tất cả

Đề bài mở đầu bằng yêu cầu **tìm hiểu hệ điều hành Linux và xây dựng website
trên hệ điều hành này**. Mục này trả lời đúng câu đó: Linux nằm ở đâu trong hệ
thống, ta đã dùng những tính năng nào của nó, và chỗ nào nó đã thật sự làm hệ
thống gãy.

### Không phải một Linux, mà ba

Một điểm hay bị bỏ qua: hệ thống này chạy **ba bản phân phối Linux khác nhau
cùng lúc**, mỗi bản chọn có lý do.

| Ở đâu | Bản phân phối | Vì sao chọn nó |
|---|---|---|
| **Máy EC2** (host) | Amazon Linux 2023, bản ECS-optimized | AWS đã cài sẵn `docker` và `ecs-agent`. Không phải cài gì → `user_data` gần như không phải làm gì |
| **Container `web`** | Alpine (`nginx:alpine`) | Nhỏ nhất — image ~50MB. Chỉ cần serve file tĩnh nên không cần gì hơn |
| **Container `api`** | Debian (`aspnet:10.0`) | Microsoft build image .NET trên Debian. Đây là lý do trong `Dockerfile` ta gõ `useradd` và `update-ca-certificates` — đó là lệnh của Debian, Alpine dùng `adduser` và cơ chế khác |
| **Task `migrator` / `seeder`** | Debian (`runtime-deps:10.0`, `debian:12-slim`) | Chạy-một-lần-rồi-thoát. `seeder` cần `postgresql-client-17` từ apt repo của PostgreSQL |

Alpine dùng thư viện C tên **musl**, còn Debian và Amazon Linux dùng **glibc**.
(**Thư viện C** là lớp trung gian mà gần như mọi chương trình gọi để nhờ kernel làm
việc — mở file, mở socket, cấp bộ nhớ. Một file thực thi build sẵn không tự chứa lớp
đó, nó chỉ *gọi tên hàm* và trông chờ tìm thấy lúc chạy. Hai thư viện khác nhau nghĩa
là tên hàm và cách gọi lệch nhau, nên file build cho bên này thiếu thứ bên kia cần.)
Đây là khác biệt sâu nhất giữa chúng, và là lý do một file thực thi build trên
Debian không chắc chạy được trên Alpine. Ta không gặp vấn đề này vì mỗi container
tự mang runtime của nó.

Khác biệt đó dẫn tới một chuyện tinh vi hơn: **kiến trúc CPU**. Máy phát triển
hiện tại là Intel (`uname -m` → `x86_64`) và `t3.micro` cũng Intel, nên mọi thứ
vô tình khớp. Nhưng một máy Mac chip Apple Silicon hay một CI runner Graviton đều
là `arm64` — và lúc đó **hai trong bốn** image sẽ gãy, mỗi cái vì một lý do khác:

| Image | Phụ thuộc kiến trúc? | Vì sao |
|---|---|---|
| `web` | không | nginx + file tĩnh; base image tự chọn đúng kiến trúc |
| `api` | không | .NET *framework-dependent* — `.dll` là bytecode IL, runtime kiến trúc nào cũng chạy |
| `migrator` | **có** | build *self-contained* `-r linux-x64` → ra file thực thi máy, chỉ chạy trên x64 |
| `seeder` | không | `postgresql-client-17` có cả `amd64` lẫn `arm64` trong apt repo của PostgreSQL. ⚠️ Trước đợt 7 thì **có** phụ thuộc: `mssql-tools18` của Microsoft ghi cứng `arch=amd64`. Đổi engine đã gỡ luôn ràng buộc kiến trúc này |

Chỗ gãy của `migrator` là kiểu lỗi khó tìm nhất: stage 1 build binary x64, stage 2
lấy base image theo kiến trúc **máy build**. Trên máy arm64 sẽ ra một image arm64
chứa binary chỉ chạy được x64. Build **xanh**, push ECR **thành công**, và lỗi chỉ
hiện ra lúc ECS *khởi động* container.

Nên kiến trúc được ghim ngay trong `Dockerfile`, ở **mọi** stage — không phải chỉ
trong lệnh build, để không phụ thuộc vào người gõ lệnh có nhớ hay không:

```dockerfile
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0        AS build
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
```

Đây là phòng ngừa, không phải sửa lỗi đang có: hiện tại nó đúng nhờ **may mắn**
(máy build tình cờ là x86_64), ghim lại để nó đúng vì **thiết kế**.

### Máy Linux khởi động như thế nào — và chỗ ta đã làm nó treo

Đây là phần đáng học nhất, vì nó là **sự cố thật đã gặp hai lần**.

Trình tự boot của Amazon Linux 2023:

```
kernel  →  systemd  →  cloud-init  →  cloud-final.service  →  user_data
                                              ↓ (xong)
                                        ecs.service khởi động
```

- **`systemd`** là tiến trình số 1, quản mọi dịch vụ. Mỗi dịch vụ là một *unit*
  (`docker.service`, `ecs.service`), có thứ tự phụ thuộc khai báo bằng `After=`.
- **`cloud-init`** là cơ chế chuẩn để một máy ảo tự cấu hình lúc boot đầu tiên.
  Nó là chỗ AWS chạy đoạn `user_data` ta viết trong Launch Template.

Bẫy nằm ở chỗ: `ecs.service` khai báo

```
After=docker.service cloud-final.service
```

mà `user_data` **chạy bên trong** `cloud-final`. Nên nếu trong `user_data` ta gọi:

```bash
systemctl enable --now ecs      # ❌ TUYỆT ĐỐI KHÔNG
```

thì thành **deadlock ba tầng**: `systemctl` chờ unit `ecs` active → unit `ecs`
chờ `cloud-final` xong → `cloud-final` chờ `user_data` trả về → `user_data` đang
chờ `systemctl`. Không bên nào nhường.

Triệu chứng đo được lúc đó:

```bash
cloud-init status              # running — đứng mãi ở modules-final
systemctl is-active ecs        # inactive (dead)
ps -ef | grep systemctl        # `systemctl enable --now ecs` treo,
                               # là tiến trình con của cloud-init modules --mode=final
```

Máy **chạy bình thường**, SSM vào được, nhưng không bao giờ đăng ký vào ECS
cluster — nên `terraform apply` xanh mà không có container nào lên. Gỡ bằng cách
`kill` tiến trình `systemctl` đang treo: agent lên ngay lập tức.

Bản sửa là **xoá dòng đó đi**, không thay bằng gì cả — trên AMI ECS-optimized
unit `ecs` đã được `enable` sẵn từ trước, nó tự lên sau khi cloud-init xong. Toàn
bộ việc `user_data` cần làm chỉ là ghi một dòng cấu hình:

```bash
echo "ECS_CLUSTER=hushstore" >> /etc/ecs/ecs.config
```

Bài học Linux ở đây: **đừng gọi `systemctl start` từ trong cloud-init.** Và bài
học kiểm thử: có một test tự động (`user_data_dung_thu_tu_va_khong_tu_khoi_dong_ecs_agent`)
đọc `user_data`, bỏ dòng comment ra, rồi bắt lỗi nếu thấy `systemctl`,
`service ecs`, hoặc `start ecs`. Sự cố đã trở thành một rào chắn vĩnh viễn.

### Bộ nhớ ảo — vì sao phải tự tạo swap

`t3.micro` chỉ có **1 GB RAM**. Cần chạy đồng thời:

| Tiến trình | RAM xấp xỉ |
|---|---|
| `ecs-agent` | ~100 MB |
| `nginx` (container web) | ~15 MB |
| .NET API (container api) | ~250 MB |
| Hệ điều hành + `docker` daemon | ~200 MB |

Cộng lại đã chật, và lúc deploy còn có task `migrator` chạy chồng lên. Khi Linux
hết RAM, **OOM killer** của kernel sẽ chọn một tiến trình và `SIGKILL` nó — trong
container biểu hiện là **exit code 137**, task chết không rõ lý do.

Nên `user_data` tự tạo 2 GB **swap**: một file trên đĩa được kernel dùng như RAM
mở rộng, chậm hơn nhiều nhưng còn hơn bị giết.

```bash
dd if=/dev/zero of=/swapfile bs=1M count=2048   # cấp phát file 2GB toàn số 0
chmod 600 /swapfile                             # CHỈ root đọc được
mkswap /swapfile                                # định dạng thành vùng swap
swapon /swapfile                                # bật ngay
echo '/swapfile none swap sw 0 0' >> /etc/fstab # bật lại sau reboot
sysctl -w vm.swappiness=60                      # mức độ chịu đẩy ra swap
```

Ba chi tiết Linux đáng giải thích:

- **`chmod 600`** là bắt buộc, không phải cho gọn. Swap chứa nguyên xi nội dung
  RAM — gồm mật khẩu database và JWT secret. `600` nghĩa là chỉ chủ sở hữu
  (`root`) được đọc/ghi, mọi người khác **không có quyền nào**. Để mặc định
  `644` là bất kỳ user nào trên máy cũng đọc được bí mật.
- **`/etc/fstab`** là danh sách những gì cần mount lúc boot. `swapon` chỉ có tác
  dụng cho phiên hiện tại; không ghi vào `fstab` thì reboot là mất swap.
- **`vm.swappiness`** (0–100) là mức kernel *sẵn sàng* đẩy trang nhớ ra swap.
  `60` là mặc định của Linux; giữ nguyên vì hạ xuống sẽ làm OOM killer ra tay
  sớm hơn — đúng thứ ta đang tránh.

Và vì swap phải sẵn sàng **trước khi** ECS agent lên, thứ tự trong `user_data` là
swap trước, ghi `ecs.config` sau. Cũng có một test tự động khoá thứ tự này lại.

### Container, nhìn từ phía Linux

Container **không phải máy ảo**. Khác biệt gốc rễ nằm ở **kernel**: máy ảo có kernel
riêng của nó (phần cứng được ảo hoá, nên phải boot cả một hệ điều hành); container thì
**dùng chung kernel của máy host** — không boot gì cả. Đó là lý do container nhẹ và
khởi động trong mili-giây, còn máy ảo mất hàng chục giây.

Nó là một tiến trình Linux bình thường, bị kernel
giới hạn tầm nhìn bằng hai tính năng có sẵn:

- **namespace** — quyết định tiến trình *thấy* được gì. Nó có `/` riêng, danh
  sách tiến trình riêng (`ps` trong container chỉ thấy chính nó), network riêng.
  Nhờ network namespace, `web` nghe port 80 và `api` nghe port 8080 **trong không
  gian mạng riêng của mỗi container** — hai bản của cùng một container cũng không
  đụng nhau ở đó. Nhưng ta dùng `bridge` mode với **host port tĩnh**, nên port
  còn bị map ra máy thật: và ở đó thì chỉ một tiến trình được giữ port 80. Đó
  chính là chốt chặn scale ngang, ghi ở Phần V.
- **cgroup** (control group) — quyết định tiến trình *dùng* được bao nhiêu. RAM,
  CPU, I/O. Khi task definition ghi `memory = 512`, ECS đang đặt một giới hạn
  cgroup; vượt là kernel `SIGKILL`.

Vì cgroup là của **kernel trên máy ta quản**, ta mới đặt được:

```hcl
linuxParameters = {
  initProcessEnabled = true
  maxSwap            = 1024
  swappiness         = 60
}
```

`maxSwap` và `swappiness` **chỉ tồn tại với EC2 launch type**. Fargate không có
— vì ở Fargate kernel không thuộc về ta. Đây là một lợi ích cụ thể, đo được của
việc đề bài yêu cầu EC2 thật.

`initProcessEnabled = true` chèn một tiến trình `init` nhỏ làm PID 1 trong
container. Cần nó vì PID 1 trên Linux có nghĩa vụ đặc biệt: **thu dọn tiến trình
con đã chết** (zombie). `dotnet` không làm việc đó, nên không có init thì mỗi
phiên ECS Exec để lại một zombie.

### Người dùng và quyền — container không chạy bằng root

Mặc định container chạy bằng `root`. Nếu ai đó thoát được ra khỏi container
(container escape), họ là `root` trên máy. Nên `Dockerfile` của API hạ quyền:

```dockerfile
RUN useradd -m appuser && chown -R appuser /app
USER appuser
```

Thứ tự trong file rất quan trọng và có comment ghi rõ: phần cài **CA certificate
của RDS** phải nằm **trước** `USER appuser`, vì `update-ca-certificates` ghi vào
`/etc/ssl/certs` — chỉ `root` mới ghi được ở đó.

Đó cũng là một điểm Linux đáng nói: **trust store**. Connection string dùng
`SSL Mode=VerifyFull`, nghĩa là API *thật sự kiểm tra* chứng
chỉ của RDS thay vì tin bừa. Muốn kiểm được thì trong container phải có CA của
Amazon RDS, nên `Dockerfile` tải nó về `/usr/local/share/ca-certificates/` rồi
gọi `update-ca-certificates` để nạp vào trust store hệ thống.

Và vì trust store là thứ quyết định container tin ai, lệnh tải phải ghim nội dung:

```dockerfile
ADD --checksum=sha256:3c69...e979 https://truststore.pki.rds.amazonaws.com/...
```

Không có `--checksum`, ai kiểm soát được đường tải là ghi thêm được CA vào trust
store — và từ đó giả mạo được database.

### Không có SSH — và vì sao vẫn vào được máy

Đề bài chấm nguyên tắc tối thiểu, nên **port 22 đóng hoàn toàn**: không SG rule
nào, không key pair, không file `.pem`; default NACL không dùng để chặn SSH.

Nhưng vẫn cần vào máy để chẩn đoán. Đường vào là **SSM Session Manager**: trên
Amazon Linux 2023 có sẵn daemon `amazon-ssm-agent`, nó **tự gọi ra** AWS
(outbound qua NAT) và giữ một kênh mở. Ta bấm "connect" từ phía AWS, không có ai
gọi *vào* máy cả.

Khác biệt về bảo mật là bản chất, không phải hình thức:

| | SSH | SSM Session Manager |
|---|---|---|
| Chiều kết nối | từ ngoài **vào** | từ máy **ra** |
| Cần port mở | 22 | **không** |
| Cần credential trên máy | khoá riêng `.pem` | **không** — dùng IAM |
| Thu hồi quyền | phải xoá `authorized_keys` trên từng máy | sửa IAM policy, có hiệu lực ngay |
| Nhật ký | phải tự cấu hình `sshd`, và log nằm **trên chính máy** bị chiếm | `ssm:StartSession` là management event nên hiện trong **CloudTrail Event History** (bật sẵn, miễn phí, giữ 90 ngày) *— CloudTrail ghi lại mọi lời gọi API tới AWS; mức mặc định này xem được trên console, còn muốn giữ lâu hơn hay đẩy ra S3 để truy vấn thì phải tự tạo thêm một **trail**, dự án chưa tạo* — ở ngoài máy, không sửa được từ trong |

> **Nói cho đúng:** dự án **chưa tạo CloudTrail trail** nào, nên chỉ có Event
> History mặc định: 90 ngày, chỉ management event, không lưu ra S3 và không
> query được bằng Athena. Đủ để trả lời "ai đã vào máy", không đủ làm bằng
> chứng lâu dài. Đây là một trong các giới hạn ở Phần V.

### Vài chi tiết Linux khác đã gặp thật

- **`set -euxo pipefail`** ở đầu `user_data`. Bốn cờ bash: `e` dừng ngay khi có
  lệnh lỗi, `u` báo lỗi nếu dùng biến chưa gán, `x` in mọi lệnh ra log (đọc được
  trong `/var/log/cloud-init-output.log` — đây là chỗ đầu tiên phải xem khi máy
  boot sai), `o pipefail` để lỗi giữa một pipeline không bị che bởi lệnh cuối.
  Không có mấy cờ này, một lệnh gãy giữa `user_data` sẽ đi qua im lặng.
- **Đồng hồ hệ thống.** `sg-web` egress chỉ mở `80`, `443`, `5432` — không có
  `123` (NTP), nên về lý máy không đồng bộ được giờ. **Đã đo được 8 bản ghi
  `REJECT`** đúng như vậy: `chrony` thử các NTP công khai của AWS và bị chặn.
  Nhưng thực tế vẫn đúng giờ vì nó dùng được đường chính —
  **Amazon Time Sync** ở `169.254.169.123`, một địa chỉ
  *link-local* — dải `169.254.0.0/16`, loại IP thứ ba bên cạnh private và public: nó
  chỉ có nghĩa **trên đúng một đường mạng**, không định tuyến đi đâu được, nên kernel
  gửi thẳng ra card mạng mà không tra route table, không qua NAT, không cần rule
  nào — và cũng vì thế **không xuất hiện** trong Flow Logs. Các bản ghi `REJECT`
  port 123 vì thế **không phải sự cố**.

  Chứng minh gián tiếp rằng đồng hồ đúng, không cần vào máy: (1) task migrator
  nối RDS với `SSL Mode=VerifyFull`, tức **có xác thực** certificate —
  lệch giờ thì cert bị coi là chưa hiệu lực hoặc đã hết hạn, và nó exit 0;
  (2) ECS agent pull được image từ ECR, mà request tới AWS ký **SigV4** (cách AWS bắt
  mọi lời gọi API phải ký bằng khoá bí mật **kèm dấu thời gian**, để chống phát lại
  request cũ) và bị từ
  chối nếu lệch quá ~15 phút. Hai điều đó bất khả nếu đồng hồ sai.
- **`gzip_static on`** trong nginx thay vì nén lúc chạy: `dotnet publish` đã sinh
  sẵn `.gz` cạnh mỗi file, nginx chỉ việc gửi file có sẵn. Đổi CPU lấy đĩa — đúng
  hướng trên máy 2 vCPU burstable. Bundle cũng có sẵn `.br` (Brotli) nhưng
  `nginx:alpine` **không** biên dịch kèm module brotli, nên các file đó chỉ nằm
  chiếm chỗ.

### Bộ lệnh chẩn đoán tối thiểu

Vào máy bằng `aws ssm start-session --target <instance-id>`, rồi:

| Cần biết | Lệnh |
|---|---|
| cloud-init xong chưa, có lỗi gì | `cloud-init status`, `cat /var/log/cloud-init-output.log` |
| ECS agent sống không | `systemctl status ecs`, `journalctl -u ecs -n 50` |
| Container nào đang chạy | `docker ps` |
| RAM và swap còn bao nhiêu | `free -h` |
| Có ai bị OOM killer giết không | `dmesg -T \| grep -i "killed process"` |
| Đĩa còn chỗ không | `df -h` |
| Máy đang nghe port nào | `ss -tlnp` |
| Cấu hình ECS đã ghi đúng chưa | `cat /etc/ecs/ecs.config` |

`ss -tlnp` là lệnh đáng chạy nhất khi bảo vệ đồ án: nó liệt kê **mọi port máy
đang nghe**, và trên máy này danh sách đó không có `22`.

## RDS — database do AWS quản

**PostgreSQL 17**, `db.t4g.micro`, 20GB `gp3`, **Multi-AZ**, nằm trong db subnet.

**Vì sao đổi khỏi SQL Server Express (đợt 7)** — lý do là **edition**, không phải
kiến trúc. Ba giới hạn của Express, cái nào cũng không vá được bằng cấu hình:

| | `sqlserver-ex` | `postgres` |
|---|---|---|
| Multi-AZ | **không hỗ trợ** (`MultiAZCapable = False`) | **có** (`True` cho cả `db.t3.micro` lẫn `db.t4g.micro`) |
| Read replica | chỉ có ở Enterprise Edition, và đòi ≥ 4 vCPU | có |
| Trần dung lượng | **10 GB — vượt là TỪ CHỐI GHI** | không có |
| **Họ instance** khả dụng *(mỗi họ là một dòng máy có đặc tính riêng)* | **1** (chỉ `t3`) | **20**, có cả **Graviton** *(CPU kiến trúc ARM do AWS tự thiết kế, rẻ hơn Intel cùng cỡ)* |
| CPU lúc rảnh | **~36%** (baseline `t3.micro` là 10%) ⇒ luôn phải trả CPU surplus | dưới baseline |

*(Hai dòng đầu **tự kiểm chứng lại được**, không phải khẳng định suông — gõ:
`aws rds describe-orderable-db-instance-options --engine postgres --db-instance-class
db.t4g.micro --query 'OrderableDBInstanceOptions[0].MultiAZCapable'`, đổi `postgres`
thành `sqlserver-ex` để so. Lệnh chỉ đọc, miễn phí.)*

*(**CPU surplus / baseline**: `t3`/`t4g` là máy **burstable** — AWS chỉ cho dùng miễn
phí một mức CPU nền (10% với cỡ `micro`), vượt lên thì "vay" credit và bị tính tiền
phần vay. Mục [Điều khiển chi phí](#điều-khiển-chi-phí) nói kỹ, kể cả con số thật.)*

Trần 10 GB là thứ nguy hiểm nhất trong bảng: nó không làm chậm hệ thống mà làm
**hỏng** hệ thống, và hỏng ở thời điểm không ai chọn.

**Multi-AZ nghĩa là gì, và không nghĩa là gì.** AWS giữ một bản **standby đồng bộ**
ở AZ thứ hai và tự chuyển sang đó khi primary hỏng. Nhưng:

> *"You can't configure the secondary DB instance to accept database read activity."*

Standby **không phục vụ đọc**. Multi-AZ là **tính sẵn sàng**, không phải chia tải
đọc — muốn chia tải đọc thì cần **read replica**, là một resource khác. Báo cáo
phải nói đúng hai chuyện đó, đừng gộp.

**Điều làm Multi-AZ khả thi ở đồ án này:**

> *"RDS for SQL Server doesn't support stopping a DB instance in a Multi-AZ deployment."*

PostgreSQL thì **stop được**. Nghĩa là bật Multi-AZ **không phá** cơ chế tắt tiền
(`down.sh` + cost guard) — trên SQL Server thì có. Đây là khác biệt quyết định,
không phải chi tiết phụ.

**Read replica khác standby của Multi-AZ ở đúng một chữ: ĐỒNG BỘ hay KHÔNG.** Standby
chép **đồng bộ** — mỗi thay đổi phải ghi xong ở cả hai nơi mới báo thành công, nên nó
luôn khớp tuyệt đối với bản chính, và đó là điều kiện để failover mà không mất dữ liệu.
Read replica chép **bất đồng bộ** — bản chính báo xong trước, replica đuổi theo sau vài
mili-giây tới vài giây. Nhờ vậy nó **không** làm chậm bản chính và **được phép** phục
vụ truy vấn đọc; cái giá là dữ liệu đọc từ nó có thể **cũ hơn một chút**.

**Read replica: hạ tầng đã có, mặc định TẮT.** `enable_read_replica = false`, và
đó là **default an toàn chứ không phải default tiết kiệm**:

> *"You can't stop a DB instance that has a read replica, or that is a read replica."*

Còn replica thì AWS **từ chối** stop primary ⇒ `down.sh` và cost guard mất tác
dụng, mà RDS lại tự khởi động lại sau 7 ngày stopped. `down.sh` đã tự huỷ replica
trước khi stop và hỏi thẳng AWS chứ không tin file cấu hình, nhưng nó chỉ chạy khi
có người gõ. ⚠️ Và phải biết trước: **chưa có dòng code nào trong `src/` đọc từ
replica** (1 `AddDbContext`, 1 `UseNpgsql`, 0 tham chiếu replica) — bật nó lên là
thêm một instance tính tiền mà primary không được giảm tải chút nào.

**`publicly_accessible = false`** — và cần hiểu chính xác nó làm gì. Nó **không**
ẩn tên DNS của database; tên đó vẫn tra được từ Internet. Nó làm việc khác: tên
đó **phân giải ra một IP private** (`10.20.21.81`). Kẻ tấn công tra được tên,
nhưng nhận về một địa chỉ không tồn tại trên Internet — nên không kết nối được.
Kiểm chứng thực tế: dò cổng 5432 tới endpoint đó từ laptop → **timeout**, không
phải "từ chối kết nối".

**Và sự khác nhau giữa hai câu trả lời đó chính là bằng chứng.** *"Connection
refused"* nghĩa là gói tin **đã tới nơi** và có thứ gì đó chủ động trả lời "cổng này
đóng" — tức đường đi tồn tại. *"Timeout"* nghĩa là gói tin **đi vào hư không**: không
ai trả lời, vì không có đường nào dẫn tới địa chỉ đó. Ta muốn vế thứ hai, và đo được
đúng vế thứ hai.

**`SSL Mode=VerifyFull` — thuộc tính bảo mật dễ mất nhất.** Npgsql mặc định
`Prefer`: **mã hoá nhưng KHÔNG xác thực chứng chỉ**, tức không chống được
man-in-the-middle (kẻ đứng giữa: chen vào đường truyền, giả làm máy chủ để đọc và sửa
dữ liệu mà hai đầu không biết). `Require` cũng chưa đủ — ở Npgsql nó vẫn không kiểm
chain. Chỉ
`VerifyFull` mới vừa xác thực cert vừa kiểm hostname. Tụt về mặc định là mất lớp
đó **trong im lặng**: kết nối vẫn thành công, log vẫn sạch. Vì cert của RDS do
Amazon RDS CA cấp mà CA đó không nằm trong trust store mặc định, image `api` và
`migrator` phải cài sẵn bundle CA và chuỗi kết nối trỏ thẳng `Root Certificate`
vào file đó — Npgsql/libpq **không** đọc trust store hệ thống.

**Vì sao dùng RDS thay vì tự cài PostgreSQL lên EC2?** Vì AWS lo hộ: backup tự
động, vá lỗi, chứng chỉ TLS, snapshot, khôi phục theo thời điểm. Tự cài thì
những việc đó thành việc của nhóm, và đó chính là loại việc dễ bị bỏ quên nhất.

## IAM — bốn danh tính, mỗi cái một phạm vi

Điểm **least-privilege** (chỉ cấp đúng quyền tối thiểu để làm được việc, không hơn
một quyền nào) mạnh nhất của thiết kế nằm ở đây. Thay vì một danh tính có
mọi quyền, có **bốn** danh tính tách rời:

| Role | Gắn vào | Được làm gì |
|---|---|---|
| `container-instance-role` | Bản thân máy EC2 | Tham gia ECS cluster, dùng SSM. **Không có quyền S3 nào, không đọc được bí mật nào** |
| `task-execution-role` | ECS agent, lúc *khởi động* container | Tải image từ ECR, ghi log, đọc **đúng 2** bí mật: connection string và khoá JWT |
| `task-app-role` | Container API, lúc *đang chạy* | **Chỉ** đọc/ghi S3 bucket ảnh + ECS Exec. Không RDS, không ECR, không bí mật |
| `task-execution-seeder-role` | ECS agent, khi chạy seeder | Đọc **đúng 1** bí mật: mật khẩu DB |

**Vì sao tách execution role và app role?** Vì hai thời điểm khác nhau. Execution
role dùng *trước khi* container chạy (tải image, lấy bí mật để tiêm vào). App
role dùng *trong khi* container chạy (code gọi API AWS). Tách ra nghĩa là code
trong container **không bao giờ có quyền đọc bí mật** — nó chỉ nhận được giá trị
đã được tiêm vào biến môi trường. Kẻ tấn công chiếm được container cũng không
lấy được thêm bí mật nào.

**Deny tường minh — chi tiết đáng nói nhất.** Role của máy EC2 cần managed policy
`AmazonSSMManagedInstanceCore` để dùng Session Manager. Nhưng policy đó *có kèm*
quyền đọc SSM Parameter. Nghĩa là ai vào được máy sẽ đọc được mật khẩu database.
Ta bịt lại bằng một statement **Deny tường minh** cho `ssm:GetParameter*` trên
`/hushstore/*`. Trong IAM, **Deny luôn thắng Allow**, bất kể Allow đến từ đâu.

Kiểm chứng bằng công cụ mô phỏng của AWS: role của máy EC2 khi thử đọc bí mật
trả về `explicitDeny` (bị cấm tường minh), còn các quyền không liên quan trả
`implicitDeny` (không được cấp). Đó là hai kết quả khác nhau, và sự khác biệt
chứng minh rằng cái Deny kia thật sự đang hoạt động.

**Hai tập bí mật giao nhau bằng rỗng.** `api/web/migrator` đọc được connection
string + khoá JWT nhưng **không** đọc được mật khẩu DB. `seeder` đọc được mật
khẩu DB nhưng **không** đọc được hai cái kia. Nếu dùng chung một role thì cả bốn
task đều thấy cả ba bí mật.

## Bí mật — không còn credential dài hạn nào

| Trước | Sau |
|---|---|
| File `.env` trên máy chủ | ECS tiêm bí mật từ SSM Parameter Store lúc khởi động container |
| Access key AWS ghi trong `appsettings.json` | Code lấy credential tạm thời từ task role, tự động hết hạn |
| SSH key dài hạn để deploy | SSM Session Manager, xác thực bằng IAM |
| Mật khẩu registry để tải image | ECR, xác thực bằng IAM role |

Đã kiểm chứng từ **trong container đang chạy**: không có access key nào trong
biến môi trường, không có file `.env` nào trên đĩa.

**Vì sao Parameter Store mà không phải Secrets Manager?** Parameter Store
SecureString **miễn phí**; Secrets Manager tốn $0.40/bí mật/tháng. Với 3 bí mật
thì đó là $14/năm cho một tính năng ta không cần (tự động luân chuyển mật khẩu).

## Quan sát hệ thống

- **CloudWatch Logs** — log của cả 4 container, giữ 3 ngày.
- **VPC Flow Logs** — ghi lại các kết nối **bị từ chối**. Đây là bằng chứng tầng
  mạng cho báo cáo bảo mật: nó cho thấy rule thật sự chặn, chứ không chỉ là "tôi
  gọi thử và không vào được". Mặc định tắt vì tốn phí ingest.

Một quan sát đáng kể từ Flow Logs: trong **một giờ** hệ thống mở ra Internet, các
máy quét tự động đã dò port 22 (SSH), 23 (Telnet) và 6379 (Redis) — tất cả đều
bị chặn. Đây là bằng chứng có giá trị nhất trong báo cáo, chính vì nhóm không tạo
ra nó.

## Điều khiển chi phí

NAT Gateway và ALB tính theo giờ. Nên hệ thống được thiết kế để **tắt được**:

| Biến | Bật/tắt cái gì | Giá (ap-southeast-1) |
|---|---|---|
| `enable_nat` | NAT Gateway (số lượng theo `nat_gateway_count`) | **$0.0590/giờ mỗi cái** |
| `enable_alb` | ALB + target group + 2 ECS service | $0.0252/giờ |
| `instance_count` | 0 … `max_instance_count` EC2 | $0.0132/giờ mỗi cái |
| `enable_flow_logs` | VPC Flow Logs | phí ingest |
| `enable_ecr_endpoints` | 2 interface endpoint ECR (4 ENI) | **$0.01/giờ mỗi ENI ≈ $0.04** — 🚨 tính tiền ngay khi apply, không đợi `up.sh` |

RDS bật/tắt bằng lệnh riêng, không qua Terraform.

**Hai giá trị KHÔNG phải công tắc, mà là kiến trúc — ghim trong mã đã commit:**
`nat_gateway_count = 2` và `enable_multi_az = true`, đặt ở default của
`envs/prod/variables.tf`.

*(Cần luật ưu tiên của Terraform thì đoạn dưới mới có nghĩa: giá trị khai trong
`terraform.tfvars` **đè lên** `default` viết trong `variables.tf`; không có `.tfvars`
hoặc không có dòng đó thì Terraform tự dùng `default`. Nên "khai ở tfvars" và "đặt
default" nghe giống nhau nhưng khác hẳn về **độ bền**: một cái theo máy, một cái theo
mã nguồn.)*

Cố ý **không** khai trong `terraform.tfvars`: file đó bị
`.gitignore` (`*.tfvars`), nên giá trị khai ở đó không sang máy khác và không sang
CI — một lần clone lại là hạ tầng âm thầm rơi về 1 NAT / không Multi-AZ, trong khi
cả hai đều là thuộc tính đã ghi vào báo cáo này.

🔖 **ĐỌC BẢNG GIÁ Ở MỤC NÀY THEO HAI LOẠI SỐ — ĐỪNG TRỘN:**
> - 📏 **Số ĐO THẬT** — lấy từ hoá đơn/Cost Explorer của chính tài khoản này, sau khi
>   hệ thống đã chạy. Đáng tin nhất, nhưng chỉ có cho cấu hình **cũ** (SQL Server).
> - 🏷️ **Số NIÊM YẾT** — tra từ bảng giá công bố của AWS (Price List API). Đúng về đơn
>   giá, nhưng **chưa gồm** những khoản chỉ lộ ra khi chạy thật, mà CPU surplus là ví
>   dụ đắt nhất.
>
> Cấu hình PostgreSQL hiện tại **chưa có số đo thật nào** — nó mới chạy từ 2026-09-06.
> Mọi con số cho nó bên dưới đều là 🏷️.

**Con số phản trực giác, và nó vừa thay đổi:** trước đợt 7, RDS là khoản đắt nhất
— 📏 **$0.098/giờ**, trong đó 📏 **$0.0674 là CPU credit surplus** *(cả hai là số ĐO
THẬT từ Cost Explorer sau 13,67 giờ chạy, không phải ước lượng)*. `db.t3.micro` là máy
*burstable*: chỉ được miễn phí 10% CPU, vượt lên thì tính tiền, mà SQL Server
Express **không tải** vẫn ngồi ~36% CPU. Riêng phần vượt hạn mức đã đắt xấp xỉ
NAT + ALB cộng lại. Tệ hơn: EC2 cho chọn giữa hai chế độ — `standard` (hết credit thì
chạy chậm lại, **không** tính thêm tiền) và `unlimited` (cho vay credit rồi tính tiền
phần vay). **RDS chỉ có `unlimited`**, không có tham số nào để đổi. Nên
đòn bẩy duy nhất là đổi engine.

Sau khi đổi sang PostgreSQL (idle dưới baseline), giá niêm yết đo bằng Price List
API cho `ap-southeast-1`:

| | Single-AZ | Multi-AZ | Loại số |
|---|---|---|---|
| `postgres` `db.t4g.micro` | $0.025/giờ | **$0.051/giờ** | 🏷️ niêm yết |
| `sqlserver-ex` `db.t3.micro` — phần instance | $0.031/giờ | không tồn tại | 🏷️ niêm yết |
| `sqlserver-ex` — **CPU surplus thực tế phải trả thêm** | **+$0.067/giờ** | — | 📏 **đo thật** |

Dòng cuối là chỗ hai loại số gặp nhau, và là lý do bảng này phải tách cột: nhìn riêng
giá niêm yết thì SQL Server ($0.031) chỉ đắt hơn PostgreSQL ($0.025) một chút — nhưng
khoản surplus **đo thật** mới là thứ nhân đôi hoá đơn, và nó **không xuất hiện ở bảng
giá niêm yết nào cả**.

Cộng storage (🏷️ ~$0.006/giờ) thì Multi-AZ PostgreSQL khoảng **$0.057/giờ** —
**vẫn rẻ hơn $0.098 của SQL Server single-AZ trước đây.** Tức đổi engine vừa tăng
khả dụng vừa giảm tiền, và đó là lý lẽ chính của đợt 7.

🔴 **Một khoản Multi-AZ thêm vào mà `down.sh` KHÔNG tắt được:** storage được cấp
phát ở cả hai AZ và AWS tính tiền cả hai **kể cả khi instance đã stopped**. Sàn chi
phí của dự án vì thế tăng khoảng **+$2.3/tháng chạy vĩnh viễn**. Nhỏ, nhưng nó là
loại chi phí không có công tắc nào chạm tới, nên phải nằm trong bảng chứ không
nằm trong đầu ai đó.

⚠️ `HS_RATE_RDS_UP = 0.098` trong `lib.sh` **cố ý chưa sửa**: nó là số **đo được**
trên SQL Server, nay thành **cận trên** cho PostgreSQL. Thay bằng một con số ước
lượng sẽ biến bảng chi phí từ "đã đo" thành "nghe hợp lý" mà không có gì đánh dấu
sự khác nhau đó. Chỉ sửa khi có số thật sau 24–48h chạy. Chi tiết đo được nằm
trong [terraform-runbook.md](terraform-runbook.md).

---

# Phần IV — Vì sao thiết kế này đáp ứng đề bài

Đề bài đòi ba mảng kiến thức (Linux, AWS, Terraform), 5 thành phần hạ tầng và
2 kết quả. Đối chiếu từng dòng:

| Đề bài yêu cầu | Ở đâu trong hệ thống |
|---|---|
| **Tìm hiểu HĐH Linux + xây website trên đó** | Mục ["Linux — hệ điều hành chạy bên dưới tất cả"](#linux--hệ-điều-hành-chạy-bên-dưới-tất-cả): 3 bản phân phối, cloud-init/systemd, swap, namespace/cgroup, quyền file, không SSH |
| **Tìm hiểu AWS Cloud + Terraform** | Phần II (khái niệm), và toàn bộ hạ tầng khai bằng Terraform: 8 module, bộ test tự động, không resource nào bấm tay |
| VPC | `10.20.0.0/16`, 6 subnet, 3 tier, 2 AZ |
| Security Group | 3 SG ứng dụng, default SG rỗng; SG endpoint tuỳ chọn, rule tham chiếu web SG, không có port 22 |
| **Network ACL** | Default allow-all của AWS; ba custom NACL đã bỏ trong mã. Phần demo deny cũ chỉ còn là lịch sử |
| **Application Load Balancer** | ALB + ACM + 2 target group + allowlist Host |
| EC2 Instance chạy website | 1 `t3.micro` chạy container nginx + .NET |
| Máy tấn công verify rule | Hồ sơ 12 kịch bản cũ trong `docs/evidence/acc-551897327153/`; cấu hình SG hiện tại cần đo lại theo migration guide |
| Rule mở theo nguyên tắc tối thiểu | Xem bảng dưới |
| Rule đã ngăn được tấn công | [security-validation-report.md](security-validation-report.md) là bằng chứng lịch sử, chưa xác nhận bản bỏ custom NACL |
| Giải thích **vì sao** rule chặn được | [bao-mat-he-thong.md](bao-mat-he-thong.md) — sáu nhóm phòng thủ, và mục câu hỏi phản biện |

**Nguyên tắc tối thiểu — chứng minh cụ thể, không phải khẩu hiệu:**

| Điều đã KHÔNG mở | Hệ quả |
|---|---|
| Không port 22 ở bất kỳ SG nào | Không có bề mặt SSH; máy quét tự động dò 22 đều bị chặn |
| App tier không có IP public | Không thể gọi trực tiếp từ Internet |
| Db tier không có route ra Internet | Không tồn tại đường đi tới database |
| `sg-rds` egress rỗng | DB không tự mở kết nối mới; phản hồi PostgreSQL vẫn được phép |
| `sg-web` ingress chỉ 2 port, chỉ từ `sg-alb` | Không ai ngoài ALB gọi được container |
| ALB default action = 403 | Tên miền lạ không lọt vào hệ thống |
| Role máy EC2 bị Deny tường minh đọc bí mật | Vào được máy cũng không đọc được mật khẩu DB |
| Role app chỉ có quyền S3 | Chiếm được container không chạm được RDS qua API |
| Hai tập bí mật giao nhau bằng rỗng | Một task bị chiếm không làm lộ bí mật của task khác |

---

# Phần V — Giới hạn đã biết

Trung thực về những gì hệ thống **không** làm được — phần này thường được hỏi khi
bảo vệ:

| Giới hạn | Vì sao chấp nhận |
|---|---|
| Default NACL allow-all | Không còn lớp lọc subnet hay deny IP. Mọi giới hạn kết nối phụ thuộc SG và đường mạng; thay đổi SG phải được review/test |
| NAT Gateway không gắn được Security Group | Khác NAT instance. Kiểm soát egress của EC2 nằm ở `sg-web`; default NACL không lọc NAT |
| Không lọc egress theo tên miền | Cần AWS Network Firewall (~$300/tháng), không khả thi |
| Chỉ 1 EC2, deploy có downtime 20–40s | Đổi lấy bề mặt SG nhỏ hơn hàng nghìn lần. Lựa chọn có ý thức |
| Rate limiter đếm trong RAM | 2 instance sẽ thành 2× hạn mức, làm kịch bản KB6 của báo cáo bảo mật **sai sự thật**. Đây là lý do `max_instance_count` vẫn ghim 1: Terraform chặn nâng trần nếu chưa khai `rate_limiter_is_distributed = true`, và cờ đó chỉ được khai sau khi sửa tầng app |
| **NAT thứ hai chỉ cứu egress, không cứu đường phục vụ** | ALB → EC2 → RDS đi hoàn toàn trong VPC. Mất AZ chứa NAT thì instance còn lại **vẫn phục vụ**, chỉ mất deploy/ECR/SSM |
| **Read replica dựng được nhưng chưa ai dùng** | `src/` có 1 `AddDbContext`, 1 `UseNpgsql`, 0 tham chiếu replica. Bật lên là thêm một instance tính tiền mà primary không giảm tải. Cần một `DbContext` chỉ-đọc cho các truy vấn `AsNoTracking()` — ứng viên sạch nhất là `AnalyticsService` (10 chỗ, 0 `SaveChanges`) và `StorefrontService` (12 chỗ, 0 `SaveChanges`) |
| **Đọc từ replica có hai chỗ TUYỆT ĐỐI không được dùng** | Replication bất đồng bộ ⇒ (1) mọi đường "ghi rồi đọc lại để map DTO" phải ở primary, nếu không người dùng lưu xong xem lại thấy bản cũ; (2) `IsActive`/role/quyền không bao giờ đọc từ replica — hướng nguy hiểm là hướng **mở khoá** |
| **Không scale ngang được** dù đã có ASG + ALB | Bộ máy có đủ, nhưng bị ghim: `max_size = 1`, `managed_scaling = DISABLED`, và chốt cứng nhất là **host port tĩnh** 80/8080 — hai task không cùng bind một port trên một máy. Mở ra thì phải chọn `awsvpc` (nhiều ENI hơn `t3.micro` chịu nổi) hoặc dải ephemeral `32768–65535` trên `sg-web` — tức **đánh đổi trực tiếp với chiều đề bài đang chấm**. Đã chọn tối thiểu rule, chấp nhận một máy |
| **Default security group của VPC** mở mọi port từ chính nó | AWS tạo sẵn một cái cho mỗi VPC và không cho xoá. Đo được **0 ENI** dùng nó nên chưa có bề mặt thật, nhưng ai launch instance mà không chỉ định SG sẽ rơi vào nó. ✅ **Đã bịt** bằng `aws_default_security_group` rỗng (apply 2026-09-06) |
| Không có CloudTrail trail | Chỉ có Event History mặc định: 90 ngày, chỉ management event, không lưu ra S3. Tạo trail tốn ~$0.03/tháng cho S3 — đã cân nhắc, hoãn vì mọi thao tác hạ tầng đều đi qua Terraform và Git đã là nhật ký |
| Không có alarm nào | Lambda cost guard lỗi thì im lặng. Một CloudWatch alarm trên metric `Errors` là ~$0.10/tháng — đã cân nhắc, hoãn |
| WAF Free có giới hạn | Cấu hình Cloudflare Free chưa apply; không có đầy đủ OWASP rules. ALB yêu cầu Host + secret khi lockdown; EF Core tham số hoá vẫn cần |
| Giữa hai phiên làm việc, domain không hoạt động | ALB chạy 24/7 tốn $18/tháng cho một đồ án |
| **Chưa đo chi phí thật sau khi đổi engine** | Giá đang dùng là niêm yết + một số đo cũ trên SQL Server giữ làm cận trên. Cần 24–48h chạy thật mới biết CPU surplus của PostgreSQL có thực sự về 0 không |

---

# Phần VI — Lộ trình học trên AWS Study Group

Trang **[cloudjourney.awsstudygroup.com/vi](https://cloudjourney.awsstudygroup.com/vi/)**
là bộ workshop tiếng Việt do cộng đồng AWS Việt Nam biên soạn, làm trực tiếp trên
console. Nó bổ trợ rất tốt cho tài liệu này: ở đây bạn đọc *vì sao*, ở đó bạn
**tự tay bấm** để thấy resource thật.

Cách dùng hiệu quả nhất: làm workshop trước để có cảm giác về resource, rồi quay
lại đọc phần tương ứng ở Phần III để hiểu vì sao ta cấu hình khác họ.

## Bản đồ: thành phần hệ thống → workshop tương ứng

| Thành phần của ta | Workshop | Học được gì dùng ngay | Workshop KHÔNG dạy phần nào ta cần |
|---|---|---|---|
| VPC, subnet, SG, **NACL** | [Bắt đầu với Amazon VPC](https://000003.awsstudygroup.com/vi/) — chương 2 tên đúng là **"Tường lửa trong VPC"** | Phân biệt SG và NACL trên console | Đối chiếu Phần III để hiểu vì sao dự án dùng default ACL và tập trung lọc ở SG |
| Mạng nâng cao, VPC Endpoint | [AWS Networking and Content Delivery](https://000092.awsstudygroup.com/vi/) — chương "VPC Endpoints cho AWS Services" | Hiểu S3 Gateway Endpoint của ta làm gì và vì sao nó miễn phí | Nặng về Transit Gateway / VPC Peering — ta không dùng |
| EC2 | [Giới thiệu về Amazon EC2](https://000004.awsstudygroup.com/vi/) | AMI, instance type, key pair, user data, gắn SG | Workshop **có dùng key pair và SSH**; ta cố ý bỏ cả hai |
| Launch Template, ASG, **Load Balancer** | [Triển khai ứng dụng với Auto Scaling Group](https://000006.awsstudygroup.com/vi/) — chương 3 và 4 | Đây là chỗ duy nhất trên site dạy **Launch Template + ALB + target group** cùng nhau | Không có allowlist Host header, không có listener rule theo tên miền |
| ECS, task definition, service, ALB | [Triển khai ứng dụng trên Amazon ECS](https://000016.awsstudygroup.com/vi/) | **Workshop sát kiến trúc của ta nhất.** Có đủ cluster → task definition → ALB + target group → service, và cả chiến lược deploy | Dùng Fargate + `awsvpc`; ta dùng **EC2 launch type + `bridge`**. Khác biệt này chính là lý do SG của ta chỉ mở 2 port |
| Docker, ECR | [Triển khai ứng dụng trên Docker với AWS](https://000015.awsstudygroup.com/vi/) — chương 8 "Image Registry" | Build image, đẩy lên ECR, xác thực bằng IAM | Không bật IMMUTABLE tag — phần làm rollback có nghĩa |
| RDS | [Bắt đầu với Amazon RDS](https://000005.awsstudygroup.com/vi/) — chương 2 có sẵn phần **Security Group + DB Subnet Group** | Đúng ba thứ ta cần: subnet group 2 AZ, SG chỉ mở 5432, backup/restore | Không nói về `publicly_accessible`, và **không nói về CPU credit của lớp `t3`** — đúng cái đã làm ta trả tiền |
| IAM cơ bản | [Quản trị quyền truy cập với AWS IAM](https://000002.awsstudygroup.com/vi/) | User, group, policy, role, và cơ chế chuyển role | Không có Deny tường minh — kỹ thuật ta dùng để bịt managed policy |
| **IAM Role cho ứng dụng** | [Cấp quyền cho ứng dụng với IAM Role](https://000048.awsstudygroup.com/vi/) | **Nên làm sớm.** Workshop so sánh trực tiếp access key với IAM role và giải thích vì sao role tốt hơn — đúng thay đổi thứ 3 ta làm trong code | Chỉ có role cho EC2, không có task role của ECS |
| Vào máy không cần SSH | [Systems Manager Session Manager](https://000058.awsstudygroup.com/vi/) | Chính đường quản trị của ta. Có cả session log và port forwarding | — |
| Quản lý máy chủ | [Patch Manager và Run Command](https://000031.awsstudygroup.com/vi/) | `Run Command` là cách chạy lệnh trên host **không cần plugin** — hữu ích khi chẩn đoán | — |
| CloudWatch | [Giám sát hệ thống với CloudWatch](https://000008.awsstudygroup.com/vi/) | Metrics, Logs Insights, Alarm, và **Container Insights** cho ECS | — |
| Chi phí | [Trực quan hóa chi phí](https://000034.awsstudygroup.com/vi/) — chương 7 "Phân tích chi phí bằng Cost Explorer" | Đọc hoá đơn theo dịch vụ, theo tag | **Không dạy bóc theo `usage type`** — mà đó chính là cách ta tìm ra khoản CPU credit. Xem mục "Chi phí" trong runbook |
| Quản lý bí mật | [Secrets Manager với RDS và Fargate](https://000096.awsstudygroup.com/vi/) | Bối cảnh chung về quản lý credential cho DB | Dạy **Secrets Manager**; ta dùng **Parameter Store SecureString** vì miễn phí. Đọc để biết vì sao ta chọn khác |
| Khái niệm IaC | [Giới thiệu về Infrastructure as Code](https://000102.awsstudygroup.com/vi/) | Khái niệm IaC và kiến trúc three-tier | **Dạy CloudFormation, KHÔNG phải Terraform.** Xem mục khoảng trống bên dưới |

## Lộ trình đề nghị — 3 tuần

**Tuần 1 — nền tảng.** Làm đúng thứ tự, vì mỗi cái là điều kiện của cái sau:

1. [IAM](https://000002.awsstudygroup.com/vi/) — không có quyền thì không làm được gì
2. [VPC](https://000003.awsstudygroup.com/vi/) — **ưu tiên chương "Tường lửa trong VPC"**
3. [EC2](https://000004.awsstudygroup.com/vi/)
4. [RDS](https://000005.awsstudygroup.com/vi/) — chú ý chương chuẩn bị: SG + DB subnet group

Sau tuần 1, đọc lại **Phần III mục "Mạng"**, **"Security Group"** và **"Network
ACL"** của tài liệu này. Lúc đó bảng rule sẽ đọc được, và câu chuyện rule
90/95/115 sẽ có nghĩa.

**Tuần 2 — container và load balancer.** Đây là phần đúng kiến trúc của ta:

5. [Docker trên AWS](https://000015.awsstudygroup.com/vi/) — nhất là chương ECR
6. [Auto Scaling Group](https://000006.awsstudygroup.com/vi/) — Launch Template + ALB
7. [Amazon ECS](https://000016.awsstudygroup.com/vi/) — **workshop quan trọng nhất**

Sau tuần 2, đọc lại **Phần III mục "Application Load Balancer"** và **"EC2 và
ECS"**. Sẽ hiểu vì sao ta chọn `bridge` + port cố định thay vì port động, và cái
giá phải trả.

**Tuần 3 — vận hành và bảo mật.**

8. [IAM Role cho ứng dụng](https://000048.awsstudygroup.com/vi/)
9. [Session Manager](https://000058.awsstudygroup.com/vi/)
10. [CloudWatch](https://000008.awsstudygroup.com/vi/)
11. [Trực quan hóa chi phí](https://000034.awsstudygroup.com/vi/)

Sau tuần 3, đọc **Phần III mục "IAM"** và **Phần IV**. Lúc này đủ nền để hiểu vì
sao bốn role tách rời là điểm least-privilege mạnh nhất của thiết kế.

Ai chỉ có thời gian cho **ba** workshop: chọn
[VPC](https://000003.awsstudygroup.com/vi/) →
[ECS](https://000016.awsstudygroup.com/vi/) →
[IAM Role cho ứng dụng](https://000048.awsstudygroup.com/vi/). Ba cái này phủ
phần lớn những gì bị hỏi khi bảo vệ.

## Ba khoảng trống — và học ở đâu thay thế

AWS Study Group phủ được phần lớn hệ thống, nhưng có **ba thứ nó không dạy** — mà
cả ba đều là thành phần đề bài nhấn mạnh. Mục này lấp đúng ba chỗ đó, **ưu tiên
nguồn tiếng Việt**, và nói rõ mỗi nguồn dạy tới đâu thì hết.

Mọi liên kết dưới đây đã được kiểm tra truy cập được ngày **2026-08-25**.

### Khoảng trống 1 — Terraform

Workshop IaC duy nhất trên site ([000102](https://000102.awsstudygroup.com/vi/))
dạy **CloudFormation**, không phải Terraform. Khái niệm dùng chung được — IaC,
state, plan trước khi apply, khai báo thay vì bấm tay — nhưng cú pháp khác hoàn
toàn, và `terraform plan`/`state`/`module` thì CloudFormation không có tương
đương một-một.

**Lộ trình thay thế, theo đúng thứ tự:**

| # | Nguồn | Ngôn ngữ | Học được gì | Hết ở đâu |
|---|---|---|---|---|
| 1 | [Terraform Series — Bài 1: Infrastructure as Code và Terraform](https://viblo.asia/p/terraform-series-bai-1-infrastructure-as-code-va-terraform-maGK7Bqa5j2) (Viblo) | 🇻🇳 | Vì sao cần IaC, Terraform khác Ansible/CloudFormation ở chỗ nào, vòng đời `init → plan → apply → destroy` | Chỉ khái niệm, chưa động tới AWS |
| 2 | [Học Terraform với AWS: 5 bước tạo EC2 đầu tiên](https://devops.vn/posts/hoc-terraform-voi-aws-5-buoc-tao-ec2-dau-tien/) (devops.vn) | 🇻🇳 | Bài thực hành ngắn nhất từ số 0 tới một EC2 chạy thật: provider, resource, `terraform apply` | Một file, không module, không state từ xa |
| 3 | [Terraform Series — Bài 5: Module — tạo VPC trên AWS](https://viblo.asia/p/terraform-series-bai-5-terraform-module-create-virtual-private-cloud-on-aws-ORNZqp2MK0n) (Viblo) | 🇻🇳 | **Bài quan trọng nhất cho dự án này.** Cách gom resource thành module, truyền `variable`, lấy `output` — đúng cấu trúc `infra/tf/modules/` của ta | Dùng module VPC có sẵn từ Registry; ta tự viết module |
| 4 | [Terraform Series — Bài 6: Module in depth — ứng dụng multi-tier](https://viblo.asia/p/terraform-series-bai-6-module-in-depth-create-multi-tier-application-1VgZvAb2KAw) (Viblo) | 🇻🇳 | Ghép **VPC + ALB + target group + listener + ASG + Launch Template + RDS** thành một hệ thống — gần kiến trúc của ta nhất trong mọi nguồn tiếng Việt tìm được | **Không có `aws_network_acl` nào.** Và dùng module từ Registry thay vì tự viết. Bài đăng 24/02/2022 nên cú pháp là Terraform 1.x đời đầu — vẫn đọc được |
| 5 | [HashiCorp — AWS Get Started](https://developer.hashicorp.com/terraform/tutorials/aws-get-started) | 🇬🇧 | Nguồn chính thức, luôn cập nhật. Nhất là hai chương **`Store remote state`** và **`Manage resource drift`** | Tiếng Anh |
| 6 | [terraform-runbook.md](terraform-runbook.md) của chính dự án này | 🇻🇳 | Cách chạy stack thật: backend S3 + lockfile, biến toggle bật/tắt, thứ tự `up.sh`/`down.sh`, và bộ test `.tftest.hcl` | — |

**Chỗ không nguồn tiếng Việt nào phủ**, và ta dùng thật:

- **`terraform test`** (`.tftest.hcl`) — framework test tích hợp, có từ Terraform 1.6.
  Bộ test khẳng định các bất biến bảo mật (không rule 22 nào,
  quan hệ nguồn/đích SG, không có custom NACL, NAT tắt theo mặc định). Tài liệu chính thức:
  [developer.hashicorp.com/terraform/language/tests](https://developer.hashicorp.com/terraform/language/tests).
  Một điểm đã vấp phải và đáng biết trước: giá trị *known-after-apply* **không
  assert được** ở `command = plan` — xem khối chú thích trong
  [`modules/network/tests/vpc.tftest.hcl`](../infra/tf/modules/network/tests/vpc.tftest.hcl).
- **`import` block** — đưa resource đã tồn tại vào state mà không tạo lại. Ta dùng
  đúng một lần, cho bucket ảnh sản phẩm.
- **Toggle chi phí bằng `count = var.enable_x ? 1 : 0`** — mẫu cho phép bật/tắt
  NAT Gateway và ALB mà state không lệch. Không nguồn nào dạy, vì các bài hướng
  dẫn không quan tâm tới việc tắt hạ tầng đi để khỏi tốn tiền.

### Khoảng trống 2 — Network ACL và ranh giới với SG

Cần hiểu NACL để đọc hồ sơ cũ và thực hiện migration an toàn, dù thiết kế hiện
tại không quản lý custom ACL. [AWS Docs — Network ACLs](https://docs.aws.amazon.com/vpc/latest/userguide/vpc-network-acls.html)
là nguồn học về default ACL, thứ tự rule và traffic trả lời của bộ lọc stateless.

Ba câu cần giải thích được:

1. *Bỏ custom NACL có bỏ mọi NACL không?* Không. Mỗi subnet vẫn gắn default ACL
   allow-all do AWS tạo; Terraform không nhận quản lý ACL đó.
2. *Vì sao không mở dải ephemeral trên SG?* SG theo dõi kết nối và cho phép phản
   hồi; chỉ các kết nối mới mới cần khớp ingress/egress.
3. *Đã mất gì?* Lớp deny theo subnet/CIDR và demo chặn IP KB-08. SG không cung cấp
   deny tương đương; đây là đánh đổi, không phải hai tính năng giống nhau.

Đọc [mục Network ACL ở Phần III](#network-acl--dùng-default-allow-all-của-aws) và
[migration guide](nacl-to-sg-migration.md) để gắn kiến thức với cấu hình hiện tại.

### Khoảng trống 3 — Application Load Balancer

ALB nằm lẫn trong workshop [ASG](https://000006.awsstudygroup.com/vi/) (chương 4)
và [ECS](https://000016.awsstudygroup.com/vi/) (chương 7), không có workshop riêng.

| Nguồn | Ngôn ngữ | Phủ được | Không phủ |
|---|---|---|---|
| [AWS Elastic Load Balancer cho người mới bắt đầu](https://viblo.asia/p/aws-elastic-load-balancer-cho-nguoi-moi-bat-dau-6J3ZgPyWlmB) (Viblo) | 🇻🇳 | Nhập môn: ELB là gì, health check, phân bổ traffic qua nhiều AZ | Không có listener rule |
| [Tìm hiểu Classic ELB và ALB](https://viblo.asia/p/tim-hieu-classic-elb-va-alb-GrLZDOB3Kk0) (Viblo) | 🇻🇳 | Vì sao ALB thay CLB: định tuyến **theo path và theo header**, mỗi rule trỏ một target group khác nhau — đúng cơ chế ta dùng để tách `tg-web` và `tg-api` | Chỉ mô tả trên console, không có Terraform |
| [AWS Docs — Listener rules](https://docs.aws.amazon.com/elasticloadbalancing/latest/application/listener-update-rules.html) | 🇬🇧 | Nguồn thẩm quyền cho `host-header`, `path-pattern`, thứ tự `priority`, và `fixed-response` | Tiếng Anh |
| [Terraform Series — Bài 6](https://viblo.asia/p/terraform-series-bai-6-module-in-depth-create-multi-tier-application-1VgZvAb2KAw) (Viblo) | 🇻🇳 | Nguồn tiếng Việt duy nhất tạo ALB + target group + listener **bằng Terraform** | Dùng module Registry; chỉ listener HTTP :80, không TLS, không rule theo Host |

**Bốn thứ hệ thống của ta làm mà không nguồn nào ở trên dạy:**

- **Allowlist theo header Host.** Listener của ta có `fixed-response` 403 mặc
  định, và chỉ request mang đúng tên miền mới được chuyển tiếp. Đây là thứ chặn
  Host header injection — kịch bản KB-07.
- **`drop_invalid_header_fields = true`.** Loại header dị dạng trước khi tới app.
  Cần biết giới hạn của nó: nó **không** chặn được giả mạo `X-Forwarded-For`; thứ
  chặn là `ForwardLimit = 1` phía ASP.NET.
- **Redirect 301 từ `:80` sang `:443`** và chứng chỉ ACM xác thực bằng DNS.
- **`deregistration_delay`** và mối quan hệ thứ tự với `stopTimeout` của ECS +
  `ShutdownTimeout` của .NET. Sai thứ tự thì mỗi lần deploy cắt ngang request
  đang bay. Xem mục B4 của
  [ra-soat-ung-dung-multi-task.md](ra-soat-ung-dung-multi-task.md).

### Ba chỗ workshop dạy KHÁC có chủ ý — đừng nhầm là ta làm sai

| Workshop dạy | Ta làm | Vì sao |
|---|---|---|
| Dùng key pair + SSH vào EC2 | Không có key pair, vào bằng SSM | SSH là bề mặt tấn công lớn nhất; Flow Logs bắt được máy quét dò port 22 trong đúng một giờ |
| ECS trên Fargate, network mode `awsvpc` | ECS trên **EC2 launch type**, `bridge` | Đề bài yêu cầu "triển khai website thông qua EC2 Instance" |
| Secrets Manager | Parameter Store SecureString | Miễn phí, và ta không cần tự động luân chuyển mật khẩu |
| Bật Multi-AZ cho RDS | **Cũng bật Multi-AZ** | Từ đợt 7 engine là PostgreSQL nên làm được — và quan trọng hơn, PostgreSQL vẫn `stop` được khi Multi-AZ, nên nó không phá cơ chế tắt tiền |
| Một NAT Gateway cho cả VPC | **Hai — mỗi AZ một cái** | Một route table chỉ chứa được một dòng `0.0.0.0/0`, nên NAT theo AZ đòi tách route table theo AZ. Đổi lại: hết phí cross-AZ, và mất một AZ không cắt egress của AZ kia |

---

# Từ điển thuật ngữ

| Thuật ngữ | Nghĩa ngắn |
|---|---|
| **AZ** | Trung tâm dữ liệu độc lập trong một region |
| **CIDR** | Cách viết một dải IP, `10.20.0.0/16` |
| **Egress / Ingress** | Chiều ra / chiều vào |
| **Ephemeral port** | Cổng tạm 1024–65535 mà bên gọi mở để nhận trả lời |
| **Health check** | ALB gọi thử một URL để biết target còn sống |
| **IaC** | Hạ tầng viết thành code, lưu trong Git |
| **Idempotent** | Chạy nhiều lần cho cùng một kết quả |
| **IGW** | Cửa Internet của VPC |
| **Least privilege** | Chỉ cấp đúng quyền tối thiểu cần thiết |
| **NAT** | Cho máy private đi ra Internet mà không mở đường vào |
| **Stateful** | Firewall ghi nhớ kết nối, tự cho chiều trả lời |
| **Stateless** | Không ghi nhớ, phải viết rule cả hai chiều |
| **State (Terraform)** | File ghi lại Terraform đã tạo những gì |
| **Target group** | Nhóm đích mà ALB gửi traffic tới |
| **TLS termination** | Chỗ mã hoá HTTPS được bóc ra |
