
# IMPLEMENTATION PLAN: AUTH SERVICE & JWT (TASK 2)

**Project:** HushStore
**Module:** Authentication (Cấp phát Token)
**Rule:** Bắt buộc áp dụng luồng Access Token + Refresh Token.

## 1. CẬP NHẬT ENTITY `AppUser` (CORE LAYER)

Để lưu trữ Refresh Token, chúng ta cần bổ sung thêm 2 cột vào class `AppUser` (đã tạo ở Task 1):

* `RefreshToken` (string, nullable): Chứa chuỗi Refresh Token ngẫu nhiên.
* `RefreshTokenExpiryTime` (DateTime, nullable): Thời hạn của Refresh Token.

## 2. DATA TRANSFER OBJECTS (SHARED LAYER)

Tạo trong `HushStore.Shared/DTOs/Auth`:

* **`LoginRequest`**: `Email`, `Password` (Bắt buộc nhập, dùng FluentValidation).
* **`TokenResponse`**: `AccessToken` (string), `RefreshToken` (string).
* **`RefreshTokenRequest`**: `AccessToken` (string), `RefreshToken` (string) - Dùng để gửi yêu cầu xin cấp lại token mới khi token cũ hết hạn.

## 3. CẤU HÌNH BÍ MẬT (`appsettings.json`)

Thêm block cấu hình JWT vào project `HushStore.API`:

```json
"JwtSettings": {
  "SecretKey": "DayLaMotChuoiBiMatSieuDaiVaPhucTapCuaHushStore_KhongDuocDeLo_2026",
  "Issuer": "HushStoreAPI",
  "Audience": "HushStoreBlazorClient",
  "AccessTokenExpirationMinutes": 15,
  "RefreshTokenExpirationDays": 7
}

```

*(Lưu ý: Chuỗi `SecretKey` phải dài ít nhất 32 ký tự để thuật toán HS256 không báo lỗi).*

## 4. SERVICE LAYER (`IAuthService` & `AuthService`)

Sử dụng `UserManager<AppUser>` và `RoleManager<AppRole>` của Identity.

**Luồng hàm `LoginAsync(LoginRequest request)`:**

1. Tìm User theo Email. Nếu không thấy -> Trả về lỗi "Tài khoản hoặc mật khẩu không đúng".
2. Dùng `UserManager.CheckPasswordAsync` để kiểm tra mật khẩu.
3. Nếu đúng, kiểm tra `IsActive`. Nếu `false` -> Báo lỗi "Tài khoản đã bị khóa".
4. Gọi hàm `GenerateJwtToken` (Tạo Access Token chứa Claims: Id, Email, FullName, và danh sách Roles).
5. Gọi hàm `GenerateRefreshToken` (Sinh chuỗi ngẫu nhiên bằng `RNGCryptoServiceProvider`).
6. Lưu `RefreshToken` và hạn sử dụng vào DB (`UserManager.UpdateAsync`).
7. Trả về `TokenResponse`.

**Luồng hàm `RefreshTokenAsync(RefreshTokenRequest request)`:**

1. Bóc tách Access Token (dù đã hết hạn) để lấy `UserId`.
2. Tìm User trong DB. Kiểm tra `User.RefreshToken` có khớp với chuỗi gửi lên không, và đã hết hạn chưa.
3. Nếu hợp lệ -> Sinh ra cặp Access Token + Refresh Token MỚI, lưu DB và trả về cho Client.

## 5. API ENDPOINTS (`AuthController`)

* `POST /api/auth/login`: Nhận `LoginRequest`, trả về `TokenResponse`.
* `POST /api/auth/refresh-token`: Nhận `RefreshTokenRequest`, trả về `TokenResponse`.
