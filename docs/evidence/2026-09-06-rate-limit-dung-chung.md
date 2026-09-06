# Bằng chứng — bộ đếm rate limit chuyển sang PostgreSQL, dùng chung giữa các task

**Ngày đo:** 2026-09-06 · **Môi trường:** local, `hushstore_postgres_dev` (PostgreSQL 17) · **Chi phí:** $0

---

## Vì sao phải làm

Bộ đếm cũ nằm trong RAM của **một tiến trình** (`System.Threading.RateLimiting`). Với N task
sau ALB, mỗi task đếm riêng ⇒ **mọi hạn mức nhân N**:

| Policy | 1 task | 2 task | 5 task |
|---|---|---|---|
| `LoginRateLimit` | 5/phút | 10 | 25 |
| `RegisterRateLimit` | 3/giờ | 6 | 15 |
| `RefreshRateLimit` | 10/phút | 20 | 50 |
| `LookupRateLimit` | 10/phút | 20 | 50 |

Dòng đầu là dòng đắt nhất: **KB6** của [`security-validation-report.md`](../security-validation-report.md)
đã nộp con số đo *"req 1-5 → 400, req 6-20 → 429"*. Scale ra mà không sửa là biến **một bằng
chứng đã nộp thành lời khai sai**, và sai **âm thầm** — không log, không alarm, ALB vẫn xanh.

Đây cũng là điều kiện `modules/ecs/variables.tf` đòi trước khi cho `max_instance_count > 1`.

---

## Đã đo gì

### 1. Tính nguyên tử — 40 upsert song song vào **cùng một ô đếm**

Câu lệnh đang dùng (`PostgresRateLimitStore.AcquireSql`):

```sql
INSERT INTO "RateLimitCounters" ("PartitionKey", "WindowStart", "Count")
VALUES (@key, @windowStart, 1)
ON CONFLICT ("PartitionKey", "WindowStart")
DO UPDATE SET "Count" = "RateLimitCounters"."Count" + 1
RETURNING "Count";
```

| Cách đếm | 40 request song song | Kết quả |
|---|---|---|
| **Atomic upsert** (đang dùng) | `Count` cuối = **40** | ✅ **không mất update nào** |
| **Check-then-act** (`SELECT` → `UPDATE SET c+1`) | `Count` cuối = **16** | 🔴 **mất 24 update** |

Dòng thứ hai là **ca đối chứng âm**, và nó là thứ làm dòng thứ nhất trở thành bằng chứng chứ
không phải lời khẳng định. Đọc con số `16` theo nghĩa nghiệp vụ: nếu viết theo cách sai thì
**24 request lọt qua một hạn mức**, và không có lỗi nào báo ra — đúng loại lỗi mà
`tools/LoadProbe/` tồn tại để bắt.

### 2. Migration chạy thật

`20260906130624_AddRateLimitCounters` đã `dotnet ef database update` lên PostgreSQL thật. Khoá
chính là **ghép** `(PartitionKey, WindowStart)` — không phải chỉ một index. Điều đó bắt buộc:
`ON CONFLICT (...)` chỉ hợp lệ khi có một ràng buộc unique **đúng trên cặp cột đó**; thiếu nó
thì câu lệnh **không chạy được** — hỏng lúc chạy, không lúc biên dịch, và hỏng ở đúng đường
đăng nhập.

### 3. Build

`dotnet build PBL3.sln` → **0 Error**. Số warning **16 trước = 16 sau**, tức không thêm cái nào
(đo bằng `git stash` rồi đếm lại).

---

## Cái gì KHÔNG chuyển, và vì sao

| Policy | Ở đâu | Lý lẽ |
|---|---|---|
| `GlobalLimiter` (100/10s, **mọi** request) | in-process, per-instance | Chạm **mọi** request. Ghi DB trên đường nóng đó là biến DB thành **cổ chai** — đúng ngược mục đích của việc scale ra |
| `PublicReadRateLimit` (60/phút) | in-process, per-instance | Như trên: duyệt catalogue, tìm kiếm |

Với N task hai cái này thành `100N` và `60N`. **Chấp nhận được**, và đây là lý lẽ: chúng là
chốt chặn **burst thô**, và **không có bằng chứng nào đã nộp dựa vào con số của chúng**. Cờ
`rate_limiter_is_distributed` đã được sửa `description` + `error_message` để nói **đúng** phạm
vi này, thay vì để lại một lời khai blanket không chính xác.

---

## Ba hệ quả mới, phải biết trước khi vận hành

**① Bộ đếm nay SỐNG QUA khởi động lại.** Trước đây restart API là reset sạch hạn mức. Nay nó
nằm ở DB nên **không reset** — đó chính là điều cần, nhưng nó đổi cách đo: hai lần chạy
LoadProbe trong cùng một phút sẽ **cộng dồn**. `ProbeFixture.CleanupAsync` phải xoá hàng
`RateLimitCounters` do probe sinh ra.

**② Fail-open khi DB không tới được, có chủ ý.** Cả bốn endpoint đi qua đây **tự nó đã cần DB**
(kiểm mật khẩu, ghi `AppUsers`, tra serial), nên DB sập thì chúng hỏng bất kể limiter nói gì;
fail-closed chỉ biến "DB chậm" thành "không ai đăng nhập được". Trần chờ của **chính câu đếm**
là **2 giây**, hết hạn thì cho đi + log `Warning`. Cờ `Degraded` đi kèm và filter có nghĩa vụ
log nó — fail-open **im lặng** thì là tắt rate limit mà không ai biết.

**③ Bảng cần dọn.** Mỗi (policy, IP, cửa sổ) sinh một hàng và không tự mất.
`RateLimitCounterCleanupService` chạy mỗi 30 phút, xoá hàng cũ hơn **3 giờ** — lớn hơn hẳn cửa
sổ dài nhất (1 giờ), **không phải bằng nó**: xoá hàng của cửa sổ đang mở là đặt bộ đếm về 0
giữa cửa sổ, tức tự tay nới hạn mức.

---

## Hai cái bẫy tìm được lúc viết phép đo — không phải lúc thiết kế

**① `[ApiController]` chạy validator TRƯỚC rate limiter.** Filter `ModelState` của
`[ApiController]` có `Order = -2000`; `[DbRateLimit]` là action filter `Order = 0`. Nên một
thân request **sai định dạng** nhận `400` mà **không chạm bộ đếm**.

Hệ quả cho phép đo: nếu S11 bắn thân không hợp lệ thì nó âm thầm biến thành một bài test
validator, và vẫn "xanh". Thân của S11 vì vậy **cố ý hợp lệ về định dạng và chỉ sai về nội
dung** (email đúng dạng, mật khẩu khác rỗng nhưng sai).

Hệ quả cho vận hành: request rác **không bị** bốn policy này tính. Điều đó **được che bởi
`GlobalLimiter`** — nó là middleware, chạy trước cả MVC, nên 100 request/10 giây vẫn chặn.
Đây đúng là một chỗ mà việc giữ `GlobalLimiter` in-process trở nên có ích.

**② Cổng chống-rỗng của LoadProbe sẽ đóng dấu S11 là KHÔNG KẾT LUẬN vĩnh viễn.**
`Program.cs` gọi `fire.VacuityReason(...)` **trước** `AssertAsync`, và hàm đó trả về khác
`null` mỗi khi `RateLimited > 0` — theo đúng luật "bị rate limiter chặn thì code cần đo chưa
chạy". Nhưng S11 là kịch bản duy nhất mà **429 chính là thứ đang đo**. Đã thêm cờ
`RateLimitIsUnderTest` (mặc định `false`, chỉ S11 override) và nó **chỉ** tắt nhánh 429; các
chốt chống-rỗng khác (lỗi truyền tải, bắn quá ít) vẫn nguyên.

---

## 4. Đo ở mức hệ thống — 2 replica thật, có ca đối chứng âm

Hạ tầng: `devops/docker/docker-compose.multi.yml` — **2 replica API + nginx round-robin**
(xác nhận luân phiên `172.21.0.2` / `172.21.0.3` qua header `X-Upstream`), dùng chung
`hushstore_postgres_dev`.

Phép đo đổi **đúng một biến**: nơi bộ đếm sống. Filter, kịch bản, thân request, số replica —
giữ nguyên.

| Bộ đếm ở đâu | Qua limiter | Chặn 429 | Hàng `RateLimitCounters` | Kết luận |
|---|---|---|---|---|
| **PostgreSQL** (đang dùng) | **5** ✅ | 15 | `LoginRateLimit:192.168.65.1 → Count=20` | **ĐẠT** |
| **RAM tiến trình** (ca đối chứng âm) | **10** 🔴 | 10 | KHÔNG CÓ | KHÔNG KẾT LUẬN |
| **PostgreSQL** (khôi phục) | **5** ✅ | 15 | `Count=20` | **ĐẠT** |

Ba dòng, theo thứ tự, chứng minh nhiều hơn dòng đầu một mình: kết quả **đảo được**, nên khác
biệt đến từ đúng thứ ta đổi chứ không từ một yếu tố môi trường nào khác.

**Con số `10` ở dòng giữa chính là lỗi cần chặn**, đo trực tiếp: 2 replica × 5 = 10 request
lọt qua một hạn mức 5. Với 5 replica nó sẽ là 25.

🎯 **Chú ý dòng giữa trả `KHÔNG KẾT LUẬN`, không phải `HỎNG`.** S11 không thấy hàng đếm nên nó
**từ chối kết luận** thay vì báo đạt — đúng luật của repo (*"`KHÔNG KẾT LUẬN` ≠ `ĐẠT`"*). Con
số `10` vẫn nằm trong phần chẩn đoán để đọc. Đây là hành vi đúng: mã HTTP một mình **không**
phân biệt được "dùng chung" với "per-process mà tình cờ chỉ có một process".

### Không hồi quy — chạy lại toàn bộ

`dotnet run --project tools/LoadProbe -- --api http://localhost:8088` trên **2 instance**:

> **10 đạt · 0 hỏng · 0 không kết luận** (S01–S09 + S11)

Đáng chú ý **S09** (đua refresh-token) vẫn ĐẠT dù `RefreshRateLimit` nay đếm ở DB — nó chỉ bắn
**2** request và **cố ý gieo cặp token thẳng vào DB** thay vì đăng nhập, đúng để không đốt suất
rate limit. Ghi chú đó có sẵn trong code từ trước và hôm nay nó trả cổ tức.

---

## Còn thiếu để coi là XONG

- [x] ✅ **LoadProbe S11 trên 2 replica** — 5 qua / 15 chặn / một ô đếm `Count=20`.
- [x] ✅ **Ca đối chứng âm ở mức hệ thống** — bản in-process cho **10** qua, đúng như dự đoán.
- [x] ✅ **Không hồi quy** — 10/10 kịch bản ĐẠT trên 2 instance.
- [ ] **Lật `rate_limiter_is_distributed = true`** — chỉ sau khi hai gạch trên xanh. Cờ là
      **lời khai của người vận hành**, Terraform không kiểm được, nên khai trước khi đo là
      đúng thứ mà chính `error_message` của nó cảnh báo.
- [ ] **Đo lại KB6** và cập nhật `security-validation-report.md`.
