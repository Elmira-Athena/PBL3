# Manual Test: Admin — Mã Giảm Giá & Thống Kê

Tài liệu này liệt kê tất cả các flow cần kiểm thử thủ công trên UI cho 2 module: Quản lý Mã Giảm Giá và Trang Thống Kê.

**Yêu cầu trước khi test:**
- Đăng nhập tài khoản có role `Admin`
- Ứng dụng đang chạy (API + Client)

**Ký hiệu kết quả:** ✅ Pass &nbsp;|&nbsp; ❌ Fail &nbsp;|&nbsp; ⬜ Chưa test

---

## 1. Quản lý Mã Giảm Giá (`/admin/vouchers`)

### 1.1 Happy Path — Danh sách & Tìm kiếm

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-VOU-01 | Tải danh sách mặc định | 1. Vào `/admin/vouchers` | Bảng tải đúng, hiển thị trang 1, cột đủ: Mã, Tên, Loại & Giá trị, Hiệu lực, Lượt dùng, Trạng thái, Thao tác | ⬜ |
| TC-VOU-02 | Tìm kiếm theo mã voucher | 1. Nhập mã voucher (VD: `SUMMER`) vào ô tìm kiếm<br>2. Chờ debounce ~400ms | Bảng chỉ hiển thị voucher có mã chứa từ khóa; xóa ô tìm kiếm → danh sách gốc phục hồi | ⬜ |
| TC-VOU-03 | Tìm kiếm theo tên voucher | 1. Nhập tên voucher (VD: `Mùa hè`) vào ô tìm kiếm | Bảng hiển thị đúng voucher có tên chứa từ khóa | ⬜ |
| TC-VOU-04 | Tìm kiếm không có kết quả | 1. Nhập từ khóa không khớp voucher nào (VD: `XYZNOTEXIST`) | Bảng hiện empty state với thông báo phù hợp | ⬜ |
| TC-VOU-05 | Filter trạng thái — Đang hoạt động | 1. Chọn **Đang hoạt động** trong dropdown Trạng thái | Chỉ hiện voucher có chip `Đang hoạt động` (xanh); chip `Tạm dừng`, `Hết hạn`, `Sắp diễn ra` không xuất hiện | ⬜ |
| TC-VOU-06 | Filter trạng thái — Tạm dừng | 1. Chọn **Tạm dừng** trong dropdown Trạng thái | Chỉ hiện voucher có `IsActive = false`; chip `Tạm dừng` màu xám | ⬜ |
| TC-VOU-07 | Xóa filter trạng thái | 1. Sau khi chọn filter, nhấn nút X (Clear) trên dropdown | Danh sách trở về hiển thị tất cả voucher | ⬜ |
| TC-VOU-08 | Filter theo khoảng ngày — có kết quả | 1. Chọn From Date = ngày trong quá khứ<br>2. Chọn To Date = hôm nay | Bảng chỉ hiện voucher có ngày tạo (hoặc ngày hiệu lực) trong khoảng đã chọn | ⬜ |
| TC-VOU-09 | Filter theo khoảng ngày — không có kết quả | 1. Chọn khoảng ngày không có voucher nào được tạo | Bảng hiện empty state | ⬜ |
| TC-VOU-10 | Kết hợp tìm kiếm + filter trạng thái | 1. Nhập từ khóa `SALE`<br>2. Chọn trạng thái **Đang hoạt động** | Kết quả chỉ hiện voucher có mã/tên chứa `SALE` VÀ đang hoạt động | ⬜ |
| TC-VOU-11 | Phân trang | 1. Chọn 25 dòng/trang<br>2. Chuyển sang trang 2 | Bảng tải đúng 25 dòng; trang 2 hiển thị đúng offset | ⬜ |
| TC-VOU-12 | Nút Refresh | 1. Nhấn icon **Refresh** trên thanh filter | Dữ liệu reload; bảng hiển thị lại trạng thái mới nhất | ⬜ |
| TC-VOU-13 | Copy mã voucher | 1. Nhấn icon **Copy** bên cạnh mã voucher trong bảng | Mã được copy vào clipboard; có feedback (tooltip/snackbar) xác nhận | ⬜ |
| TC-VOU-14 | Chip hiển thị trạng thái đúng — Sắp diễn ra | 1. Xem voucher có `StartDate` ở tương lai | Chip hiển thị `Sắp diễn ra` màu xanh dương | ⬜ |
| TC-VOU-15 | Chip hiển thị trạng thái đúng — Hết hạn | 1. Xem voucher có `EndDate` đã qua | Chip hiển thị `Hết hạn` màu đỏ | ⬜ |
| TC-VOU-16 | Hiển thị số lượng không giới hạn | 1. Xem voucher có `Quantity = null` trong cột Lượt dùng | Hiển thị `X / ∞` thay vì số cụ thể | ⬜ |

---

### 1.2 Happy Path — Tạo Voucher Giảm %

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-VOU-17 | Tạo voucher giảm % đầy đủ thông tin | 1. Nhấn **Thêm Voucher mới**<br>2. Mã: `SUMMER25`<br>3. Tên: `Mùa hè 25%`<br>4. Loại: **Giảm %**<br>5. Giá trị: `25`<br>6. Giảm tối đa: `200000`<br>7. Đơn tối thiểu: `500000`<br>8. Ngày bắt đầu: hôm nay<br>9. Ngày kết thúc: 30 ngày sau<br>10. Số lượng: `100`<br>11. Nhấn **Tạo mới** | Điều hướng về `/admin/vouchers`; snackbar thành công; voucher mới xuất hiện trong bảng với đúng loại `Giảm %` | ⬜ |
| TC-VOU-18 | Tạo voucher giảm % — không giới hạn số lượng | 1. Tạo voucher giảm % với tất cả trường hợp lệ<br>2. Để trống trường **Số lượng phát hành** | Tạo thành công; cột Lượt dùng hiển thị `0 / ∞` | ⬜ |
| TC-VOU-19 | Field "Giảm tối đa" chỉ hiện với loại % | 1. Chọn loại **Giảm %** → kiểm tra field Giảm tối đa<br>2. Chuyển sang **Giảm tiền** → kiểm tra lại | Field `Giảm tối đa` hiện khi chọn **Giảm %**; biến mất hoàn toàn khi chuyển sang **Giảm tiền** | ⬜ |
| TC-VOU-20 | Preview panel cập nhật realtime | 1. Nhập mã, giá trị, ngày, kênh áp dụng<br>2. Quan sát panel bên phải | Panel preview cập nhật ngay khi thay đổi từng trường: hiển thị label giảm giá lớn, mã voucher, ngày hiệu lực, kênh | ⬜ |
| TC-VOU-21 | Kênh áp dụng — chỉ Online | 1. Bỏ chọn **Tại quầy**, giữ **Online** | Preview hiển thị `Online`; `ApplyFor = 1` được lưu | ⬜ |
| TC-VOU-22 | Kênh áp dụng — chỉ Tại quầy | 1. Bỏ chọn **Online**, giữ **Tại quầy** | Preview hiển thị `Tại quầy`; `ApplyFor = 2` được lưu | ⬜ |
| TC-VOU-23 | Kênh áp dụng — cả hai | 1. Giữ cả **Online** và **Tại quầy** | Preview hiển thị `Online + Tại quầy`; `ApplyFor = 0` | ⬜ |
| TC-VOU-24 | Giới hạn dùng / khách | 1. Nhập `MaxUsesPerUser = 2` | Voucher được tạo với giới hạn 2 lần/khách | ⬜ |
| TC-VOU-25 | Phạm vi — chọn danh mục cụ thể | 1. Mở dropdown **Phạm vi áp dụng**<br>2. Chọn 2 danh mục<br>3. Tạo | Summary panel hiển thị "2 danh mục"; voucher chỉ áp dụng cho sản phẩm thuộc 2 danh mục đó | ⬜ |
| TC-VOU-26 | Phạm vi — toàn bộ sản phẩm (để trống) | 1. Không chọn gì ở dropdown **Phạm vi** | Summary hiển thị `Toàn bộ sản phẩm` | ⬜ |
| TC-VOU-27 | Cho phép cộng dồn | 1. Bật toggle **Cho phép dùng kèm khuyến mãi khác** | `IsStackable = true` được lưu | ⬜ |
| TC-VOU-28 | Thêm ghi chú | 1. Nhập nội dung vào field **Ghi chú** (tối đa 500 ký tự) | Counter hiển thị đúng số ký tự hiện tại; ghi chú được lưu | ⬜ |
| TC-VOU-29 | Tạo voucher ở trạng thái Tạm dừng | 1. Chọn **Tạm dừng** ở dropdown Trạng thái trước khi tạo | Voucher được tạo với chip `Tạm dừng` ngay trong danh sách | ⬜ |

---

### 1.3 Happy Path — Tạo Voucher Giảm Tiền

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-VOU-30 | Tạo voucher giảm tiền cố định | 1. Nhấn **Thêm Voucher mới**<br>2. Mã: `FIXED50K`<br>3. Loại: **Giảm tiền**<br>4. Giá trị: `50000`<br>5. Đơn tối thiểu: `200000`<br>6. Điền đủ ngày hợp lệ<br>7. Nhấn **Tạo mới** | Tạo thành công; cột loại hiển thị `Giảm tiền` với đơn vị `đ` | ⬜ |
| TC-VOU-31 | Unit thay đổi theo loại giảm | 1. Chọn loại **Giảm %** → xem đơn vị field Giá trị<br>2. Chuyển sang **Giảm tiền** → xem lại | Đơn vị hiển thị `%` khi giảm %; hiển thị `đ` khi giảm tiền | ⬜ |

---

### 1.4 Happy Path — Sửa Voucher

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-VOU-32 | Sửa tên và mô tả | 1. Nhấn icon **Edit** trên một voucher<br>2. Đổi tên sang `Khuyến mãi tháng 6`<br>3. Thêm ghi chú<br>4. Nhấn **Cập nhật** | Snackbar thành công; bảng reload với tên mới | ⬜ |
| TC-VOU-33 | Mã voucher là read-only khi sửa | 1. Mở trang edit bất kỳ voucher | Field **Mã voucher** hiển thị nhưng bị disable/read-only, không thể chỉnh sửa | ⬜ |
| TC-VOU-34 | Sửa giá trị giảm giá | 1. Mở edit voucher giảm %<br>2. Đổi Giá trị từ `25` sang `30`<br>3. Đổi Giảm tối đa từ `200000` sang `300000`<br>4. Nhấn **Cập nhật** | Lưu thành công; bảng hiện giá trị mới | ⬜ |
| TC-VOU-35 | Sửa ngày kết thúc — gia hạn | 1. Mở edit voucher sắp hết hạn<br>2. Đổi ngày kết thúc sang 3 tháng sau<br>3. Nhấn **Cập nhật** | Chip trạng thái trong bảng cập nhật đúng (từ `Hết hạn` sang `Đang hoạt động` nếu còn trong thời gian) | ⬜ |
| TC-VOU-36 | Hủy trang sửa bằng nút Back | 1. Mở trang edit<br>2. Thay đổi một số trường<br>3. Nhấn nút **Quay lại** | Điều hướng về `/admin/vouchers`; không có thay đổi nào được lưu | ⬜ |

---

### 1.5 Happy Path — Toggle Trạng Thái & Xóa

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-VOU-37 | Tạm dừng voucher đang hoạt động | 1. Nhấn icon **Pause** trên voucher `Đang hoạt động` | Snackbar xác nhận; chip đổi sang `Tạm dừng` (xám); icon đổi sang **Play** | ⬜ |
| TC-VOU-38 | Kích hoạt lại voucher đang tạm dừng | 1. Nhấn icon **Play** trên voucher `Tạm dừng` | Snackbar xác nhận; chip đổi sang `Đang hoạt động` (xanh) hoặc `Sắp diễn ra`/`Hết hạn` nếu ngoài thời hạn | ⬜ |
| TC-VOU-39 | Xóa voucher — xác nhận | 1. Nhấn icon **Xóa** trên một voucher<br>2. Dialog xác nhận hiện ra<br>3. Nhấn **Xác nhận xóa** | Dialog đóng; snackbar `"Xóa thành công"`; voucher biến khỏi bảng | ⬜ |
| TC-VOU-40 | Xóa voucher — hủy | 1. Nhấn icon **Xóa** trên một voucher<br>2. Nhấn **Hủy** trong dialog | Dialog đóng; voucher vẫn còn trong bảng | ⬜ |

---

### 1.6 Edge Cases — Validation khi Tạo/Sửa

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-VOU-41 | Mã chứa ký tự không hợp lệ (chữ thường) | 1. Nhập mã: `summer25` (chữ thường) | Validation lỗi: chỉ chấp nhận IN HOA + số + `-_`; field viền đỏ | ⬜ |
| TC-VOU-42 | Mã chứa khoảng trắng | 1. Nhập mã: `SUMMER 25` | Validation lỗi ngay lập tức | ⬜ |
| TC-VOU-43 | Mã chứa ký tự đặc biệt (ngoài `-_`) | 1. Nhập mã: `SUMMER@25` | Validation lỗi | ⬜ |
| TC-VOU-44 | Mã vượt 50 ký tự | 1. Nhập mã có 51 ký tự IN HOA | Field chặn input sau ký tự thứ 50 hoặc hiện lỗi | ⬜ |
| TC-VOU-45 | Tên voucher để trống | 1. Để trống field **Tên voucher**<br>2. Nhấn **Tạo mới** | Validation lỗi: "Tên voucher là bắt buộc" hoặc tương đương; form không submit | ⬜ |
| TC-VOU-46 | Tên voucher vượt 200 ký tự | 1. Nhập tên có 201 ký tự | Counter hiển thị đỏ; form không submit | ⬜ |
| TC-VOU-47 | Giá trị giảm = 0 | 1. Nhập `DiscountValue = 0` | Validation lỗi: phải > 0 | ⬜ |
| TC-VOU-48 | Giá trị giảm % vượt 100 | 1. Chọn loại **Giảm %**<br>2. Nhập giá trị `101` | Validation lỗi: giảm % không được vượt 100 | ⬜ |
| TC-VOU-49 | Giảm tối đa = 0 (khi có nhập) | 1. Chọn loại **Giảm %**<br>2. Nhập Giảm tối đa = `0` | Validation lỗi: phải > 0 khi có nhập | ⬜ |
| TC-VOU-50 | Đơn tối thiểu âm | 1. Nhập đơn tối thiểu = `-1` | Validation lỗi: phải >= 0 | ⬜ |
| TC-VOU-51 | Ngày kết thúc trước ngày bắt đầu | 1. Đặt ngày bắt đầu: hôm nay<br>2. Đặt ngày kết thúc: ngày hôm qua | Validation lỗi: ngày kết thúc phải sau ngày bắt đầu | ⬜ |
| TC-VOU-52 | Ngày bắt đầu = ngày kết thúc | 1. Đặt Start Date = End Date (cùng một ngày, giờ bắt đầu < giờ kết thúc) | Tuỳ logic backend — ghi nhận kết quả thực tế | ⬜ |
| TC-VOU-53 | Số lượng phát hành = 0 | 1. Nhập `Quantity = 0` | Validation lỗi: số lượng phải >= 1 khi có nhập | ⬜ |
| TC-VOU-54 | Giới hạn / khách = 0 | 1. Nhập `MaxUsesPerUser = 0` | Validation lỗi: phải >= 1 khi có nhập | ⬜ |
| TC-VOU-55 | Bỏ chọn cả hai kênh áp dụng | 1. Bỏ chọn cả **Online** lẫn **Tại quầy** | Form không submit hoặc ít nhất 1 kênh phải được chọn (ghi nhận behavior thực tế) | ⬜ |
| TC-VOU-56 | Submit form hoàn toàn trống | 1. Vào trang tạo voucher<br>2. Nhấn **Tạo mới** ngay mà không điền gì | Tất cả field bắt buộc báo lỗi; form không submit | ⬜ |
| TC-VOU-57 | Mã voucher trùng với voucher đã tồn tại | 1. Tạo voucher với mã đã có trong hệ thống | API trả về lỗi; snackbar hiện thông báo lỗi (VD: "Mã voucher đã tồn tại") | ⬜ |
| TC-VOU-58 | Ghi chú vượt 500 ký tự | 1. Nhập 501 ký tự vào field **Ghi chú** | Counter đỏ; form không submit | ⬜ |

---

### 1.7 Edge Cases — Hành vi tại Danh sách

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-VOU-59 | Toggle voucher Hết hạn sang Hoạt động | 1. Kích hoạt lại (`Play`) một voucher có `EndDate` đã qua | `IsActive = true` nhưng chip vẫn là `Hết hạn` vì ngày đã hết; không bị ẩn chip | ⬜ |
| TC-VOU-60 | Toggle voucher Sắp diễn ra | 1. Tạm dừng (`Pause`) voucher có `StartDate` ở tương lai | Chip đổi sang `Tạm dừng` bất kể trạng thái thời gian | ⬜ |
| TC-VOU-61 | Xóa voucher đã được dùng (UsedCount > 0) | 1. Nhấn xóa voucher có `UsedCount > 0`<br>2. Xác nhận | Ghi nhận: API có cho phép hay trả lỗi — ghi lại kết quả thực tế | ⬜ |
| TC-VOU-62 | Danh sách khi không có voucher nào | 1. Filter tới trạng thái không có voucher | Empty state hiển thị với thông báo thay vì bảng trắng | ⬜ |

---

## 2. Thống Kê (`/admin/reports`)

### 2.1 Happy Path — Tải Trang & Bộ Lọc Mặc Định

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-01 | Tải trang thống kê lần đầu | 1. Vào `/admin/reports` | Trang tải với khoảng ngày mặc định = đầu tháng hiện tại → hôm nay; 6 KPI card, 4 khu vực biểu đồ đều hiển thị skeleton/spinner khi loading, sau đó hiện dữ liệu | ⬜ |
| TC-RPT-02 | Skeleton loading hiển thị trong khi chờ | 1. Quan sát ngay khi vào trang | Các card và chart hiển thị skeleton placeholder trước khi dữ liệu tải xong; không có màn hình trắng | ⬜ |
| TC-RPT-03 | Tất cả 6 KPI card có giá trị | 1. Sau khi trang tải xong với dữ liệu mặc định | Cả 6 card đều hiện giá trị: Doanh thu, Lợi nhuận gộp, Tổng đơn hàng, Đơn đã hủy, Giá trị TB đơn, Khách hàng mới | ⬜ |

---

### 2.2 Happy Path — Bộ Lọc Khoảng Thời Gian

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-04 | Preset "Hôm nay" | 1. Nhấn nút **Hôm nay** | Button được highlight; khoảng ngày cập nhật = hôm nay → hôm nay; tất cả biểu đồ và KPI tải lại với dữ liệu trong ngày | ⬜ |
| TC-RPT-05 | Preset "Tuần này" | 1. Nhấn nút **Tuần này** | Khoảng ngày = Thứ Hai tuần hiện tại → hôm nay; dữ liệu tải lại | ⬜ |
| TC-RPT-06 | Preset "Tháng này" | 1. Nhấn nút **Tháng này** | Khoảng ngày = ngày 1 tháng hiện tại → hôm nay; dữ liệu tải lại | ⬜ |
| TC-RPT-07 | Preset "Năm nay" | 1. Nhấn nút **Năm nay** | Khoảng ngày = 01/01/năm hiện tại → hôm nay; dữ liệu tải lại | ⬜ |
| TC-RPT-08 | Khoảng ngày tùy chỉnh | 1. Nhấn vào **Date Range Picker**<br>2. Chọn ngày bắt đầu = 01/01/2025<br>3. Chọn ngày kết thúc = 31/03/2025<br>4. Xác nhận | Khoảng ngày cập nhật; không có preset nào được highlight; dữ liệu tải lại theo khoảng đã chọn | ⬜ |
| TC-RPT-09 | Chuyển đổi giữa các preset — dữ liệu cập nhật đúng | 1. Nhấn **Hôm nay** → ghi nhận doanh thu<br>2. Nhấn **Tháng này** → ghi nhận doanh thu | Giá trị doanh thu thay đổi tương ứng (tháng có giá trị >= ngày) | ⬜ |
| TC-RPT-10 | Loading indicator hiện khi đổi bộ lọc | 1. Nhấn bất kỳ preset | Spinner/circular progress hiển thị trên chip hiển thị khoảng ngày trong khi dữ liệu đang tải | ⬜ |

---

### 2.3 Happy Path — KPI Cards

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-11 | Doanh thu hiển thị định dạng rút gọn | 1. Xem card **Doanh thu** khi doanh thu >= 1 triệu | Giá trị hiển thị dạng `X.XTr` (VD: `5.2Tr`) thay vì con số đầy đủ | ⬜ |
| TC-RPT-12 | Lợi nhuận gộp — màu dương khi có lãi | 1. Xem card **Lợi nhuận gộp** trong kỳ có lợi nhuận dương | Giá trị màu xanh lá | ⬜ |
| TC-RPT-13 | Lợi nhuận gộp — màu âm khi thua lỗ | 1. Xem card **Lợi nhuận gộp** trong kỳ lợi nhuận âm (nếu có) | Giá trị màu đỏ | ⬜ |
| TC-RPT-14 | Đơn đã hủy hiển thị cả số lẫn tỷ lệ % | 1. Xem card **Đơn đã hủy** | Hiển thị dạng `X (Y%)` — VD: `5 (10%)` | ⬜ |
| TC-RPT-15 | Khách hàng mới — đơn vị "người" | 1. Xem card **Khách hàng mới** | Giá trị có đơn vị `người` kèm theo | ⬜ |
| TC-RPT-16 | Tổng đơn hàng — đơn vị "đơn" | 1. Xem card **Tổng đơn hàng** | Giá trị có đơn vị `đơn` kèm theo | ⬜ |

---

### 2.4 Happy Path — Biểu Đồ Xu Hướng Doanh Thu

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-17 | Biểu đồ line hiển thị 2 series | 1. Xem khu vực **Doanh thu & Lợi nhuận** | Biểu đồ đường có 2 đường: `Doanh thu (₫)` và `Lợi nhuận (₫)`; legend/chú thích đúng | ⬜ |
| TC-RPT-18 | Nhãn trục X thay đổi theo khoảng ngày | 1. Xem biểu đồ khi chọn **Hôm nay**<br>2. Xem khi chọn **Năm nay** | Trục X hiện nhãn phù hợp (giờ cho ngày, ngày cho tháng, tháng cho năm) | ⬜ |

---

### 2.5 Happy Path — Biểu Đồ Kênh Bán & Thanh Toán

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-19 | Biểu đồ donut kênh bán | 1. Xem khu vực **Kênh bán** | Biểu đồ donut hiển thị phần Online và POS với label `Online (X)` và `POS (Y)` | ⬜ |
| TC-RPT-20 | Biểu đồ donut thanh toán | 1. Xem khu vực **Phương thức thanh toán** | Biểu đồ donut hiển thị COD, Banking, VNPay với số lượng đơn tương ứng | ⬜ |
| TC-RPT-21 | Chỉ có đơn Online — donut kênh | 1. Lọc khoảng ngày chỉ có đơn online | Donut hiển thị 100% Online; không crash hay hiển thị lỗi | ⬜ |

---

### 2.6 Happy Path — Top Sản Phẩm & Danh Mục

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-22 | Biểu đồ Top 10 sản phẩm | 1. Xem khu vực **Top sản phẩm bán chạy** | Biểu đồ cột ngang hiển thị tối đa 10 sản phẩm; tên sản phẩm bị cắt ở 15 ký tự nếu dài hơn | ⬜ |
| TC-RPT-23 | Bảng Top 5 danh mục | 1. Xem khu vực **Top danh mục** | Bảng hiển thị tối đa 5 danh mục với: Rank chip, Tên, Số đơn, SL bán, Doanh thu — sắp xếp theo doanh thu giảm dần | ⬜ |
| TC-RPT-24 | Rank chip hiển thị số thứ tự | 1. Xem cột **#** trong bảng Top danh mục | Chip hiển thị số 1, 2, 3, 4, 5 lần lượt | ⬜ |

---

### 2.7 Happy Path — Tổng Quan Tồn Kho

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-25 | Tồn kho 6 chỉ số hiển thị đúng | 1. Xem khu vực **Tổng quan tồn kho** | Hiển thị đầy đủ 6 ô: Tổng SKU, Có sẵn, Đang giữ/chờ giao, Đã bán, Hàng lỗi/hỏng, SKU sắp hết hàng | ⬜ |
| TC-RPT-26 | SKU sắp hết hàng — ngưỡng ≤ 3 | 1. Xem ô **SKU sắp hết hàng** | Số hiển thị khớp với số SKU có tồn kho ≤ 3 | ⬜ |
| TC-RPT-27 | Tồn kho không thay đổi khi đổi khoảng ngày | 1. Đổi preset từ **Tháng này** sang **Hôm nay** | Các số liệu tồn kho KHÔNG thay đổi — đây là snapshot hiện tại, không phụ thuộc date range | ⬜ |

---

### 2.8 Edge Cases — Khoảng Ngày Đặc Biệt

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-28 | Ngày không có dữ liệu (Hôm nay — chưa có đơn) | 1. Nhấn preset **Hôm nay** vào lúc chưa có đơn nào hôm nay | KPI card hiển thị `0`; biểu đồ hiện empty state với thông báo `"Không có dữ liệu trong khoảng thời gian này"` thay vì lỗi | ⬜ |
| TC-RPT-29 | Khoảng ngày rất rộng (cả năm) | 1. Chọn custom range từ 01/01/2020 → hôm nay | Trang không bị timeout hay lỗi; biểu đồ tải được dù dữ liệu lớn | ⬜ |
| TC-RPT-30 | Date picker — không chọn được ngày tương lai | 1. Mở **Date Range Picker**<br>2. Cố click vào ngày mai hay ngày sau | Ngày sau hôm nay bị disabled trong calendar; không thể chọn | ⬜ |
| TC-RPT-31 | Custom range — ngày bắt đầu = ngày kết thúc | 1. Chọn cùng một ngày cho cả start và end | Trang tải dữ liệu cho 1 ngày đó mà không báo lỗi | ⬜ |
| TC-RPT-32 | Chuyển nhanh nhiều preset liên tiếp | 1. Nhấn **Hôm nay** → ngay lập tức nhấn **Tuần này** → nhấn **Tháng này** | Không bị lỗi race condition; dữ liệu cuối cùng khớp với preset cuối chọn | ⬜ |

---

### 2.9 Edge Cases — Hiển Thị Biểu Đồ

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-33 | Biểu đồ line — chỉ 1 điểm dữ liệu (Hôm nay) | 1. Nhấn preset **Hôm nay** khi có đơn hàng | Biểu đồ vẫn render được dù chỉ 1 điểm; không bị crash hay white screen | ⬜ |
| TC-RPT-34 | Biểu đồ donut — chỉ 1 kênh có đơn | 1. Lọc khoảng ngày chỉ có đơn từ 1 kênh (VD: chỉ POS) | Donut hiển thị 1 phần 100%; không hiển thị phần 0% dưới dạng lỗi | ⬜ |
| TC-RPT-35 | Biểu đồ Top sản phẩm — ít hơn 10 sản phẩm | 1. Lọc khoảng ngày chỉ có < 10 sản phẩm bán được | Biểu đồ hiển thị đúng số sản phẩm có, không padding thêm cột trống | ⬜ |
| TC-RPT-36 | Top danh mục — không có dữ liệu | 1. Lọc khoảng ngày không có đơn hàng | Bảng Top danh mục hiển thị `"Không có dữ liệu trong khoảng thời gian này"` | ⬜ |
| TC-RPT-37 | Định dạng tiền tệ — dưới 1000đ | 1. Xem card doanh thu khi doanh thu tổng < 1000đ | Hiển thị số đầy đủ, không viết tắt thành `K` | ⬜ |
| TC-RPT-38 | Định dạng tiền tệ — trên 1 tỷ | 1. Xem card doanh thu khi doanh thu tổng >= 1 tỷ | Hiển thị dạng `X.XT` (VD: `1.5T`) | ⬜ |

---

### 2.10 Edge Cases — Quyền Truy Cập

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-RPT-39 | Truy cập khi không phải Admin | 1. Đăng xuất<br>2. Đăng nhập tài khoản role **Employee** hoặc **Customer**<br>3. Truy cập `/admin/reports` trực tiếp qua URL | Bị redirect về trang đăng nhập hoặc trang 403 Forbidden | ⬜ |
| TC-RPT-40 | Truy cập khi chưa đăng nhập | 1. Không đăng nhập<br>2. Truy cập `/admin/reports` | Bị redirect về trang đăng nhập | ⬜ |
| TC-VOU-63 | Truy cập `/admin/vouchers` khi không phải Admin | 1. Đăng nhập tài khoản role **Employee**<br>2. Truy cập `/admin/vouchers` trực tiếp | Bị redirect hoặc trang 403; không được thao tác | ⬜ |
