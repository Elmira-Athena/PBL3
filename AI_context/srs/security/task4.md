

# IMPLEMENTATION PLAN: BLAZOR AUTH STATE & INTERCEPTOR (TASK 4)

**Project:** HushStore
**Module:** Frontend Security (Blazor WebAssembly)

## 1. MỤC TIÊU (OBJECTIVES)

Giúp Blazor WebAssembly lưu trữ Token an toàn, tự động giải mã JWT để cập nhật giao diện (ẩn/hiện menu), và tự động đính kèm Token vào tất cả các HTTP Request gọi xuống API.

## 2. CÁC THÀNH PHẦN CẦN XÂY DỰNG

Cần cài đặt thư viện `Blazored.LocalStorage` vào project Client để thao tác với bộ nhớ trình duyệt.

### 2.1. `JwtAuthenticationStateProvider` (Cốt lõi)

* Kế thừa từ `AuthenticationStateProvider` của Microsoft.
* **Nhiệm vụ:**
* Đọc chuỗi Token từ `LocalStorage`.
* Nếu không có hoặc token rỗng -> Trả về trạng thái "Chưa đăng nhập" (`new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))`).
* Nếu có -> Dùng logic decode Base64Payload để bóc tách các Claims (Id, Name, Roles...).
* Gắn các Claims này vào `ClaimsPrincipal` để báo cho Blazor biết: *"Đang có ông WarehouseManager đăng nhập nè!"*.



### 2.2. `AuthHeaderHandler` (DelegatingHandler)

* Là một Interceptor (người chặn đường) nằm giữa Blazor và API.
* **Nhiệm vụ:** Trôi dạt ngầm dưới background. Cứ mỗi lần các Client Service (như `ImportReceiptClientService`) gọi hàm `PostAsync` hay `GetAsync`, class này sẽ chặn lại, móc Token từ LocalStorage và nhét vào Header: `Authorization: Bearer <token>`, rồi mới cho request đi tiếp. Nhờ vậy, ta không phải viết code add token thủ công ở hàng chục file Service khác nhau.

### 2.3. Đăng ký Services (`Program.cs` của Client)

* Thêm `AddBlazoredLocalStorage()`.
* Đăng ký `AuthenticationStateProvider` thành `JwtAuthenticationStateProvider`.
* Gắn `AuthHeaderHandler` vào tất cả các `HttpClient` đang được đăng ký (dùng `.AddHttpMessageHandler<AuthHeaderHandler>()`).
