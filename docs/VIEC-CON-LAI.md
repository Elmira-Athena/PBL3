# Việc còn lại — từ đây đến hết dự án

**Bổ sung WAF 2026-10-28:** chọn [Cloudflare Free](waf-cloudflare.md), với root `infra/cloudflare/` riêng. Khi bật `enable_cloudflare_origin_lockdown`, SG ALB chỉ nhận IP Cloudflare và listener yêu cầu Host **cùng** header bí mật. Flag mặc định false để chuyển NACL an toàn; các bảng SG Internet phía dưới là cấu hình trước lockdown. **Chưa apply hoặc kiểm thử runtime WAF.**

> **Viết cho một agent không có ngữ cảnh gì.** File này tự chứa. Đọc hết trước khi
> gõ dòng lệnh đầu tiên.
>
> **Đo ngày 2026-10-26.** Mọi con số dưới đây đo bằng lệnh thật tại thời điểm đó,
> không lấy từ trí nhớ hay từ tài liệu khác.

> **Ghi chú revision mạng (2026-10-26, sửa code/tài liệu local; chưa đo lại AWS):**
> Bỏ 3 custom NACL, dùng default ACL allow-all do AWS quản lý cho cả 6 subnet.
> SG giữ chuỗi Internet TCP 80/443 → ALB → web TCP 80/8080 → RDS TCP 5432;
> web outbound TCP 80/443 được giữ, default SG rỗng. ECR endpoints tuỳ chọn
> chỉ nhận TCP 443 từ SG web; không bật endpoint hay thêm tài nguyên tính phí.
> Không còn yêu cầu `my_ip` / `enable_deny_demo` hoặc demo chặn IP bằng ACL
> trong revision hiện tại. Bẫy số 3 ở mục 7 là bản ghi cũ, không phải hướng dẫn
> thực hiện nữa. Dùng [quy trình migration hai giai đoạn](nacl-to-sg-migration.md)
> trước khi apply từ state cũ. Các số đo và trạng thái nhánh dưới đây được giữ
> nguyên tại thời điểm đo, không chứng minh revision này đã chạy trên AWS.

---

## 0. Luật của repo này — vi phạm là tạo ra lỗi mới

Bốn luật sinh ra từ lỗi có thật. Đọc `CLAUDE.md` để biết đủ, nhưng bốn cái này
quyết định cách bạn **đo** và **kết luận**:

1. **`grep` chỉ chứng minh HÌNH DẠNG code, không chứng minh HÀNH VI.** Mọi lỗi
   đúng đắn dữ liệu tìm thấy ở repo này đều trả HTTP **200**.
2. **`KHÔNG KẾT LUẬN` ≠ `ĐẠT`.** Kịch bản bị rate limiter chặn sẽ thoả mọi bất
   biến vì code cần đo chưa chạy. Đó là bằng chứng an toàn **giả**.
3. **Một ✅ không là bằng chứng an toàn; một 🔴 LÀ bằng chứng hỏng.** Bất đối xứng
   này áp cho mọi bảng trong `docs/evidence/`.
4. **Không sửa số trong một bản ghi đã đo.** Sửa số trong bản ghi là làm giả bằng
   chứng. Đo lại thì ghi bản **mới**, giữ bản cũ để đối chiếu.

⚠️ **Tài liệu của repo này có tiền sử nói ngược code.** Đã phát hiện hàng chục
lần. **Kiểm bằng lệnh trước khi tin bất kỳ con số nào — kể cả số trong file này.**

---

## 1. Dự án là gì

**HushStore** — web bán linh kiện máy tính. ASP.NET Core 10 Web API + Blazor WASM
(MudBlazor) + **PostgreSQL 17**. Triển khai lên AWS bằng Terraform (đề tài 513).

```
Core ← Infrastructure ← Service ← API
 ↑                                 ↑
 └────────── Shared ───────────────┘
             ↑
           Client
```

Hạ tầng: ECS on EC2 (`bridge` + static host port) · ALB + ACM · RDS PostgreSQL 17
Multi-AZ `db.t4g.micro` · NAT ×2 · SSM Parameter Store · S3 ×3 · ECR ×4 · Lambda
cost guard + EventBridge Scheduler · GitHub OIDC · state ở S3.

---

## 2. Trạng thái đo được (2026-10-26)

| | |
|---|---|
| Nhánh | `main` @ `12b5b8b`, cây làm việc sạch |
| **CI** | 🔴 **ĐỎ** — `dotnet build` ra `0 Error(s)` nhưng **cổng NuGet chặn** |
| AWS | RDS `stopped` · ALB 0 · NAT 0 · EC2 0 — chỉ chảy tiền storage |
| `terraform test` | 109/109 trên 8 module |
| Cổng rò rỉ thông báo lỗi | `Sạch [all]: 255 file, 0 chỗ`, exit 0 |
| `GetFromJsonAsync` trong `src/Client/` | **0** (mục 🅹 đã merge ở `993e44c`) |
| `hushstore.io.vn` qua DNS công khai | **530** — chưa sửa |

**Account AWS:** `551897327153`, IAM user `hushstore-ops` (**không** SSO),
region `ap-southeast-1`, profile `hushstore`.

### Lệnh kiểm nhanh mọi thứ trên

```bash
git log --oneline -3 && git status --short
gh run list --workflow=CI --limit 1
bash infra/tf/scripts/status.sh
bash devops/scripts/check-error-message-leaks.sh      # phải: 0 chỗ, exit 0
grep -rn "GetFromJsonAsync" src/Client/ | wc -l       # phải: 0
curl -s -o /dev/null -w "%{http_code}\n" https://hushstore.io.vn
```

---

## 3. 🔴 CHẶN — làm trước mọi thứ khác

### B1. CI đang đỏ — cổng NuGet chặn 13 advisory

`dotnet build` **sạch** (`0 Error(s)`). Thứ chặn là
`devops/scripts/check-vulnerable-packages.sh`.

"13" là đếm theo project × advisory. Thật ra chỉ **hai gói**:

| Gói | Bản hiện tại | Ghim ở đâu |
|---|---|---|
| `System.Security.Cryptography.Xml` | 10.0.10 | `src/Service/Service.csproj:32`, `src/Infrastructure/Infrastructure.csproj:45` |
| `System.Security.Cryptography.Pkcs` | 10.0.10 / 10.0.0 | **chưa ghim ở đâu cả** — hoàn toàn transitive |

Cả hai từng được ghim để vá CVE cũ; advisory **mới** vừa công bố trên chính
10.0.10. Dính ở 4 project: `API`, `Service`, `Infrastructure`, `tools/LoadProbe`.

**Cách làm:**
1. Tra bản nhỏ nhất đóng **hết** advisory của từng gói (một gói có thể dính nhiều
   advisory với ngưỡng vá khác nhau).
2. Nâng pin `Cryptography.Xml`, **thêm** pin `Cryptography.Pkcs` vào cả 4 `.csproj`,
   kèm comment nói rõ chuỗi phụ thuộc và khi nào xoá được — copy khuôn ở
   `src/Service/Service.csproj:22-32`.
3. `dotnet build PBL3.sln` rồi `bash devops/scripts/check-vulnerable-packages.sh`.

🚨 **Đừng "kiểm nhanh" bằng `dotnet list package --vulnerable` rồi tin mã thoát** —
lệnh đó trả `0` **kể cả khi tìm thấy lỗ hổng**. Đã đo.

⚠️ **Máy phát triển hiện KHÔNG có .NET SDK** (`dotnet` không tồn tại, đã tìm khắp
`/usr/local/share/dotnet`, `~/.dotnet`, homebrew). Hoặc cài SDK, hoặc đẩy lên nhánh
rồi để CI build hộ. **Công sức: nhỏ · Chặn: mọi PR.**

### B2. DNS công khai không vào được — và chưa ai từng đo nó

```
hushstore.io.vn      → 104.21.0.90, 172.67.150.201   (Cloudflare)  → HTTP 530
ALB trong hồ sơ 24/09: hushstore-alb-718170503...     (đã bị destroy)
```

**ALB đổi tên DNS MỖI LẦN `down.sh` rồi `up.sh`.** Bản ghi DNS ở Cloudflare đặt
bằng tay nên không tự đổi theo → Cloudflare không tìm thấy origin → 530 (lỗi 1016).

🚨 **Phát hiện quan trọng nhất của cả dự án về mặt vận hành:** mọi bằng chứng
`200` từ trước tới nay đều dùng `curl --resolve`, tức **bỏ qua DNS công khai**.
Xem nguyên văn `docs/evidence/acc-551897327153/2026-09-23/kb07-host-allowlist.txt`:

```
-- duong hop le qua --resolve --
hushstore.io.vn -> HTTP 200
```

Chúng chứng minh ALB + app + cert đúng. Chúng **KHÔNG** chứng minh người ngoài mở
được bằng trình duyệt. **Khoảng trống này chưa ai đo.** Nếu có buổi bảo vệ mà thầy
mở link, đây là thứ hỏng đầu tiên.

**Hai đường sửa:**
- *Tạm:* sửa tay 2 bản ghi CNAME trong Cloudflare mỗi lần bật. **Cần quyền
  Cloudflare — không nằm trong repo, phải hỏi người dùng.**
- *Gốc:* Route 53 alias record do Terraform quản lý, hoặc một bước trong `up.sh`
  tự cập nhật DNS sau khi ALB lên. **Chưa có.** Công sức: vừa.

⚠️ Nếu bật proxy Cloudflare (mây cam), SSL/TLS mode phải là **Full (strict)** —
ALB có cert ACM thật. Để "Flexible" sẽ thành vòng lặp chuyển hướng.

### B3. Tiền rỉ đều đặn vì cost guard quét thưa hơn sự cố

Đo từ `aws rds describe-events`:

```
10-01 03:14 started (tự bật) → 10-01 17:11 stopped    hở 13h56
10-08 17:15 started          → 10-09 17:11 stopped    hở 23h55
10-16 17:14 started          → 10-17 17:11 stopped    hở ~24h
```

AWS tự bật lại RDS `stopped` sau **7 ngày**. Cost guard quét **1 lần/ngày** lúc
00:00 giờ VN (17:00 UTC) nên hở tối đa 24 giờ. Lần 08/10 nó bật lại lúc 17:15,
**trễ 4 phút sau lần quét**, phải đợi trọn một ngày.

Tiền tự-bật-lại (~**$8/tháng**) đang **nhiều hơn** tiền storage (~$5,8/tháng).

**Sửa một dòng** trong `infra/tf/envs/prod/terraform.tfvars`:
```hcl
stop_cron = "cron(0 0,6,12,18 * * ? *)"   # 4×/ngày thay vì 1×
```
Hở 24h → 6h. Vẫn trọn free tier (Scheduler 14M lượt/tháng, Lambda 1M request).
Lưới an toàn **hoạt động đúng thiết kế** — nó chỉ quét thưa. **Công sức: nhỏ.**

---

## 4. ❓ Hai thứ KHÔNG AI BIẾT — phải hỏi người dùng

Không tự quyết hai mục này. Không có dữ liệu nào trong repo trả lời được.

### H1. Đề bài 513 yêu cầu những đầu ra nào?

Repo **chỉ nhắc tên "Đầu ra số 2"** (báo cáo kiểm thử bảo mật — đã xong). Đầu ra
số 1, số 3… là gì thì **không file nào ghi**. Không thể kết luận "đồ án xong chưa"
mà không có danh sách này.

### H2. Đợt 6 — "7 hình" là 7 hình gì?

Dòng duy nhất còn lại: `docs/bat-dau-phien-moi.md:175` — *"đo tải + 7 hình + báo
cáo"*. Kế hoạch gốc trỏ tới `/Users/ml/.claude/plans/hi-n-t-i-t-i-ang-memoized-quill.md`
— **đường dẫn không tồn tại trên máy này**. Repo cũng **không có công cụ sinh tải
nào** (0 k6 / JMeter / Artillery); `tools/LoadProbe/` cố ý **không phải** công cụ
đo hiệu năng.

**H2 chặn toàn bộ đợt 6.** Hỏi sớm.

---

## 5. Việc còn lại theo nhóm

### 5A. Đợt 4 — nửa hạ tầng

Nửa code **đã xong và đã đo** (`ICacheService`, `ShutdownTimeout = 45`,
DataProtection → SSM). Đã có trong `infra/tf/`: `stopTimeout = 90`
(`modules/ecs/taskdef.tf:114,178`), `ECS_CONTAINER_STOP_TIMEOUT=90s`
(`modules/ecs/user_data.sh.tftpl:30`), connection pool (`modules/data/main.tf:224-232`).

| # | Việc | Công sức | Chặn |
|---|---|---|---|
| 4.1 | `deregistration_delay` 5 → 30 (`modules/alb/alb.tf:58,83`) + viết lại `modules/alb/tests/alb.tftest.hcl:167-170` | nhỏ | nên gộp vào 5.5 |
| 4.2 | IAM task role đọc `/hushstore/prod/dataprotection/*` + env `DataProtection__SsmPrefix` vào taskdef | vừa | **chặn 5.5** |
| 4.3 | Sửa comment nói ngược ở `src/API/Program.cs:183-191` | nhỏ | — |

**Về 4.1:** một test thứ hai cũng đọc con số này —
`modules/ecs/tests/taskdef.tftest.hcl:265-276` khẳng định
`dereg + ShutdownTimeout < stopTimeout`. Tin tốt: `30 + 45 = 75 < 90` vẫn xanh, nên
chỉ `alb.tftest.hcl:167` đỏ.

🚨 **Về 4.2 — env var và chính sách IAM PHẢI vào CÙNG một lần deploy.** Đã đo có ca
đối chứng: đặt env var mà task role chưa có quyền thì app **vẫn khởi động**,
`health/live` xanh, ECS coi task healthy — nhưng `IDataProtector.Protect` ném
`CryptographicException`. Chỉ vài đường 500, **không dashboard nào đỏ**.
⚠️ `ssm:DescribeParameters` không hỗ trợ resource-level — thử **không** có nó trước.
⚠️ Đừng chạm khối DENY ở `modules/ecs/iam.tf:65-83` — đó là deliverable bảo mật.

### 5B. Đợt 5 — co giãn hai tầng

**Mục tiêu:** 2 task trên 2 instance ở 2 AZ, deploy 0 downtime, số máy tự co giãn.

**Bước 1 ✅ XONG, đã đo, có ca đối chứng đảo được** (`866015e` + `79fff46`). Làm
bằng **PostgreSQL**, không phải Redis như kế hoạch khuyến nghị: bảng
`RateLimitCounters`, atomic upsert `ON CONFLICT`. Phủ **4/6 policy** (`Login`,
`Register`, `Refresh`, `Lookup`); `GlobalLimiter` và `PublicReadRateLimit` cố ý giữ
in-process. Đo bằng LoadProbe **S11** trên 2 replica thật: 5 qua / 15 chặn, ca đối
chứng âm → *KHÔNG KẾT LUẬN*, khôi phục → lại 5/15.

**Xuất phát điểm đo được hôm nay:** `aws_appautoscaling_*` = **0** ·
`aws_autoscaling_policy` = **0** · `managed_scaling = "DISABLED"`
(`modules/ecs/cluster.tf:193`) · `min_size = 0` (`:106`) · `max_instance_count = 1`
· `rate_limiter_is_distributed = false`. **Ngoài bước 1, chưa chạm một dòng hạ tầng.**

| # | Việc | Công sức | Chặn |
|---|---|---|---|
| 5.2 | **Trùng hoàn toàn 4.2** — DataProtection key ring dùng chung | vừa | → chặn 5.5 |
| 5.3 | `SHOW max_connections;` trên **RDS thật**, so với `max_instance_count × Maximum Pool Size (30)`, biên ≥ 20% | nhỏ | **cần cửa sổ AWS có người canh** |
| 5.4 | Sửa `down.sh` + `min_size` **TRƯỚC** 5.5 | vừa | **chặn cứng 5.5** |
| 5.5 | Bật co giãn hai tầng | **lớn**, chạy trên AWS (~$0,27/h) | bị chặn bởi 5.2 + 5.4 |

🚨 **Vì sao 5.4 phải đi trước 5.5.** `infra/tf/scripts/down.sh:76` tắt tiền bằng
`hs_tfvar_set instance_count 0`, tức đi qua Terraform. Bước 5.5 đặt
`ignore_changes = [desired_capacity]` ⇒ **dòng đó thành no-op IM LẶNG**. Tiền chảy
mà không lỗi nào báo.

**Nội dung 5.5:** `cluster.tf:180-193` `managed_scaling` DISABLED → ENABLED · thêm
`lifecycle { ignore_changes = [desired_capacity] }` cho ASG · thêm
`aws_appautoscaling_target` + `_policy` cho 2 service với **`min_capacity = 0`**
(bắt buộc) · `tfvars`: `max_instance_count`, `rate_limiter_is_distributed = true`.

**Ba chỗ kế hoạch đợt 5 nói ngược code — kiểm trước khi theo nó:**
1. Kế hoạch đề nghị `max_instance_count = 3`, nhưng `modules/ecs/variables.tf:76-79`
   có validation **cứng** `>= 1 && <= 2`. Muốn 3 phải sửa chính validation đó.
2. Kế hoạch chỉ liệt `alb.tftest.hcl:167` sẽ đỏ. Nhưng
   `modules/ecs/tests/cluster.tftest.hcl:199-209` khẳng định
   `managed_scaling == "DISABLED"` **"ở MỌI trần"** — cũng đỏ, cũng phải viết lại.
3. `docs/ke-hoach-dot-5-autoscaling.md` **chưa cập nhật lần nào** kể từ khi tạo. Nó
   vẫn trình bày bước 1 như việc phải làm và vẫn khuyến nghị Redis. **Dễ làm phiên
   sau đi dựng ElastiCache thừa.**

### 5C. Đợt 6 — đo tải + báo cáo

**Bị chặn bởi H2** (không có đặc tả) **và bởi toàn bộ đợt 5**.

| # | Việc | Công sức | Chặn |
|---|---|---|---|
| 6.0 | Dựng lại đặc tả — hỏi người dùng | nhỏ | chặn mọi thứ còn lại |
| 6.1 | Chọn + dựng bộ sinh tải | vừa | sau 6.0 |
| 6.2 | Đo tải trên AWS 2 instance | lớn | **toàn bộ đợt 5** |
| 6.3 | Nửa "TRƯỚC" của bằng chứng khoá tài khoản không kẹt RAM một task | nhỏ, $0, local | không chặn |
| 6.4 | Viết báo cáo + 7 hình | lớn | sau 6.2 |

⚠️ **Một rủi ro cho luận điểm của báo cáo:** `ICacheService` có **0 chỗ gọi** trong
`src/Service` và `src/Infrastructure` — chỉ đăng ký DI ở `Program.cs:97` rồi để đó.
`CLAUDE.md` hứa *"API listing < 2 giây"* nhờ cache, nhưng **chưa có cache nào chạy**.
Muốn báo cáo vẽ được đường "caching giảm tải" thì phải viết chỗ gọi trước.

Tương tự: **read replica là hạ tầng chết** — `grep "connection-string-readonly" src/`
= 0, chỉ một `DbContext`, `modules/data/outputs.tf` xuất ARN mà không ai nối vào
taskdef.

### 5D. Nợ kiểm thử

| # | Việc | Công sức | Chặn |
|---|---|---|---|
| T1 | Ép **retry thật** (bơm lỗi transient). Chế độ hỏng *"`Add` hai lần sau retry"* mới bị bác bỏ **trong phạm vi các lần chạy đã thực hiện**, chưa tuyệt đối | vừa, local $0 | — |
| T2 | Nợ giao diện: 4 luồng đo bằng gọi API + JWT admin, **không** click qua Blazor ⇒ chưa chứng minh trang Razor bind đúng DTO | vừa, local $0 | — |
| T3 | **S04 + S07 tín hiệu yếu** — phụ thuộc thời điểm, chỉ có **1** lần quan sát ở cấu hình 2 instance. Chạy lặp và đếm tỉ lệ | nhỏ, local | — |
| T4 | **Mục 🅹 chưa đo bằng mắt** — ép một `400`, xác nhận câu tiếng Việt của server hiện nguyên văn trên UI | nhỏ | cần stack chạy |
| T5 | Phần còn lại cửa sổ đo đợt 7: **failover Multi-AZ** · **`ReplicaLag` + ca đối chứng cost guard khi có replica** · đối chiếu `HS_RATE_RDS_UP=0.098` với hoá đơn thật | vừa | **cửa sổ AWS có người canh** |

🚨 **Về T5 — tuyệt đối không bật `enable_read_replica` rồi để qua đêm.** Có replica
thì AWS **từ chối** stop primary ⇒ `down.sh` và cost guard mất tác dụng, mà RDS còn
tự bật lại sau 7 ngày. `down.sh` đã tự huỷ replica trước khi stop và `status.sh` in
một dòng đỏ khi thấy replica — **nhưng cả hai chỉ chạy khi có người gõ.** Bật → đo
→ tắt trong **cùng một phiên**.

### 5E. Tài liệu nói ngược code — đã xác minh bằng lệnh

Sửa khi tiện, nhưng **mục 1 và 2 nguy hiểm** vì tin vào chúng sẽ hành động sai:

| # | Tài liệu nói | Sự thật đo được |
|---|---|---|
| 1 🚨 | `CLAUDE.md`: *"Three roles: Admin, Employee, Customer"* | **4 role** — `Program.cs:627-629` seed thêm `Technician` (`RoleCode='KTV'`). *(Đã kiểm: KHÔNG phải lỗ hổng — `EmployeeService.cs:110` gán `Employee` trước, luôn luôn; `Technician` chỉ là nhãn cộng thêm.)* |
| 2 🚨 | *"Hai chỗ `ButtonType.Submit` trong `EditForm`"* | **4 chỗ** — thêm `Pages/Auth/Login.razor:32` và `Pages/Auth/Register.razor:39`. Ai tin "chỉ hai chỗ" rồi đổi Login/Register sang `<ActionButton>` sẽ **âm thầm gỡ** chống double-submit trên đúng hai đường đăng nhập/đăng ký — `ActionButton` **vô hiệu** với submit form (đã đo: 3 click → 3 request) |
| 3 | *"6 nút double-submit chưa nút nào được đo"* | **đã đo 2026-08-31**, 6/6, có ca đối chứng âm — `docs/evidence/ui/2026-08-31-6-nut-double-submit.md` |
| 4 | mục 🅹 *"chưa build, trên nhánh riêng"* | **đã merge** `993e44c`, CI build xanh |
| 5 | `Program.cs:183-191`: *"HAI SỐ CÒN LẠI CHƯA ĐƯỢC ĐỔI"*, *"biên là 0"* | 2/3 đã đổi từ `ee76b72`; biên hiện là **40s** |
| 6 | runbook: *"Default SG của VPC chưa khoá"* | **đã khoá**, có test `vpc.tftest.hcl:255` |
| 7 | runbook: *"3 package NuGet CVE High — để sau"* | đã vá đợt đó — nhưng **xem B1**, advisory mới đã tới |
| 8 | *"kỳ vọng `Sạch [all]: 249 file`"* | **255 file**. Nên ghi tiêu chí là "0 chỗ, exit 0", đừng ghim số file |
| 9 | *"`grep 'ex.Message'` đếm cả 41 chỗ relay đúng"* | nay **75** chỗ. Lập luận vẫn đúng, số đã cũ |
| 10 | `CLAUDE.md`: *"mười ba cái bẫy im lặng"* | file được trỏ tới ghi *"**Hai mươi** cái bẫy"* |
| 11 | *"một `InitialCreatePostgres` duy nhất"* | **2 migration** (thêm `AddRateLimitCounters`) |
| 12 | `terraform.tfvars:67-77`: *"rate limiter đếm trong RAM"* | 4/6 policy đã ở DB từ `866015e` |

---

## 6. Thứ tự đề nghị

```
B1  vá NuGet                        → CI xanh lại, mở đường mọi PR
B3  stop_cron 4×/ngày               → chặn rỉ tiền, một dòng
4.3 sửa comment stale               → nhỏ, $0
H1 + H2  hỏi người dùng             → không tự quyết được

4.2 = 5.2  IAM + env DataProtection (MỘT deploy)  ─┐
5.4  sửa down.sh trước ignore_changes             ─┤→ 5.5 (AWS, lớn)
5.3  SHOW max_connections (cần cửa sổ RDS)        ─┘    └ 4.1 gộp vào đây
T5   failover + ReplicaLag — gộp CÙNG cửa sổ AWS với 5.3, có người canh
T4   đo bằng mắt mục 🅹 — gộp vào bất kỳ cửa sổ AWS nào đã mở

B2  DNS — tạm thời sửa tay mỗi lần bật; đường gốc gộp vào 5.5
T1 · T2 · T3 · 6.3 · 5E   local, $0, không chặn gì
6.x  sau khi có H2 và xong đợt 5
```

**Hai chốt chặn cứng, nhớ kỹ:**
- **5.4 trước 5.5** — nếu không, `down.sh` thành no-op im lặng.
- **4.2/5.2 trước khi có instance thứ hai**, và IAM + env var vào **cùng** một deploy.

**Gộp cửa sổ AWS.** Bật stack tốn ~$0,25/giờ và ~6 phút. `5.3`, `T5`, `T4` đều cần
stack chạy — làm chung một lần, đừng bật ba lần.

---

## 7. Vận hành AWS — đọc trước khi bật bất cứ thứ gì

```bash
aws sts get-caller-identity --profile hushstore    # phải ra hushstore-ops
bash infra/tf/scripts/status.sh                    # RDS đang chạy hay stopped?

bash infra/tf/scripts/up.sh         # ~6 phút, ~$0,25/giờ
bash infra/tf/scripts/down.sh       # rồi status.sh XÁC MINH: ALB=0 NAT=0 EC2=0
```

### Bẫy đã gặp thật

1. **`up.sh` có thể thoát mã 0 mà vẫn hỏng giữa chừng.** Đã xảy ra 2026-10-19: chết
   ở BƯỚC 2/6 sau 5 giây, mã thoát vẫn 0. **Luôn xác minh bằng AWS API**, đừng tin
   mã thoát. Dấu hiệu sớm: dòng `terraform plan mã 1` ở BƯỚC 1.
2. **Cache provider lệch lock file** → mọi lệnh terraform ở `envs/prod` chết. Đã sửa
   ở `12b5b8b` bằng `providers lock -platform=linux_amd64 -platform=darwin_arm64`.
   **Nếu lại chạy `init` trên macOS rồi hoàn nguyên lock mà không xoá cache, nó tái
   diễn.** Lock giờ có 3 hash/provider — giữ nguyên, đừng hoàn nguyên.
3. **[Bản ghi cũ; hết áp dụng ở revision mạng hiện tại] `my_ip` trong tfvars lệch** với IP hiện tại. **Không** chặn truy cập (nó chỉ
   dùng ở `modules/network/nacl.tf:109`, rule DENY demo, mà `enable_deny_demo=false`).
   Khi đo cũ, **phải cập nhật trước khi diễn lại kịch bản 8**, nếu không rule DENY chặn
   nhầm IP người khác và test "đạt" **GIẢ**.
   ```bash
   # Lệnh của bản ghi cũ, không chạy theo revision hiện tại:
   curl -s https://checkip.amazonaws.com    # rồi sửa my_ip = "<ip>/32"
   ```
4. **`image_tag` trong tfvars có thể cũ hơn ECR.** `up.sh` cảnh báo nhưng **không tự
   sửa** — cố ý. Kiểm cả 4 repo (`api`, `web`, `migrator`, `seeder`) đều có tag đó
   trước khi bật, nếu không ECS chỉ trả `CannotPullContainerError`. ECR lifecycle
   **chỉ giữ 5 image gần nhất**.
5. **RDS `stopped` giữ nguyên dữ liệu** — schema + seed từ 24/09 vẫn còn. Không cần
   chạy lại migrator/seeder, không cần hai pha.
6. **`enable_ecr_endpoints`** là công tắc **tính tiền** duy nhất **không** được
   `enable_nat`/`enable_alb`/`instance_count` che chắn. Bật thì $0,04/giờ chảy ngay
   cả sau `down.sh`. `status.sh` có dòng riêng cho nó.

### Chi phí

| Khoản | $/giờ |
|---|---|
| ALB | 0,0252 |
| NAT × 2 | 0,1180 |
| EC2 `t3.micro` | 0,0132 |
| RDS đang chạy | 0,0980 *(số đo trên SQL Server — cận trên, xem T5)* |
| **Tổng khi bật đủ** | **≈ 0,25** |
| Sàn khi đã `down.sh` | 0,008 *(storage 20GB × 2 AZ, tính cả khi stopped)* |

---

## 8. Đọc theo thứ tự

| # | File | Để biết |
|---|---|---|
| 1 | file này | việc cần làm |
| 2 | `CLAUDE.md` | quy ước bắt buộc — **nhớ luật kiểm số ở §0** |
| 3 | `docs/bat-dau-phien-moi.md` | hồ sơ tra cứu cả dự án (1835 dòng, có bảng định tuyến ở đầu) |
| 4 | `docs/nhat-ky-sua-loi-nang-cap.md` | mọi lỗi đã sửa, kèm *sai ở đâu · vì sao · cách sửa* |
| 5 | `docs/security-validation-report-2026-09-24.md` | bản ghi bảo mật mới nhất; §5 là phần còn thiếu |
| 6 | `docs/evidence/acc-551897327153/2026-10-19/kb14-*.txt` | cửa sổ đo gần nhất + khoảng trống DNS |
| 7 | `docs/ban-giao-cua-so-do-dot-7.md` | trình tự mở cửa sổ đo AWS |
| 8 | `docs/terraform-runbook.md` | chi tiết hạ tầng |
| 9 | `tools/LoadProbe/README.md` | cách đo đúng đắn dưới tải, và **bốn cách đo sai** |

---

## 9. Cập nhật file này thế nào

Khi đóng một mục: sửa **tại chỗ**, ghi ngày đo và lệnh đã dùng. Đừng thêm dòng
"đã xong" ở cuối rồi để nguyên phần cũ — đó chính là cách `CLAUDE.md` tích được
12 chỗ nói ngược chính nó ở §5E.
