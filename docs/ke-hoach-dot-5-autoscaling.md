# Kế hoạch đợt 5 — co giãn theo tải, hai tầng

> **Mục tiêu đo được:** 2 task chạy trên **2 instance ở 2 AZ khác nhau**, deploy
> **0 downtime**, và số máy **tự** tăng/giảm theo tải chứ không do người gõ lệnh.
>
> **Kiến trúc chọn: phương án C — co giãn hai tầng.**
> Tải tăng → **task** tăng (ECS Service Auto Scaling đo CPU/số request) → task
> không xếp được chỗ → **máy** tăng (capacity provider managed scaling) → tải
> giảm → cả hai rút về. Máy **không tự đo tải**; nó bị ECS kéo theo. Đây là
> chuỗi nhân quả một chiều, và là kiến trúc AWS thiết kế ra.

---

## 0. Trạng thái xuất phát — đo ngày 2026-09-06, không theo trí nhớ

| Thứ | Giá trị thật | Nguồn |
|---|---|---|
| ASG `max_size` | **1** | AWS `describe-auto-scaling-groups` + `terraform.tfvars:73` |
| ASG `desired` / instance đang chạy | **0 / 0** | AWS |
| `aws_autoscaling_policy` | **0 cái** | `grep` toàn `modules/` |
| ECS Service Auto Scaling | **0 cái** | không có `aws_appautoscaling_*` |
| `managed_scaling` | **`DISABLED`** cố ý | `modules/ecs/cluster.tf:193` |
| `rate_limiter_is_distributed` | **`false`** | `terraform.tfvars:74` |
| NAT Gateway đang chạy | **0** — `enable_nat = false` | `terraform.tfvars:20` ⇒ `vpc.tf:97` |
| Read replica | **0** — và **0 dòng code app đọc nó** | `terraform.tfvars:55`; `grep connection-string-readonly src/` = 0 |
| RDS Multi-AZ | **BẬT** (ghim `true` ở `envs/prod/variables.tf:112`) | ✅ đúng cả hai vế |

**Kết luận: hôm nay hệ thống KHÔNG có cơ chế co giãn nào.** Số máy do
`up.sh`/`down.sh` đặt tay. Đây không phải lỗi — `cluster.tf:190` khai rõ đó là
quyết định về chi phí, không phải quyết định do tải sinh ra. Đợt 5 là lúc đảo
lại quyết định đó.

### Repo đã chuẩn bị sẵn nhiều hơn tưởng — ba thứ không phải làm lại

| Đã có | Ở đâu | Nghĩa là |
|---|---|---|
| `deployment_minimum_healthy_percent = 50` khi `count > 1` | `service.tf:45` | ECS hạ **một** task, dựng bản mới, đợi healthy, rồi mới làm cái còn lại ⇒ **downtime deploy = 0** mà không cần dynamic port mapping |
| `ordered_placement_strategy` spread theo **AZ trước**, rồi mới theo instance | `service.tf:55` | Hai task không bị gom vào một AZ |
| `managed_termination_protection = DISABLED` | `cluster.tf:178` | Capacity provider xoá được instance khi rút về — thiếu nó thì scale-in treo |

---

## 🔴 1. Xung đột thứ tự trong kế hoạch cũ — phải giải trước khi viết dòng code nào

Bảng gói ở [`bat-dau-phien-moi.md:157`](bat-dau-phien-moi.md) xếp:

- **đợt 5** = scale-out (cần `max_instance_count = 2`)
- **đợt 8** = Redis

Nhưng `modules/ecs/variables.tf:70` có validation **chặn cứng**:

> `max_instance_count > 1` đòi `rate_limiter_is_distributed = true`

Và cờ đó chỉ khai đúng được khi bộ đếm rate limit dùng chung giữa các task — tức
**cần Redis, thứ đang xếp ở đợt 8**. Hai gói phụ thuộc vòng.

**Đây không phải thủ tục rườm rà.** Rate limiter đếm trong **RAM tiến trình**
(`Program.cs:436-512`, bốn policy fixed-window theo IP). Hai task ⇒ mọi hạn mức
nhân đôi:

| Policy | Hiện tại | Với 2 task | Với 5 task |
|---|---|---|---|
| Đăng nhập | 5 lần/phút | **10** | 25 |
| Đăng ký | 3 lần/giờ | 6 | 15 |
| Refresh token | 10 lần/phút | 20 | 50 |
| Toàn cục | 100 lần/10s | 200 | 500 |

Dòng đầu là thứ đắt nhất: kịch bản **KB6** của
[`security-validation-report.md`](security-validation-report.md) đã nộp con số đo
**"req 1-5 → 400, req 6-20 → 429"**. Scale ra mà không sửa là **biến một bằng
chứng đã nộp thành lời khai sai** — và sai **âm thầm**: không log, không alarm,
ALB vẫn xanh.

### Ba lối, cần chốt một

| Lối | Cách làm | Tiền | Ưu | Nhược |
|---|---|---|---|---|
| **A** | **ElastiCache Redis** (`cache.t4g.micro`), thêm vào nhóm toggle như NAT/ALB | ~$0,016/giờ **chỉ khi bật** | Đúng chuẩn doanh nghiệp; **đồng thời** giải luôn `ICacheService` đang là `AddDistributedMemoryCache` (vốn xếp ở đợt 8) | Phải viết `RateLimiter` tuỳ biến — .NET **không** có limiter phân tán sẵn |
| **B** | **AWS WAF rate-based rule** trước ALB | ~$5/tháng WebACL + $1/rule | Doanh nghiệp thật chặn brute-force ở **biên**, không ở app. Bỏ được cả bài toán chia sẻ trạng thái | ⚠️ WAF có **mức sàn** cho rate limit — **phải xác minh trước** liệu 5 lần/phút có đặt được không. Nếu sàn cao hơn thì KB6 phải đo lại với con số mới |
| **C** | **Chia hạn mức cho N**: `PermitLimit = ceil(5/N)` | **$0** | Rẻ nhất, không thêm hạ tầng | Chỉ **chặn trên** chứ không chính xác; và N thay đổi theo autoscaling nên phải đọc động ⇒ mong manh |

**Khuyến nghị: A, và kéo Redis từ đợt 8 về đợt 5.** Lý do: nó là thứ duy nhất
vừa mở khoá được validation, vừa giải luôn `ICacheService`, vừa đúng chuẩn doanh
nghiệp. Và vì Redis vào nhóm toggle nên nó **chỉ tốn tiền trong cửa sổ làm
việc**, giống NAT và ALB — không phải $11,7/tháng như chạy 24/7.

⚠️ **Nếu chọn A thì lý lẽ hoãn gói Redis ở
[`bat-dau-phien-moi.md:437`](bat-dau-phien-moi.md) không còn đúng.** Lý lẽ đó là
*"gói chưa dùng là nợ bảo mật nằm im"* (bài học AutoMapper). Ở đợt 5 gói **được
dùng ngay**, nên nó không còn là gói nằm im. Ghi lại điều này để phiên sau không
tưởng ta quên bài học cũ.

---

## 2. Các bước, theo thứ tự BẮT BUỘC

Thứ tự không đảo được: mỗi bước mở khoá cho bước sau, và **bước 4 phải xong
trước bước 5** — vì bước 5 là bước làm hỏng thứ bước 4 sửa.

### Bước 1 — Rate limiter dùng chung *(theo lối đã chốt ở §1)*

| | |
|---|---|
| **Sửa** | `src/API/Program.cs` (4 policy), `src/API/API.csproj`, `infra/tf/modules/data/` hoặc module mới cho Redis |
| **Xong khi** | Chạy `docker-compose.multi.yml` (2 replica + nginx), bắn **6 request đăng nhập** qua nginx → request thứ **6 phải nhận `429`**, bất kể nginx định tuyến sang replica nào. Hiện tại cùng phép đo đó cho `429` ở request thứ **11**. |
| **Ca đối chứng bắt buộc** | Tắt Redis đi rồi đo lại — phải quay về `429` ở request 11. Không có ca này thì không chứng minh được Redis là thứ tạo ra khác biệt |
| **Tiền** | $0 (đo ở local bằng container Redis) |

### Bước 2 — DataProtection key ring dùng chung

| | |
|---|---|
| **Sửa** | `modules/ecs/iam.tf` (quyền SSM cho role `task_app`), `modules/ecs/taskdef.tf` (env `DataProtection__SsmPrefix`) |
| **Xong khi** | Hai task cùng giải mã được một payload do task kia mã hoá |
| **🚨 Bắt buộc** | IAM và env var vào **CÙNG một** deploy. Đã đo có ca đối chứng: đặt env var mà thiếu quyền thì app **vẫn khởi động, `health/live` xanh, ECS coi healthy** — rồi `Protect` ném `CryptographicException` trên vài đường 500. Không dashboard nào đỏ |
| **Ghi chú** | Hôm nay app có **0** chỗ dùng `IDataProtector` (đã đo). Đây là chuẩn bị, không phải vá lỗi đang chảy máu — nhưng phải xong **trước** khi có instance thứ hai |

### Bước 3 — Chốt trần thật theo sức chịu của DB

`db.t4g.micro` có 1 GiB RAM. PostgreSQL trên RDS tính `max_connections` theo
`RAM / 9531392` ⇒ **~112 kết nối**, trừ vài cái dành cho superuser còn ~109.
Chuỗi kết nối đang khai `Maximum Pool Size=30` **mỗi tiến trình**
(`modules/data/main.tf:225`).

| Số instance | Kết nối tối đa | |
|---|---|---|
| 1 | 30 | ✅ |
| 3 | 90 | ✅ sát trần |
| **4** | **120** | ❌ vượt ~112 |
| 5 | 150 | ❌ |

⚠️ **Con số 112 là suy ra từ công thức, CHƯA đo trên instance thật.** Bước này
bắt đầu bằng `SHOW max_connections;` trên RDS thật, không phải bằng phép tính.

| | |
|---|---|
| **Xong khi** | Đã đo `max_connections` thật, và `max_instance_count × Maximum Pool Size` **nhỏ hơn** nó với biên ≥ 20% |
| **Đề nghị** | Trần **3**, không phải 5 — hoặc hạ pool xuống 20 nếu muốn 5 |

### Bước 4 — Sửa `down.sh` và `min_size` TRƯỚC khi bật bước 5

Bước 5 đặt `ignore_changes = [desired_capacity]`. Mà `down.sh:76` tắt tiền bằng
đúng một dòng đi **qua Terraform**:

```bash
hs_tfvar_set instance_count 0     # → Terraform đặt desired_capacity = 0
```

`ignore_changes` biến dòng đó thành **no-op im lặng**: apply vẫn xanh, máy vẫn
chạy, tiền vẫn chảy. Đây đúng loại "bằng chứng an toàn giả" mà tài liệu dự án
gọi tên nhiều lần.

Kèm theo: nếu đặt `min_size = 1` (chuẩn doanh nghiệp), Lambda cost-guard gọi
`SetDesiredCapacity(0)` sẽ bị AWS **từ chối** — và Lambda **cố ý không có**
`autoscaling:UpdateAutoScalingGroup` (`modules/costguard/lambda.tf:125`) nên nó
không tự hạ `min_size` được.

| | |
|---|---|
| **Sửa** | `infra/tf/scripts/down.sh`, `infra/tf/scripts/lib.sh`, có thể cả IAM của cost-guard |
| **Xong khi** | Chạy `down.sh` với `managed_scaling` ĐANG bật ⇒ `describe-auto-scaling-groups` trả `DesiredCapacity: 0` và `length(Instances): 0` |
| **Ưu tiên** | Theo hiệu chỉnh 2026-09-06, cost-guard **không phải deliverable**. Nhưng "sửa cho đúng" khác "ưu tiên cao" — bước này rẻ và nó chặn một đường mất tiền im lặng, nên vẫn làm |

### Bước 5 — Bật co giãn hai tầng

| Sửa | Nội dung |
|---|---|
| `modules/ecs/cluster.tf:193` | `managed_scaling` `DISABLED` → `ENABLED` |
| `modules/ecs/cluster.tf` | thêm `lifecycle { ignore_changes = [desired_capacity] }` cho ASG |
| `modules/ecs/` (mới) | `aws_appautoscaling_target` + `aws_appautoscaling_policy` cho 2 service, **`min_capacity = 0`** |
| `envs/prod/terraform.tfvars` | `max_instance_count = 3`, `rate_limiter_is_distributed = true` |
| `modules/alb/alb.tf:58,83` | `deregistration_delay` `5 → 30` *(giờ mới có nghĩa — có instance thứ hai để chuyển traffic sang)* |
| `modules/alb/tests/alb.tftest.hcl:167` | viết lại `error_message`; lý lẽ cũ (`max_size 1` ⇒ draining vô ích) hết đúng |

**`min_capacity = 0` ở tầng ECS là bắt buộc**, không phải tuỳ chọn: đặt `≥ 1`
thì Application Auto Scaling sẽ **đẩy service trở lại** mỗi lần `down.sh` hạ về
0. Hai cơ chế đánh nhau và `down.sh` thua.

| | |
|---|---|
| **Xong khi** | (a) `terraform plan` sau apply ra **"No changes"** — chứng minh `ignore_changes` đã dập được drift; (b) bắn tải tới ngưỡng ⇒ số task tăng ⇒ số instance tăng theo; (c) ngừng tải ⇒ **cả hai rút về**; (d) deploy trong lúc 2 task chạy ⇒ **0 request lỗi** |
| **Tiền** | Đây là bước đầu tiên **bắt buộc chạy trên AWS**. ~$0,27/giờ với 2 máy |

---

## 3. Một đánh đổi kiến trúc phải quyết ở bước 5

Host port **tĩnh** (`80`/`8080`) khoá **1 task/service/instance** — chính
`service_desired_count` có validation ép `≤ max_instance_count`. Nghĩa là task
và máy đi **1:1**, không tách rời được.

Với phương án C điều đó **vẫn chạy**: task thứ hai không xếp được (trùng port)
⇒ ở trạng thái `PENDING` ⇒ capacity provider thấy và thêm máy. Nhưng nó cứng, và
luôn trễ một nhịp.

| Lối | Được | Mất |
|---|---|---|
| **Giữ host port tĩnh** *(khuyến nghị)* | `sg-web` giữ **đúng 2 rule** — đây là bằng chứng "nguyên tắc tối thiểu" của đề bài | Task và máy khoá 1:1 |
| Dynamic port mapping | Task tách khỏi máy, co giãn mượt hơn | Phải mở dải `32768-65535` trên `sg-web` ⇒ **làm yếu đúng thứ đề bài đang chấm** |
| `awsvpc` | Mỗi task một ENI riêng, sạch nhất | `t3.micro` chỉ gắn được vài ENI ⇒ không đủ |

**Khuyến nghị giữ tĩnh.** Đề bài chấm "rule mở tối thiểu"; đổi lấy một chút mượt
mà để hy sinh đúng thứ đang được chấm là lỗ vốn.

---

## 4. Rủi ro

| Rủi ro | Vì sao nguy | Chặn bằng |
|---|---|---|
| **Quên bước 4, bật bước 5 trước** | `down.sh` thành no-op **im lặng** — tiền chảy mà không lỗi nào báo | Thứ tự bắt buộc; và thêm một dòng vào `status.sh` cảnh báo khi thấy `desired > 0` sau `down.sh` |
| **Khai `rate_limiter_is_distributed = true` mà chưa thật sự làm** | Cờ là **lời khai của người vận hành**, Terraform không kiểm được | Ca đối chứng ở bước 1 phải chạy **trước** khi lật cờ |
| **`max_connections` thật nhỏ hơn 112** | Instance thứ 3-4 làm DB từ chối kết nối — lỗi xuất hiện **dưới tải**, đúng lúc tệ nhất | `SHOW max_connections;` ở bước 3, không tin công thức |
| **Bật replica rồi quên huỷ** | AWS **từ chối stop** primary khi có replica ⇒ mất cơ chế tắt tiền, mà RDS còn tự bật lại sau 7 ngày | `down.sh` đã tự huỷ replica trước khi stop; `status.sh` in dòng đỏ. Cả hai **chỉ chạy khi có người gõ** |

---

## 5. Việc phát sinh — read replica hiện là hạ tầng CHẾT

Đo được ngày 2026-09-06: kể cả khi lật `enable_read_replica = true`, replica
**không giảm tải cho primary một chút nào**, vì phía ứng dụng chưa có gì đọc nó:

| Kiểm | Kết quả |
|---|---|
| `grep "connection-string-readonly" src/` | **0** |
| Số `DbContext` | **1** (`HushStoreDbContext.cs:10`) |
| Số `AddDbContext` / `UseNpgsql` | **1** (`Program.cs:218-220`) |
| ARN parameter readonly có được truyền vào module `ecs` không | **KHÔNG** — `grep 'replica\|readonly' modules/ecs/` = 0 |

Dòng cuối là chỗ đáng nói: ngay cả khi viết code đọc replica, container **cũng
không nhận được** chuỗi kết nối đó, vì `modules/data/outputs.tf:64-67` xuất ARN
ra mà **không ai nối vào** `taskdef`, và task role không có quyền IAM đọc nó.

Nên hôm nay replica là **hạ tầng để ĐO và để trình bày trong báo cáo**, không
phải để tăng hiệu năng — đúng như `modules/data/main.tf:236` đã tự khai. Muốn nó
thật sự có tác dụng thì phải làm ba việc, và đây là **việc mới, chưa nằm trong
gói nào**:

1. Nối ARN parameter readonly từ module `data` → module `ecs` → khối `secrets`
   của taskdef, kèm quyền IAM cho task execution role
2. Đăng ký `DbContext` thứ hai (chỉ đọc) trong `Program.cs`
3. Lái các service **chỉ đọc** sang nó. Ứng viên đã đo: `AnalyticsService`
   (10 `AsNoTracking`, 0 `SaveChanges`) và `StorefrontService` (12/0)

⚠️ Và phải hiểu replica là **bất đồng bộ**: dữ liệu trên nó có thể cũ vài giây.
Tuyệt đối **không** lái đường đăng nhập / phân quyền / kiểm tồn kho sang replica.

⚠️ **Đừng bật replica rồi để qua đêm** — xem hàng cuối bảng rủi ro.
