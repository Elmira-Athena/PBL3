

# BLUEPRINT: BLAZOR AUTH UI & ROUTING (TASK 5)

**Domain:** Identity & Access Management (Frontend Security)
**Mục tiêu:** Cung cấp "Quầy bán vé" (Login) và giăng lưới bảo vệ các "Cửa phòng" (Router/Menu) trên giao diện Blazor.

---

## 1. PHÂN RÃ HỆ THỐNG UI (UI COMPONENTS)

Thay vì code tràn lan, UI bảo mật được chia làm 3 phòng tuyến rạch ròi:

* **Phòng tuyến 1 - Cửa ngõ (Login Form):** Nơi thu thập thông tin, xác thực và nhận/lưu trữ JWT Token.
* **Phòng tuyến 2 - Vòng ngoài (App Router):** Chặn các truy cập trực tiếp bằng cách gõ URL bậy bạ trên thanh địa chỉ.
* **Phòng tuyến 3 - Vòng trong (Navigation/Layout):** Ẩn/hiện các chức năng (Nút bấm, Menu) dựa trên Role của Token đang giữ.

---

## 2. BẢN ĐỒ NGHIỆP VỤ - GIAO DIỆN (BUSINESS - UI MAPPING)

| Tính năng / Component | Vị trí thực thi | Ghi chú logic & Nghiệp vụ (AI Coder cần lưu ý) |
| --- | --- | --- |
| **Đăng nhập (Login)** | `Pages/Auth/Login.razor` | Bắt buộc validate định dạng Email/Password trước khi gọi API. Nhận Token thành công phải lưu ngay vào `LocalStorage` và **bắt buộc** gọi hàm ép Blazor cập nhật trạng thái Auth. |
| **Đăng xuất (Logout)** | `Layout/MainLayout` hoặc `NavMenu` | Xóa toàn bộ Access/Refresh Token khỏi `LocalStorage`. Cập nhật lại Auth State về "Rỗng" và ép chuyển hướng (Redirect) về trang Login. |
| **Bảo vệ URL (Routing)** | `App.razor` (Root Component) | Thay thế bộ định tuyến mặc định bằng bộ định tuyến yêu cầu xác thực (`AuthorizeRouteView`). Bắt các trường hợp `NotAuthorized` để đẩy ra trang Login. |
| **Phân quyền Menu** | `Layout/AdminNavMenu.razor`<br>

<br>`Layout/WarehouseNavMenu.razor` | Bọc các menu bằng Component kiểm tra quyền (`AuthorizeView`). **Luật:** Admin thấy "Sản phẩm, Người dùng"; WarehouseManager thấy "Nhập kho, Kiểm kê". |

---

## 3. CÁC QUY TẮC NGHIỆP VỤ & KỸ THUẬT CỐT LÕI (CRITICAL RULES)

**A. Quy tắc Trải nghiệm người dùng (UX State)**

* **Không yêu cầu tải lại trang (No F5):** Đây là ứng dụng SPA (Single Page Application). Sau khi đăng nhập hoặc đăng xuất, giao diện phải tự động "bật/tắt" các menu ngay lập tức thông qua Provider. Nếu bắt người dùng tự nhấn F5 trình duyệt để cập nhật quyền thì **"chỗ này không ổn"**.
* **Xử lý lỗi thân thiện:** Nếu API trả về lỗi 401 (Sai mật khẩu) hoặc 403 (Bị khóa tài khoản), tuyệt đối không được crash app. Phải bắt Exception và hiển thị thông báo bằng `ISnackbar` (Tiếng Việt có dấu).

**B. Quy tắc Bảo mật UI (Frontend Security)**

* **Không lưu trữ mật khẩu:** UI chỉ làm nhiệm vụ "truyền tin". Tuyệt đối không lưu mật khẩu của khách hàng vào LocalStorage hay bất kỳ biến tĩnh (static) nào sau khi Login xong.
* **UI không phải là bảo mật tuyệt đối:** Hãy nhớ việc ẩn Menu trên UI chỉ để UX gọn gàng. Dù hacker có dùng F12 bật cái Menu "Nhập hàng" lên, API Backend vẫn sẽ khóa mõm chúng lại bằng lỗi 403. Không được đặt niềm tin 100% vào UI.

---

## 4. LỘ TRÌNH TRIỂN KHAI ĐỀ XUẤT (EXECUTION PHASES)

* **Bước 1: Khởi tạo Trang Đăng nhập**
* Dựng Form bằng thư viện UI, tích hợp FluentValidation.
* Gọi `AuthClientService`, bắt luồng lưu Token.


* **Bước 2: Nối luồng State (Cực kỳ quan trọng)**
* Tiêm (Inject) `AuthenticationStateProvider` vào trang Login.
* Ép gọi hàm báo cáo trạng thái (`MarkUserAsAuthenticated`) ngay sau bước lưu Token.


* **Bước 3: Giăng lưới App Router**
* Vào file cấu hình Router gốc, bọc lại bằng thẻ kiểm duyệt quyền.
* Setup luồng: Ai chưa đăng nhập mà gõ link `/admin` -> Đẩy về `/login`.


* **Bước 4: Cắt tỉa Menu theo Role**
* Rà soát toàn bộ các thanh điều hướng (Sidebar).
* Đặt các block điều kiện Role để giấu đi những nghiệp vụ không thuộc thẩm quyền của User đang đăng nhập. Add thêm nút Đăng xuất.
