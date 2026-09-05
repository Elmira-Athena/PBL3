# Bằng chứng — LoadProbe 9/9 trên PostgreSQL 17.11

**Ngày đo:** 2026-09-03 · **Engine:** PostgreSQL 17.11 (`hushstore_postgres_dev`) ·
**Cấu hình:** 1 instance API · **Nhánh:** `fix/muc-I-client-error-leaks`

> Ca đối chứng trên SQL Server:
> [2026-09-03-baseline-hoa-thuong-truoc-postgresql.md](2026-09-03-baseline-hoa-thuong-truoc-postgresql.md)

---

## 1. Kết quả

```
S01 ✅ 50 đơn, 50 mã phân biệt              · 200×50
S02 ✅ Đúng 1 lượt được tiêu thụ            · 200×1, 400×19
S03 ✅ Đúng 1 lượt cho mỗi người            · 200×1, 409×9
S04 ✅ Đúng 1 phiếu chưa đóng               · 200×10
S05 ✅ Đúng một lần duyệt được ghi nhận     · 200×10
S06 ✅ Đúng 1 bản ghi điều chỉnh            · 200×1, 400×4
S07 ✅ Serial không bán được, ghi nhận thất thoát — nhất quán
S08 ✅ Đúng 1 báo giá Pending               · 200×1, 409×1
S09 ✅ Đúng một lời gọi được chấp nhận      · 200×1, 400×1

Tổng kết: 9 đạt, 0 hỏng, 0 không kết luận.
```

## 2. Schema — đo trong DB, không đọc file migration

| Tính chất | Cách kiểm | Kết quả |
|---|---|---|
| 6 cột `citext` | `information_schema.columns WHERE udt_name='citext'` | `Categories.Slug`, `InventoryCheckDetailSerials.SerialNumberRaw`, `ProductSerials.SerialNumber`, `ProductVariants.SKU`, `Products.Slug`, `Vouchers.Code` |
| 4 computed column **STORED** | `information_schema.columns WHERE is_generated='ALWAYS'` | `InventoryCheckDetails.Difference`, `OrderDetails.TotalLine`, `QuotationItems.LineTotal`, `ServiceInvoiceItems.LineTotal` |
| Partial index giữ `WHERE` | `pg_indexes.indexdef` | `… USING btree ("SerialId") WHERE (("Status" <> 3) AND … AND ("IsDeleted" = false))` |
| `xmin` là concurrency token | migration | `xmin = table.Column<uint>(type: "xid", rowVersion: true)` × 6 bảng |

🚨 **Phép kiểm partial index đáng giữ thành cổng CI.** Nếu ai đó "sửa" lỗi cú pháp `HasFilter`
bằng cách **bỏ nó đi**, index thành unique TOÀN PHẦN trên `SerialId` ⇒ mỗi serial chỉ được đúng
một phiếu dịch vụ **trọn đời**, kể cả phiếu đã đóng. Triệu chứng chỉ lộ trên dữ liệu **có lịch
sử** ⇒ không lộ trên DB vừa seed ⇒ **không lộ trước demo**. Một dòng `SELECT indexdef … LIKE
'%WHERE%'` rẻ hơn nhiều so với một kịch bản LoadProbe.

## 3. `citext` — kiểm đầu-cuối, không phải kiểm hình dạng

`S02`/`S03` seed mã **`LP-VQ1`** chữ hoa nhưng gửi **`lp-vq1`** chữ thường. Thân phản hồi:

```json
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng. Vui lòng bỏ mã này và thử lại."}
```

Câu lỗi hiện **`LP-VQ1`** chữ hoa — chuỗi đó đọc từ **bản ghi trong DB**, nên nó chứng minh truy
vấn chữ thường đã khớp hàng chữ hoa. **Giống hệt ca đối chứng trên SQL Server.**

Ba điểm đo khép kín:

| Điểm | Kết quả |
|---|---|
| SQL Server (collation CI) | ✅ — bất biến đang đúng nhờ **cấu hình DB**, không nhờ dòng code nào |
| PostgreSQL, cột `text` | **0 khớp** — đo trực tiếp bằng SQL, lỗi được tái hiện |
| PostgreSQL, cột `citext` | ✅ — bất biến khôi phục |

## 4. Ba đường code MỚI có thật sự chạy không

Câu hỏi bắt buộc, vì cả đợt này đang truy đúng lớp lỗi *"code trông như đang bảo vệ cái gì đó
nhưng không đường nào tới được nó"*. Đếm trong log của API:

| Đường | Số lần | Ở đâu |
|---|---|---|
| `ConflictClassifier.IsUniqueViolation(…, ServiceTicketSerialOpen)` | **9** | S04 |
| `ConflictClassifier.IsUniqueViolation(…, InventoryAdjustmentLogAuditSerial)` | **4** | S06 → `400×4` |
| `ConflictExceptionHandler` → 409 | **19** | S03 (`409×9`), S08 (`409×1`) |

Và nó **phân biệt đúng**: kẻ thua ở S06 nhận **câu nghiệp vụ theo ngữ cảnh** (`400`), kẻ thua ở
S03 nhận **409 chung**. Đây chính là khoản lãi của việc chuyển provider — `PostgresException`
mang `ConstraintName`, thứ `SqlException` không có, nên phép phân biệt nay đúng **theo cấu trúc**
thay vì dựa vào lập luận cục bộ *"trong phạm vi hàm này chỉ có một nguồn"*.

## 5. `setval` — đo bằng hành vi, không bằng đọc script

Seed chèn `Id` tường minh cho 4 bảng identity (122 dòng dữ liệu). Nếu thiếu `setval`, sequence
vẫn đứng ở `1` và **mọi thứ vẫn xanh** cho tới lần đầu admin tạo bản ghi mới.

```sql
INSERT INTO "Manufacturers" ("Name", …) VALUES ('LP-SETVAL-TEST', …) RETURNING "Id";
→ 22
```

21 hàng đã seed, hàng mới nhận `Id = 22`. Nếu thiếu `setval` thì nó nhận `1` và đâm khoá chính.

## 6. Ba lỗi tự tạo trong quá trình chuyển, và cách chúng lộ ra

Ghi lại vì cả ba đều thuộc loại *"grep nói đã xong, hành vi nói chưa"*.

| Lỗi | Vì sao lọt | Lộ ra nhờ |
|---|---|---|
| Sót cột `citext` thứ **6** (`InventoryCheckDetailSerials.SerialNumberRaw`) | Script bám mẫu `HasIndex(x).IsUnique()`; cột này nằm trong index **tổ hợp** | **Đếm** cột `citext` trong migration sinh ra (5 ≠ 6) |
| Định danh trần trong LoadProbe — 3 vòng: mảnh chuỗi nối bắt đầu `OR `, câu `UPDATE`, cột trần trong khối dọn seed | Bộ lọc chỉ xử lý mảnh có `DELETE FROM`/`SELECT` | Chạy thật → `relation "productserials" does not exist` |
| `PRINT` đứng **trước** 4 lệnh `setval` | — | `ON_ERROR_STOP=1` dừng ở `PRINT` ⇒ `setval` không chạy. **Không có cờ đó thì seed "thành công" mà sequence vẫn ở 1** |

## 7. Điều lần đo này KHÔNG chứng minh

- **Chỉ 1 instance.** S04/S07/S08 phụ thuộc thời điểm — một lần ✅ ở 1 instance **không** là bằng
  chứng an toàn ở 2 instance. Phải chạy lại trên `docker-compose.multi.yml`.
- **Deadlock chưa đo lại trên PostgreSQL.** Bằng chứng `40P01` → 409 hiện **chưa có**;
  `docs/evidence/2026-09-03-conflict-classifier.md` đo trên SQL Server (`1205`) và **không được
  chép sang**. Đã tra: `40P01` nằm trong danh sách transient của Npgsql y như `1205` của SQL
  Server, nên đường bọc `RetryLimitExceededException` vẫn tồn tại — nhưng *tra tài liệu* không
  thay được *đo*.
- **Nhóm C (8 chỗ `.Contains()` ở ô tìm kiếm) chưa được phủ bởi bất kỳ kịch bản nào**, và `citext`
  **không** che chúng vì các cột đó không đổi kiểu.
- **`DateTime`/`DateOnly` chưa sửa.** Chưa có phép đo nào chạm cửa sổ hiệu lực voucher hay ngày sinh.
