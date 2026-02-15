

# IMPLEMENTATION PLAN: API SECURITY & ENDPOINT PROTECTION (TASK 3)

**Project:** HushStore
**Module:** Authorization (Phân quyền API)

## 1. MỤC TIÊU

Khóa chặt các API quan trọng. Đảm bảo chỉ những User có JWT hợp lệ và đúng Role (Quyền) mới được phép truy cập và thực hiện thao tác thay đổi dữ liệu.

## 2. PHÂN BỔ QUYỀN HẠN (ROLE MAPPING)

Hệ thống hiện có 3 Role chính: `Admin`, `WarehouseManager`, `Customer`.

* **`ProductsController`:**
* `GET`: Cho phép vô danh (Anonymous) hoặc `Customer` xem để mua hàng.
* `POST`, `PUT`, `DELETE`: Yêu cầu `[Authorize(Roles = "Admin, WarehouseManager")]`.


* **`SuppliersController`:**
* Toàn bộ Controller: Yêu cầu `[Authorize(Roles = "Admin, WarehouseManager")]`.


* **`ImportReceiptsController`:**
* Toàn bộ Controller (Nghiệp vụ kho): Yêu cầu `[Authorize(Roles = "WarehouseManager")]`.


* **`ProductSerialsController`:**
* `GET` (Check tồn tại, lấy danh sách kho): Yêu cầu `[Authorize(Roles = "Admin, WarehouseManager")]`.



## 3. TRIỂN KHAI TRÊN CONTROLLER

* Khai báo namespace: `using Microsoft.AspNetCore.Authorization;`
* Đặt thuộc tính `[Authorize]` ở cấp độ Class (để khóa toàn bộ) hoặc cấp độ Method (để khóa từng hàm).
* Dùng `[AllowAnonymous]` cho các hàm ngoại lệ (như `GetList` sản phẩm).
