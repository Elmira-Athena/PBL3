# Design: Nút Duyệt Đơn & Validate Serial Real-time

**Ngày:** 2026-05-15  
**Trạng thái:** Approved

---

## 1. Tổng quan

Hai tính năng độc lập cần bổ sung:

1. **Nút "Duyệt đơn"** — Admin/Employee có thể duyệt thủ công đơn hàng đang ở trạng thái `Pending` (status=0) → `Confirmed` (status=1). Đơn POS (`PosDraft`, status=6) vẫn tự động xử lý như cũ.
2. **Validate serial real-time** — Khi nhập mã serial trên trang xuất kho, hệ thống gọi API kiểm tra ngay lập tức. Mã hợp lệ mới được thêm vào danh sách; mã sai hiển thị thông báo lỗi cụ thể và không được thêm.

---

## 2. Tính năng 1 — Nút Duyệt Đơn

### Phạm vi
- Chỉ áp dụng cho đơn có `Status = Pending (0)`
- Role: `Admin`, `Employee`
- Đơn `PosDraft (6)` không liên quan, giữ nguyên flow hiện tại

### Backend

**Service** — `IOrderService` / `OrderService`:
```
Task<ApiResult<bool>> ConfirmOrderAsync(int orderId)
```
Logic:
- Load đơn hàng theo `orderId`
- Kiểm tra `Status == Pending (0)` → nếu không, trả về lỗi tiếng Việt
- Chuyển `Status = Confirmed (1)`, cập nhật `ModifiedDate`
- Lưu và trả `ApiResult<bool>.Ok(true, "Đã duyệt đơn hàng thành công.")`

**Controller** — `OrdersController`:
```
PUT /api/orders/{id}/confirm
[Authorize(Roles = "Admin, Employee")]
```
Trả về `400` nếu service thất bại, `200` nếu thành công.

### Frontend (Client)

**OrderList.razor:**
- Thêm nút "Duyệt" (Color.Success, Size.Small) trong cột Action khi `Status == Pending`
- Sau khi duyệt thành công: reload danh sách + hiện snackbar "Đã duyệt đơn hàng thành công."

**OrderDetail.razor:**
- Thêm nút "Duyệt đơn" (Color.Success) trong khu vực action khi `Status == Pending`
- Sau khi duyệt thành công: reload chi tiết đơn + hiện snackbar

**Client Service** — `IOrderClientService` / `OrderClientService`:
```
Task<ApiResult<bool>> ConfirmOrderAsync(int orderId)
```
Gọi `PUT /api/orders/{id}/confirm`.

---

## 3. Tính năng 2 — Validate Serial Real-time

### Phạm vi
- Áp dụng tại trang `ExportOrder.razor`
- Khi người dùng nhấn Enter sau khi nhập mã serial
- Validate trước khi thêm vào danh sách

### Backend

**Endpoint mới** — `InventoryController`:
```
GET /api/inventory/serials/validate?serialNo={serialNo}&variantId={variantId}
[Authorize(Roles = "Admin, Employee")]
```

Response: `ApiResult<bool>`

Logic kiểm tra (theo thứ tự):
1. Serial tồn tại trong `ProductSerials` → nếu không: "Mã Serial '{serialNo}' không tồn tại trong hệ thống."
2. Serial đúng `VariantId` → nếu không: "Mã Serial '{serialNo}' không thuộc sản phẩm yêu cầu."
3. Serial có `Status == Available (0)` → nếu không: "Mã Serial '{serialNo}' không ở trạng thái Available (có thể đã bán hoặc hỏng)."
4. Tất cả hợp lệ → trả `Ok(true)`

**Service** — `IInventoryExportService` / `InventoryExportService`:
```
Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId)
```

### Frontend (Client)

**`IInventoryExportClientService`:**
```
Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId)
```

**`InventoryExportClientService`:**
Gọi `GET /api/inventory/serials/validate?serialNo=...&variantId=...`

**`ExportOrder.razor` — sửa `OnSerialKeyUp`:**

Flow mới:
1. Kiểm tra empty → bỏ qua (giữ nguyên)
2. Kiểm tra đã đủ số lượng → cảnh báo (giữ nguyên)
3. Kiểm tra trùng lặp nội bộ → lỗi (giữ nguyên)
4. **MỚI**: Set `_isScanning = true` (disable input, hiện spinner icon)
5. **MỚI**: Gọi `ValidateSerialAsync(code, _activeDetail.VariantId)`
   - Nếu `!result.Success` → Snackbar lỗi với `result.Message`, xóa input, `_isScanning = false`, return
   - Nếu thành công → tiếp tục
6. Thêm serial vào `ScannedSerials`
7. `_isScanning = false`, xóa input, refocus

Lưu ý: `_isScanning` đã tồn tại trong component (disable input + icon spinner) — tận dụng trực tiếp.

---

## 4. Các file cần tạo/sửa

### Tính năng 1
| File | Thao tác |
|------|----------|
| `src/Service/Orders/IOrderService.cs` | Thêm `ConfirmOrderAsync` |
| `src/Service/Orders/OrderService.cs` | Implement `ConfirmOrderAsync` |
| `src/API/Controllers/OrdersController.cs` | Thêm endpoint `PUT /{id}/confirm` |
| `src/Client/Services/Order/IOrderClientService.cs` | Thêm `ConfirmOrderAsync` |
| `src/Client/Services/Order/OrderClientService.cs` | Implement `ConfirmOrderAsync` |
| `src/Client/Pages/Orders/OrderList.razor` | Thêm nút Duyệt khi Status=Pending |
| `src/Client/Pages/Orders/OrderDetail.razor` | Thêm nút Duyệt khi Status=Pending |

### Tính năng 2
| File | Thao tác |
|------|----------|
| `src/Service/Inventory/IInventoryExportService.cs` | Thêm `ValidateSerialAsync` |
| `src/Service/Inventory/InventoryExportService.cs` | Implement `ValidateSerialAsync` |
| `src/API/Controllers/InventoryController.cs` | Thêm endpoint `GET /serials/validate` |
| `src/Client/Services/Inventory/IInventoryExportClientService.cs` | Thêm `ValidateSerialAsync` |
| `src/Client/Services/Inventory/InventoryExportClientService.cs` | Implement `ValidateSerialAsync` |
| `src/Client/Pages/Inventory/ExportOrder.razor` | Sửa `OnSerialKeyUp` |

---

## 5. Không nằm trong scope

- Thông báo email/SMS khi đơn được duyệt
- Lịch sử thay đổi trạng thái đơn hàng
- Validate serial theo batch (chỉ validate từng mã)
- Thay đổi flow POS
