# BLUEPRINT: AUTH SECURITY PATCH (VÁ LỖ HỔNG BẢO MẬT)

**Domain:** Identity & Access Management
**Mục tiêu:** Nâng cấp độ an toàn cho hệ thống Xác thực bằng cách băm Refresh Token (chống lộ lọt data) và áp dụng Rate Limiting (chống DoS / Credential Stuffing).

## 1. TASK 1: HASHING REFRESH TOKEN (SHA-256)

**Vị trí sửa đổi:** `AuthService.cs` (Tầng Service)

**Yêu cầu nghiệp vụ:**

* Tuyệt đối không lưu Refresh Token dạng Plaintext vào thuộc tính `AppUser.RefreshToken`.
* **Luồng sinh Token (`GenerateRefreshToken`):** Sau khi tạo ra chuỗi ngẫu nhiên 64-byte (Base64), bắt buộc phải dùng `SHA256.Create()` để băm chuỗi này. Lưu giá trị đã băm (Hashed Token) vào DB, nhưng trả về cho Client chuỗi Plaintext ban đầu.
* **Luồng xác thực (`RefreshTokenAsync`):** Khi Client gửi Refresh Token cũ lên để xin cấp mới, hệ thống phải băm chuỗi nhận được bằng SHA-256, sau đó mới mang đi so sánh (hoặc query) với cột `RefreshToken` trong DB.

## 2. TASK 2: RATE LIMITING CHỐNG DOS & BRUTE-FORCE

**Vị trí sửa đổi:** `Program.cs` (Tầng API) và `AuthController.cs`

**Yêu cầu nghiệp vụ:**

* **Cấu hình Middleware (`Program.cs`):** Tích hợp `Microsoft.AspNetCore.RateLimiting`. Tạo một policy dạng `FixedWindowRateLimiter` có tên là `"LoginRateLimit"`.
* **Tham số giới hạn:** Cấu hình tối đa **5 requests / 1 phút / 1 địa chỉ IP** (Phân biệt qua `HttpContext.Connection.RemoteIpAddress`).
* **Phản hồi khi vi phạm:** Bắt buộc cấu hình trạng thái trả về là `429 Too Many Requests`. KHÔNG cho phép request đi sâu vào Controller nếu đã vượt ngưỡng.
* **Áp dụng:** Gắn attribute `[EnableRateLimiting("LoginRateLimit")]` trực tiếp lên endpoint `POST /api/auth/login` trong `AuthController`.

