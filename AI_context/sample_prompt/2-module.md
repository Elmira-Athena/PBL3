Dựa vào context đã học, hãy implement Backend cho module: **[TÊN MODULE - Ví dụ: Quản lý Sản phẩm]**.

Yêu cầu chi tiết:
1. **Core Layer:**
   - Tạo Entity class `[TênEntity]` khớp với bảng trong file SQL.
   - Định nghĩa `I[TênEntity]Repository` và `I[TênEntity]Service`.

2. **Infrastructure Layer:**
   - Implement Repository dùng EF Core.
   - Cấu hình Entity Configuration (Fluent API) nếu cần.

3. **Service Layer (Business Logic):**
   - Tạo DTOs: `Create[Tên]Request`, `Update[Tên]Request`, `[Tên]Dto` (Response).
   - Implement Service class: Xử lý mapping (AutoMapper), Validate (FluentValidation) và logic nghiệp vụ.
   - *Lưu ý:* Tuyệt đối không return Entity ra ngoài, phải map sang DTO.

4. **API Layer:**
   - Tạo Controller với các endpoint CRUD chuẩn RESTful (GET list, GET id, POST, PUT, DELETE).
   - Trả về response theo format chuẩn: `{ success: true, data: ... }`.

5. **Logic đặc thù:**
   - [Mô tả thêm logic nếu có, ví dụ: Kiểm tra trùng tên, tự động tạo mã SKU...]

Hãy viết code lần lượt từng Layer, code đến đâu giải thích đến đó.