# Bằng chứng — LoadProbe 9/9 trên PostgreSQL ở **2 instance**, và seeder chạy thật

**Ngày đo:** 2026-09-05 · **Engine:** PostgreSQL 17.11 (`hushstore_postgres_dev`) ·
**Nhánh:** `feat/dot-7-postgresql`

> Lần đo trước (1 instance): [2026-09-03-postgresql-9-tren-9.md](2026-09-03-postgresql-9-tren-9.md)
> — §7 của nó liệt kê đúng những khoảng trống mà file này lấp.

---

## 1. Vì sao 1 instance là chưa đủ, nói bằng số của chính lần đo này

CLAUDE.md đã ghi bài học cũ: *"S04 ĐẠT với 1 instance, HỎNG với 2. Kết luận từ một cấu
hình là kết luận sai."* Lần này cả hai cấu hình đều ĐẠT — nhưng **phân bố mã HTTP khác
nhau**, và đó là thứ chứng minh hai lần chạy không phải một:

| Kịch bản | 1 instance | 2 instance |
|---|---|---|
| S03 | `200×1, 409×9` | `200×1, 400×6, 409×3` |

Kẻ thua ở 2 instance rơi vào **hai đường khác nhau**: 6 request bị **chốt ngoài** bắt kịp
(câu nghiệp vụ, 400), 3 request đi lọt tới **unique index** (409). Ở 1 instance cả 9 đều
là 409. Đây đúng là lý do CLAUDE.md bắt giữ **cả hai** chốt: chốt ngoài cho thông báo tử
tế ở đường thường, unique index là lưới cuối — và chỉ ở 2 instance mới thấy cả hai cùng
làm việc.

## 2. Chứng minh bài kiểm KHÔNG suy biến thành 1 instance

Cái bẫy đã ghi sẵn trong `nginx.multi.conf`: nếu nginx chỉ phân giải một địa chỉ thì
*"vòng lặp kiểm tra vẫn xanh, log vẫn đẹp, và bài kiểm đa instance trở thành bài kiểm một
instance"*. Nên phải đo, không phải tin:

```
8 request qua nginx  ->  4 × 172.21.0.2:8080   (api-2)
                         4 × 172.21.0.3:8080   (api-1)
```

Và tải thật trong lúc chạy probe, đếm ở **hai container** sau khi xong:

| | api-1 | api-2 |
|---|---|---|
| Câu lệnh EF thực thi | **429** | **442** |
| Dòng log | 4 644 | 4 648 |
| Xung đột xử lý tại chỗ | 3 | 8 |

Chia gần đều ⇒ mọi kịch bản đều thật sự chạy xuyên hai tiến trình.

## 3. Kết quả

```
S01 ✅ 50 đơn, 50 mã phân biệt          · 200×50
S02 ✅ Đúng 1 lượt được tiêu thụ        · 200×1, 400×19
S03 ✅ Đúng 1 lượt cho mỗi người        · 200×1, 400×6, 409×3
S04 ✅ Đúng 1 phiếu chưa đóng           · 200×10
S05 ✅ Đúng một lần duyệt               · 200×10
S06 ✅ Đúng 1 bản ghi điều chỉnh        · 200×1, 400×4
S07 ✅ Serial không bán được, ghi nhận thất thoát — nhất quán
S08 ✅ Đúng 1 báo giá Pending           · 200×1, 409×1
S09 ✅ Đúng một lời gọi được chấp nhận  · 200×1, 400×1

Tổng kết: 9 đạt, 0 hỏng, 0 không kết luận.
```

Báo cáo đầy đủ: `docs/evidence/loadprobe/loadprobe-20260905-153958.md`
(1 instance cùng ngày, sau khi thêm `ILike` + converter JSON:
`loadprobe-20260905-152912.md` — cũng 9/9, tức hai thay đổi đó không gây hồi quy.)

---

## 4. Seeder — chạy thật, kèm hai ca đối chứng âm

Image `hushstore-seeder` đã chuyển `sqlcmd` → `psql 17` (kho PGDG, không dùng bản 15 của
Debian 12). Không đọc code để kết luận — build image rồi chạy vào PostgreSQL thật:

| Ca | Kỳ vọng | Đo được |
|---|---|---|
| Mặc định `PGSSLMODE=verify-full`, PG local **không** bật TLS | phải GÃY | **exit 2** — `server does not support SSL, but SSL was required` |
| `SEED_TRUST_SERVER_CERT=1` **+ giả lập đang trong ECS** | phải TỪ CHỐI | **exit 2** — câu tiếng Việt chỉ rõ lý do |
| `SEED_TRUST_SERVER_CERT=1`, ngoài ECS | phải chạy xong | **exit 0**, đếm ra `AppRoles=4, AppUsers=1, Categories=18, Manufacturers=21, Products=49, ProductVariants=52` |

Ca thứ nhất là ca quan trọng nhất: nó chứng minh `verify-full` **đang thật sự có hiệu
lực**. Nếu nó chạy trót lọt thì nghĩa là sslmode đã tụt xuống mức không xác thực — mất im
lặng đúng thuộc tính đang bảo vệ.

### 4.1 Chốt sequence — và ranh giới của nó, đo cả hai chiều

Thêm bước `[3b/3]` kiểm 4 sequence identity đã vượt qua `max("Id")` chưa.

| Ca | Kết quả |
|---|---|
| Sequence đúng | exit 0, in `Manufacturers : max(Id)=21, next=22 OK` … |
| Ép sequence về `1` rồi chạy **riêng khối chốt** | **exit 3** — `SEED THAT BAI: sequence cua Manufacturers dang o 1 nhung max(Id) = 21 — thieu setval.` |
| Ép sequence về `1` rồi chạy **cả seeder** | **exit 0** |

Ca thứ ba **không phải lỗ hổng**, nhưng phải ghi ra kẻo tin nhầm phạm vi: bước `[2/3]`
chạy seed, mà seed *có* `setval`, nên tới `[3b]` sequence đã được vá. Chốt này canh
**"ai đó xoá 4 lệnh `setval` khỏi `seed_product_data.sql`"**, không canh "DB đang lệch".

---

## 5. Điều lần đo này vẫn KHÔNG chứng minh

- **Deadlock `40P01` → 409 vẫn chưa đo lại trên PostgreSQL.** `docs/evidence/2026-09-03-conflict-classifier.md`
  đo trên `1205` của SQL Server và **không được chép sang**. Đây là khoảng trống lớn nhất
  còn lại ở tầng code.
- **Seeder mới chạy vào PostgreSQL local, chưa chạy vào RDS.** Nhánh `verify-full` với CA
  thật của RDS chưa từng được thực thi — ca đối chứng ở §4 chỉ chứng minh nó *từ chối* khi
  không có TLS, chưa chứng minh nó *chấp nhận* đúng cert của RDS.
- **Toàn bộ Terraform còn nguyên SQL Server** (`engine = "sqlserver-ex"`, cổng 1433 ở 4
  file), và **cost guard chưa vá** — `cost_guard.py:264` vẫn xếp `InvalidDBInstanceState`
  vào `notes` với câu *"trạng thái đích vẫn đạt được"*, đúng mã lỗi RDS trả về khi instance
  có read replica.

---

## 6. Bổ sung — deadlock `40P01` đã đo trên PostgreSQL (không chép từ SQL Server)

§5 ở trên viết khi mục này còn trống. Đã đo xong cùng ngày, bằng **deadlock thật** ép từ
PostgreSQL (hai transaction khoá hai hàng theo thứ tự ngược nhau, gặp nhau ở `Barrier`):

| Ca | Đo được |
|---|---|
| Deadlock thật, exception **trần** | `PostgresException`, `SqlState = 40P01` → `IsConflict = True`, `Classify` = *"Hệ thống đang bận, vui lòng thử lại sau giây lát."* |
| Cùng exception **bọc 3 lớp** (`RetryLimitExceededException` → `DbUpdateException` → `InvalidOperationException` → gốc) | `IsConflict = True`, `Classify` ra **cùng câu** |
| **Đối chứng âm**: bảng không tồn tại (`SqlState = 42P01`) | `IsConflict = False` |

Ca thứ hai là ca đáng giá: `EnableRetryOnFailure` đang bật và `40P01` **nằm trong danh sách
transient của Npgsql**, nên hết lượt thử lại thì EF bọc nguyên nhân gốc lại. Phép so khớp
**một tầng** sẽ không khớp cái nào ⇒ **500**. `ConflictClassifier` đi hết chuỗi
`InnerException` nên bắt được — đúng cái đã đo trên SQL Server với `1205`, nay đo lại trên
đúng engine đang chạy thay vì chép kết luận sang.

**Từ đó suy ra 409, và suy ra được vì handler không có nhánh nào khác:**
`ConflictExceptionHandler.TryHandleAsync` chỉ làm `Classify(exception)`, `null` thì trả
`false`, khác `null` thì ghi `409`. Mà nhánh "`Classify` khác null ⇒ HTTP 409" đã được đo
riêng **19 lần** trong lần chạy 1 instance (S03 `409×9`, S08 `409×1`, …). Hai mắt xích khớp
nhau ⇒ `40P01` → `409`.

