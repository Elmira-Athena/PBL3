## 5. PROMPT FIX BUG CHUYÊN SÂU (DEBUGGING EXPERT)
*Dùng khi gặp lỗi khó, logic sai hoặc Exception phức tạp.*

```text
Tôi đang gặp lỗi trong module **[TÊN MODULE]**.
Đóng vai trò là một Senior Debugger, hãy phân tích và sửa lỗi này giúp tôi.

### 1. Thông tin lỗi (Context)
- **Hành vi mong muốn:** [Mô tả ngắn gọn bạn muốn code chạy thế nào, VD: Khi bấm nút Lưu thì phải cập nhật DB và đóng Modal]
- **Hành vi hiện tại:** [Mô tả lỗi, VD: Bấm Lưu thì xoay vòng vòng không phản hồi, hoặc báo lỗi 500]
- **Error Log / Stack Trace:**

[DÁN LOG LỖI TỪ CONSOLE/NETWORK TAB VÀO ĐÂY]

### 2. Code hiện tại (Suspected Code)
*File: [Tên File, VD: ProductService.cs]*
```csharp
[DÁN ĐOẠN CODE NGHI NGỜ GÂY LỖI VÀO ĐÂY]

### 3. Yêu cầu xử lý
## 1. Phân tích nguyên nhân (Root Cause Analysis): Giải thích tại sao lỗi này xảy ra dựa trên Stack Trace. Đừng đoán mò.

## 2. Kiểm tra tính toàn vẹn: Đảm bảo giải pháp sửa lỗi tuân thủ kiến trúc N-Layer (VD: Không được gọi DB trực tiếp từ View, phải qua Service/API).

### 3. Đưa ra giải pháp (Solution):

## 1. Viết lại toàn bộ hàm/đoạn code đã sửa (để tôi copy paste đè lên cho nhanh).

## 2. Comment vào dòng code đã sửa: // FIX: [Lý do sửa].

### Hãy bắt đầu phân tích.