# PROJECT CONTEXT & TECHNICAL SPECIFICATION
## Project Name: IT Hardware E-commerce & Management System

---

## 1. TECHNOLOGY STACK (CORE)
* **Backend Framework:** ASP.NET Core 8 Web API.
* **Frontend Framework:** Blazor WebAssembly (Standalone) - *Ưu tiên dùng C# full-stack để đẩy nhanh tiến độ.*
* **Database:** SQL Server 2019+ (Triển khai bằng Entity Framework Core 8 - Code First).
* **UI Component Library:** MudBlazor (Material Design).
* **Authentication:** JWT Bearer Token (IdentityServer hoặc Custom Implementation).

## 2. KEY LIBRARIES & PACKAGES
* **Mapping:** `AutoMapper` (Map giữa Entity và DTO).
* **Validation:** `FluentValidation` (Validate dữ liệu đầu vào tại tầng Service/API).
* **Mediator Pattern:** `MediatR` (Dùng cho CQRS - Tách biệt Command/Query để code sạch hơn).
* **File Processing:** `EPPlus` (Để nhập/xuất file Excel kiểm kê kho).
* **API Documentation:** `Swagger / OpenAPI` (Bắt buộc để Frontend team tích hợp).

---

## 3. SOLUTION ARCHITECTURE (N-LAYER CLEAN ARCHITECTURE)
AI phải tuân thủ nghiêm ngặt cấu trúc phân tầng này, không được code tắt (ví dụ: không gọi DBContext trực tiếp từ Controller).

### 3.1. `MyProject.Core` (The Heart)
* *Mô tả:* Chứa các thành phần cốt lõi, không phụ thuộc vào bất kỳ project nào khác.
* *Thành phần:*
    * **Entities:** Các class POCO ánh xạ Database (e.g., `Product`, `Order`).
    * **Interfaces:** Các bản thiết kế cho Repo và Service (e.g., `IGenericRepository`, `IProductService`).
    * **Enums:** Các định nghĩa trạng thái cứng (e.g., `SerialStatus`, `OrderStatus`).
    * **Domain Exceptions:** Các lỗi nghiệp vụ (e.g., `OutOfStockException`).

### 3.2. `MyProject.Infrastructure` (The Bones)
* *Mô tả:* Nơi giao tiếp với Database và các dịch vụ hạ tầng.
* *Thành phần:*
    * **Data Context:** Class kế thừa `DbContext`.
    * **Repositories:** Implement `IGenericRepository<T>` và các Repo riêng biệt.
    * **Configurations:** Cấu hình Fluent API (Max length, Relationship, Index).
    * **Migrations:** Folder chứa các version migration của DB.

### 3.3. `MyProject.Service` (The Brain)
* *Mô tả:* Chứa toàn bộ logic nghiệp vụ (Business Logic).
* *Thành phần:*
    * **DTOs:** Data Transfer Objects (Request/Response). Tuyệt đối không để lộ Entity ra ngoài.
    * **Services Impl:** Logic xử lý (e.g., `ProductService` xử lý logic thêm/sửa/xóa và validate).
    * **Business Rules:** Các logic kiểm tra tồn kho, tính giá, check tương thích PC.

### 3.4. `MyProject.API` (The Face)
* *Mô tả:* Cổng giao tiếp RESTful API.
* *Thành phần:*
    * **Controllers:** Chỉ nhận Request -> Gọi Service -> Trả về Response chuẩn.
    * **Middlewares:** Xử lý lỗi tập trung (Global Exception Handling), Logging.

### 3.5. `MyProject.Client` (Frontend)
* *Mô tả:* Ứng dụng Blazor WASM.
* *Thành phần:*
    * **Pages:** Các màn hình chính (ProductList, BuildPC, Checkout).
    * **Components:** Các UI tái sử dụng (ProductCard, ConfirmDialog).
    * **Services:** Gọi API Backend thông qua `HttpClient`.

---

## 4. CODING CONVENTIONS (Quy tắc viết code)

### 4.1. Naming Rules
* **Classes/Methods:** `PascalCase` (e.g., `ProductService`, `GetByIdAsync`).
* **Interfaces:** Prefix 'I' + `PascalCase` (e.g., `IProductService`).
* **Variables/Parameters:** `camelCase` (e.g., `productId`, `serialNumber`).
* **Private Fields:** Underscore + `camelCase` (e.g., `_dbContext`, `_mapper`).
* **Database Tables:** Plural nouns (Số nhiều) (e.g., `Products`, `Categories`).
* **Async Methods:** Luôn có hậu tố `Async` (e.g., `SaveChangesAsync`).

### 4.2. API Standards (Backend)
* **Không trả về Entity:** Tuyệt đối không return Entity (Product) ra API. Phải dùng DTO (ProductDto).
* **RESTful URLs:**
    * GET `/api/products` (List)
    * GET `/api/products/{id}` (Detail)
    * POST `/api/products` (Create)
    * PUT `/api/products/{id}` (Update)
    * DELETE `/api/products/{id}` (Delete)
* **Standard Response Wrapper:** Mọi API phải trả về JSON theo mẫu:
    ```json
    {
      "success": true,
      "message": "Thao tác thành công",
      "data": { ... } // hoặc null nếu lỗi
    }
    ```
### 3.3. Database Rules
* **Soft Delete:** Các bảng quan trọng (Product, User, Order) phải có cột `IsDeleted` (bool). Khi xóa chỉ update `IsDeleted = true`.
* **Auditing:** Các bảng phải có `CreatedDate`, `CreatedBy`, `ModifiedDate`, `ModifiedBy`.

## 5. CRITICAL BUSINESS LOGIC (Nghiệp vụ cốt lõi - CẤM SAI)

### 5.1. Quản lý Sản phẩm & Serial (Product vs ProductSerial)
* **Mô hình:** Một `Product` (ví dụ: RAM Kingston 8GB) có nhiều `ProductSerial` (ví dụ: SN-1001, SN-1002).
* **Trạng thái Serial (`SerialStatus` Enum):**
    1.  `Available`: Trong kho, sẵn sàng bán.
    2.  `Reserved`: Có khách đặt Online (đang chờ giao), không được bán cho khách khác.
    3.  `Sold`: Đã bán thành công.
    4.  `Defective`: Hàng lỗi, chờ trả hãng.
* **Quy tắc:** Khi tạo đơn hàng Online, chưa gán Serial ngay. Chỉ khi nhân viên kho "Xác nhận đóng gói" -> Quét mã Serial -> Chuyển trạng thái từ `Available` sang `Reserved`.

### 5.2. Tính năng Build PC (Kiểm tra tương thích)
Khi viết logic Build PC, phải check các bảng luật (`CompatibilityRules`):
* **CPU vs Mainboard:** Phải khớp `SocketType` (VD: LGA1700).
* **RAM vs Mainboard:** Phải khớp `DDRGeneration` (DDR4 hoặc DDR5).
* **Power Supply (PSU):** Công suất nguồn phải > Tổng TDP của (CPU + VGA) + 100W (buffer).

### 5.3. Kiểm kê kho (Inventory Check)
* Sử dụng phương pháp **Snapshot**:
    * Lúc bắt đầu kiểm: Lưu `SystemQuantity` (Tồn kho lý thuyết) vào bảng chi tiết kiểm kê.
    * Lúc quét mã: Đếm `ActualQuantity`.
    * Kết quả: `Diff` = `Actual` - `System`.
* Không khóa chức năng bán hàng trong lúc kiểm kê.



## 6. UI GUIDELINES (Frontend - MudBlazor)
* **Table:** Sử dụng `<MudTable>` có ServerData (Phân trang phía Server) cho các danh sách lớn (Sản phẩm, Đơn hàng).
* **Form:** Sử dụng `<MudForm>` kết hợp `FluentValidationValidator`.
* **Feedback:** Dùng `ISnackbar` để hiển thị thông báo (Success/Error) góc màn hình.
* **Dialog:** Dùng `IDialogService` cho các form Thêm mới/Sửa (Popup form) thay vì chuyển trang.

### Tiêu chuẩn Kỹ thuật & Review Code: Sync vs Async
**1. Xử lý Đồng bộ (Synchronous):**
* **Định nghĩa:** Tuần tự, Blocking (Chờ đợi).
* **Phạm vi:** Các nghiệp vụ cần tính nhất quán dữ liệu ngay lập tức (Immediate Consistency).
* **Ví dụ:** Thanh toán tiền (Payment), Trừ tồn kho (Inventory), Login.

**2. Xử lý Bất đồng bộ (Asynchronous):**
* **Định nghĩa:** Non-blocking, Fire-and-forget hoặc Promise.
* **Phạm vi:** Các nghiệp vụ tốn thời gian, không cần phản hồi ngay, ưu tiên trải nghiệm người dùng (UX).
* **Ví dụ:** Gửi Email/SMS, Tạo báo cáo PDF, Resize ảnh, Ghi log phức tạp.

**3. Realtime:**
* Sử dụng WebSocket/SignalR để push thông báo/dữ liệu, không đánh đồng với xử lý Đồng bộ.

**4. Quy tắc phán xét "Code bị đần" vs "Ổn":**
* **Code bị đần:** Bắt người dùng chờ đợi (Loading) cho các tác vụ phụ trợ (như gửi mail, xuất file) trong luồng chính.
* **Code ổn/Chuẩn:** Tách các tác vụ nặng sang Background Job/Message Queue, trả phản hồi (Response) ngay lập tức cho Client.
