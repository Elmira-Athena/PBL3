
# IMPLEMENTATION PLAN: AUTH - DATABASE IDENTITY (TASK 1)

**Project:** HushStore
**Module:** Authentication & Identity (Nền móng Dữ liệu)
**Architecture:** Clean Architecture / N-Layer

## 1. MỤC TIÊU (OBJECTIVES)

Thiết lập hệ thống ASP.NET Core Identity để quản lý Người dùng (Users), Phân quyền (Roles) và Mã hóa mật khẩu, tuân thủ nguyên tắc không "phát minh lại bánh xe". Hệ thống sẽ sử dụng khóa chính (PK) kiểu `Guid` thay vì `string` mặc định để tăng tính bảo mật và tối ưu hiệu năng.

## 2. CORE LAYER (ENTITIES)

Tạo các thực thể tùy chỉnh kế thừa từ Identity của Microsoft. Đặt trong thư mục `HushStore.Core/Entities/System/`.

* **`AppUser : IdentityUser<Guid>`**
* Bổ sung các cột thực tế: `FullName` (nvarchar 200, bắt buộc), `Avatar` (varchar 500, nullable), `DateOfBirth` (date, nullable), `IsActive` (bit, mặc định true - dùng để khóa tài khoản khi nhân viên nghỉ việc).


* **`AppRole : IdentityRole<Guid>`**
* Bổ sung: `Description` (nvarchar 255, nullable - mô tả quyền này làm gì).



## 3. INFRASTRUCTURE LAYER (DBCONTEXT)

Sửa đổi `HushStoreDbContext.cs`:

* **Thay đổi kế thừa:** Chuyển từ `DbContext` sang `IdentityDbContext<AppUser, AppRole, Guid>`. (Bắt buộc phải using `Microsoft.AspNetCore.Identity.EntityFrameworkCore`).
* **Cấu hình OnModelCreating:**
* Gọi `base.OnModelCreating(builder)`.
* Đổi tên các bảng mặc định của Identity để Database nhìn "sạch sẽ" hơn (tùy chọn nhưng khuyến khích):
* `AspNetUsers` -> `AppUsers`
* `AspNetRoles` -> `AppRoles`
* `AspNetUserRoles` -> `AppUserRoles`
... (các bảng Claims, Tokens, Logins tương tự).





## 4. API LAYER (CẤU HÌNH ĐĂNG KÝ)

Trong `Program.cs` (hoặc file extension `ServiceCollectionExtensions`):

* Đăng ký Identity:
```csharp
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    // Tùy chỉnh độ khó của Password (cho giống thực tế)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;

    // Cấu hình Lockout (Chống Brute-force)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<HushStoreDbContext>()
.AddDefaultTokenProviders(); // Bắt buộc để sau này làm chức năng Quên mật khẩu/Refresh Token

```



## 5. SEED DATA (DỮ LIỆU KHỞI TẠO MẶC ĐỊNH)

Tạo một file `DbInitializer.cs` hoặc cấu hình thẳng trong `OnModelCreating` để khi chạy Migration, Database sẽ có sẵn các Role tối quan trọng:

* `Admin` (Quản trị viên toàn quyền).
* `WarehouseManager` (Quản lý kho - dùng cho tính năng Nhập kho).
* `Customer` (Khách hàng).