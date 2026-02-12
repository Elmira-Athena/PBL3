# MODULE REQUIREMENT: CATEGORY MANAGEMENT (UC015)
## Project: HushStore

## 5. UI/UX REQUIREMENTS (BLAZOR + MUDBLAZOR)

### 5.1. Trang danh sách (Category Tree View)
- Sử dụng `<MudTreeView>` để hiển thị cấu trúc phân cấp.
- Có ô `MudTextField` để search nhanh. Khi search, các nhánh chứa kết quả phải tự động `Expand`.

### 5.2. Form Thêm/Sửa (Dialog)
- **Tên danh mục:** Textbox.
- **Danh mục cha:** `MudSelect` hiển thị danh sách danh mục hiện có dưới dạng phẳng (đã thụt lề) hoặc dạng cây để chọn.
- **Hình ảnh:** MudFileUpload (Xử lý lưu URL).
- **Thứ tự:** MudNumericField.
