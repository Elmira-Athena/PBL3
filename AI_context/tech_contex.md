# PROJECT CONTEXT & TECHNICAL SPECIFICATION
## Project Name: IT Hardware E-commerce & Management System

---

## 1. TECHNOLOGY STACK (CORE)
* **Backend Framework:** ASP.NET Core 10 Web API.
* **Frontend Framework:** Blazor WebAssembly (Standalone) - *Ưu tiên dùng C# full-stack để đẩy nhanh tiến độ.*
* **Database:** SQL Server 2025+ (Triển khai bằng Entity Framework Core 10 - Code First).
* **UI Component Library:** MudBlazor (Material Design).
* **Authentication:** JWT Bearer Token (IdentityServer hoặc Custom Implementation).

## 2. KEY LIBRARIES & PACKAGES
* **Mapping:** `AutoMapper` (Map giữa Entity và DTO).
* **Validation:** `FluentValidation` (Validate dữ liệu đầu vào tại tầng Service/API).
* **Mediator Pattern:** `MediatR` (Dùng cho CQRS - Tách biệt Command/Query để code sạch hơn).
* **File Processing:** `EPPlus` (Để nhập/xuất file Excel kiểm kê kho).
* **API Documentation:** `Swagger / OpenAPI` (Bắt buộc để Frontend team tích hợp).

## 3. SOLUTION ARCHITECTURE (N-LAYER CLEAN ARCHITECTURE)

AI phải tuân thủ nghiêm ngặt cấu trúc phân tầng này. Đặc biệt lưu ý Project **Shared** để tận dụng sức mạnh của C# Full-stack.

### 3.1. `MyProject.Core` (The Domain Heart)

* *Mô tả:* Chứa các thành phần cốt lõi của nghiệp vụ, không phụ thuộc vào Database hay UI.
* *Thành phần:*
* **Entities:** Các class POCO ánh xạ Database (e.g., `Product`, `Order`).
* **Interfaces:** Các bản thiết kế cho Repo (e.g., `IGenericRepository`, `IProductRepository`).
* **Domain Exceptions:** Các lỗi nghiệp vụ (e.g., `OutOfStockException`).
* *Lưu ý:* Project này **không được** tham chiếu đến `Shared` hay `Infrastructure`.



### 3.2. `MyProject.Shared` (The Contract / The Bridge) 

* *Mô tả:* Class Library chứa các thành phần dùng chung cho cả **Backend (API)** và **Frontend (Blazor)**. Giúp chia sẻ code, tránh lặp lại (DRY).
* *Thành phần:*
* **DTOs:** Tất cả Request/Response Models (e.g., `ProductDto`, `LoginRequest`).
* **Enums:** Các định nghĩa trạng thái dùng chung (e.g., `SerialStatus`, `OrderStatus`).
* **Constants:** Các hằng số, thông báo lỗi (e.g., `SystemConstants`, `ErrorMessages`).
* **Custom Results:** Class bọc kết quả API (e.g., `ApiResult<T>`).
* **Validators:** FluentValidation Rules (để Client có thể validate form ngay lập tức mà chưa cần gọi API).


### 3.3. `MyProject.Infrastructure` (The Bones)

* *Mô tả:* Nơi giao tiếp với Database và các dịch vụ hạ tầng.
* *Thành phần:*
* **Data Context:** Class kế thừa `DbContext` (EF Core).
* **Repositories Impl:** Implement các Interface từ Core.
* **Configurations:** Cấu hình Fluent API (Max length, Relationship, Index).
* **Migrations:** Folder chứa các version migration của DB.



### 3.4. `MyProject.Service` (The Brain)

* *Mô tả:* Chứa toàn bộ logic xử lý nghiệp vụ.
* *Thành phần:*
* **Services Impl:** Logic xử lý chính (e.g., `ProductService` xử lý logic thêm/sửa/xóa).
* **Mapping Logic:** Cấu hình AutoMapper (Map từ Entity trong `Core` sang DTO trong `Shared`).
* **Business Rules:** Các logic kiểm tra tồn kho, tính giá, check tương thích PC.



### 3.5. `MyProject.API` (The Face)

* *Mô tả:* Cổng giao tiếp RESTful API.
* *Thành phần:*
* **Controllers:** Chỉ nhận Request -> Gọi Service -> Trả về `ApiResult<T>` (từ `Shared`).
* **Middlewares:** Xử lý lỗi tập trung, Logging, JWT Auth.
* **Program.cs:** Cấu hình Dependency Injection (DI).



### 3.6. `MyProject.Client` (The Frontend)

* *Mô tả:* Ứng dụng Blazor WebAssembly.
* *Thành phần:*
* **Pages:** Các màn hình chính (ProductList, BuildPC, Checkout).
* **Components:** Các UI tái sử dụng (ProductCard, ConfirmDialog) dùng thư viện MudBlazor.
* **Client Services:** Sử dụng `HttpClient` để gọi API.
* *Lưu ý:* Project này sẽ Reference trực tiếp `MyProject.Shared` để dùng lại DTO và Enums.

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
### 5.4. PRODUCT & CATEGORY ARCHITECTURE (CORE DOMAIN)
*Mô tả chi tiết cấu trúc dữ liệu cốt lõi của hệ thống.*

#### A. Category Hierarchy (Cấu trúc Danh mục Đa cấp - Composite Pattern)
* **Mô hình Database:** Sử dụng chiến lược **Adjacency List** (Danh sách kề).
  * Bảng `Categories`:
    * `Id` (PK)
    * `Name`
    * `ParentId` (FK, Nullable): Self-reference về chính bảng `Categories`.
    * `Level` (int): Độ sâu của danh mục (0: Root, 1: Sub, 2: Sub-sub).
* **Business Rules (AI phải code logic này):**
  1.  **Độ sâu:** Hỗ trợ N cấp độ (Tuy nhiên UI thường hiển thị tối đa 3-4 cấp để đỡ rối).
  2.  **Circular Dependency Check (Quan trọng):** Khi cập nhật `ParentId`, hệ thống bắt buộc kiểm tra để đảm bảo không tạo vòng lặp (VD: A là cha B, thì B không được phép sửa thành cha của A).
  3.  **Deletion Rule (Ràng buộc xóa):** KHÔNG ĐƯỢC xóa danh mục nếu nó đang chứa:
    * Danh mục con (Has Children).
    * Sản phẩm (Has Products).
  4.  **Display:** API phải hỗ trợ trả về dữ liệu dạng cây (Recursive JSON) để Frontend dễ render component `TreeView` hoặc `MegaMenu`.

* **Ví dụ Cấu trúc (IT Hardware):**
  * Linh kiện máy tính (Root, Level 0)
    ├── RAM (Level 1)
    │   ├── RAM DDR4 (Level 2)
    │   └── RAM DDR5 (Level 2)
    └── VGA (Level 1)
    ├── NVIDIA (Level 2)
    └── AMD (Level 2)

#### B. Product Identity vs. Physical Item (Quan trọng)
Hệ thống phân tách rõ ràng giữa "Thông tin sản phẩm" và "Sản phẩm vật lý".

**1. Product (Master Data):**
* Đại diện cho mẫu sản phẩm (Model).
* Định danh bằng: `ProductCode` (SKU) - Duy nhất.
* Chứa thông tin chung: Tên, Mô tả, Giá bán, Thông số kỹ thuật, Ảnh, Thời gian bảo hành.
* **Cờ quản lý:** `IsSerialManaged` (bool).
  * `true`: Quản lý từng cái (Main, CPU, VGA...).
  * `false`: Chỉ quản lý số lượng (Dây cáp, Chuột giá rẻ...).

**2. ProductSerial (Physical Item):**
* Đại diện cho một vật thể cầm nắm được trong kho.
* Quan hệ: **1 Product - N ProductSerials** (Một mẫu sản phẩm có nhiều cái trong kho).
* Định danh bằng: `SerialNumber` (SN) - Quét từ mã vạch trên hộp.
* **Thuộc tính:**
  * `SerialNumber` (Unique): Mã định danh duy nhất.
  * `Status` (Enum): `Available` (Trong kho), `Reserved` (Đã có người đặt), `Sold` (Đã bán), `Defective` (Hàng lỗi).
  * `ImportReceiptId`: Nhập từ phiếu nào.
  * `OrderId`: Bán trong đơn hàng nào (Nullable).

**3. Logic Đồng bộ Tồn kho (Inventory Sync):**
* Với sản phẩm có Serial: Số lượng tồn (`StockQuantity`) trong bảng Product là con số **Computed** (Được tính toán) = Số lượng các dòng trong bảng `ProductSerial` có status là `Available`.
* AI phải viết code trigger hoặc service logic để đảm bảo con số này luôn đúng.


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

---

## 8. NON-FUNCTIONAL CONSTRAINTS & QUALITY ATTRIBUTES
*AI phải tuân thủ nghiêm ngặt các chỉ số dưới đây để đảm bảo chất lượng hệ thống.*

### 8.1. Performance Goals (Hiệu năng)
* [cite_start]**Page Load & Listing:** API lấy danh sách sản phẩm/trang chủ phải phản hồi dưới **2 giây**.
  * *Yêu cầu kỹ thuật:* Sử dụng `MemoryCache` hoặc `DistributedCache` (Redis) cho các dữ liệu ít thay đổi (Danh mục, Menu). Dùng `.AsNoTracking()` cho mọi truy vấn GET.
* [cite_start]**Interactive Actions:** Các thao tác Giỏ hàng, Thanh toán phải phản hồi **tức thì (< 1 giây)**[cite: 358].
  * *Yêu cầu kỹ thuật:* Frontend (Blazor) phải áp dụng **Optimistic UI** (Cập nhật giao diện ngay lập tức trước khi chờ Server phản hồi). Backend phải tối ưu query SQL.
* [cite_start]**Concurrency:** Hệ thống chịu tải tối thiểu **50 concurrent users**[cite: 359]. Connection Pool của Database phải được cấu hình hợp lý trong `appsettings.json`.

### 8.2. Security & Compliance (Bảo mật)
* [cite_start]**Authentication:** Password bắt buộc Hash (BCrypt/PBKDF2)[cite: 363].
* **Authorization:**
  * [cite_start]Phân quyền 3 vai trò cứng: `Admin`, `Employee`, `Customer`[cite: 364].
  * [cite_start]**Chặn truy cập URL:** Ngăn chặn việc Customer đổi ID trên URL để xem đơn hàng của người khác (Lỗi IDOR/BOLA). *AI phải check logic: `if (order.UserId != currentUserId) return Forbid();`*.
* **Web Security:**
  * [cite_start]Chống **SQL Injection**: 100% dùng EF Core LINQ/Parameter[cite: 367].
  * [cite_start]Chống **XSS/CSRF**: Tự động sanitize input và dùng Antiforgery Token[cite: 368].
* [cite_start]**Data Protection:** Không log thông tin nhạy cảm (Password, Thẻ tín dụng) ra file log[cite: 369].

### 8.3. Data Management (Quản lý dữ liệu)
* **Image Optimization:** Không lưu file ảnh vào SQL Server.
  * [cite_start]*Yêu cầu:* Tích hợp API lưu trữ bên thứ 3 (Cloudinary/AWS S3/Firebase) theo yêu cầu SRS[cite: 360]. Database chỉ lưu URL.
* [cite_start]**Data Integrity (ACID):** [cite: 372]
  * Sử dụng `IDbContextTransaction` cho các luồng nghiệp vụ phức tạp:
    1. Đặt hàng (Trừ kho -> Tạo đơn -> Xóa giỏ).
    2. Nhập/Xuất kho (Cập nhật tồn -> Lưu lịch sử -> Đổi trạng thái Serial).
  * Nếu 1 bước lỗi -> Rollback toàn bộ.

### 8.4. User Experience (UX Standards)
* **Error Handling:** API không được trả về "Yellow Screen of Death" (Lỗi server thô). [cite_start]Phải trả về JSON lỗi chuẩn (Mã 404, 500) để Frontend hiện trang lỗi thân thiện[cite: 371].
* **Loading States:** Trong khi chờ API > 2s, giao diện phải hiển thị **Skeleton Loading** hoặc **Spinner**, không được để màn hình trắng.

### 8.5. Code Quality & Maintainability
* [cite_start]**Design Patterns:** Bắt buộc áp dụng **Repository Pattern** và **Dependency Injection (DI)**[cite: 82].
* [cite_start]**Clean Code:** Tách biệt rõ ràng UI Layer (Blazor), Business Layer (Service) và Data Access (Repo)[cite: 374].
* [cite_start]**Environment:** Backend phải chạy tốt trên cả Windows và Linux (Docker Containerization ready)[cite: 81].
### 8.6. Localization & Error Messages (Quan trọng)
* **Language:** Tất cả thông báo lỗi trả về cho Frontend (User-facing messages) bắt buộc phải là **Tiếng Việt có dấu**.
  * *Sai:* `return BadRequest("Product not found");`
  * *Đúng:* `return BadRequest("Không tìm thấy sản phẩm yêu cầu.");`
* **Validation Messages:** Cấu hình FluentValidation để báo lỗi tiếng Việt (VD: "Tên sản phẩm không được để trống").

### 8.7. Logging & Monitoring
* **Framework:** Sử dụng `Serilog` hoặc `Built-in ILogger`.
* **Requirement:** * Ghi log `Information` khi thực hiện các hành động quan trọng (Tạo đơn hàng, Nhập kho).
  * Ghi log `Error` kèm StackTrace khi gặp Exception trong `try-catch`.
  * *Lưu ý:* Không log mật khẩu hoặc thông tin thẻ tín dụng.