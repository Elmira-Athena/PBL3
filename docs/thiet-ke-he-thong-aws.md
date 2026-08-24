# Thiết kế hệ thống AWS — HushStore

> **Tài liệu này dành cho ai:** thành viên trong nhóm chưa từng dùng AWS và chưa
> vững mạng máy tính, nhưng cần hiểu **bản chất** hệ thống để bảo vệ đồ án.
> Không cần đọc code. Mỗi phần trả lời hai câu: *cái này là gì* và *vì sao ta
> chọn như vậy*.
>
> Đọc theo thứ tự. Phần I và II là kiến thức nền — nếu đã biết thì nhảy sang
> Phần III. Trong Phần III, mục **"Linux — hệ điều hành chạy bên dưới tất cả"**
> trả lời gạch đầu dòng đầu tiên của đề bài và đọc được độc lập.

---

# Phần I — Kiến thức mạng tối thiểu

Không nắm 8 khái niệm này thì phần thiết kế bên dưới sẽ chỉ là danh sách tên
riêng. Nắm rồi thì mọi quyết định trong hệ thống đều tự giải thích.

## 1. Địa chỉ IP và CIDR

Mỗi máy trong mạng có một địa chỉ IP, ví dụ `10.20.10.37`. Bốn số, mỗi số 0–255.

**CIDR** là cách viết gọn một *dải* địa chỉ: `10.20.0.0/16`.

Con số sau dấu `/` cho biết **bao nhiêu bit đầu bị cố định**. IP có 32 bit, nên:

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
firewall nào cả. Đây là cách app tier và db tier của ta được bảo vệ.

## 4. Cổng (port) và TCP

Một IP là địa chỉ toà nhà; **port** là số phòng. Máy chủ nghe trên một port, và
mỗi port thường ứng với một loại dịch vụ:

| Port | Dịch vụ | Ghi chú trong hệ thống này |
|---|---|---|
| 22 | SSH — điều khiển máy Linux từ xa | **cố ý đóng hoàn toàn** |
| 80 | HTTP — web không mã hoá | chỉ để redirect sang 443 |
| 443 | HTTPS — web có mã hoá | cửa chính duy nhất |
| 1433 | SQL Server | chỉ mở giữa app tier và db tier |
| 8080 | API .NET | chỉ mở giữa ALB và container |

**Ephemeral port (cổng tạm).** Khi máy A gọi máy B ở port 443, máy A tự mở một
port ngẫu nhiên trong dải **1024–65535** để nhận câu trả lời. Nghe lạ nhưng đây
là nguyên nhân của phần rắc rối nhất trong thiết kế NACL bên dưới — hãy nhớ nó.

## 5. Stateful vs stateless — phân biệt quan trọng nhất tài liệu này

Hình dung một người bảo vệ ở cổng.

**Stateful** (có ghi nhớ): bảo vệ ghi sổ "ông A vừa vào lúc 9h". Khi ông A đi ra,
bảo vệ nhìn sổ và cho ra luôn, không cần rule riêng cho chiều ra. **Security
Group hoạt động như vậy** — chỉ cần cho phép chiều vào, chiều trả lời tự động
được phép.

**Stateless** (không ghi nhớ): bảo vệ không có sổ. Mỗi gói tin, dù đi vào hay đi
ra, đều bị xét lại từ đầu như thể chưa từng thấy. **Network ACL hoạt động như
vậy** — phải viết rule cho **cả hai chiều**, kể cả chiều trả lời.

Hệ quả trực tiếp: NACL bắt buộc phải mở dải ephemeral 1024–65535, vì câu trả lời
luôn đi về một port trong dải đó. Và vì mở dải đó, một số port nguy hiểm (1433,
8080) vô tình nằm trong khoảng mở — nên phải chặn chúng bằng rule *số nhỏ hơn*.
Toàn bộ phần NACL ở Phần III chỉ là hệ quả của một câu này.

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

HTTPS = HTTP + mã hoá TLS. Để mã hoá, server cần một **chứng chỉ (certificate)**
do một tổ chức đáng tin cậy cấp, xác nhận "tôi đúng là chủ của tên miền này".

**TLS termination** là chỗ mã hoá được *bóc ra*. Trong hệ thống này, load
balancer bóc TLS rồi chuyển tiếp HTTP thường vào trong mạng riêng. Nhờ vậy các
container không phải quản chứng chỉ — một chỗ duy nhất lo việc đó.

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
| **Network ACL (NACL)** | Firewall **stateless**, gắn vào từng subnet | 3 cái, mỗi tier một cái |
| **Application Load Balancer (ALB)** | Bộ chia tải hiểu HTTP | Cửa vào duy nhất, bóc TLS, định tuyến theo tên miền |
| **Target Group** | Nhóm đích để ALB gửi traffic tới | 2 cái: web (:80) và api (:8080) |
| **EC2** | Máy chủ ảo | 1 máy `t3.micro` chạy container |
| **AMI** | Ảnh đĩa để tạo máy | Bản Amazon Linux đã cài sẵn ECS agent |
| **Launch Template** | Khuôn mẫu tạo EC2 | Định nghĩa máy sẽ được tạo thế nào |
| **Auto Scaling Group (ASG)** | Quản lý số lượng EC2 | Đặt 0 để tắt, 1 để bật |
| **ECS** | Bộ điều phối container | Quyết định container nào chạy ở đâu |
| **Task Definition** | Bản mô tả một container sẽ chạy thế nào | 4 cái: web, api, migrator, seeder |
| **ECS Service** | Giữ cho container luôn chạy đủ số lượng | 2 cái: web, api |
| **ECR** | Kho chứa container image | 4 repo |
| **RDS** | Database do AWS quản lý hộ | SQL Server Express |
| **IAM Role** | Danh tính có quyền, không có mật khẩu | 4 role, mỗi role một phạm vi |
| **SSM Parameter Store** | Kho lưu bí mật, mã hoá | Mật khẩu DB, connection string, khoá JWT |
| **SSM Session Manager** | Vào máy chủ **không cần SSH** | Đường quản trị duy nhất |
| **CloudWatch Logs** | Nơi tập trung log | Log của mọi container |
| **VPC Flow Logs** | Ghi lại mọi kết nối bị chặn/cho phép | Bằng chứng tầng mạng cho báo cáo |
| **ACM** | Cấp chứng chỉ TLS miễn phí | Chứng chỉ cho ALB, tự gia hạn |
| **S3** | Lưu file | Ảnh sản phẩm, log, state của Terraform |

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

---

# Phần III — Thiết kế hệ thống HushStore

## Đường đi của một request

Người dùng mở `https://hushstore.io.vn` và bấm xem sản phẩm. Gói tin đi qua
**bảy** lớp kiểm soát:

```
Người dùng
   │  ① DNS: hushstore.io.vn → Cloudflare → ALB
   ▼
Cloudflare (proxy, TLS lớp ngoài)
   │  ② NACL public (stateless) — cho vào 443?
   ▼
   │  ③ SG alb (stateful) — cho vào 443?
   ▼
Application Load Balancer  ── bóc TLS, đọc header Host
   │  ④ Listener rule — Host có trong danh sách cho phép?
   │       hushstore.io.vn     → target group web
   │       api.hushstore.io.vn → target group api
   │       tên khác            → trả 403, KHÔNG chuyển đi đâu
   ▼
   │  ⑤ NACL app (stateless) — cho vào 80/8080 từ public tier?
   ▼
   │  ⑥ SG web (stateful) — cho vào 80/8080 từ sg-alb?
   ▼
EC2 (không có IP public) ── container nginx :80 · container API :8080
   │  ⑦ NACL db + SG rds — cho vào 1433 từ app tier?
   ▼
RDS SQL Server (không có đường ra Internet)
```

Điểm cần hiểu: **các lớp không trùng lặp vô ích, chúng bù cho nhau.** SG không
chặn được một IP cụ thể (nó chỉ có danh sách cho phép); NACL làm được. NACL
không hiểu HTTP; ALB làm được. Đây là **defense in depth** — phòng thủ nhiều
lớp, mỗi lớp bắt được loại tấn công lớp khác bỏ sót.

## Mạng — VPC và 6 subnet

VPC `10.20.0.0/16`. Chọn `10.20` chứ không phải `10.0` để không trùng với hạ
tầng cũ trong lúc chuyển đổi song song.

| Subnet | CIDR | AZ | Chứa | Có IP public? | Có đường ra Internet? |
|---|---|---|---|---|---|
| `public-a` | `10.20.0.0/24` | 1a | ALB, NAT Gateway | Có | Trực tiếp qua IGW |
| `public-b` | `10.20.1.0/24` | 1b | ALB | Có | Trực tiếp qua IGW |
| `app-a` | `10.20.10.0/24` | 1a | EC2 chạy container | **Không** | Chỉ đi ra, qua NAT |
| `app-b` | `10.20.11.0/24` | 1b | (dự phòng AZ) | **Không** | Chỉ đi ra, qua NAT |
| `db-a` | `10.20.20.0/24` | 1a | RDS | **Không** | **Không có** |
| `db-b` | `10.20.21.0/24` | 1b | RDS (subnet group) | **Không** | **Không có** |

**Vì sao 3 tier chứ không phải 2?** Vì db tier tách riêng thì mới viết được rule
"chỉ app tier mới được nói chuyện với database" ở tầng subnet. Nếu app và db
cùng subnet, rule đó không tồn tại được. Subnet không tốn phí, nên tách là lựa
chọn miễn phí đổi lấy một lớp phòng thủ thật.

**Vì sao mỗi tier 2 AZ?** ALB bắt buộc 2 AZ. Và db subnet group của RDS cũng đòi
tối thiểu 2 subnet ở 2 AZ, dù ta chỉ chạy single-AZ.

**Định tuyến:**
- Public subnet: `0.0.0.0/0` → **Internet Gateway**. Có đường vào và ra.
- App subnet: `0.0.0.0/0` → **NAT Gateway**. Chỉ có đường ra.
- Db subnet: **không có dòng `0.0.0.0/0` nào**. Không đường ra, không đường vào.

Đó là lý do database an toàn: không phải vì firewall giỏi, mà vì **không tồn tại
con đường nào** từ Internet tới nó.

**S3 Gateway Endpoint.** Một cửa riêng đi tới S3 ngay trong VPC, miễn phí. Nhờ
nó, traffic đọc/ghi ảnh sản phẩm không đi qua NAT Gateway — tiết kiệm phí data
($0.045/GB) và vẫn hoạt động khi NAT đã bị tắt.

## Security Group — firewall stateful, chỉ có danh sách cho phép

Ba SG. Điểm đặc biệt: **hầu hết rule dùng SG khác làm nguồn, không dùng dải IP.**

| SG | Cho vào (ingress) | Cho ra (egress) |
|---|---|---|
| `sg-alb` | 80 ← `0.0.0.0/0`<br>443 ← `0.0.0.0/0` | 80 → `sg-web`<br>8080 → `sg-web`<br>*(không gì khác)* |
| `sg-web` | 80 ← **chỉ `sg-alb`**<br>8080 ← **chỉ `sg-alb`**<br>**không có rule port 22 nào** | 1433 → `sg-rds`<br>80, 443 → `0.0.0.0/0` *(tải image, gọi API AWS)* |
| `sg-rds` | 1433 ← **chỉ `sg-web`** | **rỗng hoàn toàn** |

**Vì sao tham chiếu SG thay vì ghi dải IP?** Vì IP của EC2 thay đổi mỗi lần máy
được tạo lại, còn SG thì không. Viết `80 ← sg-alb` nghĩa là "cho vào nếu gói tin
đến từ *bất cứ máy nào đang gắn sg-alb*" — rule tự đúng mãi, không cần cập nhật.
Đây là cách đúng, và nó cũng chặt hơn: không có cách nào giả mạo được việc "tôi
đang gắn SG đó".

**Vì sao egress của `sg-rds` rỗng?** Database không cần chủ động gọi ra ngoài
bao giờ. SG là stateful nên câu trả lời cho app vẫn đi được. Egress rỗng nghĩa
là: nếu kẻ tấn công có chiếm được database, nó **không thể gửi dữ liệu ra
ngoài**. Đây là chặn đường rút, không phải chặn đường vào.

**Vì sao không có port 22 ở đâu cả?** Vì SSH là bề mặt tấn công lớn nhất và phổ
biến nhất của một máy Linux trên Internet (Flow Logs của ta bắt được máy quét tự
động dò port 22 và 23 chỉ trong một giờ). Ta bỏ nó hoàn toàn: **không có key
pair nào, không có file `.pem` nào**. Quản trị đi bằng **SSM Session Manager** —
AWS mở kênh từ trong máy ra, nên không cần mở port nào từ ngoài vào, và mọi
phiên đều được ghi log và kiểm soát bằng IAM.

## Network ACL — phần kỹ thuật đáng nhất của đồ án

NACL gắn vào **subnet** (không phải vào máy), **stateless**, và xét rule **theo
số thứ tự từ nhỏ đến lớn — gặp rule khớp đầu tiên là dừng**.

### `nacl-public` — chứa ALB và NAT Gateway

| # | Vào | # | Ra |
|---|---|---|---|
| **50** | **DENY tất cả từ `my_ip`** *(bật khi demo)* | 100 | allow 80 → app tier |
| 100 | allow 80 ← `0.0.0.0/0` | 110 | allow 8080 → app tier |
| 110 | allow 443 ← `0.0.0.0/0` | 120 | allow 80 → `0.0.0.0/0` |
| 120 | allow 1024–65535 ← `0.0.0.0/0` | 125 | allow 443 → `0.0.0.0/0` |
| `*` | **deny** (mặc định) | 130 | allow 1024–65535 → `0.0.0.0/0` |
| | | `*` | **deny** (mặc định) |

Rule **50** là điểm chứng minh giá trị của NACL: nó **chặn đúng một IP**. Security
Group *không làm được việc này* vì SG chỉ có rule cho phép, không có rule cấm.
Muốn chặn một kẻ tấn công cụ thể mà vẫn phục vụ mọi người khác — chỉ NACL làm
được.

### `nacl-app` — chứa EC2, và là phần khó nhất

| # | Vào | # | Ra |
|---|---|---|---|
| **90** | **DENY 22** ← `0.0.0.0/0` | 100 | allow 1433 → db tier |
| **95** | **DENY 1433** ← `0.0.0.0/0` | 110 | allow 80 → `0.0.0.0/0` |
| 100 | allow 80 ← public tier | 115 | allow 443 → `0.0.0.0/0` |
| 110 | allow 8080 ← public tier | 120 | allow 1024–65535 → public tier |
| **115** | **DENY 8080** ← `0.0.0.0/0` | `*` | **deny** |
| 120 | allow 1024–65535 ← `0.0.0.0/0` | | |
| `*` | **deny** | | |

**Đây là chỗ cần hiểu kỹ, và cũng là chỗ đáng trình bày nhất khi báo cáo.**

Rule 120 mở dải 1024–65535 cho **cả thế giới**. Nghe như một lỗ hổng khổng lồ.
Nhưng nó **bắt buộc phải có**: NACL là stateless, và khi EC2 tải container image
từ Internet qua NAT, câu trả lời quay về sẽ đi tới một ephemeral port trong dải
đó, với địa chỉ nguồn là `0.0.0.0/0` (vì server ở đâu trên Internet ta không
biết trước). Không mở rule 120 thì EC2 không tải được gì.

Nhưng `1433` và `8080` **nằm trong khoảng 1024–65535**. Nghĩa là rule 120 vô
tình mở database port và API port ra Internet ở tầng NACL.

Cách xử lý: đặt **DENY ở số nhỏ hơn**. Rule 95 (`DENY 1433`) và rule 115
(`DENY 8080`) được xét *trước* rule 120, nên gói tin nhắm vào hai port đó bị
chặn trước khi rule 120 kịp cho qua. Rule 90 (`DENY 22`) cũng vậy — chặn SSH
tường minh ở tầng mạng, thêm một lớp nữa bên cạnh việc SG không có rule 22.

Ba điều rút ra, đều là ý hay để nói khi báo cáo:

1. **Thứ tự rule trong NACL là logic, không phải hình thức.** Đổi 95 thành 130
   là mở database ra Internet.
2. **Stateless là con dao hai lưỡi.** Nó cho phép viết rule DENY (điều SG không
   làm được), nhưng buộc phải mở dải ephemeral (điều SG không cần).
3. **Vì thế SG vẫn cần thiết.** NACL ở tầng app buộc phải mở 1024–65535 cho cả
   thế giới; chính SG mới là lớp nói "8080 chỉ nhận từ sg-alb". Hai lớp bù đúng
   điểm yếu của nhau — đó là ý nghĩa thật của defense in depth, không phải làm
   hai lần cho chắc.

### `nacl-db` — chặt nhất

| # | Vào | # | Ra |
|---|---|---|---|
| 100 | allow 1433 ← **chỉ app tier** | 100 | allow 1024–65535 → **chỉ app tier** |
| `*` | **deny mọi thứ khác** | `*` | **deny mọi thứ khác** |

Không có rule nào khác. Chiều ra chỉ mở dải ephemeral về app tier — tức
database *chỉ được trả lời*, không được chủ động gọi đi đâu.

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
| **Task `migrator` / `seeder`** | Debian (`runtime-deps:10.0`, `debian:12-slim`) | Chạy-một-lần-rồi-thoát. `seeder` cần `mssql-tools18`, mà Microsoft chỉ phát hành package apt cho Debian |

Alpine dùng thư viện C tên **musl**, còn Debian và Amazon Linux dùng **glibc**.
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
| `seeder` | **có** | cài `mssql-tools18` từ apt repo của Microsoft, mà dòng repo ghi rõ `arch=amd64` — không có bản arm64 |

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

Container **không phải máy ảo**. Nó là một tiến trình Linux bình thường, bị kernel
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
`Encrypt=True;TrustServerCertificate=False`, nghĩa là API *thật sự kiểm tra* chứng
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
nào, không key pair, không file `.pem`, `nacl-app` còn có rule DENY 22 tường minh.

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
| Nhật ký | phải tự cấu hình `sshd`, và log nằm **trên chính máy** bị chiếm | `ssm:StartSession` là management event nên hiện trong **CloudTrail Event History** (bật sẵn, miễn phí, giữ 90 ngày) — ở ngoài máy, không sửa được từ trong |

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
- **Đồng hồ hệ thống.** `sg-web` egress chỉ mở `80`, `443`, `1433` — không có
  `123` (NTP), nên về lý máy không đồng bộ được giờ. Thực tế vẫn đúng giờ vì
  Amazon Linux dùng **Amazon Time Sync** ở `169.254.169.123`, một địa chỉ
  *link-local*: nó không đi qua route table, không qua NAT, nên không cần rule
  nào. Các bản ghi `REJECT` port 123 trong Flow Logs vì thế **không phải sự cố**.
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

SQL Server Express, `db.t3.micro`, 20GB, single-AZ, nằm trong db subnet.

**`publicly_accessible = false`** — và cần hiểu chính xác nó làm gì. Nó **không**
ẩn tên DNS của database; tên đó vẫn tra được từ Internet. Nó làm việc khác: tên
đó **phân giải ra một IP private** (`10.20.21.81`). Kẻ tấn công tra được tên,
nhưng nhận về một địa chỉ không tồn tại trên Internet — nên không kết nối được.
Kiểm chứng thực tế: dò cổng 1433 tới endpoint đó từ laptop → **timeout**, không
phải "từ chối kết nối". Đúng như mong đợi.

**Vì sao dùng RDS thay vì tự cài SQL Server lên EC2?** Vì AWS lo hộ: backup tự
động, vá lỗi, chứng chỉ TLS, snapshot, khôi phục theo thời điểm. Tự cài thì
những việc đó thành việc của nhóm, và đó chính là loại việc dễ bị bỏ quên nhất.

## IAM — bốn danh tính, mỗi cái một phạm vi

Điểm least-privilege mạnh nhất của thiết kế nằm ở đây. Thay vì một danh tính có
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

| Biến | Bật/tắt cái gì | Giá APS1 |
|---|---|---|
| `enable_nat` | NAT Gateway | $0.0590/giờ |
| `enable_alb` | ALB + target group + 2 ECS service | $0.0252/giờ |
| `instance_count` | 0 hoặc 1 EC2 | $0.0132/giờ |
| `enable_flow_logs` | VPC Flow Logs | phí ingest |
| `enable_deny_demo` | NACL rule 50 | $0 |

RDS bật/tắt bằng lệnh riêng, không qua Terraform.

**Con số cần nhớ, và nó phản trực giác:** RDS là khoản đắt nhất — $0.098/giờ,
trong đó **$0.0674 là CPU credit surplus**. `db.t3.micro` là loại máy
*burstable*: nó chỉ được dùng miễn phí 10% CPU, vượt lên thì bị tính tiền. SQL
Server Express **không tải** vẫn ngồi ở ~36% CPU. Nên riêng phần vượt hạn mức đã
đắt xấp xỉ NAT + ALB cộng lại. Chi tiết đo được nằm trong
[terraform-runbook.md](terraform-runbook.md).

---

# Phần IV — Vì sao thiết kế này đáp ứng đề bài

Đề bài đòi ba mảng kiến thức (Linux, AWS, Terraform), 5 thành phần hạ tầng và
2 kết quả. Đối chiếu từng dòng:

| Đề bài yêu cầu | Ở đâu trong hệ thống |
|---|---|
| **Tìm hiểu HĐH Linux + xây website trên đó** | Mục ["Linux — hệ điều hành chạy bên dưới tất cả"](#linux--hệ-điều-hành-chạy-bên-dưới-tất-cả): 3 bản phân phối, cloud-init/systemd, swap, namespace/cgroup, quyền file, không SSH |
| **Tìm hiểu AWS Cloud + Terraform** | Phần II (khái niệm), và toàn bộ hạ tầng khai bằng Terraform: 8 module, 92 test tự động, không resource nào bấm tay |
| VPC | `10.20.0.0/16`, 6 subnet, 3 tier, 2 AZ |
| Security Group | 3 cái, rule tham chiếu SG, không có port 22 |
| **Network ACL** | 3 cái, mỗi tier một cái, có rule DENY và thứ tự có ý nghĩa |
| **Application Load Balancer** | ALB + ACM + 2 target group + allowlist Host |
| EC2 Instance chạy website | 1 `t3.micro` chạy container nginx + .NET |
| Máy tấn công verify rule | Laptop (`my_ip`), 11 kịch bản, bằng chứng lưu trong `docs/evidence/` |
| Rule mở theo nguyên tắc tối thiểu | Xem bảng dưới |
| Rule đã ngăn được tấn công | [security-validation-report.md](security-validation-report.md) |

**Nguyên tắc tối thiểu — chứng minh cụ thể, không phải khẩu hiệu:**

| Điều đã KHÔNG mở | Hệ quả |
|---|---|
| Không port 22 ở bất kỳ SG nào | Không có bề mặt SSH; máy quét tự động dò 22 đều bị chặn |
| App tier không có IP public | Không thể gọi trực tiếp từ Internet |
| Db tier không có route ra Internet | Không tồn tại đường đi tới database |
| `sg-rds` egress rỗng | Chiếm được DB cũng không rút được dữ liệu ra |
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
| `nacl-app` buộc mở dải 1024–65535 ra Internet | Bản chất của NACL stateless. Đã bù bằng DENY 1433/8080 số nhỏ hơn, và bằng SG ở lớp trong |
| NAT Gateway không gắn được Security Group | Khác NAT instance. Kiểm soát egress dồn vào `sg-web` và `nacl-app` |
| Không lọc egress theo tên miền | Cần AWS Network Firewall (~$300/tháng), không khả thi |
| Chỉ 1 EC2, deploy có downtime 20–40s | Đổi lấy bề mặt SG nhỏ hơn hàng nghìn lần. Lựa chọn có ý thức |
| Rate limiter đếm trong RAM | 2 instance sẽ thành 2× hạn mức. Cần Redis nếu scale thật |
| **Không scale ngang được** dù đã có ASG + ALB | Bộ máy có đủ, nhưng bị ghim: `max_size = 1`, `managed_scaling = DISABLED`, và chốt cứng nhất là **host port tĩnh** 80/8080 — hai task không cùng bind một port trên một máy. Mở ra thì phải chọn `awsvpc` (nhiều ENI hơn `t3.micro` chịu nổi) hoặc dải ephemeral `32768–65535` trên `sg-web` — tức **đánh đổi trực tiếp với chiều đề bài đang chấm**. Đã chọn tối thiểu rule, chấp nhận một máy |
| Không có CloudTrail trail | Chỉ có Event History mặc định: 90 ngày, chỉ management event, không lưu ra S3. Tạo trail tốn ~$0.03/tháng cho S3 — đã cân nhắc, hoãn vì mọi thao tác hạ tầng đều đi qua Terraform và Git đã là nhật ký |
| Không có alarm nào | Lambda cost guard lỗi thì im lặng. Một CloudWatch alarm trên metric `Errors` là ~$0.10/tháng — đã cân nhắc, hoãn |
| Không WAF | ALB có allowlist Host nhưng không lọc SQL injection ở tầng mạng. Phòng thủ nằm ở tầng ứng dụng (EF Core tham số hoá) |
| Single-AZ RDS | SQL Server Express không hỗ trợ Multi-AZ |
| Giữa hai phiên làm việc, domain không hoạt động | ALB chạy 24/7 tốn $18/tháng cho một đồ án |

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
| VPC, subnet, SG, **NACL** | [Bắt đầu với Amazon VPC](https://000003.awsstudygroup.com/vi/) — chương 2 tên đúng là **"Tường lửa trong VPC"** | Đây là chương gần nhất với deliverable trọng tâm của đồ án: phân biệt SG và NACL trên console | Không dạy thứ tự rule NACL và bài toán dải ephemeral. Đó là phần Phần III của tài liệu này |
| Mạng nâng cao, VPC Endpoint | [AWS Networking and Content Delivery](https://000092.awsstudygroup.com/vi/) — chương "VPC Endpoints cho AWS Services" | Hiểu S3 Gateway Endpoint của ta làm gì và vì sao nó miễn phí | Nặng về Transit Gateway / VPC Peering — ta không dùng |
| EC2 | [Giới thiệu về Amazon EC2](https://000004.awsstudygroup.com/vi/) | AMI, instance type, key pair, user data, gắn SG | Workshop **có dùng key pair và SSH**; ta cố ý bỏ cả hai |
| Launch Template, ASG, **Load Balancer** | [Triển khai ứng dụng với Auto Scaling Group](https://000006.awsstudygroup.com/vi/) — chương 3 và 4 | Đây là chỗ duy nhất trên site dạy **Launch Template + ALB + target group** cùng nhau | Không có allowlist Host header, không có listener rule theo tên miền |
| ECS, task definition, service, ALB | [Triển khai ứng dụng trên Amazon ECS](https://000016.awsstudygroup.com/vi/) | **Workshop sát kiến trúc của ta nhất.** Có đủ cluster → task definition → ALB + target group → service, và cả chiến lược deploy | Dùng Fargate + `awsvpc`; ta dùng **EC2 launch type + `bridge`**. Khác biệt này chính là lý do SG của ta chỉ mở 2 port |
| Docker, ECR | [Triển khai ứng dụng trên Docker với AWS](https://000015.awsstudygroup.com/vi/) — chương 8 "Image Registry" | Build image, đẩy lên ECR, xác thực bằng IAM | Không bật IMMUTABLE tag — phần làm rollback có nghĩa |
| RDS | [Bắt đầu với Amazon RDS](https://000005.awsstudygroup.com/vi/) — chương 2 có sẵn phần **Security Group + DB Subnet Group** | Đúng ba thứ ta cần: subnet group 2 AZ, SG chỉ mở 1433, backup/restore | Không nói về `publicly_accessible`, và **không nói về CPU credit của lớp `t3`** — đúng cái đã làm ta trả tiền |
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

Nói thẳng để không ai mất thời gian tìm:

**Không có workshop Terraform.** Workshop IaC duy nhất trên site
([000102](https://000102.awsstudygroup.com/vi/)) dạy **CloudFormation**. Khái
niệm thì dùng chung được — IaC, state, plan trước khi apply, resource khai báo
thay vì bấm tay — nhưng cú pháp thì khác hoàn toàn. Học Terraform ở
[developer.hashicorp.com/terraform/tutorials/aws-get-started](https://developer.hashicorp.com/terraform/tutorials/aws-get-started),
rồi đọc [terraform-runbook.md](terraform-runbook.md) của dự án.

**Không có workshop riêng về Network ACL.** Chỉ có một chương trong workshop VPC.
Mà NACL lại là thành phần đề bài nhấn mạnh nhất. Nên phần NACL ở Phần III của tài
liệu này **là nguồn chính**, không phải phần bổ trợ — đọc kỹ đoạn giải thích vì
sao rule 95 và 115 tồn tại.

**Không có workshop riêng về ALB.** ALB nằm lẫn trong workshop
[ASG](https://000006.awsstudygroup.com/vi/) (chương 4) và
[ECS](https://000016.awsstudygroup.com/vi/) (chương 7). Cả hai đều **không** dạy
định tuyến theo header Host và allowlist — phần chặn được tấn công Host header
injection trong hệ thống của ta.

Ngoài ra ba chỗ workshop dạy **khác có chủ ý** với hệ thống của ta, đừng nhầm là
ta làm sai:

| Workshop dạy | Ta làm | Vì sao |
|---|---|---|
| Dùng key pair + SSH vào EC2 | Không có key pair, vào bằng SSM | SSH là bề mặt tấn công lớn nhất; Flow Logs bắt được máy quét dò port 22 trong đúng một giờ |
| ECS trên Fargate, network mode `awsvpc` | ECS trên **EC2 launch type**, `bridge` | Đề bài yêu cầu "triển khai website thông qua EC2 Instance" |
| Secrets Manager | Parameter Store SecureString | Miễn phí, và ta không cần tự động luân chuyển mật khẩu |
| Bật Multi-AZ cho RDS | Single-AZ | SQL Server Express không hỗ trợ Multi-AZ |

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
