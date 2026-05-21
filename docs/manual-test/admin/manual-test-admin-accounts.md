# Manual Test: Admin — Quản lý Khách hàng & Nhân viên

Tài liệu này liệt kê tất cả các flow cần kiểm thử thủ công trên UI cho 2 module: Quản lý Khách hàng và Quản lý Nhân viên.

**Yêu cầu trước khi test:**
- Đăng nhập tài khoản có role `Admin`
- Ứng dụng đang chạy (API + Client)

**Ký hiệu kết quả:** ✅ Pass &nbsp;|&nbsp; ❌ Fail &nbsp;|&nbsp; ⬜ Chưa test

---

## 1. Quản lý Khách hàng (`/admin/customers`)

### 1.1 Happy Path — Danh sách & Tìm kiếm

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CUST-01 | Tải danh sách mặc định | 1. Vào `/admin/customers` | Bảng tải đúng, hiển thị trang 1 (10 dòng), phân trang hoạt động, cột đủ: Khách hàng, SĐT, Email, Tham gia, Trạng thái, Thao tác | ⬜ |
| TC-CUST-02 | Tìm kiếm theo tên | 1. Nhập tên khách hàng vào ô tìm kiếm<br>2. Chờ debounce ~400ms | Bảng lọc chỉ còn khách hàng tên chứa từ khóa; bỏ trống ô tìm kiếm → danh sách gốc phục hồi | ⬜ |
| TC-CUST-03 | Tìm kiếm theo email | 1. Nhập địa chỉ email (hoặc một phần) vào ô tìm kiếm | Bảng hiển thị đúng khách hàng có email khớp | ⬜ |
| TC-CUST-04 | Tìm kiếm theo số điện thoại | 1. Nhập SĐT (hoặc một phần) vào ô tìm kiếm | Bảng hiển thị đúng khách hàng có SĐT khớp | ⬜ |
| TC-CUST-05 | Filter trạng thái — Hoạt động | 1. Chọn **Hoạt động** trong dropdown Trạng thái | Chỉ hiện khách hàng có chip `Hoạt động` (xanh); chip `Đã khóa` không xuất hiện | ⬜ |
| TC-CUST-06 | Filter trạng thái — Đã khóa | 1. Chọn **Đã khóa** trong dropdown Trạng thái | Chỉ hiện khách hàng có chip `Đã khóa` (đỏ) | ⬜ |
| TC-CUST-07 | Filter trạng thái — Xóa filter | 1. Sau khi chọn filter, nhấn nút X (Clear) trên dropdown Trạng thái | Danh sách trở về hiển thị tất cả | ⬜ |
| TC-CUST-08 | Filter giới tính — Nam | 1. Chọn **Nam** trong dropdown Giới tính | Chỉ hiện khách hàng giới tính Nam | ⬜ |
| TC-CUST-09 | Filter giới tính — Nữ | 1. Chọn **Nữ** trong dropdown Giới tính | Chỉ hiện khách hàng giới tính Nữ | ⬜ |
| TC-CUST-10 | Kết hợp filter trạng thái + giới tính | 1. Chọn **Đã khóa** + **Nữ** | Chỉ hiện khách hàng nữ đã bị khóa | ⬜ |
| TC-CUST-11 | Phân trang | 1. Chọn 20 dòng/trang từ dropdown phân trang<br>2. Chuyển sang trang 2 | Bảng tải đúng 20 dòng; trang 2 hiển thị đúng offset | ⬜ |

---

### 1.2 Happy Path — Thêm khách hàng

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CUST-12 | Thêm khách hàng đầy đủ thông tin | 1. Nhấn **Thêm tài khoản**<br>2. Email: `test@example.com`<br>3. Họ tên: `Nguyễn Văn A`<br>4. SĐT: `0901234567`<br>5. Giới tính: Nam<br>6. Ngày sinh: `01/01/1990`<br>7. Địa chỉ: `123 Lê Lợi`<br>8. Thành phố: `Đà Nẵng`<br>9. Nhấn **Tạo mới** | Dialog đóng; snackbar `"Tạo khách hàng thành công!"`; khách hàng mới xuất hiện trong bảng | ⬜ |
| TC-CUST-13 | Thêm khách hàng chỉ điền trường bắt buộc | 1. Nhấn **Thêm tài khoản**<br>2. Email: `minimal@test.com`<br>3. Họ tên: `Trần Thị B`<br>4. SĐT: `0356789012`<br>5. Để trống ngày sinh, địa chỉ, thành phố<br>6. Nhấn **Tạo mới** | Tạo thành công; các trường tùy chọn hiển thị trống/dash ở detail | ⬜ |
| TC-CUST-14 | Hủy dialog thêm bằng nút Hủy | 1. Mở dialog **Thêm tài khoản**<br>2. Nhập một số dữ liệu<br>3. Nhấn **Hủy** | Dialog đóng; không có bản ghi mới trong bảng | ⬜ |
| TC-CUST-15 | Hủy dialog thêm bằng phím Esc | 1. Mở dialog **Thêm tài khoản**<br>2. Nhấn phím **Esc** | Dialog đóng; không có bản ghi mới | ⬜ |

---

### 1.3 Happy Path — Sửa khách hàng

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CUST-16 | Sửa thông tin cơ bản | 1. Nhấn icon **Edit** (bút vàng) trên một khách hàng<br>2. Đổi Họ tên sang `Nguyễn Văn B`<br>3. Đổi Giới tính sang Nữ<br>4. Đổi Ngày sinh sang `15/06/1995`<br>5. Nhấn **Cập nhật** | Dialog đóng; bảng reload; tên mới hiển thị đúng; snackbar `"Cập nhật thành công!"` | ⬜ |
| TC-CUST-17 | Email và SĐT là read-only khi sửa | 1. Mở dialog **Cập nhật** bất kỳ khách hàng | Field Email và Số điện thoại hiển thị nhưng không cho phép chỉnh sửa (ReadOnly) | ⬜ |
| TC-CUST-18 | Sửa — upload ảnh đại diện mới | 1. Mở dialog **Cập nhật**<br>2. Click vào vùng upload ảnh<br>3. Chọn file ảnh jpg/png hợp lệ | Spinner `"Đang upload..."` hiện trong lúc upload; sau khi xong, preview ảnh hiển thị; nhấn **Cập nhật** → lưu thành công | ⬜ |
| TC-CUST-19 | Sửa — xóa ảnh đại diện hiện tại | 1. Mở dialog **Cập nhật** của khách hàng có ảnh<br>2. Nhấn nút **Xóa** bên cạnh avatar<br>3. Nhấn **Cập nhật** | Avatar về null; danh sách hiển thị avatar chữ cái viết hoa thay ảnh | ⬜ |
| TC-CUST-20 | Hủy dialog sửa | 1. Mở dialog **Cập nhật**<br>2. Thay đổi tên<br>3. Nhấn **Hủy** | Dialog đóng; tên trong bảng không đổi | ⬜ |

---

### 1.4 Happy Path — Xem chi tiết khách hàng

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CUST-21 | Điều hướng tới trang chi tiết | 1. Nhấn icon **Xem** (mắt xanh) trên một khách hàng | Điều hướng tới `/admin/customers/{id}`; thông tin Profile, liên lạc hiển thị đúng | ⬜ |
| TC-CUST-22 | Tab Đơn hàng — có dữ liệu | 1. Vào chi tiết khách hàng đã có đơn<br>2. Xem tab **Đơn hàng (n)** | Bảng liệt kê mã đơn, ngày, tổng tiền, trạng thái chip đúng màu (Chờ xác nhận=vàng, Thành công=xanh, Đã hủy=đỏ…) | ⬜ |
| TC-CUST-23 | Tab Giỏ hàng — có hàng | 1. Vào chi tiết khách hàng đang có hàng trong giỏ<br>2. Click tab **Giỏ hàng (n)** | Hiển thị ảnh thumbnail, tên sản phẩm, phân loại, số lượng, đơn giá, thành tiền | ⬜ |
| TC-CUST-24 | Tab Đơn hàng — không có dữ liệu | 1. Vào chi tiết khách hàng chưa có đơn nào | Tab Đơn hàng hiển thị `"Không có đơn hàng nào."` | ⬜ |
| TC-CUST-25 | Tab Giỏ hàng — rỗng | 1. Vào chi tiết khách hàng có giỏ hàng rỗng<br>2. Click tab **Giỏ hàng (0)** | Hiển thị `"Giỏ hàng rỗng."` | ⬜ |
| TC-CUST-26 | Khách hàng bị khóa hiển thị chip | 1. Vào chi tiết một khách hàng đã bị khóa | Chip `"Tài khoản đã khóa"` màu đỏ xuất hiện dưới tên | ⬜ |
| TC-CUST-27 | Quay lại danh sách từ chi tiết | 1. Trên trang chi tiết, nhấn nút **Quay lại** | Điều hướng về `/admin/customers` | ⬜ |

---

### 1.5 Happy Path — Khóa / Mở khóa tài khoản

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CUST-28 | Khóa tài khoản — có lý do | 1. Nhấn icon **Khóa** (ổ khóa đỏ) trên khách hàng đang hoạt động<br>2. Dialog hiện tên khách hàng, field "Lý do khóa"<br>3. Nhập lý do: `"Vi phạm chính sách"`<br>4. Nhấn **Tiến hành khóa** | Dialog đóng; snackbar `"Khóa tài khoản thành công."`; icon ổ khóa chuyển sang mở (xanh); chip trạng thái đổi sang `Đã khóa` | ⬜ |
| TC-CUST-29 | Khóa tài khoản — không nhập lý do | 1. Mở dialog khóa<br>2. Để trống field Lý do<br>3. Nhấn **Tiến hành khóa** | Khóa vẫn thành công (lý do không bắt buộc); snackbar thành công | ⬜ |
| TC-CUST-30 | Hủy dialog khóa | 1. Nhấn icon **Khóa** trên một khách hàng<br>2. Nhấn **Hủy** trong dialog | Dialog đóng; trạng thái khách hàng không đổi | ⬜ |
| TC-CUST-31 | Mở khóa tài khoản | 1. Nhấn icon **Mở khóa** (ổ khóa mở, xanh) trên khách hàng đã bị khóa<br>2. Confirm dialog hỏi `"Bạn có chắc chắn muốn mở khóa...?"`<br>3. Nhấn **Mở khóa** | Snackbar `"Mở khóa tài khoản thành công."`; chip đổi sang `Hoạt động`; icon đổi sang ổ khóa đỏ | ⬜ |
| TC-CUST-32 | Hủy xác nhận mở khóa | 1. Nhấn icon **Mở khóa** trên khách hàng bị khóa<br>2. Nhấn **Hủy** trong confirm dialog | Dialog đóng; tài khoản vẫn ở trạng thái Đã khóa | ⬜ |

---

### 1.6 Edge Cases & Validation — Thêm khách hàng

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CUST-33 | Họ tên rỗng | 1. Mở dialog Thêm<br>2. Để trống **Họ và tên**<br>3. Nhấn **Tạo mới** | Lỗi validation: *"Họ tên không được để trống."*; form không submit | ⬜ |
| TC-CUST-34 | Họ tên vượt quá 100 ký tự | 1. Nhập chuỗi 101 ký tự vào Họ tên<br>2. Nhấn **Tạo mới** | Lỗi: *"Họ tên không được vượt quá 100 ký tự."* | ⬜ |
| TC-CUST-35 | Email rỗng | 1. Để trống **Email**<br>2. Nhấn **Tạo mới** | Lỗi: *"Email không được để trống."* | ⬜ |
| TC-CUST-36 | Email sai định dạng | 1. Nhập `khongphaiemail`<br>2. Nhấn **Tạo mới** | Lỗi: *"Email không đúng định dạng."* | ⬜ |
| TC-CUST-37 | Email đã tồn tại trong hệ thống | 1. Nhập email của một khách hàng đang có<br>2. Nhấn **Tạo mới** | Snackbar lỗi từ server (email trùng); không tạo bản ghi mới | ⬜ |
| TC-CUST-38 | SĐT rỗng | 1. Để trống **Số điện thoại**<br>2. Nhấn **Tạo mới** | Lỗi: *"Số điện thoại không được để trống."* | ⬜ |
| TC-CUST-39 | SĐT sai định dạng — bắt đầu bằng 02 | 1. Nhập `0212345678`<br>2. Nhấn **Tạo mới** | Lỗi: *"Số điện thoại không hợp lệ..."* | ⬜ |
| TC-CUST-40 | SĐT sai định dạng — 9 chữ số | 1. Nhập `090123456` (9 số)<br>2. Nhấn **Tạo mới** | Lỗi: *"Số điện thoại không hợp lệ..."* | ⬜ |
| TC-CUST-41 | SĐT sai định dạng — chứa chữ | 1. Nhập `09012345ab`<br>2. Nhấn **Tạo mới** | Lỗi: *"Số điện thoại không hợp lệ..."* | ⬜ |
| TC-CUST-42 | SĐT đã tồn tại | 1. Nhập SĐT của khách hàng khác đang có<br>2. Nhấn **Tạo mới** | Snackbar lỗi từ server (SĐT trùng) | ⬜ |
| TC-CUST-43 | Ngày sinh là ngày hôm nay | 1. Chọn ngày sinh là ngày hiện tại (hôm nay)<br>2. Nhấn **Tạo mới** | Lỗi: *"Ngày sinh phải nhỏ hơn ngày hiện tại."* | ⬜ |
| TC-CUST-44 | Ngày sinh là ngày trong tương lai | 1. Chọn ngày sinh là ngày mai hoặc sau này<br>2. Nhấn **Tạo mới** | Lỗi: *"Ngày sinh phải nhỏ hơn ngày hiện tại."* | ⬜ |
| TC-CUST-45 | Địa chỉ vượt quá 255 ký tự | 1. Nhập chuỗi 256 ký tự vào **Địa chỉ**<br>2. Nhấn **Tạo mới** | Lỗi: *"Địa chỉ không được vượt quá 255 ký tự."* | ⬜ |
| TC-CUST-46 | Thành phố vượt quá 100 ký tự | 1. Nhập chuỗi 101 ký tự vào **Thành phố**<br>2. Nhấn **Tạo mới** | Lỗi: *"Thành phố không được vượt quá 100 ký tự."* | ⬜ |

---

### 1.7 Edge Cases & Validation — Sửa khách hàng

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CUST-47 | Sửa — Họ tên rỗng | 1. Mở dialog Cập nhật<br>2. Xóa hết nội dung field **Họ và tên**<br>3. Nhấn **Cập nhật** | Lỗi: *"Họ tên không được để trống."*; form không submit | ⬜ |
| TC-CUST-48 | Sửa — Ngày sinh tương lai | 1. Mở dialog Cập nhật<br>2. Đổi ngày sinh sang ngày tương lai<br>3. Nhấn **Cập nhật** | Lỗi: *"Ngày sinh phải nhỏ hơn ngày hiện tại."* | ⬜ |
| TC-CUST-49 | Sửa — upload file không phải ảnh | 1. Mở dialog Cập nhật<br>2. Click vùng upload ảnh đại diện<br>3. Chọn file `.pdf` hoặc `.exe` | File bị từ chối; vùng upload không thay đổi (do `accept="image/*"`) | ⬜ |

---

### 1.8 Edge Cases — Danh sách & Điều hướng

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CUST-50 | Tìm kiếm không có kết quả | 1. Nhập từ khóa không tồn tại: `xyzabcdef`<br>2. Chờ debounce | Bảng hiện `"Không tìm thấy khách hàng nào."`; tổng items = 0 | ⬜ |
| TC-CUST-51 | Xem chi tiết khách hàng không tồn tại | 1. Truy cập thủ công `/admin/customers/00000000-0000-0000-0000-000000000000` | Snackbar lỗi *"Không thể tải chi tiết khách hàng."*; điều hướng về `/admin/customers` | ⬜ |
| TC-CUST-52 | Khóa — tài khoản đã bị khóa không hiện nút Khóa | 1. Tìm một khách hàng đang có chip `Đã khóa` | Icon ổ khóa hiển thị màu xanh (mở khóa), không có icon ổ khóa đỏ | ⬜ |
| TC-CUST-53 | Mở khóa — tài khoản đang hoạt động không hiện nút Mở khóa | 1. Tìm khách hàng đang có chip `Hoạt động` | Icon ổ khóa đỏ hiển thị, không có icon mở khóa xanh | ⬜ |

---

## 2. Quản lý Nhân viên (`/admin/employees`)

> Lưu ý: Chỉ tài khoản `Admin` mới có quyền truy cập trang này (`[Authorize(Roles = "Admin")]`).

### 2.1 Happy Path — Danh sách & Tìm kiếm

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-EMP-01 | Tải danh sách mặc định | 1. Vào `/admin/employees` | Bảng tải đúng, hiển thị trang 1 (10 dòng), cột đủ: Nhân viên, SĐT, Email, Tham gia, Trạng thái, Thao tác | ⬜ |
| TC-EMP-02 | Tìm kiếm theo tên | 1. Nhập tên nhân viên vào ô tìm kiếm<br>2. Chờ debounce ~400ms | Bảng lọc đúng; bỏ từ khóa → danh sách gốc phục hồi | ⬜ |
| TC-EMP-03 | Tìm kiếm theo email | 1. Nhập địa chỉ email (hoặc một phần) | Bảng hiển thị đúng nhân viên có email khớp | ⬜ |
| TC-EMP-04 | Tìm kiếm theo số điện thoại | 1. Nhập SĐT hoặc phần đầu SĐT | Bảng lọc đúng theo SĐT | ⬜ |
| TC-EMP-05 | Filter trạng thái — Hoạt động | 1. Chọn **Hoạt động** trong dropdown Trạng thái | Chỉ hiện nhân viên chip `Hoạt động`; không có chip `Đã khóa` | ⬜ |
| TC-EMP-06 | Filter trạng thái — Đã khóa | 1. Chọn **Đã khóa** trong dropdown Trạng thái | Chỉ hiện nhân viên có chip `Đã khóa` | ⬜ |
| TC-EMP-07 | Filter trạng thái — Xóa filter | 1. Sau khi chọn filter, nhấn X (Clear) | Danh sách hiển thị lại tất cả | ⬜ |
| TC-EMP-08 | Filter giới tính | 1. Chọn **Nam** / **Nữ** / **Khác** trong dropdown Giới tính | Danh sách lọc đúng theo giới tính chọn | ⬜ |
| TC-EMP-09 | Kết hợp filter | 1. Chọn **Hoạt động** + **Nữ** | Chỉ hiện nhân viên nữ đang hoạt động | ⬜ |
| TC-EMP-10 | Phân trang | 1. Đổi sang 20 dòng/trang<br>2. Sang trang 2 | Bảng tải đúng, offset đúng | ⬜ |

---

### 2.2 Happy Path — Thêm nhân viên

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-EMP-11 | Thêm nhân viên (chức vụ: Nhân viên) | 1. Nhấn **Thêm nhân viên**<br>2. Email: `nv1@store.com`<br>3. Họ tên: `Lê Văn C`<br>4. SĐT: `0912345678`<br>5. Giới tính: Nam<br>6. Ngày sinh: `10/03/1998`<br>7. Địa chỉ, Thành phố: tùy chọn<br>8. Chức vụ: **Nhân viên**<br>9. Mật khẩu: `Abc12345`<br>10. Xác nhận MK: `Abc12345`<br>11. Nhấn **Tạo mới** | Dialog đóng; snackbar `"Tạo nhân viên thành công!"`; nhân viên xuất hiện trong bảng | ⬜ |
| TC-EMP-12 | Thêm nhân viên (chức vụ: Kỹ thuật viên) | 1–10 tương tự TC-EMP-11<br>8. Chức vụ: **Kỹ thuật viên**<br>11. Nhấn **Tạo mới** | Tạo thành công; nhân viên có role Kỹ thuật viên | ⬜ |
| TC-EMP-13 | Toggle hiện/ẩn mật khẩu | 1. Mở dialog Thêm nhân viên<br>2. Nhập mật khẩu vào field Mật khẩu<br>3. Click icon mắt bên phải | Mật khẩu chuyển từ `•••` sang văn bản rõ ràng; click lần 2 → ẩn lại | ⬜ |
| TC-EMP-14 | Toggle hiện/ẩn xác nhận mật khẩu | 1. Tương tự TC-EMP-13 nhưng với field **Xác nhận mật khẩu** | Tương tự, độc lập với field Mật khẩu | ⬜ |
| TC-EMP-15 | Hủy dialog thêm bằng nút Hủy | 1. Mở dialog Thêm<br>2. Nhập một số dữ liệu<br>3. Nhấn **Hủy** | Dialog đóng; không có bản ghi mới | ⬜ |
| TC-EMP-16 | Hủy dialog thêm bằng phím Esc | 1. Mở dialog Thêm<br>2. Nhấn **Esc** | Dialog đóng; không có bản ghi mới | ⬜ |

---

### 2.3 Happy Path — Sửa nhân viên

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-EMP-17 | Sửa thông tin cơ bản | 1. Nhấn icon **Edit** (bút vàng) trên một nhân viên<br>2. Đổi Họ tên, Giới tính, Ngày sinh, Địa chỉ, Thành phố<br>3. Nhấn **Cập nhật** | Dialog đóng; bảng reload; tên mới đúng; snackbar `"Cập nhật thành công!"` | ⬜ |
| TC-EMP-18 | Email và SĐT là read-only khi sửa | 1. Mở dialog **Cập nhật** bất kỳ nhân viên | Field Email và Số điện thoại hiển thị giá trị hiện tại, không cho chỉnh sửa | ⬜ |
| TC-EMP-19 | Sửa — đổi chức vụ sang Kỹ thuật viên | 1. Mở dialog **Cập nhật** nhân viên đang là Nhân viên<br>2. Bật checkbox **Kỹ thuật viên**<br>3. Nhấn **Cập nhật** | Cập nhật thành công; vai trò thay đổi trong hệ thống | ⬜ |
| TC-EMP-20 | Sửa — tắt Kỹ thuật viên | 1. Mở dialog **Cập nhật** nhân viên đang là KTV<br>2. Bỏ tick checkbox **Kỹ thuật viên**<br>3. Nhấn **Cập nhật** | Cập nhật thành công; vai trò trở về Nhân viên | ⬜ |
| TC-EMP-21 | Sửa — upload ảnh đại diện | 1. Mở dialog **Cập nhật**<br>2. Click vùng upload ảnh<br>3. Chọn file jpg/png hợp lệ | Spinner upload hiện; sau khi xong, preview ảnh mới hiện; lưu thành công | ⬜ |
| TC-EMP-22 | Sửa — xóa ảnh đại diện | 1. Mở dialog **Cập nhật** của nhân viên có ảnh<br>2. Nhấn **Xóa** bên cạnh avatar<br>3. Nhấn **Cập nhật** | Avatar về null; danh sách hiển thị avatar chữ cái đầu | ⬜ |
| TC-EMP-23 | Hủy dialog sửa | 1. Mở dialog **Cập nhật**<br>2. Thay đổi tên<br>3. Nhấn **Hủy** | Dialog đóng; dữ liệu trong bảng không đổi | ⬜ |
| TC-EMP-24 | Form sửa không có trường Mật khẩu | 1. Mở dialog **Cập nhật** bất kỳ nhân viên | Các field Password và ConfirmPassword không xuất hiện; chỉ có Chức vụ là checkbox | ⬜ |

---

### 2.4 Happy Path — Khóa / Mở khóa tài khoản

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-EMP-25 | Khóa tài khoản — có lý do | 1. Nhấn icon **Khóa** (đỏ) trên nhân viên đang hoạt động<br>2. Dialog hiện tên nhân viên<br>3. Nhập lý do: `"Nghỉ việc"`<br>4. Nhấn **Tiến hành khóa** | Snackbar `"Khóa tài khoản thành công."`; chip trạng thái đổi sang `Đã khóa`; icon chuyển sang mở khóa xanh | ⬜ |
| TC-EMP-26 | Khóa tài khoản — không nhập lý do | 1. Mở dialog khóa<br>2. Để trống lý do<br>3. Nhấn **Tiến hành khóa** | Khóa thành công (lý do không bắt buộc); snackbar thành công | ⬜ |
| TC-EMP-27 | Hủy dialog khóa | 1. Nhấn icon **Khóa**<br>2. Nhấn **Hủy** trong dialog | Dialog đóng; tài khoản không đổi | ⬜ |
| TC-EMP-28 | Mở khóa tài khoản | 1. Nhấn icon **Mở khóa** (xanh) trên nhân viên bị khóa<br>2. Confirm dialog: `"Bạn có chắc chắn muốn mở khóa...?"`<br>3. Nhấn **Mở khóa** | Snackbar `"Mở khóa tài khoản thành công."`; chip đổi sang `Hoạt động` | ⬜ |
| TC-EMP-29 | Hủy xác nhận mở khóa | 1. Nhấn icon **Mở khóa**<br>2. Nhấn **Hủy** trong confirm dialog | Dialog đóng; tài khoản vẫn `Đã khóa` | ⬜ |

---

### 2.5 Edge Cases & Validation — Thêm nhân viên

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-EMP-30 | Họ tên rỗng | 1. Mở dialog Thêm<br>2. Để trống **Họ và tên**<br>3. Nhấn **Tạo mới** | Lỗi: *"Họ tên không được để trống."*; form không submit | ⬜ |
| TC-EMP-31 | Họ tên vượt quá 100 ký tự | 1. Nhập 101 ký tự vào Họ tên<br>2. Nhấn **Tạo mới** | Lỗi: *"Họ tên không được vượt quá 100 ký tự."* | ⬜ |
| TC-EMP-32 | Email rỗng | 1. Để trống **Email**<br>2. Nhấn **Tạo mới** | Lỗi: *"Email không được để trống."* | ⬜ |
| TC-EMP-33 | Email sai định dạng | 1. Nhập `khongphaiemail`<br>2. Nhấn **Tạo mới** | Lỗi: *"Email không đúng định dạng."* | ⬜ |
| TC-EMP-34 | Email đã tồn tại | 1. Nhập email của nhân viên hoặc khách hàng đang có<br>2. Nhấn **Tạo mới** | Snackbar lỗi từ server; không tạo bản ghi mới | ⬜ |
| TC-EMP-35 | SĐT rỗng | 1. Để trống **Số điện thoại**<br>2. Nhấn **Tạo mới** | Lỗi: *"Số điện thoại không được để trống."* | ⬜ |
| TC-EMP-36 | SĐT sai định dạng | 1. Nhập `0212345678` (bắt đầu 02)<br>2. Nhấn **Tạo mới** | Lỗi: *"Số điện thoại không hợp lệ..."* | ⬜ |
| TC-EMP-37 | Mật khẩu rỗng | 1. Để trống **Mật khẩu**<br>2. Nhấn **Tạo mới** | Lỗi: *"Mật khẩu không được để trống."* | ⬜ |
| TC-EMP-38 | Mật khẩu dưới 8 ký tự | 1. Nhập `Abc123` (6 ký tự)<br>2. Nhấn **Tạo mới** | Lỗi: *"Mật khẩu phải có ít nhất 8 ký tự."* | ⬜ |
| TC-EMP-39 | Mật khẩu không có chữ hoa | 1. Nhập `abc12345` (toàn chữ thường)<br>2. Nhấn **Tạo mới** | Lỗi: *"Mật khẩu phải có ít nhất 1 chữ hoa."* | ⬜ |
| TC-EMP-40 | Mật khẩu không có chữ thường | 1. Nhập `ABC12345` (toàn chữ hoa)<br>2. Nhấn **Tạo mới** | Lỗi: *"Mật khẩu phải có ít nhất 1 chữ thường."* | ⬜ |
| TC-EMP-41 | Mật khẩu không có chữ số | 1. Nhập `AbcDefgh` (không có số)<br>2. Nhấn **Tạo mới** | Lỗi: *"Mật khẩu phải có ít nhất 1 chữ số."* | ⬜ |
| TC-EMP-42 | Xác nhận mật khẩu rỗng | 1. Nhập Mật khẩu hợp lệ, để trống **Xác nhận mật khẩu**<br>2. Nhấn **Tạo mới** | Lỗi: *"Xác nhận mật khẩu không được để trống."* | ⬜ |
| TC-EMP-43 | Xác nhận mật khẩu không khớp | 1. Mật khẩu: `Abc12345`<br>2. Xác nhận: `Abc12346`<br>3. Nhấn **Tạo mới** | Lỗi: *"Xác nhận mật khẩu không khớp."* | ⬜ |
| TC-EMP-44 | Ngày sinh là hôm nay | 1. Chọn ngày sinh = ngày hiện tại<br>2. Nhấn **Tạo mới** | Lỗi: *"Ngày sinh phải nhỏ hơn ngày hiện tại."* | ⬜ |
| TC-EMP-45 | Ngày sinh là ngày tương lai | 1. Chọn ngày sinh = ngày mai<br>2. Nhấn **Tạo mới** | Lỗi: *"Ngày sinh phải nhỏ hơn ngày hiện tại."* | ⬜ |

---

### 2.6 Edge Cases & Validation — Sửa nhân viên

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-EMP-46 | Sửa — Họ tên rỗng | 1. Mở dialog Cập nhật<br>2. Xóa hết **Họ và tên**<br>3. Nhấn **Cập nhật** | Lỗi: *"Họ tên không được để trống."*; form không submit | ⬜ |
| TC-EMP-47 | Sửa — Ngày sinh tương lai | 1. Mở dialog Cập nhật<br>2. Đổi ngày sinh sang tương lai<br>3. Nhấn **Cập nhật** | Lỗi: *"Ngày sinh phải nhỏ hơn ngày hiện tại."* | ⬜ |
| TC-EMP-48 | Sửa — upload file không phải ảnh | 1. Mở dialog Cập nhật<br>2. Click vùng upload ảnh<br>3. Chọn file `.pdf` | File bị từ chối (do `accept="image/*"`) | ⬜ |

---

### 2.7 Edge Cases — Danh sách & Bảo mật

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-EMP-49 | Tìm kiếm không có kết quả | 1. Nhập từ khóa không tồn tại: `xyzabcdef`<br>2. Chờ debounce | Bảng hiện `"Không tìm thấy nhân viên nào."` | ⬜ |
| TC-EMP-50 | Nhân viên bị khóa không hiện nút Khóa | 1. Tìm nhân viên chip `Đã khóa` | Chỉ hiện icon mở khóa xanh; không có icon khóa đỏ | ⬜ |
| TC-EMP-51 | Nhân viên đang hoạt động không hiện nút Mở khóa | 1. Tìm nhân viên chip `Hoạt động` | Chỉ hiện icon khóa đỏ; không có icon mở khóa xanh | ⬜ |
| TC-EMP-52 | Truy cập trang nhân viên với role Employee | 1. Đăng xuất Admin<br>2. Đăng nhập bằng tài khoản có role `Employee`<br>3. Truy cập `/admin/employees` | Bị từ chối truy cập (403 hoặc redirect về trang đăng nhập/không có quyền) | ⬜ |
| TC-EMP-53 | Truy cập trang nhân viên với role Customer | 1. Đăng nhập bằng tài khoản có role `Customer`<br>2. Truy cập `/admin/employees` | Bị từ chối truy cập | ⬜ |
