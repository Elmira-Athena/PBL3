
# IMPLEMENTATION PLAN: SUPPLIER UI

**Project:** HushStore
**Technology:** Blazor WebAssembly, MudBlazor

---

## 1. MỤC TIÊU & KIẾN TRÚC UI

* Xây dựng giao diện CRUD cho Nhà cung cấp sử dụng MudBlazor.
* Tích hợp `SupplierClientService` để gọi API Backend.
* Tối ưu hóa trải nghiệm: Dùng Popup (Dialog) cho tác vụ nhập liệu, dùng Server-side Pagination cho bảng danh sách.

---

## 2. CẤU TRÚC COMPONENTS

* **`SupplierClientService.cs`:** Chứa các hàm `GetListAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`. Đăng ký vào `Program.cs` của project Client.
* **`Pages/Admin/Suppliers/SupplierList.razor`:** Trang chính quản lý.
* **`Pages/Admin/Suppliers/SupplierDialog.razor`:** Popup form dùng chung cho cả chức năng Thêm mới và Chỉnh sửa.

---

## 3. YÊU CẦU KỸ THUẬT (CRITICAL RULES)

### 3.1. Trang danh sách (`SupplierList.razor`)

* Sử dụng `<MudTable>` với thuộc tính `ServerData` để gọi API phân trang (Tránh việc gọi `_context.ToList()` kéo toàn bộ dữ liệu về RAM của trình duyệt).
* **Cột hiển thị:** Tên nhà cung cấp, Người liên hệ, Số điện thoại, Email, Mã số thuế.
* **Thanh công cụ (ToolBar):**
* Ô Search (Lọc theo Tên hoặc Số điện thoại). Khi gõ enter hoặc click icon kính lúp sẽ trigger lại `ServerData`.
* Nút "Thêm mới" (Mở `SupplierDialog`).


* **Hành động (Actions):** Mỗi dòng có 2 icon button: Sửa (Mở Dialog kèm Data) và Xóa.
* Khi nhấn Xóa, gọi `MudMessageBox` hỏi xác nhận: *"Bạn có chắc chắn muốn ngừng hợp tác với nhà cung cấp này không?"*, sau đó gọi API Soft Delete và reload lại table.

### 3.2. Popup Form (`SupplierDialog.razor`)

* Truyền tham số `SupplierId` vào Dialog. Nếu `Id == 0` là Thêm mới, `Id > 0` là Chỉnh sửa (gọi API lấy detail ra điền vào form).
* Sử dụng `<EditForm>` kết hợp với Validation từ thư viện `HushStore.Shared`.
* **Các trường nhập liệu (`MudTextField`):**
* Tên nhà cung cấp (Bắt buộc).
* Số điện thoại (Bắt buộc).
* ... (Các trường khác)


* Hiển thị thông báo thành công/thất bại qua `ISnackbar` của MudBlazor.
