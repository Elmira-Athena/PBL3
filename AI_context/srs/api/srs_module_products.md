

# IMPLEMENTATION PLAN: PRODUCT MASTER DATA (BACKEND API)

**Project:** HushStore
**Module:** Product Management (Master Data)
**Focus:** Backend Only (Core -> Infra -> Shared -> Service -> API)

---

## 1. MỤC TIÊU (OBJECTIVES)

Xây dựng API quản lý thông tin sản phẩm theo mô hình **Parent-Child** (1 Product có nhiều Variants).

* **Quan trọng:** Đây là module quản lý **Dữ liệu gốc (Master Data)**.
* **Scope:** Chỉ bao gồm việc định nghĩa sản phẩm (Tên, Giá, Cấu hình). Việc nhập kho (Tăng số lượng tồn, Quét Serial) thuộc module Inventory (làm sau).

---

## 2. DATABASE MAPPING (ENTITY FRAMEWORK)

AI cần tạo các Entity map đúng với script SQL đã chạy.

### 2.1. Entities

* **`Product` (Parent):**
* Props: `Id`, `Name`, `Description`, `ManufacturerId`, `CategoryId`, `Status` (Enum), `ImageUrls` (JSON).
* Relations: `HasMany(Variants)`.


* **`ProductVariant` (Child - SKU):**
* Props: `Id`, `SKU` (Unique), `VariantName`, `Price`, `OriginalPrice`, `Specifications` (JSON), `StockQuantity` (Computed - tạm thời để 0).
* Relations: `BelongsTo(Product)`, `HasMany(Images)`, `HasMany(Attributes)`.


* **`ProductImage` & `ProductAttribute`:** Map bình thường.

### 2.2. Configuration (Fluent API)

* **Composite Unique Key:** Đảm bảo `SKU` là duy nhất toàn hệ thống.
* **Cascade Delete:** Khi xóa `Product` -> Xóa hết `Variants`. Khi xóa `Variant` -> Xóa hết `Attributes`/`Images`.
* **JSON Conversion:** Sử dụng `ValueConversion` của EF Core để tự động map cột `Specifications` (JSON string) sang `Dictionary<string, string>` hoặc `Object` trong C#.

---

## 3. DATA TRANSFER OBJECTS (SHARED LAYER)

Cấu trúc DTO phải hỗ trợ việc tạo/sửa lồng nhau (Nested).

### 3.1. Read DTOs

* **`ProductVariantDto`:** `Id`, `SKU`, `VariantName`, `Price`, `Specifications` (Object), `StockQuantity`.
* **`ProductDetailDto`:** `Id`, `Name`, `Description`, `ManufacturerName`, `CategoryName`, `List<ProductVariantDto> Variants`.
* **`ProductListDto`:** Dùng cho trang danh sách.
* Logic Giá: `PriceRange` (VD: "20.000.000 - 25.000.000").
* Logic Ảnh: Lấy ảnh của Variant đầu tiên hoặc ảnh chung.



### 3.2. Write DTOs (CUD)

* **`CreateProductRequest`:**
* Thông tin chung: `Name`, `CategoryId`, `ManufacturerId`.
* `List<CreateVariantRequest> Variants`: Bắt buộc phải có ít nhất 1 Variant khi tạo sản phẩm.


* **`UpdateProductRequest`:** Cho phép sửa thông tin chung.
* **`SaveVariantRequest`:** Dùng để thêm/sửa Variant trong một Product đã có.

---

## 4. SERVICE LAYER LOGIC (CORE BUSINESS)

Class: `ProductService` implements `IProductService`.

### 4.1. Create Logic (Transactional)

Do cấu trúc phức tạp, việc tạo mới phải dùng `IDbContextTransaction`.

1. Insert `Product`.
2. Loop `Variants`:
* Generate `Slug` cho Variant (VD: `product-slug` + `sku`).
* Insert `Variant`.
* Insert `Attributes` và `Images` của Variant đó.


3. Commit Transaction.

### 4.2. Update Logic

* **Check SKU:** Nếu người dùng sửa SKU, phải kiểm tra trùng lặp với tất cả SKU khác trong DB.
* **Refactor Specs:** Nếu user gửi JSON cấu hình mới, update lại cột `Specifications`.

### 4.3. Validation Rules (FluentValidation)

* `Name`: NotEmpty, MaxLength(200).
* `Variants`: Must have at least one item.
* `Variants[].Price`: GreaterThan(0).
* `Variants[].SKU`: NotEmpty, NoSpace, Unique (Check database).

---

## 5. API ENDPOINTS

Controller: `ProductsController`

* `POST /api/products`: Tạo mới trọn gói (Product + Variants).
* `PUT /api/products/{id}`: Cập nhật thông tin chung.
* `POST /api/products/{id}/variants`: Thêm biến thể mới cho sản phẩm cũ.
* `DELETE /api/products/{id}`: Soft Delete (Chuyển Status = StopBusiness nếu đã có giao dịch, hoặc Xóa cứng nếu chưa có).
* `GET /api/products`:
* Hỗ trợ Filter: `CategoryId` (bao gồm cả category con), `PriceMin`, `PriceMax`, `Keyword`.
* Sử dụng `.AsNoTracking()` và `ProjectTo<ProductListDto>` để tối ưu hiệu năng.



---

## 6. LƯU Ý CHO AI (RULES)

1. **StockQuantity:** Hiện tại chưa có module Kho, hãy để `StockQuantity` trong DTO luôn trả về 0 hoặc random để test. **Tuyệt đối không** cho phép Client gửi `StockQuantity` lên để update (Trường này là Read-only).
2. **AutoMapper:** Cấu hình kỹ việc map từ `Product` -> `ProductListDto` (Logic lấy MinPrice/MaxPrice từ list Variants).
3. **Localization:** Trả về lỗi tiếng Việt: "Mã SKU '{0}' đã tồn tại", "Sản phẩm phải có ít nhất một phiên bản".
