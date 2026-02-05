Backend API cho module **[TÊN MODULE]** đã xong. Bây giờ hãy code Frontend Blazor WebAssembly.

Yêu cầu chi tiết:
1. **Service Integration:**
   - Tạo file `[Tên]ClientService.cs` dùng `HttpClient` để gọi các API Backend vừa tạo.
   - Interface `I[Tên]ClientService` để Dependency Injection.

2. **UI Components (MudBlazor):**
   - Sử dụng thư viện `MudBlazor`.
   - Tạo trang danh sách (`[PageName].razor`): Dùng `<MudTable>` có Server-side Pagination, Search box, và nút Add/Edit/Delete.
   - Tạo Dialog (`[Name]Dialog.razor`): Dùng `<MudDialog>` chứa `<MudForm>` để dùng chung cho cả Thêm mới và Chỉnh sửa.

3. **Validation & UX:**
   - Gắn `FluentValidationValidator` vào form.
   - Hiển thị thông báo thành công/thất bại dùng `ISnackbar`.
   - Thêm `MudMessageBox` để xác nhận trước khi xóa.

Hãy viết code chi tiết cho Service Client trước, sau đó đến UI.
