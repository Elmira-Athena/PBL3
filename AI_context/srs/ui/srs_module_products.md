
# IMPLEMENTATION PLAN: PRODUCT UI (PHASE 3)

**Project:** HushStore
**Technology:** Blazor WebAssembly, MudBlazor
**Reference:** Figma Mockups (List & Form)

---

## 1. MỤC TIÊU & KIẾN TRÚC UI

* Chuyển đổi thiết kế Figma thành các component MudBlazor.
* **Xử lý linh hoạt mô hình Parent-Child:** Form thêm/sửa phải cho phép nhập thông tin chung (Product) và thêm nhiều cấu hình (Variants) trên cùng một màn hình.
* Tách biệt logic gọi API vào `ProductClientService`.

---

## 2. CẤU TRÚC COMPONENTS

Vì form này rất lớn và phức tạp (chứa mảng Biến thể lồng nhau), không nên dùng `MudDialog` (sẽ bị chật). Hãy dùng **Page (Trang riêng biệt)**.

* `ProductClientService.cs`: Nơi chứa các hàm gọi REST API.
* `Pages/Admin/Products/ProductList.razor`: Màn hình danh sách (Figma 1).
* `Pages/Admin/Products/ProductForm.razor`: Màn hình Thêm/Sửa (Figma 2). Có thể dùng chung component này cho cả thao tác Create và Update.

---

## 3. ĐIỀU CHỈNH LOGIC TỪ FIGMA SANG THỰC TẾ (CRITICAL RULES CHO AI)

AI **bắt buộc** phải tuân thủ các điều chỉnh sau để không bị lệch với DTO đã tạo ở API:

### 3.1. Đối với Màn hình Danh sách (`ProductList.razor`)

* **Hiển thị Giá:** Vì 1 sản phẩm có nhiều biến thể, cột Giá phải hiển thị dạng khoảng giá (Ví dụ: `15.000.000đ - 20.000.000đ`), lấy từ `MinPrice` và `MaxPrice` của DTO.
* **Tồn kho:** Hiển thị Tổng tồn kho của tất cả các biến thể cộng lại. Cột này chỉ xem, không được sửa.
* Dùng `MudTable` với tính năng `ServerData` để gọi API phân trang, tìm kiếm.

### 3.2. Đối với Màn hình Thêm/Sửa (`ProductForm.razor`)

Chia layout sử dụng `MudGrid` giống Figma (Cột trái to, cột phải nhỏ):

* **Cột Trái (Thông tin Product Cha):**
* Tên sản phẩm, Mô tả (MudTextField).
* Hình ảnh (Dùng `<MudFileUpload>` để xử lý nhiều ảnh).
* Thông số kỹ thuật chung: Render một form động (Dynamic Key-Value) để nhập JSON (VD: Hãng CPU: Intel, Loại RAM: DDR5).


* **Cột Phải (Phân loại):**
* Danh mục (MudSelect chọn từ API Category).
* Hãng sản xuất (MudSelect chọn từ API Manufacturer).


* **Phần dưới cùng (Danh sách Biến thể - Variants):**
* Đây là phần khác biệt so với Figma. Không để SKU và Giá ở cột phải chung chung.
* Tạo một khu vực "Danh sách phiên bản". Dùng `foreach` để render ra các `MudCard` hoặc `MudPaper`.
* Mỗi biến thể sẽ có các field: **SKU (Mã duy nhất), Tên phiên bản, Giá bán, Giá gốc, Số lượng tồn (Read-only)**.
* Cần có nút "Thêm biến thể mới" và nút "Xóa" cho từng biến thể.



---

## 4. XỬ LÝ TRẠNG THÁI (STATE MANAGEMENT)

* **Khởi tạo Form:** Request Model ban đầu (`CreateProductRequest`) phải được khởi tạo sẵn ít nhất 1 object rỗng trong list `Variants` (Vì API yêu cầu 1 sản phẩm phải có ít nhất 1 biến thể).
* **Validation:** Sử dụng `<EditForm>` kết hợp với `FluentValidation` (từ thư viện `HushStore.Shared`) để báo lỗi đỏ ngay dưới ô Textbox nếu nhập thiếu Tên hoặc SKU bị trùng (dựa vào thông báo từ API trả về qua `ISnackbar`).

---

## 5. TÍCH HỢP HÌNH ẢNH (PLACEHOLDER)

* Chức năng Upload ảnh thực tế sẽ làm ở đợt sau (khi tích hợp Cloudinary/S3).
* **Tạm thời:** AI thiết kế UI cho phép kéo thả/chọn file, nhưng lúc Submit API thì cứ gán cứng (hardcode) một URL ảnh dummy vào chuỗi `ImageUrls` của DTO để tránh lỗi.
