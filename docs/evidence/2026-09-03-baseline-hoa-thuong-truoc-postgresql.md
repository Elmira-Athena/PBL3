# Ca đối chứng — bất biến hoa/thường TRƯỚC khi chuyển PostgreSQL

**Ngày đo:** 2026-09-03 · **Engine:** SQL Server 2025 (`hushstore_sqlserver_dev`, collation mặc định)
· **Cấu hình:** 1 instance API (`http://localhost:5222`) · **Nhánh:** `fix/muc-I-client-error-leaks`

> Đây là **điểm đo trước**, không phải kết quả. Giá trị của nó chỉ hiện ra khi có điểm đo sau trên
> PostgreSQL. Đo nó **bây giờ** vì sau khi đổi engine thì không đo lại được nữa.

---

## 1. Vì sao phải có ca đối chứng này

Repo **không có `HasCollation` nào** — đã grep toàn bộ `src/`. Nghĩa là không dòng code nào *nói ra*
rằng hệ thống đang dựa vào collation case-insensitive mặc định của SQL Server. Nó chỉ đơn giản
đúng, và không ai biết nó đúng nhờ cái gì.

PostgreSQL phân biệt hoa/thường. **25 chỗ** so sánh chuỗi sẽ đổi hành vi — *không ném lỗi, không ghi
log*. Nặng nhất là hai chỗ trả HTTP **200** trong khi làm sai:

- `PosService.cs:288` — thu ngân gõ mã chữ thường ⇒ voucher **không gắn vào đơn, không trừ lượt**.
- `VoucherRepository.cs:189` — `ExecuteUpdateAsync` khớp 0 dòng ⇒ `TryConsumeByCodesAsync` trả danh
  sách rỗng = *"mọi mã tiêu thụ thành công"*.

Không có điểm đo trước, một lần đỏ sau khi chuyển sẽ **không phân biệt được** *"citext hỏng"* với
*"kịch bản vốn đã sai"*.

## 2. Vì sao bộ đo cũ KHÔNG bắt được

`S02` và `S03` **mù** với đúng lỗi này. Cả hai seed `Code = VoucherCode` rồi gửi lại **đúng chuỗi
đó**:

```csharp
private const string VoucherCode = "LP-VQ1";
…
db.Vouchers.Add(new Voucher { Code = VoucherCode, … });   // seed
…
VoucherCodes = new List<string> { VoucherCode },          // gửi — CÙNG một hằng
```

Gửi lại đúng chuỗi đã lưu thì kết quả **giống hệt nhau** dù cột có phân biệt hoa/thường hay không.
Hai kịch bản này chạy xanh suốt các đợt trước mà không khẳng định gì về hoa/thường.

**Sửa (2026-09-03):** tách làm hai hằng — mã **như lưu** và mã **như người dùng gõ**.

```csharp
private const string VoucherCode        = "LP-VQ1";   // như LƯU trong DB
private const string VoucherCodeAsTyped = "lp-vq1";   // như NGƯỜI DÙNG gõ
```

Một dòng mỗi file. Biến hai kịch bản sẵn có thành phép đo trực tiếp cho quyết định lớn nhất của đợt
7 — `Vouchers.Code` có phải `citext` không — mà không cần thêm kịch bản mới.

## 3. Kết quả — SQL Server

```
── S02: Voucher Quantity = 1, 20 khách dùng đồng thời
   ✅ ĐẠT — Đúng 1 lượt được tiêu thụ.
     · Voucher.UsedCount = 1 (kỳ vọng 1)
     · COUNT(VoucherUsages) = 1 (kỳ vọng 1)
     · Mã HTTP: 200×1, 400×19

── S03: Cùng một khách, MaxUsesPerUser = 1, 10 request
   ✅ ĐẠT — Đúng 1 lượt cho mỗi người.
     · COUNT(VoucherUsages) cho cặp (khách, voucher) = 1 (kỳ vọng 1)
     · Mã HTTP: 200×1, 409×9

Tổng kết: 2 đạt, 0 hỏng, 0 không kết luận.
```

### 🎯 Bằng chứng sắc nhất không nằm ở dấu ✅ mà ở thân phản hồi

```json
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng. Vui lòng bỏ mã này và thử lại."}
```

Probe gửi **`lp-vq1`** chữ thường. Câu lỗi trả về **`LP-VQ1`** chữ hoa. Chuỗi đó lấy từ **bản ghi
trong DB**, không phải từ đầu vào — nên nó chứng minh trực tiếp rằng **truy vấn chữ thường đã khớp
hàng chữ hoa**. Đây là cơ chế được quan sát, không phải suy luận từ một dấu tích xanh.

### Không hồi quy — cả 9 kịch bản

Chạy lại đủ bộ sau khi sửa `S02`/`S03`, cùng cấu hình 1 instance:

```
S01 ✅  S02 ✅  S03 ✅  S04 ✅  S05 ✅  S06 ✅  S07 ✅  S08 ✅  S09 ✅
Tổng kết: 9 đạt, 0 hỏng, 0 không kết luận.
```

Việc tách hằng **không làm hỏng** hai kịch bản đó, chỉ làm chúng khẳng định thêm một điều.

## 4. Cách đọc điểm đo này sau khi chuyển PostgreSQL

| Kết quả trên PostgreSQL | Nghĩa |
|---|---|
| `S02` ✅ và `S03` ✅, câu lỗi vẫn hiện `LP-VQ1` | `citext` đã áp đúng lên `Vouchers.Code` |
| `S02` 🔴 (`UsedCount = 0`) và `S03` ⚠️ KHÔNG KẾT LUẬN, **mã HTTP toàn `400`** | Thiếu `citext` — `GetByCodesWithCategoriesAsync` trả rỗng ⇒ *"Mã giảm giá không tồn tại"* |
| Mã HTTP toàn `500` | Lỗi seed hoặc lỗi provider, **không phải** hoa/thường |

⚠️ `S03` ra **KHÔNG KẾT LUẬN** chứ không phải HỎNG, vì từ góc nhìn của chính nó thì "0 lượt" không
phân biệt được *seed hỏng* với *tra mã hỏng*. Theo luật của repo, **`KHÔNG KẾT LUẬN` ≠ `ĐẠT`**.
Thông báo của `S03` đã được sửa để nêu đích danh hai giả thuyết và cách phân biệt bằng mã HTTP.

## 5. Điều ca đối chứng này KHÔNG chứng minh

- **Chỉ phủ `Vouchers.Code`.** Năm cột còn lại trong nhóm `citext` (`ProductSerials.SerialNumber`,
  `InventoryCheckDetailSerials.SerialNumberRaw`, `ProductVariants.SKU`, `Products.Slug`,
  `Categories.Slug`) **không có kịch bản nào phủ**.
- **Nhóm C (8 chỗ `.Contains()` ở ô tìm kiếm) không được phủ bởi bất kỳ kịch bản nào**, và `citext`
  cũng không che chúng vì các cột đó không đổi kiểu. Đây là lỗ kiểm chứng đã biết, cần một cổng
  grep riêng theo khuôn `check-error-message-leaks.sh`.
- **Không phủ đường GHI.** Ba chỗ ghi không chuẩn hoá (`ProductVariantService.cs:47,97`,
  `ProductService.cs:144`, `ImportReceiptService.cs:163` chỉ `.Trim()`) vẫn nguyên trạng — chính
  chúng là lý do chọn `citext` ở tầng lưu trữ thay vì chuẩn hoá bằng code.

---

## 6. Phụ lục — đo trực tiếp trên PostgreSQL 17.11 vì sao chọn `citext`

Chạy trên `hushstore_postgres_dev` (PostgreSQL 17.11, `citext` 1.6), **trước khi đổi một dòng code
nào**. Mục đích: quyết định `citext` vs collation non-deterministic phải dựa trên phép đo, không
dựa vào trí nhớ về tài liệu.

| Phép thử | Kết quả | Nghĩa |
|---|---|---|
| `text = 'lp-vq1'` (đã lưu `LP-VQ1`) | **0 khớp** | Lỗi được tái hiện — đây là hành vi PostgreSQL mặc định |
| `citext = 'lp-vq1'` | **1 khớp** | Vá nhóm A (11 chỗ `==`) |
| `citext IN ('lp-vq1')` | **1 khớp** | Vá nhóm B (6 chỗ `IN`) |
| `citext LIKE '%vq%'` | **1 khớp** | Dùng được `LIKE` |
| `UNIQUE INDEX` trên `citext` | `duplicate key value violates unique constraint` khi chèn `lp-vq1` lúc đã có `LP-VQ1` | Khôi phục **đúng** ngữ nghĩa unique của SQL Server |
| `EXPLAIN` với `enable_seqscan=off` | `Index Only Scan using ux_ci` | Vẫn ăn index — khác hẳn `LOWER(col)` vốn làm B-tree vô dụng |

### Vì sao loại collation non-deterministic — đo được, không phải nhớ

```
CREATE COLLATION ci_nd (provider=icu, locale='und-u-ks-level2', deterministic=false);
SELECT count(*) FROM t_nd WHERE c LIKE '%vq%';
ERROR:  nondeterministic collations are not supported for LIKE
```

Đây là **ràng buộc cứng**, không phải sở thích. Nhóm C là 8 chỗ `.Contains()` → dịch thành `LIKE`.
Đặt collation non-deterministic lên đúng những cột đó sẽ biến một **bug im lặng** thành **exception
lúc chạy ngay tại ô tìm kiếm** — đổi *loại* lỗi chứ không sửa lỗi, và vỡ đúng lúc có người dùng gõ.
