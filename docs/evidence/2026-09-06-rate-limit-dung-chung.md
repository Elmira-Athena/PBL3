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

## Còn thiếu để coi là XONG

- [ ] **LoadProbe S11** — 20 request đăng nhập song song qua nginx với **2 replica**, khẳng
      định đúng 5 đi qua và 15 nhận `429`. Đây là phép đo duy nhất chứng minh tính chất "dùng
      chung", vì nó là tính chất **chỉ sai khi có nhiều hơn một tiến trình**.
- [ ] **Ca đối chứng âm ở mức hệ thống** — chạy lại S11 với bản cũ (in-process) và phải thấy
      **10** đi qua thay vì 5.
- [ ] **Lật `rate_limiter_is_distributed = true`** — chỉ sau khi hai gạch trên xanh. Cờ là
      **lời khai của người vận hành**, Terraform không kiểm được, nên khai trước khi đo là
      đúng thứ mà chính `error_message` của nó cảnh báo.
- [ ] **Đo lại KB6** và cập nhật `security-validation-report.md`.
