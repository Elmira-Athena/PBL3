Tôi cần implement logic nghiệp vụ **[TÊN NGHIỆP VỤ - Ví dụ: Kiểm tra tương thích Build PC]**.

Dữ liệu đầu vào:
- [Mô tả input 1, VD: CPU object có Socket="1700"]
- [Mô tả input 2, VD: Mainboard object có Socket="1200"]

Quy tắc nghiệp vụ (Business Rules):
1. [Rule 1: Socket CPU phải trùng Socket Mainboard]
2. [Rule 2: Loại RAM (DDR4/5) phải trùng Mainboard hỗ trợ]
3. [Rule 3: Công suất nguồn >= Tổng TDP linh kiện + 100W]

Yêu cầu:
- Viết một method C# trong Service Layer: `CheckCompatibility(...)`.
- Trả về kết quả: `true` nếu khớp, hoặc `List<string>` chứa danh sách lỗi cụ thể nếu không khớp.
- Code phải clean, dễ đọc, comment giải thích logic.