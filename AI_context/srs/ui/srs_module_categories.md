
# IMPLEMENTATION PLAN: CATEGORY UI (PHASE 2)

**Project:** HushStore (E-commerce IT Hardware)
**Technology:** Blazor WebAssembly, MudBlazor, .NET 10

---

## 1. MỤC TIÊU (OBJECTIVES)

* Hiện thực hóa giao diện quản lý danh mục đa cấp dựa trên API đã xây dựng ở Đợt 1.
* Đảm bảo tính nhất quán dữ liệu thông qua project **Shared**.
* Tối ưu hóa trải nghiệm người dùng (UX) với khả năng tìm kiếm và điều hướng cây danh mục mượt mà.

---

## 2. KIẾN TRÚC FRONTEND (CLIENT ARCHITECTURE)

AI phải tuân thủ cấu trúc phân lớp trong project `Client` để tránh "code đần" (gộp logic vào UI):

* **Services Layer:** `CategoryClientService.cs` – Chịu trách nhiệm gọi API và xử lý wrapper `ApiResult<T>`.
* **State Management:** Sử dụng local variables hoặc `StateContainer` để quản lý danh sách danh mục hiện tại.
* **UI Components:**
* `CategoryList.razor`: Trang chính chứa TreeView.
* `CategoryDialog.razor`: Popup form dùng chung cho Thêm/Sửa.

---

## 3. DANH SÁCH TÁC VỤ (TASK LIST)

### 3.1. Hạ tầng & Kết nối API

* [ ] Cấu hình `HttpClient` trong `Program.cs` và đăng ký Dependency Injection cho `CategoryClientService`.
* [ ] Implement `CategoryClientService` với các method: `GetTreeAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`.
* [ ] Tích hợp `HushStore.Shared` để sử dụng lại các DTO và Enum (SerialStatus, v.v.).

### 3.2. Giao diện danh sách (Tree View)

* [ ] Sử dụng `<MudTreeView>` của MudBlazor để render cây danh mục.
* [ ] **Logic Tìm kiếm:** Khi người dùng nhập từ khóa, hệ thống phải thực hiện lọc và tự động `Expand` (mở rộng) các nhánh có chứa kết quả.
* [ ] Thêm các nút chức năng (Icon Button): Thêm con, Sửa, Xóa trên từng node của cây.

### 3.3. Form nghiệp vụ (Dialog)

* [ ] Thiết kế `CategoryDialog` với các trường: Tên danh mục, Danh mục cha (Dropdown), Thứ tự hiển thị, Ảnh.
* [ ] **Validation:** Sử dụng `FluentValidation` để kiểm tra tên không được để trống và độ dài tối đa 255 ký tự.
* [ ] **Logic Chặn tham chiếu vòng (UI-level):** Khi sửa một danh mục, danh sách "Danh mục cha" trong Dropdown không được chứa chính nó và các con của nó.

---

## 4. WALKTHROUGH: CÁC LUỒNG XỬ LÝ QUAN TRỌNG

### 4.1. Luồng hiển thị cây danh mục

1. `OnInitializedAsync` gọi `CategoryClientService.GetTreeAsync()`.
2. Map dữ liệu vào `List<CategoryDto>` và render lên `<MudTreeView>`.
3. Sử dụng `Optimistic UI`: Khi xóa thành công, xóa node khỏi list trên RAM trước khi gọi lại API để tăng cảm giác "tức thì".

### 4.2. Luồng tìm kiếm & Tự động mở rộng (Expand)

Để AI code đúng phần này, cần áp dụng thuật toán sau:

* Khi `SearchString` thay đổi -> Duyệt cây đệ quy.
* Nếu một node con khớp từ khóa -> Đánh dấu `IsExpanded = true` cho tất cả các node cha của nó.

### 4.3. Xử lý lỗi từ Backend

* Nếu API trả về `400 BadRequest` kèm thông báo lỗi (như `ERR_CIRCULAR`), UI phải hiển thị thông báo lỗi đó qua `ISnackbar` của MudBlazor bằng tiếng Việt.

---

## 5. QUY TẮC "CODE KHÔNG ĐẦN" (CODE QUALITY)

* **Không Hardcode URL:** Luôn sử dụng hằng số hoặc cấu hình cho các Endpoint API.
* **Component tái sử dụng:** Nếu thẻ sản phẩm hoặc danh mục được dùng ở nhiều nơi (Admin/User), phải tách ra thành Shared Component.
* **Loading State:** Trong khi chờ API phản hồi, phải hiển thị `MudProgressCircular` hoặc `MudSkeleton`, không để màn hình trống.
