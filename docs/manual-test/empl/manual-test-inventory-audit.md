# Manual Test Guide — Kiểm kê kho hàng (UC015)

> Thực hiện test với Swagger (`https://localhost:7010/swagger`) hoặc Frontend (`https://localhost:7107`).
> Cần 2 tài khoản: **Employee** (tạo & quét) và **Admin** (phê duyệt).

---

## Thiết lập trước khi test

| Bước | Lệnh / Hành động |
|------|-----------------|
| Khởi động DB | `docker-compose up -d` (từ thư mục `Infrastructure/db/`) |
| Chạy migration | `dotnet ef database update --project src/Infrastructure --startup-project src/API` |
| Chạy API | `dotnet run --project src/API/API.csproj` |
| Chạy Client | `dotnet run --project src/Client/Client.csproj` |
| Lấy JWT Employee | `POST /api/auth/login` với tài khoản Employee |
| Lấy JWT Admin | `POST /api/auth/login` với tài khoản Admin |

---

## Flow 1 — Tạo phiếu toàn kho (Happy Path)

**Mục tiêu:** Tạo phiếu kiểm kê phạm vi AllStore, xác nhận snapshot chốt đúng.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `POST /api/inventory-checks` body: `{ "scopeType": 0 }` (Employee JWT) | HTTP 201, trả về `InventoryCheckDto` với `status=0` (Nháp), `checkCode` dạng `KK-yyyyMMdd-NNN` |
| 2 | Kiểm tra DB: bảng `InventoryChecks` | 1 record mới, `ScopeType=0`, `SnapshotAt` ≈ thời điểm tạo |
| 3 | Kiểm tra DB: bảng `InventoryCheckDetails` | N records — 1 record / variant có serial Available |
| 4 | Kiểm tra DB: bảng `InventoryCheckDetailSerials` | M records — 1 record / serial Available, `ScanStatus=0` (Pending) |
| 5 | `GET /api/inventory-checks/{id}` | Trả về đầy đủ header + `Details` list |

---

## Flow 2 — Tạo phiếu theo danh mục

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `POST /api/inventory-checks` body: `{ "scopeType": 1, "scopeCategoryId": <id danh mục có con> }` | HTTP 201, `ScopeCategoryId` và `ScopeCategoryName` có giá trị |
| 2 | Kiểm tra `InventoryCheckDetails` | Chỉ chứa các variant thuộc danh mục đó **và toàn bộ danh mục con** (đệ quy) |

---

## Flow 3 — Quét Serial: Matched

**Điều kiện:** Phiếu ở trạng thái Nháp, serial có `Status=Available` và thuộc phạm vi phiếu.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `POST /api/inventory-checks/{id}/scan` body: `{ "serialNumber": "<serial hợp lệ>" }` | HTTP 200, `scanStatus=1` (Matched), `message` có nội dung xác nhận |
| 2 | `GET /api/inventory-checks/{id}/dashboard` | `matchedCount` tăng 1, `percentComplete` tăng |
| 3 | Kiểm tra `InventoryCheckDetailSerials` | Row tương ứng `ScanStatus=1`, `ScannedAt` ≠ null, `ScannedByEmployeeId` đúng |
| 4 | Kiểm tra `InventoryCheckDetails` | `ActualQuantity` tăng 1, `MatchedQuantity` tăng 1 |
| 5 | `ProductSerial.Status` trong DB | **KHÔNG** thay đổi (vẫn là Available) |

---

## Flow 4 — Quét Serial: Surplus — Serial đã bán (A1)

**Điều kiện:** Trong snapshot có serial `Available`, nhưng giữa lúc đó serial đã bị bán qua POS/Order (trước khi quét).

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Đổi trực tiếp trong DB: `UPDATE ProductSerials SET Status=2 WHERE SerialNumber='<target>'` | — |
| 2 | `POST /api/inventory-checks/{id}/scan` với serial đó | `scanStatus=3` (Surplus), `surplusNote` chứa "Đã bán tại Đơn hàng..." hoặc ghi chú tương ứng |
| 3 | `GET /api/inventory-checks/{id}/dashboard` | `surplusCount` tăng 1 |

---

## Flow 5 — Quét Serial: Surplus — Serial đang Reserved (A2)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Đổi DB: `UPDATE ProductSerials SET Status=1 WHERE SerialNumber='<target>'` | — |
| 2 | `POST /api/inventory-checks/{id}/scan` với serial đó | `scanStatus=3`, note "Đã giữ chỗ cho Đơn hàng..." |

---

## Flow 6 — Quét Serial: UnknownSurplus (A3) — Serial không có trong DB

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `POST /api/inventory-checks/{id}/scan` body: `{ "serialNumber": "FAKE-XXXX-9999" }` | `scanStatus=4` (UnknownSurplus), `requiresVariantInput=true` |
| 2 | Kiểm tra `InventoryCheckDetailSerials` | 1 row mới với `SerialId=null`, `DetailId=null`, `SerialNumberRaw="FAKE-XXXX-9999"` |
| 3 | Quét lại với `variantIdForUnknown=<id variant>` | Row được gắn `VariantId` |

---

## Flow 7 — Đánh dấu hàng lỗi (A5)

**Điều kiện:** Serial đã quét và ở trạng thái Matched (ScanStatus=1).

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `PUT /api/inventory-checks/{id}/serials/{detailSerialId}/mark-defective` | HTTP 200, `success=true` |
| 2 | Kiểm tra `InventoryCheckDetailSerials` | `ScanStatus=5` (Defective) |
| 3 | `GET /api/inventory-checks/{id}/dashboard` | `matchedCount` giảm 1, `defectiveCount` tăng 1 |
| 4 | `ProductSerial.Status` | **KHÔNG** thay đổi — chỉ thay đổi khi approve |

---

## Flow 8 — Cập nhật lý do chênh lệch

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `PUT /api/inventory-checks/{id}/serials/{detailSerialId}/reason` body: `{ "reason": "Serial bị mất khi vận chuyển nội bộ", "proposedActionNote": "Kiến nghị ghi thất thoát" }` | HTTP 200 |
| 2 | Kiểm tra `InventoryCheckDetailSerials` | `Note` và `ProposedActionNote` đã được cập nhật |

---

## Flow 9 — Gửi duyệt (Submit)

**Điều kiện:** Phiếu ở trạng thái Nháp (Status=0).

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `POST /api/inventory-checks/{id}/submit` | HTTP 200, `success=true` |
| 2 | `GET /api/inventory-checks/{id}` | `status=1` (AwaitingApproval) |
| 3 | Kiểm tra `InventoryCheckDetailSerials` | Tất cả row `ScanStatus=0` (Pending) → đổi thành `ScanStatus=2` (Missing) |
| 4 | Kiểm tra `InventoryCheckDetails` | `MissingQuantity` được tính đúng (= số serial Pending chưa quét) |
| 5 | Thử quét thêm serial | HTTP 400 — phiếu không còn ở trạng thái Nháp |

---

## Flow 10 — Phê duyệt (Approve) — Happy Path

**Điều kiện:** Phiếu Status=1 (AwaitingApproval), dùng Admin JWT.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `POST /api/inventory-checks/{id}/approve` (Admin JWT) | HTTP 200 |
| 2 | `GET /api/inventory-checks/{id}` | `status=2` (Completed), `approvedAt` ≠ null, `approvedByEmployeeName` đúng |
| 3 | Kiểm tra `ProductSerials` với `ScanStatus=Missing` | `Status=5` (Lost) |
| 4 | Kiểm tra `ProductSerials` với `ScanStatus=Defective` | `Status=3` (Defective) |
| 5 | Kiểm tra `ProductSerials` với `ScanStatus=Matched` | `Status=0` (Available) — không đổi |
| 6 | Kiểm tra `InventoryAdjustmentLogs` | Có các record với `AdjustmentType=1` (Lost) / `AdjustmentType=2` (Defective) tương ứng |
| 7 | Kiểm tra `ProductVariants.StockQuantity` | Đã được sync lại đúng (= số serial Available còn lại) |

---

## Flow 11 — Từ chối → Trả về Nháp (A6a)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Submit phiếu (Status → 1) | — |
| 2 | `POST /api/inventory-checks/{id}/reject` (Admin JWT) body: `{ "reason": "Dữ liệu chưa đủ", "returnToDraft": true }` | HTTP 200 |
| 3 | `GET /api/inventory-checks/{id}` | `status=0` (Nháp), `rejectReason` = "Dữ liệu chưa đủ" |
| 4 | Kiểm tra `InventoryCheckDetailSerials` | Tất cả row Missing → trở về Pending; các row Surplus/UnknownSurplus bị xóa |
| 5 | Kiểm tra `InventoryCheckDetails` | Các count (Missing, Surplus...) được reset về 0 |
| 6 | Quét lại serial | Hoạt động bình thường (phiếu trở về Draft) |
| 7 | `ProductSerial.Status` | **KHÔNG** thay đổi gì cả |

---

## Flow 12 — Từ chối → Hủy phiếu (A6b)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Submit phiếu | — |
| 2 | `POST /api/inventory-checks/{id}/reject` body: `{ "reason": "Sai phạm vi", "returnToDraft": false }` (Admin JWT) | HTTP 200 |
| 3 | `GET /api/inventory-checks/{id}` | `status=3` (Cancelled), dữ liệu serial giữ nguyên để audit |
| 4 | `ProductSerial.Status` | **KHÔNG** thay đổi |

---

## Flow 13 — Hủy phiếu nháp

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | `POST /api/inventory-checks/{id}/cancel` (Employee JWT, phiếu của chính họ, Status=0) | HTTP 200 |
| 2 | `GET /api/inventory-checks/{id}` | `status=3` (Cancelled) |

---

## Flow 14 — Business Continuity (BR1): Serial được bán trong cửa sổ kiểm kê

**Kịch bản:** Serial trong snapshot là Pending (chưa quét), nhưng trong thời gian chờ duyệt, serial đó được bán.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tạo phiếu, submit (không quét serial X) | Serial X ở Missing sau submit |
| 2 | Trước khi approve: đổi DB `UPDATE ProductSerials SET Status=2 WHERE SerialNumber='X'` | Giả lập bán trong cửa sổ |
| 3 | `POST /api/inventory-checks/{id}/approve` (Admin JWT) | HTTP 200 |
| 4 | Kiểm tra `InventoryCheckDetailSerials` row của serial X | `ResolvedDuringApproval=true`, `Note` chứa thông tin giải thích |
| 5 | Kiểm tra `ProductSerial X.Status` | **KHÔNG** bị đổi thành Lost (vẫn là Sold=2) |
| 6 | Kiểm tra `InventoryAdjustmentLogs` | **KHÔNG** có record cho serial X |

---

## Edge Cases

### EC-1: Quét trùng serial trong cùng phiếu

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Quét serial S lần 1 → Matched | OK |
| Quét lại serial S lần 2 | HTTP 400, `message` = "Serial đã được quét trong phiếu này", `isDuplicateScan=true` |

---

### EC-2: Quét serial không thuộc phạm vi Category

**Điều kiện:** Phiếu ScopeType=Category, serial thuộc danh mục khác.

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Quét serial của variant ngoài scope | `scanStatus=3` (Surplus), note "Ngoài phạm vi kiểm kê" |

---

### EC-3: Submit phiếu khi chưa quét bất kỳ serial nào

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| `POST /submit` khi 0 serial đã quét | HTTP 200 — cho phép submit (business requirement: toàn bộ chuyển Missing) |
| Sau submit: `MissingCount` | = tổng số serial trong snapshot |

---

### EC-4: Approve phiếu đã Completed

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| `POST /api/inventory-checks/{id}/approve` trên phiếu Status=2 | HTTP 400, message lỗi tiếng Việt |

---

### EC-5: Approve phiếu ở trạng thái Nháp

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| `POST /api/inventory-checks/{id}/approve` trên phiếu Status=0 | HTTP 400 |

---

### EC-6: Employee gọi endpoint Admin-only

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| `POST /approve` với Employee JWT | HTTP 403 Forbidden |
| `POST /reject` với Employee JWT | HTTP 403 Forbidden |

---

### EC-7: Employee hủy phiếu của người khác

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Employee A gọi `POST /cancel` trên phiếu của Employee B | HTTP 400, message "Bạn không có quyền hủy phiếu này" (hoặc tương đương) |

---

### EC-8: Hủy phiếu đang AwaitingApproval (Employee)

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Employee gọi `POST /cancel` trên phiếu của mình khi Status=1 | HTTP 400 — chỉ Admin mới hủy được phiếu ở trạng thái này |

---

### EC-9: Tạo phiếu ScopeType=Category mà không có ScopeCategoryId

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| `POST /api/inventory-checks` body: `{ "scopeType": 1 }` | HTTP 400, validation error "ScopeCategoryId bắt buộc khi kiểm kê theo danh mục" |

---

### EC-10: Mark Defective trên serial không phải Matched

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| `PUT /mark-defective` trên row có `ScanStatus=2` (Missing) | HTTP 400 — chỉ Matched mới được đánh dấu lỗi |

---

### EC-11: Submit phiếu không phải người tạo (Employee)

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Employee B `POST /submit` trên phiếu của Employee A | HTTP 400, message lỗi phân quyền |

---

### EC-12: Tạo 2 phiếu cùng lúc — CheckCode không trùng

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Tạo phiếu liên tiếp trong cùng ngày | `KK-yyyyMMdd-001`, `KK-yyyyMMdd-002`, ... tăng dần, không có giá trị trùng |

---

### EC-13: Tạo phiếu khi không có serial Available trong scope

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| `POST /api/inventory-checks` với scope Category không có serial Available | HTTP 201 nhưng `Details` = empty list, dashboard `totalSystem=0` |

---

## Kiểm tra UI Frontend

| Trang | Điểm cần kiểm tra |
|-------|--------------------|
| `/inventory/audit` | Bảng hiển thị đúng status chip màu sắc; nút "Tạo phiếu" mở dialog; Admin thấy icon Phê duyệt trên hàng Status=1 |
| Dialog tạo phiếu | Radio AllStore/Category hoạt động; khi chọn Category, dropdown danh mục hiện ra có phân cấp (em gạch đầu dòng); validation khi bấm Tạo mà chưa chọn danh mục |
| `/inventory/audit/{id}` (Draft) | Panel quét hiển thị; nhấn Enter sau khi nhập serial → quét; dashboard counts cập nhật sau mỗi lần quét; Alert màu xanh/vàng/đỏ hiện đúng |
| `/inventory/audit/{id}` (Draft) | Tab "Danh sách Serial": icon đánh dấu lỗi chỉ hiện trên row Matched; icon cập nhật lý do chỉ hiện trên Missing/Surplus |
| `/inventory/audit/{id}` (AwaitingApproval) | Panel quét ẩn; hiện Alert "Đang chờ Admin duyệt"; Admin thấy nút "Đi đến trang phê duyệt" |
| `/admin/inventory-audit/{id}` | Nút Phê duyệt và Từ chối hiển thị khi Status=1; warning note trước khi approve; dialog từ chối có radio ReturnToDraft/Cancel |
| `/inventory/audit/{id}` (Completed) | Không có action button nào; hiện thông tin người phê duyệt |

---

## Rollback / Clean Up

Sau khi test xong, nếu cần reset data:

```sql
-- Xóa toàn bộ dữ liệu kiểm kê (dev only)
DELETE FROM InventoryAdjustmentLogs;
DELETE FROM InventoryCheckDetailSerials;
DELETE FROM InventoryCheckDetails;
DELETE FROM InventoryChecks;

-- Khôi phục serial bị đổi status thủ công trong test
UPDATE ProductSerials SET Status = 0 WHERE SerialNumber IN ('SERIAL-X', 'SERIAL-Y');
```
