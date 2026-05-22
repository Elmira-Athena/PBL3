# Manual Test Guide — Hóa đơn dịch vụ

> Test thông qua Frontend: `https://localhost:7107`
>
> Cần tài khoản: **Employee** (hoặc Admin).

---

## Thiết lập trước khi test

| Bước | Hành động |
|------|----------|
| Khởi động DB | `docker-compose up -d` (từ thư mục `Infrastructure/db/`) |
| Chạy migration | `dotnet ef database update --project src/Infrastructure --startup-project src/API` |
| Chạy API | `dotnet run --project src/API/API.csproj` |
| Chạy Client | `dotnet run --project src/Client/Client.csproj` |
| Đăng nhập | Truy cập Frontend, login bằng tài khoản Employee |
| Dữ liệu cần có | Ít nhất 1 phiếu sửa chữa có **Trạng thái = Hoàn thành (Status 9)** và **Kiểu xử lý = Sửa có phí (PaidRepair)** và **chưa có hóa đơn** — đây là điều kiện để nút "Xuất Hóa Đơn" hiển thị |

> **Gợi ý chuẩn bị dữ liệu:** Tạo phiếu sửa → chẩn đoán → tạo báo giá → khách duyệt → sửa xong → nhấn "Hoàn Thành". Hoặc nhờ Admin seed sẵn dữ liệu test.

---

## Flow 1 — Happy Path: Phát hành hóa đơn từ phiếu sửa chữa

**Mục tiêu:** Phát hành hóa đơn dịch vụ thành công, các mục từ báo giá tự động được copy vào.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Truy cập `/admin/service-tickets` → tìm phiếu có trạng thái **"Hoàn Thành"** và kiểu xử lý **"Sửa Có Phí"** → click vào phiếu | Trang chi tiết phiếu hiển thị, thanh sidebar hành động bên phải có nút **"Xuất Hóa Đơn"** |
| 2 | Click nút **"Xuất Hóa Đơn"** | Dialog "Phát Hành Hóa Đơn Dịch Vụ" hiện ra: dropdown Phương thức thanh toán (mặc định "Tiền Mặt"), ô Ghi chú, thông báo info "Các mục từ báo giá sẽ tự động được copy vào hóa đơn này" |
| 3 | Chọn phương thức thanh toán **"Tiền Mặt"** → để trống Ghi chú → click **"Tạo Hóa Đơn"** | Dialog đóng, snackbar xanh: "Đã xuất hóa đơn." |
| 4 | Kiểm tra trang chi tiết phiếu sau khi reload | Tab **"Hóa Đơn"** xuất hiện; nút "Xuất Hóa Đơn" biến mất khỏi sidebar |
| 5 | Click vào tab **"Hóa Đơn"** | Hiển thị: Mã Hóa Đơn (dạng `SRV-yyyyMMdd-NNN`), Ngày Phát Hành, Tổng Cộng |

---

## Flow 2 — Phát hành hóa đơn với phương thức Chuyển Khoản và có ghi chú

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Mở dialog "Xuất Hóa Đơn" trên 1 phiếu hợp lệ | Dialog hiện ra |
| 2 | Chọn phương thức **"Chuyển Khoản"** → nhập ghi chú "Khách đã chuyển khoản đủ" → click **"Tạo Hóa Đơn"** | Snackbar xanh: "Đã xuất hóa đơn." |
| 3 | Vào `/admin/service-invoices` → click vào hóa đơn vừa tạo | Trang chi tiết hiển thị ghi chú "Khách đã chuyển khoản đủ" ở phần cuối |

---

## Flow 3 — Hủy dialog, không tạo hóa đơn

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Mở dialog "Xuất Hóa Đơn" | Dialog hiện ra |
| 2 | Click **"Đóng"** | Dialog đóng, không có snackbar, hóa đơn không được tạo |
| 3 | Kiểm tra tab "Hóa Đơn" trên phiếu | Tab không xuất hiện, nút "Xuất Hóa Đơn" vẫn còn |

---

## Flow 4 — Xem danh sách hóa đơn dịch vụ

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Truy cập `/admin/service-invoices` | Danh sách hóa đơn hiển thị với các cột: Mã Hóa Đơn, Phiếu Sửa, Ngày Phát Hành, Tổng Tiền, Trạng Thái Thanh Toán, Hành động |
| 2 | Kiểm tra chip trạng thái | "Chưa Thanh Toán" — chip vàng; "Đã Thanh Toán" — chip xanh |
| 3 | Kiểm tra thứ tự mặc định | Hóa đơn mới nhất (theo ngày phát hành) ở đầu danh sách |
| 4 | Kiểm tra phân trang | Có selector page size (10/20/50); điều hướng trang hoạt động nếu số hóa đơn > page size |
| 5 | Click icon **Xem chi tiết (👁)** trên 1 hàng | Chuyển đến `/admin/service-invoices/{id}` |

---

## Flow 5 — Tìm kiếm hóa đơn theo mã

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tại `/admin/service-invoices`, nhập mã hóa đơn đầy đủ vào ô "Tìm Mã Hóa Đơn" (VD: `SRV-20260522-001`) → nhấn **Enter** | Danh sách lọc, chỉ hiển thị hóa đơn khớp mã |
| 2 | Nhập một phần mã (VD: `SRV-20260522`) → nhấn **Enter** | Danh sách hiển thị tất cả hóa đơn trong ngày đó (tìm kiếm partial match) |
| 3 | Xóa nội dung ô tìm kiếm → nhấn **Enter** | Danh sách đầy đủ được khôi phục |

> **Lưu ý:** Tìm kiếm chỉ kích hoạt khi nhấn **Enter**, không tự động tìm khi gõ.

---

## Flow 6 — Lọc theo trạng thái thanh toán

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tại dropdown **"Trạng thái thanh toán"**, chọn **"Chưa Thanh Toán"** | Danh sách chỉ hiển thị hóa đơn chưa thanh toán (chip vàng) |
| 2 | Đổi sang **"Đã Thanh Toán"** | Danh sách chỉ hiển thị hóa đơn đã thanh toán (chip xanh) |
| 3 | Click **X** (Clearable) trên dropdown để xóa lọc | Danh sách đầy đủ hiển thị lại |

---

## Flow 7 — Lọc theo khoảng ngày

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Chọn **"Từ ngày"** = ngày hôm nay → **"Đến ngày"** = ngày hôm nay | Chỉ hiển thị hóa đơn phát hành hôm nay |
| 2 | Chọn "Từ ngày" = ngày trong tương lai | Danh sách trống, thông báo "Không có hóa đơn dịch vụ nào." |
| 3 | Click **X** trên cả 2 datepicker | Danh sách đầy đủ hiển thị lại |

---

## Flow 8 — Xem chi tiết hóa đơn

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Từ danh sách, click icon **Xem chi tiết** | Trang `/admin/service-invoices/{id}` mở ra |
| 2 | Kiểm tra phần header | Hiển thị: Mã hóa đơn (monospace), ngày phát hành |
| 3 | Kiểm tra bảng **"Chi Tiết Dịch Vụ"** | Các dòng từ báo giá đã được copy: Mô Tả, Số Lượng, Đơn Giá, Thành Tiền |
| 4 | Kiểm tra phần tổng tiền | Công Lao Động + Linh Kiện hiển thị riêng; **Tổng Cộng** = LaborCost + PartsTotal, chữ in đậm màu primary |
| 5 | Kiểm tra trạng thái thanh toán | Chip "Chưa Thanh Toán" (vàng) hoặc "Đã Thanh Toán" (xanh) |
| 6 | Nếu hóa đơn có ghi chú | Phần "Ghi Chú" hiển thị nội dung; nếu không có ghi chú → phần này ẩn |
| 7 | Click nút **"Quay Lại"** | Chuyển về `/admin/service-invoices` |

---

## Flow 9 — Click "In Hóa Đơn"

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tại trang chi tiết hóa đơn, click nút **"In Hóa Đơn"** | Snackbar info: "Tính năng in sẽ được thêm sau.", không có action thực sự |

---

## Edge Cases

### EC-1: Nút "Xuất Hóa Đơn" không hiển thị khi phiếu chưa Hoàn Thành

**Điều kiện:** Phiếu sửa có ResolutionType = PaidRepair nhưng Status < 9 (đang sửa, đang chờ, v.v.).

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Mở trang chi tiết phiếu chưa hoàn thành | Nút "Xuất Hóa Đơn" **không xuất hiện** trong sidebar hành động |

---

### EC-2: Nút "Xuất Hóa Đơn" không hiển thị khi phiếu không phải Sửa Có Phí

**Điều kiện:** Phiếu ở Status = 9 (Hoàn thành) nhưng ResolutionType ≠ PaidRepair (VD: Swap, RMA).

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Mở trang chi tiết phiếu đã hoàn thành kiểu khác | Nút "Xuất Hóa Đơn" **không xuất hiện** |

---

### EC-3: Nút "Xuất Hóa Đơn" không hiển thị khi phiếu đã có hóa đơn

**Điều kiện:** Phiếu đã được phát hành hóa đơn trước đó.

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Mở lại trang chi tiết phiếu đã có hóa đơn | Nút "Xuất Hóa Đơn" **không còn** trong sidebar; tab "Hóa Đơn" hiển thị thay thế |

---

### EC-4: Ghi chú vượt quá 500 ký tự

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Trong dialog, dán/nhập nội dung > 500 ký tự vào ô Ghi chú | Ô tự cắt bớt ở 500 ký tự (MaxLength="500" trên MudTextField), không thể nhập thêm |

---

### EC-5: Tìm kiếm mã hóa đơn không tồn tại

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Nhập mã không tồn tại (VD: `SRV-99991231-999`) → nhấn Enter | Bảng hiển thị trạng thái trống: "Không có hóa đơn dịch vụ nào.", không có lỗi |

---

### EC-6: Lọc ngày không có kết quả

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Chọn khoảng ngày xa trong quá khứ không có hóa đơn | Bảng hiển thị "Không có hóa đơn dịch vụ nào." |

---

### EC-7: Kết hợp nhiều bộ lọc cùng lúc

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Nhập keyword + chọn "Chưa Thanh Toán" + chọn khoảng ngày → nhấn Enter trên ô keyword | Kết quả là **giao** của tất cả bộ lọc đang áp dụng |
| Thay đổi 1 bộ lọc (VD: đổi status sang "Đã Thanh Toán") | Bảng tự reload ngay lập tức (không cần nhấn Enter), kết quả cập nhật theo |

---

### EC-8: Truy cập URL chi tiết hóa đơn không tồn tại

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Nhập thẳng URL `/admin/service-invoices/999999` lên thanh địa chỉ | Spinner tải ngắn → snackbar lỗi (tiếng Việt) → tự redirect về `/admin/service-invoices` |

---

### EC-9: Tab "Hóa Đơn" trên phiếu sửa sau khi phát hành

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Vào chi tiết phiếu đã có hóa đơn → click tab "Hóa Đơn" | Hiển thị: Mã Hóa Đơn, Ngày Phát Hành, Tổng Cộng — đúng với hóa đơn đã tạo |
| Kiểm tra tab này có link/navigate sang trang chi tiết HĐ không | Hiển thị thông tin tóm tắt (không có action navigate — chỉ đọc) |

---

## Kiểm tra UI Frontend

| Trang | Điểm cần kiểm tra |
|-------|--------------------|
| `/admin/service-invoices` | Bảng hiển thị đúng chip màu sắc (vàng/xanh); ô tìm kiếm chỉ trigger khi Enter; dropdown status filter có "Tất cả" khi clear; datepicker format `dd/MM/yyyy` |
| `/admin/service-invoices` — phân trang | Selector page size (10/20/50) hoạt động; tổng số hóa đơn hiển thị đúng |
| Dialog "Xuất Hóa Đơn" | Dropdown phương thức mặc định "Tiền Mặt"; ô ghi chú multiline (3 dòng); caption "Tối đa 500 ký tự" hiển thị; alert info hiển thị |
| `/admin/service-invoices/{id}` | Spinner loading hiển thị khi đang tải; mã hóa đơn font monospace; bảng items căn phải cho số tiền; tổng cộng màu primary in đậm; ghi chú ẩn khi rỗng |
| `/admin/service-tickets/{id}` | Nút "Xuất Hóa Đơn" chỉ hiển thị đúng điều kiện (Status=9 + PaidRepair + chưa có HĐ); tab "Hóa Đơn" chỉ xuất hiện sau khi phát hành |

---

## Lưu ý khi test

- **Phụ thuộc vào phiếu sửa:** Hóa đơn không thể tạo độc lập — phải có phiếu sửa đủ điều kiện. Nếu không có dữ liệu sẵn, cần chạy qua toàn bộ luồng phiếu sửa chữa trước.
- **Trạng thái thanh toán hiện tại:** UI chỉ hiển thị trạng thái "Chưa Thanh Toán" / "Đã Thanh Toán" — chưa có tính năng cập nhật trạng thái thanh toán từ giao diện.
- **In hóa đơn chưa hoàn thiện:** Nút "In Hóa Đơn" chỉ hiển thị snackbar thông báo, không in thực sự.
- **Mã hóa đơn tự sinh:** Format `SRV-yyyyMMdd-NNN`, tự tăng theo ngày — không thể chỉnh sửa.
