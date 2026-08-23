# Báo cáo kiểm thử bảo mật — HushStore trên AWS

**Đề tài 513** — "Đầu ra" số 2 của đề bài: chứng minh các rule được mở theo
nguyên tắc tối thiểu và **đã thực sự ngăn được tấn công**.

| | |
|---|---|
| Ngày kiểm thử | Kịch bản 1-10: 2026-08-20 (00:15 – 01:10 UTC) · Kịch bản 11: 2026-08-23, sau khi Phase 2 dựng xong hai IAM role của pipeline |
| Máy tấn công | Laptop macOS, IP công khai `42.1.89.156` |
| Công cụ | `nmap 7.991`, `curl`, `nc`, `openssl`, `python3 socket`, `aws iam simulate-principal-policy`, `aws sts assume-role-with-web-identity` |
| Mục tiêu | ALB `hushstore-alb-395664435.ap-southeast-1.elb.amazonaws.com` (`54.251.216.176`, `54.254.121.95`) · EC2 `10.20.11.22` · RDS `10.20.21.81` |
| Hạ tầng | Terraform, commit tại thời điểm test — xem `git log` |
| Output thô | [`docs/evidence/`](evidence/) — mọi số trong báo cáo này lấy từ đó, không có số nào viết tay |

Kiểm thử thực hiện trên hạ tầng **do chính nhóm sở hữu**, trong phạm vi đề bài.

---

## 1. Tóm tắt kết quả

| # | Kịch bản | Kỳ vọng | Kết quả | Rule chịu trách nhiệm | Bằng chứng |
|---|---|---|---|---|---|
| 1 | Quét port ALB | chỉ 80, 443 mở | ✅ 80 + 443 open, 998 port `filtered` | `sg-alb` ingress | `kb01-nmap-alb.txt` |
| 2 | Kết nối IP riêng của EC2 | không có đường đi | ✅ timeout 10s, cả `:8080` và `:22` | không public IP + app subnet không route ra IGW | `kb03-rds-tu-internet.txt` |
| 3 | Kết nối trực tiếp RDS | timeout | ✅ endpoint công khai phân giải ra **IP riêng** `10.20.21.81`, timeout | `publicly_accessible=false` + `sg-rds` + `nacl-db` | `kb03-rds-tu-internet.txt` |
| 4 | Gọi port ứng dụng `:8080` trên ALB | không kết nối được | ✅ timeout (exit 28), **không phải** refused | ALB chỉ có listener 80/443 | `kb04-05-port-ung-dung-va-ssh.txt` |
| 5 | SSH vào mọi hướng | refused | ✅ `No route to host`; hệ thống có **0 key pair**, **0 SG rule port 22** | không có rule 22 + `nacl-app` rule 90 DENY | `kb04-05-port-ung-dung-va-ssh.txt` |
| 6 | Brute-force `/api/auth/login` | 429 từ request 6 | ✅ req 1-5 → 400, **req 6-20 → 429** | rate limiter `LoginRateLimit` | `kb06-rate-limit-login.txt` |
| 7 | Host header lạ | không lọt sang backend | ✅ `evil.com` → 403, `www` → 403, tên DNS thô của ALB → 403 | ALB listener rule + default `fixed-response` | `kb07-host-allowlist.txt` |
| 8 | NACL DENY theo IP | chặn đúng 1 IP | ✅ **A/B từ cùng một máy**: đường trực tiếp timeout, đường qua Cloudflare 200 | `nacl-public` rule 50 | `kb08-nacl-deny-theo-ip.txt` |
| 9 | VPC Flow Logs `REJECT` | có bản ghi khớp | ✅ khớp cả 3 nhóm: máy tấn công, egress EC2, scanner ngoài | Flow Logs `REJECT`, gom 600s | `kb09-flowlog-reject.txt` |
| 10 | Bán kính ảnh hưởng của IAM role | mỗi role chỉ thấy phần của mình | ✅ ma trận 12 phép thử, host bị **explicitDeny** | 5 role tách biệt | `kb10-blast-radius-iam.txt` |
| 11 | Giả mạo OIDC assume-role | từ chối | ✅ JWT tự ký **đủ mọi claim** trust policy đòi (`aud`, `sub`, `iat`/`exp` hợp lệ) vẫn bị `InvalidIdentityToken` — AWS chặn ở bước **xác thực chữ ký**, trước cả khi xét trust policy | trust condition `StringEquals` trên `aud` + `sub` | `kb11-gia-mao-oidc.txt` |

**11/11 kịch bản đã có bằng chứng.** Kịch bản 11 chạy được sau khi Phase 2
`apply` xong hai IAM role. Kết quả đáng chú ý: lỗi trả về là
`InvalidIdentityToken`, **không phải** `AccessDenied` — và đó là kết quả mạnh
hơn. `AccessDenied` nghĩa là "token của bạn thật, nhưng quyền không đủ";
`InvalidIdentityToken` ở đây nghĩa là AWS không tin token ngay từ đầu, nên
claim `sub` trong đó không hề được xét. Giả mạo `sub` là vô nghĩa khi không
giả mạo được chữ ký của GitHub. Xem mục 6.

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
`42.1.89.156/32`. Rồi gọi cùng một website bằng **hai đường, từ cùng một máy**:

| Đường | IP nguồn AWS nhìn thấy | Kết quả |
|---|---|---|
| A. `curl https://alb.hushstore.io.vn/` (DNS only → thẳng vào ALB) | `42.1.89.156` | **timeout, exit 28** |
| A. `curl --resolve hushstore.io.vn:443:54.251.216.176` | `42.1.89.156` | **timeout, exit 28** |
| B. `curl https://hushstore.io.vn/` (qua Cloudflare proxy) | IP của Cloudflare | **HTTP 200**, 3589 bytes |
| B. `curl https://api.hushstore.io.vn/health/ready` | IP của Cloudflare | **HTTP 200** |

Cùng một lệnh `curl`, cùng một laptop, cùng một domain. Khác biệt duy nhất là
IP nguồn mà AWS nhìn thấy. Rule 50 chặn ở **tầng network**, trước khi gói tin
kịp chạm tới ALB.

*(Sau khi thu bằng chứng, `enable_deny_demo` đã tắt lại — mặc định là `false`.)*

### 2.3. Bán kính ảnh hưởng: mỗi role chỉ thấy đúng phần của mình

Đo bằng `iam simulate-principal-policy` — chạy trên **policy thật đang gắn**,
do chính bộ đánh giá của AWS phán quyết, không phải do người viết báo cáo suy luận:

| Role | `db-password` | `connection-string` | `s3:PutObject` ảnh | `rds:DeleteDBInstance` |
|---|---|---|---|---|
| `container-instance` (EC2 host) | **explicitDeny** | — | implicitDeny | implicitDeny |
| `task-app` (container API runtime) | implicitDeny | — | **allowed** | implicitDeny |
| `task-execution` (api/web/migrator) | implicitDeny | **allowed** | — | — |
| `task-execution-seeder` | **allowed** | implicitDeny | — | — |

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

---

## 3. Chi tiết từng kịch bản

Xem output thô trong [`docs/evidence/`](evidence/). Mỗi file chứa nguyên văn
lệnh đã chạy và nguyên văn kết quả.

### 3.2 / 3.3 — Tier private không tiếp cận được từ internet

```
RDS qua tên DNS công khai    → 10.20.21.81:1433   TIMEOUT sau 10s (no route)
RDS qua IP riêng trực tiếp   → 10.20.21.81:1433   TIMEOUT sau 10s (no route)
EC2 container instance       → 10.20.11.22:8080   TIMEOUT sau 10s (no route)
EC2 container instance :22   → 10.20.11.22:22     TIMEOUT sau 10s (no route)
```

Điểm đáng ghi: endpoint RDS **là tên DNS công khai** — ai cũng phân giải được.
Nhưng nó phân giải ra `10.20.21.81`, một địa chỉ riêng RFC1918 không định tuyến
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

**Nhóm A — gói tin từ máy tấn công bị chặn.** Đây là bằng chứng tầng network
cho kịch bản 8, hoàn toàn độc lập với kết quả `curl`:

```
42.1.89.156 -> 10.20.0.135  dport=443   proto=6  REJECT
42.1.89.156 -> 10.20.0.135  dport=80    proto=6  REJECT
42.1.89.156 -> 10.20.1.38   dport=8080  proto=6  REJECT
42.1.89.156 -> 10.20.1.38   dport=22    proto=6  REJECT
42.1.89.156 -> 10.20.1.38   dport=1433  proto=6  REJECT
42.1.89.156 -> 10.20.0.135  dport=3389  proto=6  REJECT
```

Chú ý **port 80 và 443 cũng bị `REJECT`**. Bình thường hai port này được
`ACCEPT` — chúng bị chặn ở đây chỉ vì rule 50 DENY theo IP nguồn. `10.20.0.135`
và `10.20.1.38` là ENI của ALB trong hai subnet public.

**Nhóm B — egress của EC2 bị chặn.** `sg-web` egress chỉ cho `1433`, `80`, `443`:

```
10.20.11.22 -> 52.207.222.50   dport=123  proto=17  REJECT
10.20.11.22 -> 54.210.225.137  dport=123  proto=17  REJECT
10.20.11.22 -> 3.86.4.106      dport=123  proto=17  REJECT
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
| **Host không ra được NTP công khai** | `sg-web` egress chỉ cho `1433`/`80`/`443` — hệ quả cố ý của egress tối thiểu | Không cần sửa: Amazon Linux dùng Amazon Time Sync ở `169.254.169.123` (link-local, không qua NAT). Ghi lại để không ai nhầm các bản ghi `REJECT` port 123 là sự cố |
| **IAM user `athena232`** có `AdministratorAccess` trực tiếp, **không MFA** | Sót lại từ lúc khởi tạo account | Chủ dự án đã quyết định giữ nguyên. Đây là lỗ hổng lớn nhất còn lại của account và đã ghi vào mục việc còn nợ của runbook |

---

## 5. Cách tái lập

```bash
aws sso login --profile hushstore
cd infra/tf/envs/prod
# bật stack theo đúng thứ tự trong docs/terraform-runbook.md
# (RDS phải available TRƯỚC khi bật service)

# kịch bản 8 cần thêm:
#   enable_deny_demo = true   trong terraform.tfvars
# kịch bản 9 cần thêm:
#   enable_flow_logs = true   (bản ghi xuất hiện sau ~10 phút, gom mỗi 600s)

# Nhớ tắt lại cả hai sau khi thu bằng chứng — mặc định đều là false.
```

Mọi lệnh tấn công nằm nguyên văn trong các file `docs/evidence/kb*.txt`, kèm
nguyên văn output. Không có số nào trong báo cáo này được viết tay.

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
