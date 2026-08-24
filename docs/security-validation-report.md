# Báo cáo kiểm thử bảo mật — HushStore trên AWS

**Đề tài 513** — "Đầu ra" số 2 của đề bài: chứng minh các rule được mở theo
nguyên tắc tối thiểu và **đã thực sự ngăn được tấn công**.

| | |
|---|---|
| Ngày kiểm thử | **2026-08-24 (08:19 – 09:40 UTC)** — chạy lại toàn bộ 12 kịch bản trên account mới |
| Account | **`551897327153`** |
| Máy tấn công | Laptop macOS, IP công khai `117.3.54.230` |
| Công cụ | `nmap 7.991`, `curl 8.7.1`, `nc`, `python3 socket`, `aws iam simulate-principal-policy`, `aws sts assume-role-with-web-identity`, `aws logs filter-log-events` |
| Mục tiêu | ALB `hushstore-alb-1075742626.ap-southeast-1.elb.amazonaws.com` (`13.251.164.95`) · EC2 `10.20.11.251` · RDS `10.20.21.168` |
| Hạ tầng | Terraform, commit `2941d31`. Cả 4 image ECR ở tag `2941d316…` |
| Output thô | [`docs/evidence/acc-551897327153/`](evidence/acc-551897327153/) — mọi số trong báo cáo này lấy từ đó, không có số nào viết tay |
| Lần đo trước | [`docs/evidence/`](evidence/) (account `667836586836`, đã bị xoá) — giữ lại để đối chiếu |

Kiểm thử thực hiện trên hạ tầng **do chính nhóm sở hữu**, trong phạm vi đề bài.

---

> ## ✅ Đã chạy lại toàn bộ trên account mới — 2026-08-24
>
> Số liệu trong báo cáo này là **phép đo mới**, thực hiện trên account
> `551897327153` sau khi dự án dời khỏi account cũ. Toàn bộ **12/12 kịch bản
> đạt**, và bằng chứng thô nằm ở
> [`evidence/acc-551897327153/`](evidence/acc-551897327153/).
>
> **Vì sao lần chạy lại này tự nó là một kết quả.** Hạ tầng cũ đã bị destroy
> hoàn toàn (132 resource, xem [`cleanup-account-cu.md`](cleanup-account-cu.md)).
> Hạ tầng đo ở đây được **dựng lại từ đầu, từ chính mã Terraform đó, trên một
> account trắng** — VPC mới, subnet mới, ALB mới, RDS trống. Rồi:
>
> - `terraform apply` dựng đủ hạ tầng, `plan` sau đó sạch;
> - task migrator chạy 20 migration của EF Core → **exit 0**;
> - task seeder nạp dữ liệu → **exit 0**, đếm lại được `AppRoles=3`,
>   `Categories=18`, `Manufacturers=21`, `Products=49`, `ProductVariants=52`;
> - cả hai target group của ALB → **healthy** (tức `/health/ready` chạm được DB);
> - và **cả 12 kịch bản bảo mật cho kết quả y hệt lần trước**.
>
> Nghĩa là tính chất bảo mật của hệ thống nằm trong **mã**, không nằm trong một
> lần cấu hình tay may mắn. Đó là điều một báo cáo chỉ đo một lần trên một
> account không chứng minh được.
>
> **Bốn thứ lần này tìm ra mà lần trước không có** — xem [mục 8](#8-bốn-phát-hiện-mới-từ-lần-chạy-lại):
> security group `default` của VPC, egress NTP bị chặn, máy quét thật từ Internet,
> và ba phép thử IAM chặt hơn.
>
> Giá trị phụ thuộc account đã đổi và đã được cập nhật khắp báo cáo: IP máy tấn
> công (`42.1.89.156` → `117.3.54.230`), DNS và IP của ALB, IP nội bộ của EC2
> (`10.20.11.22` → `10.20.11.251`) và RDS (`10.20.21.81` → `10.20.21.168`).
> CIDR `10.20.0.0/16` giữ nguyên nên IP nội bộ tương tự nhưng không trùng.

---

## 1. Tóm tắt kết quả

| # | Kịch bản | Kỳ vọng | Kết quả | Rule chịu trách nhiệm | Bằng chứng |
|---|---|---|---|---|---|
| 1 | Quét port ALB | chỉ 80, 443 mở | ✅ 80 + 443 open, 998 port `filtered` | `sg-alb` ingress | `kb01-nmap-alb.txt` |
| 2 | Kết nối IP riêng của EC2 | không có đường đi | ✅ timeout 10s trên **cả ba** port `:8080`, `:22`, `:80`; `describe-instances` trả `PublicIpAddress: null` | không public IP + app subnet không route ra IGW | `kb03-rds-tu-internet.txt` |
| 3 | Kết nối trực tiếp RDS | timeout | ✅ endpoint công khai phân giải ra **IP riêng** `10.20.21.168`; socket timeout (`EAGAIN`) — **drop im lặng**, không phải refused | `publicly_accessible=false` + `sg-rds` + `nacl-db` | `kb03-rds-tu-internet.txt` |
| 4 | Gọi port ứng dụng `:8080` trên ALB | không kết nối được | ✅ timeout (exit 28), **không phải** refused | ALB chỉ có listener 80/443 | `kb04-05-port-ung-dung-va-ssh.txt` |
| 5 | SSH vào mọi hướng | không kết nối được | ✅ `Operation timed out` cả ALB:22 và EC2:22; **0 key pair** toàn region; launch template `KeyName: None`; **0 rule ingress phủ port 22** trên cả 3 SG | không có rule 22 + `nacl-app` rule 90 DENY | `kb04-05-…txt`, `kb05-khong-co-ssh.txt` |
| 6 | Brute-force `/api/auth/login` | 429 từ request 6 | ✅ req 1-5 → 400, **req 6-20 → 429** | rate limiter `LoginRateLimit` | `kb06-rate-limit-login.txt` |
| 7 | Host header lạ | không lọt sang backend | ✅ `evil.com` → 403, `www` → 403, tên DNS thô của ALB → 403 | ALB listener rule + default `fixed-response` | `kb07-host-allowlist.txt` |
| 8 | NACL DENY theo IP | chặn đúng 1 IP | ✅ **A/B từ cùng một máy**: đường trực tiếp timeout, đường qua Cloudflare 200 | `nacl-public` rule 50 | `kb08-nacl-deny-theo-ip.txt` |
| 9 | VPC Flow Logs `REJECT` | có bản ghi khớp | ✅ **2264 bản ghi/30 phút**, trong đó **2022** là đợt nmap của nhóm — port 22 (7), 1433 (4), 8080 (5) đều có mặt. Khớp cả 4 nhóm | Flow Logs `REJECT`, gom 600s | `kb09-flowlog-reject.txt` |
| 10 | Bán kính ảnh hưởng của IAM role | mỗi role chỉ thấy phần của mình | ✅ ma trận **18 phép thử**; host bị **explicitDeny trên CẢ BỐN** action đọc parameter | 7/10 role được đo | `kb10-blast-radius-iam.txt` |
| 11 | Giả mạo OIDC assume-role | từ chối | ✅ **3/3** phép thử thất bại. JWT tự ký đủ mọi claim vẫn bị `InvalidIdentityToken`; và JWT với `sub` SAI repo nhận **cùng một lỗi** → chứng minh chữ ký được kiểm **trước** claim, nên lỗi không hé ra trust policy đòi gì | trust condition `StringEquals` trên `aud` + `sub` | `kb11-gia-mao-oidc.txt` |
| 12 | Bán kính thiệt hại của role deploy | làm được đúng 4 việc của pipeline, không hơn | ✅ 4 phép thử `allowed`, **8** phép thử `implicitDeny` — gồm `autoscaling:SetDesiredCapacity`, `rds:StartDBInstance`, `rds:StopDBInstance`, `s3:GetObject` trên tfstate, và `iam:AttachRolePolicy` (thử leo thang đặc quyền) | policy inline của role deploy, ghim theo ARN + condition | `kb12-blast-radius-deploy-role.txt` |

**12/12 kịch bản đã có bằng chứng.** Kịch bản 11 chạy được sau khi Phase 2
`apply` xong hai IAM role. Kết quả đáng chú ý: lỗi trả về là
`InvalidIdentityToken`, **không phải** `AccessDenied` — và đó là kết quả mạnh
hơn. `AccessDenied` nghĩa là "token của bạn thật, nhưng quyền không đủ";
`InvalidIdentityToken` ở đây nghĩa là AWS không tin token ngay từ đầu, nên
claim `sub` trong đó không hề được xét. Giả mạo `sub` là vô nghĩa khi không
giả mạo được chữ ký của GitHub. Xem mục 6.

Kịch bản 12 trả lời câu mà kịch bản 11 để lại. Kịch bản 11 chứng minh **không ai
assume được** role deploy; nó không nói gì về việc nếu assume được thì làm được
gì. Với tiêu chí "least privilege, và đã thực sự ngăn được tấn công" thì hai nửa
đó là hai câu hỏi khác nhau, và câu thứ hai mới là câu về bán kính thiệt hại. Xem
mục 7.

---

## 2. Ba kết quả đáng chú ý nhất

### 2.1. `filtered` chứ không phải `refused` — và điều đó tự nó là bằng chứng

Lần quét đầu tiên toàn bộ 65535 port **hết hạn 900 giây mà chưa ra kết quả**.
Đó không phải lỗi công cụ, mà là hệ quả trực tiếp của cấu hình đúng: Security
Group **drop gói tin im lặng**, không trả `RST`. Kẻ tấn công phải chờ hết
timeout cho từng port. Nếu SG trả `refused`, cùng lệnh đó sẽ xong trong vài giây
và kẻ tấn công lập được bản đồ hệ thống rất nhanh.

Quét lại theo phạm vi khai báo rõ (1000 port phổ biến nhất + các port
ứng dụng/DB/admin):

```
Not shown: 998 filtered tcp ports (no-response)
PORT    STATE SERVICE
80/tcp  open  http
443/tcp open  https

22/tcp    filtered ssh          1433/tcp  filtered ms-sql-s
3306/tcp  filtered mysql        3389/tcp  filtered ms-wbt-server
8080/tcp  filtered http-proxy   5432/tcp  filtered postgresql
```

Đúng 2 port mở, và cả hai đều là port bắt buộc để website hoạt động.

### 2.2. NACL chặn được điều Security Group không thể — chứng minh bằng A/B trên cùng một máy

Đây là lý do đề bài yêu cầu **cả** Security Group **và** Network ACL. Security
Group chỉ có allow-list: nó không có cách nào diễn đạt "chặn riêng IP này".
Network ACL có `Deny` và có thứ tự rule, nên làm được.

Bật `enable_deny_demo = true` → `nacl-public` rule 50 = DENY all từ
`117.3.54.230/32`. Rồi gọi cùng một website bằng **hai đường, từ cùng một máy** —
và quan trọng là đo **cả trước lẫn sau** khi bật rule:

| Đường | IP nguồn AWS nhìn thấy | TRƯỚC khi bật | SAU khi bật |
|---|---|---|---|
| A. `curl --resolve hushstore.io.vn:443:13.251.164.95` | `117.3.54.230` | HTTP 200, 0,15s | **timeout, exit 28, 25s** |
| B. `curl https://hushstore.io.vn/` (qua Cloudflare proxy) | IP của Cloudflare | HTTP 200, 0,25s | **HTTP 200, 0,74s** |

Cột "TRƯỚC" là thứ làm bảng này thành bằng chứng chứ chỉ là quan sát. Không có
nó thì một người phản biện đúng mực sẽ hỏi: *đường A có bao giờ hoạt động
không?* Có — 200 trong 0,15 giây, vài phút trước đó, từ cùng máy đó.

Và cột B loại trừ mọi cách giải thích khác: mất mạng thì B cũng chết; ALB chết
thì B cũng chết; DNS sai thì B cũng chết. **Chỉ A chết.** Khác biệt duy nhất
giữa A và B là IP nguồn mà AWS nhìn thấy.

Chi tiết kỹ thuật đáng nói khi bảo vệ: rule DENY này ở **số 50**, nhỏ hơn hai
rule allow 80/443 ở số 100 và 110. NACL xét rule theo **thứ tự tăng dần và dừng
ở rule đầu tiên khớp** — nên nếu đặt cùng rule đó ở số 150 thì nó **vô dụng**:
gói tin đã khớp rule 100 và được cho qua từ trước.

*(Sau khi thu bằng chứng, `enable_deny_demo` đã tắt lại — mặc định là `false`.)*

### 2.3. Bán kính ảnh hưởng: mỗi role chỉ thấy đúng phần của mình

Đo bằng `iam simulate-principal-policy` — chạy trên **policy thật đang gắn**,
do chính bộ đánh giá của AWS phán quyết, không phải do người viết báo cáo suy luận:

| Role | `db-password` | `connection-string` | `jwt-secret` | `s3:PutObject` ảnh | `rds:DeleteDBInstance` |
|---|---|---|---|---|---|
| `container-instance` (EC2 host) | **explicitDeny** | — | **explicitDeny** | implicitDeny | implicitDeny |
| `task-app` (container API runtime) | implicitDeny | — | — | **allowed** | implicitDeny |
| `task-execution` (api/web/migrator) | implicitDeny | **allowed** | **allowed** | — | — |
| `task-execution-seeder` | **allowed** | implicitDeny | — | — | — |

Lần chạy lại còn siết thêm một chỗ mà lần trước bỏ qua: **cả bốn** action đọc
parameter trên host đều được đo riêng, không chỉ `ssm:GetParameter`.

| Action trên host, resource `/hushstore/*` | Kết quả |
|---|---|
| `ssm:GetParameter` | **explicitDeny** |
| `ssm:GetParameters` | **explicitDeny** |
| `ssm:GetParameterHistory` | **explicitDeny** |
| `ssm:GetParametersByPath` | **explicitDeny** |

Điều này chứng minh trực tiếp lập luận trong `iam.tf`: statement `Deny` phải
liệt kê **đủ bốn**, không phải ba. `GetParameterHistory` với
`WithDecryption=true` trả về plaintext của các version cũ — thiếu nó là còn một
đường đọc secret. Hiện managed policy không cấp action đó nên nó *sẽ* là
implicitDeny; nhưng implicitDeny **bị override được** nếu sau này ai gắn thêm
policy, còn explicitDeny thì không. Bảng trên là bằng chứng rằng cả bốn đang ở
trạng thái mạnh.

Ba điểm quan trọng:

1. **Hai tập secret giao nhau bằng rỗng.** Execution role của app đọc được
   `connection-string` nhưng không đọc được `db-password`; role của seeder thì
   ngược lại. Nếu dùng **chung** một execution role, cả bốn task sẽ đọc được cả
   ba secret — chiếm quyền một container là chiếm hết.
2. **Host bị `explicitDeny`, không phải `implicitDeny`.** EC2 host cần managed
   policy `AmazonSSMManagedInstanceCore`, mà policy đó cho `ssm:GetParameter`.
   Nên phải thêm statement `Deny` tường minh cho `/hushstore/*`. `Deny` tường
   minh thắng mọi `Allow`, kể cả từ managed policy. Đây là chỗ dễ để lọt nhất
   và là lý do phải đo bằng simulator chứ không đọc policy bằng mắt.
3. **Không role nào chạm được RDS qua API.** Kể cả `rds:DescribeDBInstances`.
   Container chỉ nói chuyện với database bằng **TCP 1433**, không bằng
   AWS API — nên không cần quyền RDS nào cả.

Bảng trên là 4 trong 5 role của tầng chạy ứng dụng (role thứ năm là
`task-migrator`). Phase 2 thêm 2 role nữa cho CI/CD —
`github-actions-deploy-role` và `github-actions-plan-role` — thành **7 role tách
biệt**. Bán kính của role deploy đo riêng ở **mục 7 (kịch bản 12)**, vì nó là role
duy nhất SỬA được hạ tầng.

---

## 3. Chi tiết từng kịch bản

Xem output thô trong [`docs/evidence/`](evidence/). Mỗi file chứa nguyên văn
lệnh đã chạy và nguyên văn kết quả.

### 3.2 / 3.3 — Tier private không tiếp cận được từ internet

```
RDS qua tên DNS công khai    → 10.20.21.168:1433  TIMEOUT (EAGAIN, drop im lặng)
EC2 container instance :8080 → 10.20.11.251:8080  TIMEOUT sau 10,0s
EC2 container instance :22   → 10.20.11.251:22    TIMEOUT sau 10,0s
EC2 container instance :80   → 10.20.11.251:80    TIMEOUT sau 10,0s

aws rds describe-db-instances  --query PubliclyAccessible  → False
aws ec2 describe-instances     --query PublicIpAddress     → null
```

Hai dòng cuối là bằng chứng phía cấu hình, đặt cạnh phép đo phía tấn công: cái
thứ nhất giải thích *vì sao* cái thứ hai timeout.

Đáng phân biệt: kết quả là **timeout**, không phải `refused`. `refused` nghĩa là
có thứ gì đó đã trả lời "không"; timeout nghĩa là gói tin **bị bỏ im lặng** và
kẻ tấn công không học được gì — kể cả việc đích có tồn tại hay không.

Điểm đáng ghi: endpoint RDS **là tên DNS công khai** — ai cũng phân giải được.
Nhưng nó phân giải ra `10.20.21.168`, một địa chỉ riêng RFC1918 không định tuyến
trên internet. Đó là cách `publicly_accessible = false` hoạt động: không phải
ẩn tên, mà là không có đường đi. Kể cả khi kẻ tấn công biết chính xác endpoint
và mật khẩu, họ vẫn không tới được.

### 3.5 — Không có SSH ở bất kỳ đâu

```
$ nc -z -w 6 <alb> 22            → exit 1 (không mở)
$ ssh ec2-user@<alb>             → ssh: connect ... port 22: No route to host
```

Soát trực tiếp trên account: **0 key pair**, **0 Security Group rule mở port
22**. Truy cập quản trị đi qua **SSM Session Manager** (vào host) và **ECS
Exec** (vào trong container) — hai đường này dùng IAM để phân quyền và ghi log
mọi phiên, khác hẳn SSH key là một file tĩnh mà ai giữ cũng vào được.

### 3.6 — Rate limit chặn brute-force

20 request liên tiếp với mật khẩu sai:

```
req  1..5  → HTTP 400   (sai mật khẩu)
req  6..20 → HTTP 429   (Too Many Requests)
```

Đúng ngưỡng 5 request/phút. Lưu ý giới hạn đã biết: rate limiter là
**in-memory**, nên nếu chạy 2 task API song song thì ngưỡng thực tế thành 10.
Ở quy mô hiện tại (`max_size = 1`) điều này không xảy ra; muốn scale thật thì
phải chuyển state của rate limiter sang ElastiCache.

### 3.7 — Allowlist Host header trên ALB

Gọi thẳng vào IP của ALB, bỏ qua Cloudflare, thay đổi Host header:

```
Host: hushstore.io.vn        → HTTP 200   (routed → tg-web)
Host: api.hushstore.io.vn    → HTTP 404   (routed → tg-api; API không có route "/")
Host: evil.com               → HTTP 403
Host: www.hushstore.io.vn    → HTTP 403
(tên DNS thật của chính ALB) → HTTP 403
```

`404` ở dòng thứ hai là **đúng**: request đã lọt tới API và API trả lời. `403`
đến từ `default_action = fixed-response` của listener, tức bất kỳ Host không
nằm trong allowlist đều bị chặn tại ALB, không chạm tới container.

Điều dễ đọc sai: gọi ALB bằng tên DNS thô trả **403 là đúng thiết kế**. Nếu
trả **503** thì mới là lỗi (không có target healthy).

### 3.9 — VPC Flow Logs: bằng chứng ở tầng network, độc lập với `curl`

Bật `enable_flow_logs = true` (`TrafficType = REJECT`, gom mỗi 600 giây). Ba
nhóm bản ghi, mỗi nhóm nói một điều khác nhau:

**Nhóm A — đợt nmap của nhóm, nhìn từ phía hạ tầng.** `2022` bản ghi trong
tổng `2264`, tất cả tới `10.20.1.163` (ENI của ALB trong subnet public):

```
117.3.54.230 -> 10.20.1.163  dport=22    proto=6  REJECT   (7 bản ghi)
117.3.54.230 -> 10.20.1.163  dport=8080  proto=6  REJECT   (5 bản ghi)
117.3.54.230 -> 10.20.1.163  dport=1433  proto=6  REJECT   (4 bản ghi)
117.3.54.230 -> 10.20.1.163  dport=3306  proto=6  REJECT   (4 bản ghi)
117.3.54.230 -> 10.20.1.163  dport=3389  proto=6  REJECT   (4 bản ghi)
117.3.54.230 -> 10.20.1.163  dport=5432  proto=6  REJECT   (4 bản ghi)
... (988 port còn lại của --top-ports 1000)
```

**Chú ý điều KHÔNG có trong danh sách: port 80 và 443.** Lần đo này thu Flow Logs
khi `enable_deny_demo` đang **tắt**, nên hai port đó được `ACCEPT` và không xuất
hiện trong log `REJECT`. Đó là một **xác nhận độc lập** cho kịch bản 1: log tầng
network chứa đúng những port đóng, và không chứa hai port mở — khớp với kết quả
`nmap` mà không dùng chung công cụ nào.

*(Lần đo trên account cũ thu Flow Logs lúc rule 50 đang bật, nên ở đó 80 và 443
**cũng** bị `REJECT`. Hai kết quả không mâu thuẫn — chúng đo hai trạng thái cấu
hình khác nhau, và cùng cho thấy rule đang hoạt động đúng.)*

**Nhóm B — egress của EC2 bị chặn.** `sg-web` egress chỉ cho `1433`, `80`, `443`:

```
10.20.11.251 -> 52.207.222.50   dport=123  REJECT
10.20.11.251 -> 54.81.127.33    dport=123  REJECT
10.20.11.251 -> 3.94.91.31      dport=123  REJECT
...  (8 bản ghi)
```

`proto 17` là UDP, `dport 123` là NTP. **Một phát hiện thật, không phải bài test
dàn dựng**: host không ra được NTP công khai. Egress tối thiểu đang hoạt động
đúng thiết kế. Đồng hồ hệ thống vẫn đúng vì Amazon Linux đồng bộ qua Amazon
Time Sync Service ở địa chỉ link-local `169.254.169.123` — không đi qua NAT nên
không cần rule egress; các gói bị chặn ở đây là `chrony` thử thêm nguồn NTP công
khai dự phòng.

**Nhóm C — quét không mời từ internet.** Không phải traffic của nhóm:

```
125.88.205.65   -> 10.20.0.135  dport=6379   proto=6   (Redis)
145.255.160.146 -> 10.20.0.37   dport=22     proto=6   (SSH)
142.93.228.104  -> 10.20.1.38   dport=23     proto=6   (Telnet)
123.158.36.73   -> 10.20.0.135  dport=8090   proto=6
147.185.132.181 -> 10.20.1.38   dport=47320  proto=6
```

Đây là bằng chứng có giá trị nhất trong cả báo cáo, vì nó không do nhóm tạo ra.
Trong khoảng một giờ hạ tầng mở ra internet, các máy quét tự động đã dò thử
SSH, Telnet và Redis — và tất cả đều bị chặn trước khi chạm tới bất kỳ ứng dụng
nào. Các rule đang làm việc thật, không chỉ trong bài test.

*(Sau khi thu bằng chứng, `enable_flow_logs` đã tắt lại — mặc định là `false`
để không tốn phí ingest.)*

### 3.10 — Xem mục 2.3.

---

## 4. Những gì hạ tầng này **không** chống được

Một báo cáo chỉ liệt kê thành công thì không dùng được. Các giới hạn đã biết:

| Giới hạn | Nguyên nhân | Cách xử lý nếu cần |
|---|---|---|
| **`nacl-app` buộc phải mở dải ephemeral `1024-65535`** vào từ `0.0.0.0/0` | NACL **stateless**: return traffic từ internet qua NAT Gateway vào subnet với src `0.0.0.0/0` và dst port ephemeral | Đã bù bằng DENY `1433` (rule 95) và DENY `8080` (rule 115) đặt ở số **nhỏ hơn** để được xét trước. Đây chính là lý do vẫn cần SG làm lớp thứ hai — SG stateful nên không có vấn đề này |
| **Không lọc được egress theo domain** | NAT Gateway **không gắn được Security Group** (khác NAT instance) | Cần AWS Network Firewall (~$300/tháng) — không khả thi ở quy mô đồ án. Kiểm soát egress hiện dồn vào `sg-web` egress + `nacl-app` outbound |
| **`drop_invalid_header_fields` KHÔNG chặn được giả mạo `X-Forwarded-*`** | ALB **thêm vào** header này chứ không thay thế | Phòng thủ thật là `ForwardLimit` của `UseForwardedHeaders` — app chỉ tin proxy gần nhất. **Việc còn nợ:** đặt `ForwardLimit = 1` tường minh trong `Program.cs` |
| **WAF / chống DDoS tầng 7** | Chưa có AWS WAF | Cloudflare proxy đang che apex + api và cung cấp một phần; WAF của AWS là bước tiếp theo nếu cần |
| **Không có IDS/IPS trong VPC** | GuardDuty chưa bật | GuardDuty có bậc dùng thử 30 ngày; nên bật khi trình bày |
| **Host không ra được NTP công khai** — đã **đo được 8 bản ghi `REJECT`** | `sg-web` egress chỉ cho `1433`/`80`/`443` — hệ quả cố ý của egress tối thiểu | Không cần sửa: `chrony` dùng Amazon Time Sync ở `169.254.169.123` (link-local, không qua NAT). Đồng hồ đúng được **chứng minh gián tiếp** bằng việc migrator xác thực cert RDS thành công và ECR pull ký SigV4 thành công — xem [mục 8.2](#82-egress-của-chính-ec2-bị-chặn-ở-port-123--và-đồng-hồ-vẫn-đúng) |
| **Security group `default` của VPC cho phép mọi traffic từ chính nó** (gồm port 22) | AWS tự tạo một cái cho mỗi VPC và **không cho xoá**; Terraform của dự án không quản lý nó | Hiện **0 ENI** dùng nó nên không có bề mặt thật (xem [mục 8.1](#81-security-group-default-của-vpc-mở-mọi-port-từ-chính-nó)). Nhưng ai launch instance mà không chỉ định SG sẽ rơi vào nó. Bịt bằng `aws_default_security_group` với ingress/egress **rỗng**, hoặc bằng SCP. **Việc còn nợ.** |
| **IAM user `hushstore-ops` có `AdministratorAccess`, KHÔNG MFA, và một access key dài hạn** | Account mới cố ý dùng IAM user thay vì SSO, vì bật Identity Center buộc vào Organization và làm **hết hạn credit** (AWS Support xác nhận) | Đây là **lỗ hổng lớn nhất còn lại**, và là một đánh đổi có ý thức: đổi bảo mật của danh tính vận hành lấy việc giữ được credit. Giảm nhẹ được ngay bằng **bật MFA** cho user này — không ảnh hưởng credit. Spec của dự án cũng nêu rõ điểm least-privilege được chấm nằm ở 4 role workload, không ở role vận hành |
| **Account dùng chung, có 4 IAM user** | `hushstore-ops` (của nhóm, Admin, không MFA) · `DBT` (Admin, **có MFA**) · `Nhincc`, `ThinhDB` (chỉ `IAMUserChangePassword`) | Nhóm **không kiểm soát** ba user kia. Bán kính thiệt hại của account vì thế lớn hơn bán kính của hạ tầng: hai người có Admin. Ghi ra để không tuyên bố quá về mức độ cô lập |

---

## 5. Cách tái lập

```bash
# Account 551897327153 dùng IAM user (KHÔNG phải SSO) — xem lý do ở mục 4.
aws sts get-caller-identity --profile hushstore   # phải trả về hushstore-ops

# 1. ECR phải có image, nếu không ECS sẽ CannotPullContainerError.
#    Bốn image, tag = git SHA, build ghim linux/amd64:
SHA=$(git rev-parse HEAD)
for img in api web migrator seeder; do ... docker build --platform=linux/amd64 ... ; done
#    Rồi đặt image_tag = $SHA trong terraform.tfvars.

# 2. my_ip PHẢI khớp IP công khai hiện tại, nếu không kịch bản 8 sẽ
#    "đạt" một cách GIẢ (rule DENY chặn một IP không còn là của mình):
curl -s https://checkip.amazonaws.com     # rồi cập nhật my_ip = <ip>/32

# 3. Bật theo hai pha. Pha 1 KHÔNG có ALB, để chạy được migration:
bash infra/tf/scripts/up.sh --no-alb
#    rồi run-task migrator (phải exit 0) và run-task seeder.

# 4. Pha 2 bật ALB. Cần cert ACM đã ISSUED — nếu account mới thì phải thêm
#    hai record CNAME validation vào DNS trước, nếu không apply treo ở waiter.
bash infra/tf/scripts/up.sh

# kịch bản 9: enable_flow_logs = true (bản ghi hiện sau ~10 phút, gom mỗi 600s)
#             -> bật SỚM để nó bắt được luôn đợt nmap của kịch bản 1
# kịch bản 8: enable_deny_demo = true
#             -> chạy CUỐI CÙNG, vì nó chặn chính máy đang test

bash infra/tf/scripts/down.sh    # rồi XÁC MINH: ALB=0 NAT=0 EC2=0 ASG=0 RDS=stopped
```

**Ba cái bẫy đã gặp thật khi chạy lại**, ghi ra để lần sau không mất thời gian:

1. **ECR rỗng** thì bật hạ tầng chỉ để nhận `CannotPullContainerError` — kiểm
   `aws ecr describe-images` **trước** khi bật bất cứ thứ gì tính tiền.
2. **`my_ip` cũ** làm kịch bản 8 cho kết quả dương tính giả. IP nhà là IP động.
3. **RDS SQL Server Express start rất lâu** (đo được ~14 phút, trạng thái
   *Recovery*). Mọi cửa chặn chờ RDS phải có timeout tính theo đó.

Mọi lệnh tấn công nằm nguyên văn trong `docs/evidence/acc-551897327153/kb*.txt`,
kèm nguyên văn output. Không có số nào trong báo cáo này được viết tay.

---

## 6. Kịch bản 11 — giả mạo OIDC assume-role

Đã chạy. Nguyên văn output ở [evidence/kb11-gia-mao-oidc.txt](evidence/kb11-gia-mao-oidc.txt).

Kịch bản này đáng làm vì ARN của role deploy được **cố tình** lưu dưới dạng
repository *variable* trên GitHub, không phải *secret* — tức nó công khai với
bất kỳ ai đọc được log của Actions. Nếu sự an toàn phụ thuộc vào việc giữ kín
ARN thì cả thiết kế "OIDC thay credential dài hạn" đã sai từ đầu. Ba phép thử
đi từ token thô sơ tới token bịa công phu nhất mà một kẻ tấn công làm được:

| Phép thử | Token gửi lên | Kết quả |
|---|---|---|
| 1 | chuỗi bất kỳ (`token-bia-dat`) | `InvalidIdentityToken` — chặn ở tầng định dạng, chưa nói gì về bảo mật |
| 2 | JWT tự ký, `iss`/`aud`/`sub`/`iat`/`exp` **đều khớp** trust policy | `InvalidIdentityToken: Couldn't retrieve verification key from your identity provider` |
| 3 | — (đọc thẳng trust policy bằng `iam get-role`) | `StringEquals` trên cả `aud` và `sub`; `sub` ghim đúng `refs/heads/main`, không chứa `*` |

Phép thử 2 là phép thử có ý nghĩa. Token của nó chép đúng mọi giá trị trust
policy đòi; thiếu duy nhất chữ ký của GitHub. AWS đọc được JWT, thấy `iss` trỏ
về GitHub, **đi tới JWKS endpoint của GitHub** để lấy public key ứng với `kid`
mà token khai, không tìm thấy, và từ chối. Nghĩa là điều kiện thực sự để assume
role không phải "biết ARN" cũng không phải "khai đúng claim", mà là "có chữ ký
của GitHub cho đúng repo và đúng nhánh".

Trên đường tới đó, AWS còn từ chối hai lần vì lý do khác — thiếu claim `iat`,
rồi `iat` quá cũ — nên phép thử này phải làm ba lần mới cô lập được đúng nguyên
nhân là chữ ký. Cả ba lần đều nằm trong file bằng chứng.

### Lệnh để chạy lại

```bash
# Tên role: hushstore-github-actions-deploy-role
# Trust policy chỉ nhận StringEquals trên sub = repo:Elmira-Athena/PBL3:ref:refs/heads/main
aws sts assume-role-with-web-identity \
  --role-arn "$(terraform -chdir=infra/tf/envs/prod output -raw github_deploy_role_arn)" \
  --role-session-name gia-mao \
  --web-identity-token "token-bia-dat" \
  --profile hushstore --no-cli-pager
# Kỳ vọng: InvalidIdentityToken (token không do GitHub ký) — không phải AccessDenied,
# vì AWS từ chối ở bước xác thực chữ ký trước cả bước xét trust policy.

# Kiểm chứng trust policy bằng cách đọc thẳng nó:
aws iam get-role --role-name hushstore-github-actions-deploy-role \
  --query 'Role.AssumeRolePolicyDocument' --profile hushstore --no-cli-pager
# Kỳ vọng: Condition dùng StringEquals (KHÔNG phải StringLike) trên cả aud và sub,
# và giá trị sub không chứa ký tự *
```

---

## 7. Kịch bản 12 — bán kính thiệt hại của role deploy

Đã chạy. Nguyên văn output ở
[evidence/kb12-blast-radius-deploy-role.txt](evidence/kb12-blast-radius-deploy-role.txt).

Kịch bản 11 chứng minh **không ai assume được** role deploy. Đó là nửa thứ nhất.
Nửa thứ hai là câu hỏi ngược lại, và nó độc lập: *giả sử* có người assume được —
GitHub bị chiếm, hoặc một workflow trên `main` bị sửa — thì role đó làm được
những gì? Một role tối thiểu đúng nghĩa phải có câu trả lời hẹp, và phải đo được
chứ không phải suy luận từ việc đọc policy bằng mắt.

Đo bằng `iam simulate-principal-policy` trên policy THẬT đang gắn:

    ── PHẢI ĐƯỢC PHÉP ──
    rds:CreateDBSnapshot   (snapshot:pre-migrate-test)              allowed
    ecs:UpdateService      (service/hushstore/hushstore-api)        allowed
    ecr:PutImage           (repository/hushstore-api)               allowed
    ecs:RunTask            (task-definition/hushstore-migrator:1)   allowed
                           + context ecs:cluster = .../cluster/hushstore

    ── PHẢI BỊ TỪ CHỐI ──
    autoscaling:SetDesiredCapacity (autoScalingGroupName/hushstore-asg)  implicitDeny
    rds:StartDBInstance            (db:hushstore-db-tf)                  implicitDeny
    s3:GetObject                   (tfstate/prod/terraform.tfstate)      implicitDeny
    ecs:RunTask                    (task-definition/hushstore-api:1)     implicitDeny
    ssm:GetParameter               (parameter/hushstore/prod/db-password) implicitDeny
    iam:PassRole                   (task-execution-seeder-role)          implicitDeny
    elasticloadbalancing:CreateLoadBalancer                              implicitDeny

    ── điều kiện ecs:cluster không phải trang trí ──
    ecs:RunTask trên migrator + context cluster = hushstore       -> allowed
    ecs:RunTask trên migrator + context cluster = cluster-khac    -> implicitDeny

Bốn dòng `implicitDeny` đầu là bốn ràng buộc thiết kế của Phase 2, và ở đây chúng
đứng ở tầng quyền chứ không chỉ ở comment trong workflow:

- **Không tự bật hạ tầng tốn phí.** `autoscaling:SetDesiredCapacity` và
  `rds:StartDBInstance` là hai đường duy nhất để một pipeline tự làm phát sinh
  $0.1954/giờ. Không có chúng thì "pipeline không tự bật hạ tầng" là một sự thật
  về quyền, không phải một lời hứa. `modules/cicd/tests/cicd.tftest.hcl` có
  assert canh cả danh sách này.
- **Không đọc được tfstate.** State chứa master password của RDS ở dạng
  plaintext (`random_password` luôn nằm trong state — bản chất của Terraform).
- **Chỉ chạy được task migrator, không chạy được task api.** `ecs:RunTask` bị
  ghim theo ARN `task-definition/hushstore-migrator:*`. Nếu ghim rộng hơn, một
  pipeline bị chiếm có thể chạy một task api với biến môi trường tuỳ ý.
- **Không pass được execution role của seeder.** Đó là role DUY NHẤT đọc được
  `db-password` (xem mục 2.3), nên đây chính là ranh giới giữ cho mật khẩu DB
  ngoài tầm với của pipeline.

`iam:PassRole` được cấp cho ba role của ECS task và **chỉ** ba role đó, kèm
condition `iam:PassedToService = ecs-tasks.amazonaws.com` — thiếu condition đó
thì role đi được vào EC2 hoặc Lambda, không chỉ vào ECS task.

### Một quan sát về phương pháp: thiếu context key thì kết quả "an toàn" là GIẢ

Dòng cuối bảng trên đáng đọc kỹ: cùng một `ecs:RunTask` trên cùng một task
definition, đổi context `ecs:cluster` sang một cluster khác thì thành
`implicitDeny`. Nghĩa là condition trên `ecs:cluster` có tác dụng thật.

Nhưng có một cái bẫy: **nếu không truyền `--context-entries` thì simulator trả
`implicitDeny` cho cả trường hợp hợp lệ.** Nguyên văn JSON trong file bằng chứng
cho thấy lý do — `MissingContextValues: ["ecs:cluster", "iam:PassedToService"]`,
và simulator coi một condition key không được cung cấp là không thoả. Hệ quả cho
người kiểm sau: một phép thử simulate thiếu context sẽ báo "role này không chạy
được task nào cả" và nghe như một kết quả tốt, trong khi thực tế nó vừa bỏ qua
đúng con đường pipeline dùng mỗi ngày. Kết quả `implicitDeny` chỉ có nghĩa khi
biết chắc đã cung cấp đủ mọi context key mà policy đòi.

### Lệnh để chạy lại

```bash
ROLE=$(terraform -chdir=infra/tf/envs/prod output -raw github_deploy_role_arn)
CL=arn:aws:ecs:ap-southeast-1:<account>:cluster/hushstore

# Phải allowed:
aws iam simulate-principal-policy --policy-source-arn "$ROLE" \
  --action-names ecs:RunTask \
  --resource-arns arn:aws:ecs:ap-southeast-1:<account>:task-definition/hushstore-migrator:1 \
  --context-entries "ContextKeyName=ecs:cluster,ContextKeyType=string,ContextKeyValues=$CL" \
  --query 'EvaluationResults[0].EvalDecision' --profile hushstore --no-cli-pager

# Phải implicitDeny (đổi migrator thành api, giữ nguyên mọi thứ khác):
aws iam simulate-principal-policy --policy-source-arn "$ROLE" \
  --action-names ecs:RunTask \
  --resource-arns arn:aws:ecs:ap-southeast-1:<account>:task-definition/hushstore-api:1 \
  --context-entries "ContextKeyName=ecs:cluster,ContextKeyType=string,ContextKeyValues=$CL" \
  --query 'EvaluationResults[0].EvalDecision' --profile hushstore --no-cli-pager
```

---

## 8. Bốn phát hiện mới từ lần chạy lại

Lần đo trên account cũ không có bốn thứ này. Chúng đáng ghi vì hai cái đầu là
**giới hạn thật của hệ thống**, còn hai cái sau là **bằng chứng mạnh hơn** cho
những gì báo cáo đã khẳng định.

### 8.1. Security group `default` của VPC mở mọi port từ chính nó

Khi liệt kê **mọi** rule ingress trong region, bảng có hai dòng
`protocol = -1, from = -1, to = -1`. Dấu `-1` nghĩa là **mọi protocol, mọi
port** — tức **có bao gồm 22**. Truy ra cả hai:

| Security group | Thuộc VPC |
|---|---|
| `sg-0554e1f7ac63016d0` | `vpc-0b84a98c407cd4136` — **VPC của dự án** |
| `sg-0c963910a01aa5522` | `vpc-056b9396279627317` — default VPC của account |

Cả hai là security group **`default`**. AWS tự tạo một cái cho **mỗi** VPC và
**không cho xoá**; mặc định của nó là "cho phép mọi traffic từ chính nó".
Terraform của dự án không quản lý chúng.

Câu hỏi đúng phải hỏi là: *có gì đang dùng chúng không?*

```
aws ec2 describe-network-interfaces --filters group-id=<hai SG đó>
  -> 0
```

**0 ENI.** Không resource nào nằm trong chúng, nên **không có bề mặt tấn công
thật** — ba security group của dự án đều có 0 rule phủ port 22, và đó là những
SG thực sự được gắn.

**Nhưng rủi ro còn lại là thật và dự án chưa bịt:** nếu sau này ai launch một
instance mà **không chỉ định** security group, AWS gán default SG cho nó — và
instance đó lập tức nằm trong một SG mở mọi port từ chính nó. Cách bịt đúng là
đặt rule của default SG về rỗng bằng Terraform (`aws_default_security_group`
với khối ingress/egress trống) hoặc dùng SCP. Đã ghi vào phần giới hạn.

Đáng nói thêm về phương pháp: phát hiện này chỉ lộ ra khi liệt kê **mọi** rule
trong region rồi mới lọc, thay vì chỉ kiểm ba SG mình biết. Kiểm cái mình biết
thì chỉ xác nhận được cái mình biết.

### 8.2. Egress của chính EC2 bị chặn ở port 123 — và đồng hồ vẫn đúng

Trong 2264 bản ghi `REJECT`, có 8 bản ghi mà **nguồn là chính container
instance** (`10.20.11.251`), đích là các IP của AWS, port **123 (NTP)**:

```
src=10.20.11.251  dst=52.207.222.50   dport=123
src=10.20.11.251  dst=54.81.127.33    dport=123
src=10.20.11.251  dst=3.94.91.31      dport=123
...
```

Nguyên nhân: `sg-web` egress chỉ mở **80, 443, 1433**. Không có 123. Nên mỗi lần
`chrony` thử danh sách NTP **công khai** mặc định của Amazon Linux là bị chặn.

**Đây không phải sự cố, và cũng không phải lỗ hổng.** Đồng hồ hệ thống vẫn đúng
vì `chrony` dùng được đường **chính**: Amazon Time Sync ở `169.254.169.123` — địa
chỉ *link-local*, không đi qua route table, không qua NAT, nên không cần rule nào
và **không xuất hiện trong Flow Logs**.

Bằng chứng gián tiếp nhưng chắc chắn rằng đồng hồ đúng:

1. Task migrator kết nối RDS với `Encrypt=True;TrustServerCertificate=False`,
   tức **có xác thực** certificate. Lệch giờ sẽ làm cert bị coi là chưa hiệu lực
   hoặc đã hết hạn → thất bại. Nó **exit 0**.
2. ECS agent pull được image từ ECR. Request tới AWS được ký **SigV4** và bị từ
   chối nếu lệch quá ~15 phút. Pull **thành công**.

Hai điều đó không thể đúng nếu đồng hồ sai.

Ghi lại ở đây để không ai đọc Flow Logs rồi tưởng port 123 là một sự cố mạng —
đó là **hệ quả có ý** của egress tối thiểu.

### 8.3. Flow Logs bắt được máy quét thật từ Internet

Ngoài đợt nmap của nhóm, trong 30 phút Flow Logs ghi được các đợt dò **không mời**
từ hàng chục IP lạ:

| Port bị dò | Số bản ghi | Port đó thường là gì |
|---|---|---|
| **23** | 15 | Telnet — giao thức không mã hoá, mục tiêu số một của botnet IoT |
| 123 | 8 | NTP (gồm cả egress của chính ta ở mục 8.2) |
| 0 | 8 | Dò bằng gói tin dị dạng |
| 53 | 6 | DNS |
| **22** | 5 | SSH |
| 892, 808, 6002 | 2 mỗi port | RPC và cổng linh tinh |

Ví dụ: `165.22.18.23` dò port 23 năm lần; `181.160.148.152` dò **cả 22 và 23**.

Giá trị của phần này với đề bài: nó chứng minh mệnh đề *"mở port là bị dò ngay"*
bằng **lưu lượng thật trong 30 phút**, không phải bằng lập luận. Và nó cho thấy
port 22 — cái mà hệ thống cố ý không mở — **đang thực sự bị dò**.

### 8.4. Hạ tầng dựng lại từ Terraform cho kết quả bảo mật y hệt

Đây là kết quả bao trùm, và là thứ chỉ có được nhờ đo hai lần trên hai account.

| | Account cũ `667836586836` | Account mới `551897327153` |
|---|---|---|
| Ngày đo | 2026-08-20 và 08-23 | 2026-08-24 |
| Port mở trên ALB | 80, 443 (998 filtered) | 80, 443 (998 filtered) |
| Port 8080 qua ALB | timeout exit 28 | timeout exit 28 |
| SSH mọi hướng | không kết nối được | không kết nối được |
| Rate limit login | req 1-5 → 400, 6-20 → 429 | req 1-5 → 400, 6-20 → 429 |
| Host lạ | 403 | 403 |
| NACL DENY 1 IP | A timeout / B 200 | A timeout / B 200 |
| Host đọc secret | explicitDeny | explicitDeny (cả 4 action) |
| Giả mạo OIDC | InvalidIdentityToken | InvalidIdentityToken (3/3) |
| **Kết quả** | **12/12 đạt** | **12/12 đạt** |

Hạ tầng mới được dựng từ **cùng một mã Terraform**, trên một account trắng, với
VPC/subnet/ALB/RDS hoàn toàn mới. Không có bước cấu hình tay nào ngoài hai record
DNS để validate certificate.

Nghĩa là: **tính chất bảo mật của hệ thống nằm trong mã, không nằm trong một lần
cấu hình may mắn.** Một báo cáo chỉ đo một lần trên một account không phân biệt
được hai điều đó — và đó chính là luận điểm mà "hạ tầng như mã" (Infrastructure
as Code) tồn tại để bảo đảm.

Kèm theo, việc dựng lại cũng chứng minh đường dữ liệu hoạt động: migration EF
Core **exit 0**, seeder **exit 0** và đếm lại được `AppRoles=3`, `AppUsers=1`,
`Categories=18`, `Manufacturers=21`, `Products=49`, `ProductVariants=52`; cả hai
target group của ALB **healthy**, tức `/health/ready` chạm được database thật.
Bằng chứng ở [`kb00-migration-va-seed.txt`](evidence/acc-551897327153/kb00-migration-va-seed.txt).
