# MODULE REQUIREMENT: CATEGORY MANAGEMENT (UC015)
## Project: HushStore

---

## 1. BUSINESS OVERVIEW
Hệ thống quản lý dòng sản phẩm theo cấu trúc cây (Composite Pattern). Cho phép phân cấp không giới hạn (ví dụ: Máy tính -> Laptop -> Laptop Gaming).

---

## 2. TECHNICAL RULES (RÀNG BUỘC NGHIỆP VỤ)
AI phải cài đặt các logic sau vào Service Layer:

1. **Circular Reference Check (Bắt buộc):** Khi cập nhật `ParentId`, hệ thống phải kiểm tra danh mục cha mới không được là chính nó hoặc con cháu của nó.
2. **Unique Name Per Level:** Tên danh mục không được trùng nhau nếu có cùng một `ParentId`.
3. **Deletion Rule:** - Sử dụng Soft Delete (`IsActive = false`).
   - Không cho phép xóa nếu danh mục đang chứa sản phẩm hoặc có danh mục con đang hoạt động.
4. **Recursive Search:** Khi tìm kiếm theo tên, hệ thống phải trả về các node khớp và tự động mở rộng các node cha (Expand parents).

---

## 3. DATA SCHEMA (ENTITY FRAMEWORK CORE)

### Entity: `Category`
- `Id`: INT (Primary Key, Identity).
- `Name`: NVARCHAR(255), Required.
- `ParentId`: INT, Nullable (FK to `Categories.Id`).
- `ImageUrl`: NVARCHAR(MAX), Nullable.
- `DisplayOrder`: INT (Thứ tự hiển thị).
- `IsActive`: BIT (Default: true).

---

## 4. API ENDPOINTS (BACKEND)

- `GET /api/categories/tree`: Trả về toàn bộ cây danh mục (Recursive DTO).
- `GET /api/categories/{id}`: Chi tiết danh mục.
- `POST /api/categories`: Tạo mới. (Validation: Unique name in level).
- `PUT /api/categories/{id}`: Cập nhật. (Validation: Circular Reference Check).
- `DELETE /api/categories/{id}`: Xóa mềm. (Validation: Check children/products).

---

## 5. ERROR MESSAGES (LOCALIZATION)
- `ERR_CIRCULAR`: "Lỗi tham chiếu vòng: Không thể chọn cấp dưới làm cha của cấp trên."
- `ERR_DUPLICATE`: "Tên danh mục đã tồn tại trong cấp này."
- `ERR_HAS_CHILDREN`: "Không thể xóa danh mục đang có danh mục con hoặc sản phẩm."