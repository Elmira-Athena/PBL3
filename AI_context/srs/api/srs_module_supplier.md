
# IMPLEMENTATION PLAN: SUPPLIER MANAGEMENT (BACKEND API)

**Project:** HushStore
**Module:** Inventory - Suppliers (Nhà cung cấp)
**Architecture:** Clean Architecture (Core -> Infra -> Shared -> Service -> API) + Repository Pattern

---

## 1. MỤC TIÊU (OBJECTIVES)

Xây dựng API quản lý danh sách Nhà cung cấp. Dữ liệu này sẽ được dùng làm Master Data cho tính năng Nhập kho (Import Receipts) ở bước tiếp theo.

---

## 2. DATABASE MAPPING (ENTITY FRAMEWORK)

### 2.1. Entity `Supplier` (Core Layer)

Map chính xác với script SQL đã thiết kế:

### 2.2. Configuration (Fluent API)

* Cấu hình bảng trong `HushStoreDbContext`.
* Thêm logic Query Filter mặc định để luôn bỏ qua các record có `IsDeleted == true` (Global Query Filter: `builder.Entity<Supplier>().HasQueryFilter(x => !x.IsDeleted)`).

---

## 3. REPOSITORY PATTERN (INFRASTRUCTURE LAYER)

Để đồng nhất với chuẩn mã nguồn của module Product:

* Tạo `ISupplierRepository`.
* Tạo `SupplierRepository` implement các hàm CRUD cơ bản. Đăng ký DI (Dependency Injection) trong `Program.cs`.

---

## 4. DATA TRANSFER OBJECTS (SHARED LAYER)

Tạo trong project `HushStore.Shared/DTOs/Suppliers`:

* **`SupplierDto`:** Chứa toàn bộ thông tin hiển thị.
* **`CreateSupplierRequest`:** Dùng khi thêm mới.
* **`UpdateSupplierRequest`:** Dùng khi cập nhật.

**Validation (FluentValidation):**

* `Name`: Bắt buộc nhập (NotEmpty), MaxLength(200).
* `PhoneNumber`: Bắt buộc nhập, Regex chỉ cho phép số (không chứa chữ cái).
* `Email`: Nếu có nhập thì phải đúng định dạng EmailAddress.
* `TaxCode`: MaxLength(20).

---

## 5. SERVICE LAYER & API

### 5.1. `SupplierService`

* Xử lý logic CRUD gọi qua Repository.
* **Quy tắc nghiệp vụ:** Khi xóa Nhà cung cấp, sử dụng **Soft Delete** (Cập nhật `IsDeleted = true`), tuyệt đối không xóa cứng (Hard Delete) vì có thể Nhà cung cấp này đã được gắn vào các Phiếu nhập kho cũ trong quá khứ. Nếu xóa cứng sẽ gây lỗi Foreign Key.

### 5.2. `SuppliersController`

* `GET /api/suppliers`: Lấy danh sách (Hỗ trợ phân trang, tìm kiếm theo Tên hoặc Số điện thoại).
* `GET /api/suppliers/{id}`: Lấy chi tiết.
* `POST /api/suppliers`: Thêm mới.
* `PUT /api/suppliers/{id}`: Cập nhật.
* `DELETE /api/suppliers/{id}`: Xóa mềm.
