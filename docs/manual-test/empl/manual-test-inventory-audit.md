# Manual Test Guide — Kiểm kê kho hàng (UC015)

> Test thông qua Frontend: `https://localhost:7107`
> 
> Cần 2 tài khoản: **Employee** (đăng nhập, tạo & quét phiếu) và **Admin** (phê duyệt phiếu).

---

## Thiết lập trước khi test

| Bước | Hành động |
|------|----------|
| Khởi động DB | `docker-compose up -d` (từ thư mục `Infrastructure/db/`) |
| Chạy migration | `dotnet ef database update --project src/Infrastructure --startup-project src/API` |
| Chạy API | `dotnet run --project src/API/API.csproj` |
| Chạy Client | `dotnet run --project src/Client/Client.csproj` |
| Đăng nhập Employee | Truy cập Frontend, login bằng tài khoản Employee |
| Đăng nhập Admin (sau) | Dùng browser khác hoặc tab ẩn danh, login bằng tài khoản Admin |

---

## Flow 1 — Tạo phiếu toàn kho (Happy Path)

**Mục tiêu:** Tạo phiếu kiểm kê phạm vi AllStore.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Truy cập `/inventory/audit` → Click "Tạo phiếu" | Dialog tạo phiếu hiện ra |
| 2 | Chọn radio "Toàn kho" → Click "Tạo" | Phiếu mới được tạo, chuyển sang trang chi tiết phiếu |
| 3 | Kiểm tra UI | Dashboard hiển thị: "Mã phiếu" dạng `KK-yyyyMMdd-NNN`, trạng thái "Nháp", tổng số serial cần quét |

---

## Flow 2 — Tạo phiếu theo danh mục

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Truy cập `/inventory/audit` → Click "Tạo phiếu" | Dialog tạo phiếu hiện ra |
| 2 | Chọn radio "Theo danh mục" → Chọn 1 danh mục từ dropdown | Dropdown hiển thị cây danh mục phân cấp |
| 3 | Click "Tạo" | Phiếu được tạo, dashboard hiển thị "Danh mục: [tên danh mục chọn]" |

---

## Flow 3 — Quét Serial: Matched

**Điều kiện:** Phiếu ở trạng thái Nháp, tìm 1 serial hợp lệ để quét.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tại trang chi tiết phiếu, nhập serial hợp lệ vào ô tìm kiếm → Nhấn Enter | Alert xanh hiện ra với thông báo quét thành công |
| 2 | Kiểm tra dashboard | "Quét được" tăng 1, % hoàn thành tăng |
| 3 | Kiểm tra Tab "Danh sách Serial" | Serial vừa quét hiển thị trong danh sách, trạng thái "Khớp" |

---

## Flow 4 — Quét Serial: Surplus — Serial đã bán (A1)

**Điều kiện:** Serial được quét nhưng đã bị bán (ngoài scope quét).

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập serial đã bán vào ô quét → Nhấn Enter | Alert vàng hiện ra, thông báo "Serial đã bán tại..." |
| 2 | Kiểm tra dashboard | "Thừa/Ngoài scope" tăng 1 |
| 3 | Kiểm tra Tab "Danh sách Serial" | Serial hiển thị với trạng thái "Thừa" |

---

## Flow 5 — Quét Serial: Surplus — Serial đang Reserved (A2)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập serial đang reserved (giữ chỗ) vào ô quét → Nhấn Enter | Alert vàng hiện ra, thông báo "Serial đã giữ chỗ cho Đơn hàng..." |
| 2 | Kiểm tra dashboard | "Thừa/Ngoài scope" tăng 1 |

---

## Flow 6 — Quét Serial: UnknownSurplus (A3) — Serial không có trong DB

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập serial fake (không tồn tại trong DB) vào ô quét → Nhấn Enter | Dialog hiện ra yêu cầu chọn "Sản phẩm" để ghi nhận hàng thừa |
| 2 | Chọn 1 sản phẩm từ dropdown trong dialog → Click "Xác nhận" | Alert thông báo "Ghi nhận hàng thừa thành công" |
| 3 | Kiểm tra Tab "Danh sách Serial" | Serial fake hiển thị với trạng thái "Thừa" + sản phẩm vừa chọn |

---

## Flow 7 — Đánh dấu hàng lỗi (A5)

**Điều kiện:** Serial đã quét và ở trạng thái "Khớp".

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tại Tab "Danh sách Serial", tìm serial có trạng thái "Khớp" → Click icon "Đánh dấu lỗi" (🛠 hoặc tương tự) | Serial chuyển sang trạng thái "Lỗi" |
| 2 | Kiểm tra dashboard | "Quét được" giảm 1, "Hàng lỗi" tăng 1 |

---

## Flow 8 — Cập nhật lý do chênh lệch

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tại Tab "Danh sách Serial", tìm serial "Mất" hoặc "Thừa" → Click icon "Cập nhật lý do" (📝 hoặc tương tự) | Dialog/form hiện ra cho phép nhập lý do |
| 2 | Nhập lý do ("Serial bị mất khi vận chuyển nội bộ") → Click "Lưu" | Dialog đóng, thông báo cập nhật thành công |
| 3 | Kiểm tra lại row serial | Lý do được hiển thị/lưu |

---

## Flow 9 — Gửi duyệt (Submit)

**Điều kiện:** Phiếu ở trạng thái Nháp.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tại trang chi tiết phiếu, Click nút "Gửi duyệt" | Dialog xác nhận hiện ra |
| 2 | Click "Xác nhận" | Phiếu chuyển sang trạng thái "Chờ phê duyệt", panel quét ẩn |
| 3 | Thử quét thêm serial (test) | Alert lỗi hiện ra, không cho quét nữa |
| 4 | Kiểm tra dashboard | Cập nhật "Mất" = số serial chưa quét, dashboard chuyển sang chế độ xem (không quét) |

---

## Flow 10 — Phê duyệt (Approve) — Happy Path

**Điều kiện:** Phiếu ở trạng thái "Chờ phê duyệt", dùng tài khoản Admin.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Đăng nhập bằng tài khoản Admin | Truy cập `/admin/inventory-audit` hoặc danh sách phiếu chờ duyệt |
| 2 | Tìm phiếu vừa submit → Click vào phiếu hoặc nút "Chi tiết" | Trang chi tiết phiếu hiện ra, hiển thị Tab duyệt với nút "Phê duyệt" |
| 3 | Click nút "Phê duyệt" | Dialog xác nhận hiện ra (cảnh báo: hàng mất sẽ đổi status), click "Xác nhận" |
| 4 | Kiểm tra kết quả | Phiếu chuyển sang trạng thái "Hoàn thành", hiển thị tên người phê duyệt và thời gian |
| 5 | Quay lại trang listing → refresh | Phiếu cập nhật status thành "Hoàn thành" trong danh sách |

---

## Flow 11 — Từ chối → Trả về Nháp (A6a)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tạo & submit phiếu (không quét bất kỳ serial nào) | Phiếu ở trạng thái "Chờ phê duyệt" |
| 2 | Đăng nhập Admin → Tại trang chi tiết phiếu, Click "Từ chối" | Dialog từ chối hiện ra với 2 option: "Trả về Nháp" / "Hủy phiếu" |
| 3 | Chọn "Trả về Nháp" → Nhập lý do từ chối → Click "Xác nhận" | Phiếu quay về trạng thái "Nháp", panel quét hiện lại |
| 4 | Kiểm tra UI | Tất cả serial chưa quét quay về "Chờ quét" (Pending), giao diện quét hoạt động bình thường |
| 5 | Thử quét lại serial | Quét bình thường như phiếu mới |

---

## Flow 12 — Từ chối → Hủy phiếu (A6b)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tạo & submit phiếu | Phiếu ở trạng thái "Chờ phê duyệt" |
| 2 | Đăng nhập Admin → Click "Từ chối" | Dialog hiện ra với 2 option |
| 3 | Chọn "Hủy phiếu" → Nhập lý do ("Sai phạm vi") → Click "Xác nhận" | Phiếu chuyển sang trạng thái "Đã hủy" |
| 4 | Kiểm tra UI | Danh sách serial vẫn hiển thị (để audit), không có action button nào |

---

## Flow 13 — Hủy phiếu nháp

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tạo phiếu nhưng chưa submit → Click nút "Hủy phiếu" | Dialog xác nhận hiện ra |
| 2 | Click "Xác nhận hủy" | Phiếu chuyển sang trạng thái "Đã hủy", quay về danh sách |
| 3 | Thử hủy phiếu của người khác (Employee B hủy phiếu của Employee A) | Alert lỗi: "Bạn không có quyền hủy phiếu này" |

---

## Flow 14 — Business Continuity (BR1): Serial được bán trong cửa sổ kiểm kê

**Kịch bản:** Serial nằm trong snapshot nhưng chưa quét, khi chờ Admin duyệt thì serial đó được bán (Status đổi thành Sold).

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tạo phiếu, submit (không quét bất kỳ serial nào) | Phiếu ở "Chờ phê duyệt" |
| 2 | Trong lúc chờ duyệt, một serial trong phiếu được bán ngoài (mô phỏng: đơn đặt hàng khác quét serial & thanh toán) | — |
| 3 | Đăng nhập Admin → Click "Phê duyệt" phiếu | Phiếu vẫn được duyệt thành công |
| 4 | Kiểm tra trang chi tiết phiếu → Tab "Danh sách Serial" | Serial vừa bị bán hiển thị với ghi chú "Đã được bán khi đang chờ duyệt" (hoặc tương tự) |
| 5 | Kiểm tra trang danh sách phiếu | Phiếu chuyển sang "Hoàn thành", không có lỗi |

---

## Edge Cases

### EC-1: Quét trùng serial trong cùng phiếu

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Quét serial S lần 1 | Serial chuyển sang "Khớp" |
| Quét lại serial S lần 2 | Alert lỗi: "Serial đã được quét trong phiếu này" |

---

### EC-2: Quét serial ngoài phạm vi Category

**Điều kiện:** Phiếu tạo theo danh mục X, quét serial thuộc danh mục Y.

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Quét serial ngoài danh mục scope | Alert vàng: "Serial ngoài phạm vi kiểm kê" |
| Kiểm tra Tab "Danh sách Serial" | Serial hiển thị với trạng thái "Ngoài scope" |

---

### EC-3: Submit phiếu khi chưa quét bất kỳ serial nào

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Tạo phiếu → ngay lập tức Click "Gửi duyệt" | Phiếu được submit, dashboard hiển thị "Mất = tổng số serial" |

---

### EC-4: Approve phiếu đã Completed

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Đăng nhập Admin → Trên phiếu Status "Hoàn thành", cố gọi lại "Phê duyệt" | Alert lỗi tiếng Việt, phiếu không đổi trạng thái |

---

### EC-5: Approve phiếu ở trạng thái Nháp

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Phiếu chưa submit, cố tìm cách phê duyệt | Không thấy nút "Phê duyệt" (nút chỉ hiện khi "Chờ phê duyệt") hoặc alert lỗi |

---

### EC-6: Employee truy cập admin feature

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Employee truy cập URL `/admin/inventory-audit` | Redirect hoặc hiển thị "Không có quyền truy cập" |
| Employee cố click nút "Phê duyệt" (nếu có) | Nút disabled hoặc alert "Chỉ Admin" |

---

### EC-7: Employee hủy phiếu của người khác

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Employee B tạo/xem phiếu của Employee A → Click "Hủy phiếu" | Alert lỗi: "Bạn không có quyền hủy phiếu này" |

---

### EC-8: Employee hủy phiếu ở trạng thái Chờ phê duyệt

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Phiếu của Employee, status "Chờ phê duyệt" → Click "Hủy phiếu" | Alert lỗi: "Chỉ Admin hoặc tác giả ở trạng thái Nháp mới hủy được" (hoặc tương tự) |

---

### EC-9: Tạo phiếu theo danh mục nhưng không chọn danh mục

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Dialog tạo phiếu → Chọn "Theo danh mục" nhưng bỏ trống dropdown → Click "Tạo" | Alert validation: "Vui lòng chọn danh mục" |

---

### EC-10: Đánh dấu lỗi trên serial không phải Matched

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Tìm serial có trạng thái "Mất" → Cố click icon "Đánh dấu lỗi" | Icon disabled (không click được) hoặc alert lỗi "Chỉ serial Khớp mới có thể đánh dấu lỗi" |

---

### EC-11: Submit phiếu không phải người tạo

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Employee B truy cập phiếu của Employee A → Click "Gửi duyệt" | Alert lỗi: "Chỉ tác giả phiếu mới được gửi duyệt" |

---

### EC-12: Mã phiếu không trùng lặp

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Tạo 2 phiếu liên tiếp trong cùng ngày | Mã phiếu tự tăng: `KK-20260522-001`, `KK-20260522-002`, v.v., không trùng |

---

### EC-13: Tạo phiếu danh mục không có serial Available

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Chọn danh mục không có sản phẩm/serial có sẵn → Tạo phiếu | Phiếu được tạo nhưng dashboard hiển thị "Tổng = 0", Tab "Danh sách Serial" trống |

---

## Kiểm tra UI Frontend

| Trang | Điểm cần kiểm tra |
|-------|--------------------|
| `/inventory/audit` | Bảng hiển thị đúng status chip màu sắc; nút "Tạo phiếu" mở dialog; Admin thấy icon Phê duyệt trên hàng Status=1 |
| Dialog tạo phiếu | Radio AllStore/Category hoạt động; khi chọn Category, dropdown danh mục hiện ra có phân cấp (em gạch đầu dòng); validation khi bấm Tạo mà chưa chọn danh mục |
| `/inventory/audit/{id}` (Draft) | Panel quét hiển thị; nhấn Enter sau khi nhập serial → quét; dashboard counts cập nhật sau mỗi lần quét; Alert màu xanh/vàng/đỏ hiện đúng |
| `/inventory/audit/{id}` (Draft) | Tab "Danh sách Serial": icon đánh dấu lỗi chỉ hiện trên row Matched; icon cập nhật lý do chỉ hiện trên Missing/Surplus |
| `/inventory/audit/{id}` (AwaitingApproval) | Panel quét ẩn; hiện Alert "Đang chờ Admin duyệt"; Admin thấy nút "Đi đến trang phê duyệt" |
| `/admin/inventory-audit/{id}` | Nút Phê duyệt và Từ chối hiển thị khi Status=1; warning note trước khi approve; dialog từ chối có radio ReturnToDraft/Cancel |
| `/inventory/audit/{id}` (Completed) | Không có action button nào; hiện thông tin người phê duyệt |

---

## Lưu ý khi test

- **Browser khác cho Admin:** Dùng incognito/private window hoặc browser khác để đăng nhập Admin, tránh auto-logout Employee
- **Làm mới trang:** Khi chuyển flow hoặc đổi tài khoản, nhấn F5 để sync UI với dữ liệu từ API
- **Serial test:** Sử dụng các serial có sẵn trong DB (xem tại product listing nếu không biết mã serial)
