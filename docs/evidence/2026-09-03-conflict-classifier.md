# Bằng chứng — gộp phân loại xung đột vào `ConflictClassifier`

**Ngày đo:** 2026-09-03 · **Cấu hình:** 1 instance API (`http://localhost:5222`), SQL Server 2025
trong container `hushstore_sqlserver_dev` · **Nhánh:** `fix/muc-I-client-error-leaks`

---

## 1. Vấn đề — ba đường, cùng một nguyên nhân

Cùng một câu hỏi ("exception này có phải xung đột đồng thời không") được trả lời ở **hai nơi độc
lập**, và hai danh sách đã trôi khỏi nhau:

| Nơi | Nhận diện được |
|---|---|
| 27 chốt controller (quyết định cái gì **thoát ra**) | `DbUpdateConcurrencyException`, `SqlException 2601/2627` |
| `ConflictExceptionHandler` (quyết định cái gì thành **409**) | ba loại trên **+ `1205`** (deadlock) **+ `ConcurrentModificationException`** |

Giao của hai danh sách mới là thứ thật sự chạy. Phần dôi ra ở handler là **code chết**.

### Ba khoảng trống đo được

| # | Khoảng trống | Cách đo | Kết quả |
|---|---|---|---|
| 1 | Chốt controller không có `1205` | `grep -c "2601 or 2627"` = 27; `grep -rn "1205" src/` | `1205` xuất hiện đúng **1** lần trong toàn `src/` — chính dòng khai báo trong handler. Không đường nào tới được nó |
| 2 | `RetryLimitExceededException` không được gỡ bọc | `grep -rn "RetryLimitExceeded" src/` | **0 chỗ**, trong khi `EnableRetryOnFailure` đã bật ở `Program.cs:212` |
| 3 | 8 action **đọc** nuốt trọn mọi thứ | phân loại từng `catch (Exception)` theo động từ HTTP | 8/8 là `GET`. Tiền đề cũ *"GET không sinh được hai loại này"* đúng cho `2601/2627`, **sai cho `1205`** |

Khoảng trống 2 là đường khó thấy nhất. Deadlock nằm trong danh sách transient của SQL Server nên
nó **bị thử lại**; hết lượt thì EF Core bọc nguyên nhân gốc vào `RetryLimitExceededException`.
Phép so khớp cũ — `DbUpdateException { InnerException: SqlException }` hoặc `SqlException` trần —
là so khớp **một tầng**, không khớp cái nào ⇒ rơi xuống handler tổng ⇒ **500**.

> ⚠️ **Chỉ `1205` mới thật sự đi qua đường bọc này.** `2601/2627` **không** transient nên
> execution strategy ném lại nguyên trạng, không bọc. Hàng "2601 bọc trong Retry" ở bảng dưới là
> ca **phòng thủ**, không phải đường có thật hôm nay.

---

## 2. Cách sửa

Một `ConflictClassifier` (`src/Infrastructure/Concurrency/`) làm **nguồn sự thật duy nhất**.
Controller hỏi `IsConflict`, handler hỏi `Classify` — **cùng một hàm**, nên chúng không lệch được
nữa. Nó **đi hết chuỗi `InnerException`** (chặn ở 16 tầng) thay vì so khớp một tầng, nên không cần
nhắc tên `RetryLimitExceededException` và vẫn đúng cho mọi lớp bọc viết sau.

```csharp
catch (BusinessRuleException ex) { return ApiResult<T>.Fail(ex.Message); }
catch (Exception ex) when (ConflictClassifier.IsConflict(ex)) { throw; }   // → 409
catch (Exception ex) { … }                                                // PHẢI đứng cuối
```

**35 chốt** ở 4 controller (27 mutation + 8 đọc) + 2 chốt ở `OrderService`.

---

## 3. Đo — deadlock THẬT, không phải exception giả

`tools/`-ngoài, probe dùng hai kết nối khoá hai bảng theo **thứ tự ngược nhau** để ép SQL Server
tự chọn nạn nhân, rồi bắt `SqlException` sinh ra. Vi phạm unique cũng là thật (`INSERT` trùng khoá
chính).

```
Bước 1: ép deadlock  → SqlException, Number = 1205  ✓
Bước 2: ép trùng khoá → SqlException, Number = 2627  ✓
```

### Bảng MỚI vs CŨ

Cột **CŨ** là logic phân loại chép nguyên văn từ `ConflictExceptionHandler` trước khi sửa — **ca
đối chứng âm**. Không có cột này thì "đã sửa" chỉ là lời khẳng định.

| Tình huống | MỚI | CŨ | |
|---|---|---|---|
| `1205` trần | xung đột | xung đột | ✓ |
| `1205` trong `DbUpdateException` | xung đột | xung đột | ✓ |
| **`1205` trong `RetryLimitExceededException`** | xung đột | **không** | ✓ ← **CŨ ĐỂ LỌT (thành 500)** |
| **`1205` trong Retry → DbUpdate (3 tầng)** | xung đột | **không** | ✓ ← **CŨ ĐỂ LỌT (thành 500)** |
| `2601/2627` trần | xung đột | xung đột | ✓ |
| `2601/2627` trong `DbUpdateException` | xung đột | xung đột | ✓ |
| `2601/2627` trong Retry → DbUpdate | xung đột | **không** | ✓ ← CŨ ĐỂ LỌT *(ca phòng thủ)* |
| `ConcurrentModificationException` | xung đột | xung đột | ✓ |
| `DbUpdateConcurrencyException` | xung đột | xung đột | ✓ |
| `BusinessRuleException` (**không** được nuốt) | không | không | ✓ |
| `InvalidOperationException` thường | không | không | ✓ |
| `SqlException` khác (bảng không tồn tại) | không | không | ✓ |

**12/12 đúng, 0 hồi quy.**

> "Hồi quy" ở đây định nghĩa chặt: **CŨ đúng mà MỚI sai**. Khác hẳn "MỚI vá được chỗ CŨ hỏng" —
> ba dòng in đậm thuộc loại thứ hai.

### Câu trả cho người dùng

```
1205                   → "Hệ thống đang bận, vui lòng thử lại sau giây lát."
2601/2627              → "Dữ liệu này vừa được người khác tạo hoặc thay đổi. Vui lòng tải lại trang và thử lại."
ConcurrentModification → "Phiếu vừa được người khác duyệt."      ← nguyên văn từ chốt trong transaction
BusinessRule           → null ✓                                   ← KHÔNG bị xếp là xung đột
```

`1205` cố ý có câu **khác** hai số kia: không ai làm gì sai, giao dịch chỉ xui, và bấm lại gần như
chắc chắn thành công.

---

## 4. Đầu-cuối qua HTTP

Handler bị mổ lại (bỏ bản phân loại riêng, uỷ quyền cho classifier), nên phải kiểm thật.

`LoadProbe S03` — cùng một khách, `MaxUsesPerUser = 1`, 10 request song song:

```
✅ ĐẠT — Đúng 1 lượt cho mỗi người.
  · COUNT(VoucherUsages) = 1 (kỳ vọng 1)
  · Mã HTTP: 200×1, 400×4, 409×5
  ↳ 409: {"success":false,"message":"Dữ liệu này vừa được người khác tạo hoặc thay đổi. …"}
  ↳ 400: {"success":false,"message":"Bạn đã sử dụng mã 'LP-VMU' đủ số lần cho phép (tối đa 1 lần/khách).","data":null}
```

Hai điều đáng chú ý ở thân phản hồi:

- **409 không có khoá `data`** — bản vá `ErrorResponseJson` còn nguyên tác dụng sau khi mổ handler.
- **400 giữ NGUYÊN VĂN câu nghiệp vụ** theo ngữ cảnh, không bị nuốt thành câu chung. Đây là điều
  `CLAUDE.md` cảnh báo là "hồi quy UX nặng hơn lỗi ban đầu", và nó **không** xảy ra.

### Hồi quy toàn bộ

`LoadProbe` 9/9 kịch bản, **1 instance**:

```
S01 ✅  S02 ✅  S03 ✅  S04 ✅  S05 ✅  S06 ✅  S07 ✅  S08 ✅  S09 ✅
Tổng kết: 9 đạt, 0 hỏng, 0 không kết luận.
```

> ⚠️ **Đây là cấu hình 1 instance.** S04/S07/S08 phụ thuộc thời điểm — một lần ✅ ở 1 instance
> **không** là bằng chứng an toàn ở 2 instance. Lần đo này chỉ chứng minh **không hồi quy**, không
> chứng minh gì thêm.

### Cổng chặn

```
check-error-message-leaks.sh all  → Sạch [all]: 249 file, 0 chỗ.  (mã thoát 0)
grep -rn 'catch (InvalidOperationException' src/  → rỗng ✓
dotnet build PBL3.sln  → 0 Error(s)
```

---

## 5. Ngoại lệ có chủ ý — 2 chỗ vẫn tự liệt kê số lỗi

`ServiceTicketService` (tiếp nhận trùng serial) và `InventoryCheckService` (phê duyệt trùng)
**dịch** vi phạm unique thành câu nghiệp vụ riêng thay vì rethrow. Chúng phải giữ `2601/2627`:

đổi sang `IsConflict` thì một **deadlock** sẽ được báo là *"Sản phẩm này đã có phiếu sửa chữa chưa
đóng."* — **sai sự thật**, và người dùng sẽ đi tìm một phiếu không tồn tại.

**Luật rút ra:** dùng `IsConflict` ở chỗ **rethrow**; không dùng ở chỗ **dịch nghĩa**.

---

## 6. Còn nợ

- Chưa đo ở **2 instance** (`docker-compose.multi.yml`). Bốn kịch bản phụ thuộc thời điểm cần đo
  lại ở đó trước khi tin bất kỳ dấu ✅ nào.
- Chưa đo **deadlock đi hết đường HTTP** thành 409. Probe chứng minh phân loại đúng trên exception
  thật; chuỗi controller → middleware được bảo đảm bằng **cấu trúc** (hai bên gọi cùng một hàm),
  không bằng phép đo. Ép một action controller deadlock đúng lúc là việc của LoadProbe, chưa có
  kịch bản nào làm.
