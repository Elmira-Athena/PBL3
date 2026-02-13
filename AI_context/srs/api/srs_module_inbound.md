# IMPLEMENTATION PLAN: IMPORT RECEIPT (BACKEND API)

**Project:** HushStore
**Module:** Inventory - Import Receipts (Nhập kho)
**Architecture:** Repository Pattern + Unit of Work (Transaction)

---

## 1. MỤC TIÊU (OBJECTIVES)

Xây dựng API để xử lý nghiệp vụ nhập kho. Đảm bảo quy tắc **"100% Serial Managed"**: Mỗi sản phẩm nhập vào phải đi kèm với một danh sách mã Serial tương ứng với số lượng nhập.

---

## 2. DATA TRANSFER OBJECTS (SHARED LAYER)

Dựa trên UI Figma, API sẽ nhận một request tổng hợp. AI cần tạo các DTO sau:

### 2.1. Request DTOs (Write)

* **`CreateImportReceiptRequest`**
* `SupplierId` (int): Bắt buộc.
* `Note` (string): Ghi chú phiếu nhập.
* `List<ImportReceiptDetailRequest> Details`: Danh sách các sản phẩm nhập. Bắt buộc `Count > 0`.


* **`ImportReceiptDetailRequest`**
* `VariantId` (int): Sản phẩm biến thể được chọn.
* `Quantity` (int): Số lượng nhập. Phải > 0.
* `ImportPrice` (decimal): Giá nhập (Giá vốn). Phải >= 0.
* `List<string> SerialNumbers`: Danh sách các mã vạch quét từ vỏ hộp.
* **Validation Quan trọng:** `SerialNumbers.Count` BẮT BUỘC PHẢI BẰNG `Quantity`.

### 2.2. Response DTOs (Read)

* `ImportReceiptDto`: Chứa thông tin chung, TotalAmount, SupplierName, EmployeeName (tạm thời hardcode người nhập do chưa có Auth).
* `ImportReceiptDetailDto`: Chứa VariantName, Quantity, ImportPrice, SubTotal.

---

## 3. BUSINESS LOGIC (SERVICE LAYER) - *Cực kỳ quan trọng*

AI phải implement `IImportReceiptService` và `ImportReceiptService` với luồng logic (Walkthrough) tạo mới phiếu nhập như sau:

**Bước 1: Validate đầu vào (Pre-checks)**

* Kiểm tra `SupplierId` có tồn tại không.
* Kiểm tra danh sách `SerialNumbers` truyền lên có bị trùng lặp **nội bộ** không (Quét trùng 2 lần 1 mã trong cùng 1 request).
* Kiểm tra `SerialNumbers` đã tồn tại trong bảng `ProductSerials` chưa (Tránh việc phiếu nhập trước đã nhập mã này rồi).

**Bước 2: Mở Database Transaction (`IDbContextTransaction`)**

**Bước 3: Insert Header (`ImportReceipts`)**

* Sinh `ReceiptCode` tự động (VD: `PN-20260214-001`).
* Tính `TotalAmount` = Tổng (`Quantity` * `ImportPrice`) của các dòng.
* Lưu xuống DB để lấy `ReceiptId`.

**Bước 4: Insert Details & Serials (Vòng lặp)**

* Duyệt qua từng `ImportReceiptDetailRequest`.
* Insert vào bảng `ImportReceiptDetails`.
* Duyệt qua danh sách `SerialNumbers` của dòng đó:
* Tạo object `ProductSerial`.
* Gán `VariantId`, `ImportReceiptId = ReceiptId`, `Status = 0 (Available)`.
* Thêm vào context.



**Bước 5: Đồng bộ Tồn kho (Stock Sync)**

* Cộng dồn số lượng `Quantity` vào cột `StockQuantity` của bảng `ProductVariants`.

**Bước 6: Commit Transaction**

* Nếu có bất kỳ lỗi nào xảy ra ở các bước trên -> Rollback toàn bộ.

---

## 4. API ENDPOINTS (CONTROLLER)

* `POST /api/import-receipts`: Tạo phiếu nhập.
* `GET /api/import-receipts`: Lấy danh sách phiếu nhập (Lịch sử nhập kho).
* `GET /api/import-receipts/{id}`: Xem chi tiết 1 phiếu nhập (Bao gồm danh sách Serial đã nhập để sau này đối soát).
