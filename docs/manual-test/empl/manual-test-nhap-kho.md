# Manual Test Guide — Nhập kho (Phiếu nhập hàng)

> Test thông qua Frontend: `https://localhost:7107`
>
> Cần tài khoản: **Employee** (hoặc Admin) — vai trò được phép tạo & xem phiếu nhập kho.

---

## Thiết lập trước khi test

| Bước | Hành động |
|------|----------|
| Khởi động DB | `docker-compose up -d` (từ thư mục `Infrastructure/db/`) |
| Chạy migration | `dotnet ef database update --project src/Infrastructure --startup-project src/API` |
| Chạy API | `dotnet run --project src/API/API.csproj` |
| Chạy Client | `dotnet run --project src/Client/Client.csproj` |
| Dữ liệu cần có | Ít nhất 1 **Nhà cung cấp** và 1 **Sản phẩm / biến thể** đã tồn tại trong DB |
| Đăng nhập | Truy cập Frontend, login bằng tài khoản Employee |

---

## Flow 1 — Happy Path: Tạo phiếu nhập kho hoàn chỉnh

**Mục tiêu:** Tạo thành công 1 phiếu nhập với 1 dòng sản phẩm, quét đủ serial, lưu phiếu.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Truy cập `/warehouse/import-receipts` → Click nút **"Tạo phiếu nhập"** (hoặc điều hướng thẳng đến `/warehouse/import-receipts/new`) | Trang tạo phiếu hiện ra, bảng sản phẩm trống, panel quét serial bên phải hiển thị |
| 2 | Tại dropdown **"Nhà cung cấp"**, chọn 1 nhà cung cấp hợp lệ | Dropdown hiển thị tên nhà cung cấp đã chọn |
| 3 | Kiểm tra trường **"Ngày nhập"** | Hiển thị ngày hôm nay (dd/MM/yyyy), read-only |
| 4 | Click nút **"Thêm sản phẩm mới"** | Dialog tìm kiếm sản phẩm hiện ra |
| 5 | Nhập tên sản phẩm vào ô tìm kiếm (debounce ~400ms) | Danh sách biến thể khớp hiện ra: SKU, tên, tùy chọn, giá |
| 6 | Click chọn 1 biến thể từ danh sách | Dialog đóng, dòng sản phẩm xuất hiện trong bảng với Số lượng = 1, Giá nhập = giá bán mặc định |
| 7 | Kiểm tra chip **"Đã nhập"** trên dòng vừa thêm | Chip đỏ hiển thị `0` (chưa quét serial nào) |
| 8 | Click nút **"Nhập mã"** trên dòng sản phẩm | Panel quét bên phải hiển thị thông tin sản phẩm đang active, ô nhập serial được focus tự động |
| 9 | Nhập 1 serial hợp lệ (chưa có trong DB) vào ô quét → nhấn **Enter** | Progress bar xuất hiện ngắn (kiểm tra DB), serial được thêm vào danh sách bên dưới |
| 10 | Kiểm tra chip **"Đã nhập"** trên dòng | Chip chuyển xanh, hiển thị `1`, trạng thái cột đổi thành **"Đã hoàn tất"** |
| 11 | Kiểm tra snackbar | Snackbar xanh: "Đã nhập đủ 1 mã cho [tên sản phẩm]!" |
| 12 | Kiểm tra tổng tiền ở footer bảng | `Số lượng × Giá nhập` hiển thị đúng định dạng `N0 đ` |
| 13 | Click nút **"Lưu phiếu nhập hàng"** | Spinner xuất hiện trên nút (đang lưu) |
| 14 | Chờ lưu xong | Snackbar xanh: "Lưu phiếu nhập kho thành công!", tự động chuyển về `/warehouse/import-receipts` |
| 15 | Kiểm tra danh sách | Phiếu vừa tạo xuất hiện ở đầu danh sách, mã phiếu dạng `PN-yyyyMMdd-NNN` |

---

## Flow 2 — Thêm nhiều dòng sản phẩm trong 1 phiếu

**Mục tiêu:** Xác nhận phiếu có thể chứa nhiều dòng khác nhau, mỗi dòng có serial riêng.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Vào trang tạo phiếu → chọn NCC → Click **"Thêm sản phẩm mới"** 2 lần liên tiếp, mỗi lần chọn 1 biến thể **khác nhau** | Bảng có 2 dòng riêng biệt |
| 2 | Dòng 1: Điều chỉnh số lượng lên 2 (nhấn `+`), giá nhập tùy chỉnh | Tổng tiền dòng 1 = `2 × giá nhập`, chip Đã nhập vẫn đỏ `0` |
| 3 | Click **"Nhập mã"** dòng 1 → quét 2 serial khác nhau | Sau serial thứ 2: chip xanh `2`, "Đã hoàn tất" |
| 4 | Click **"Nhập mã"** dòng 2 → quét 1 serial | Chip xanh `1`, "Đã hoàn tất" |
| 5 | Kiểm tra tổng tiền ở footer | Tổng = tổng tất cả các dòng |
| 6 | Click **"Lưu phiếu nhập hàng"** | Lưu thành công, chuyển về danh sách |

---

## Flow 3 — Xóa dòng sản phẩm

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Thêm 2 dòng sản phẩm vào phiếu | Bảng có 2 dòng |
| 2 | Click icon **xóa (🗑)** trên dòng 1 | Dòng 1 bị xóa khỏi bảng ngay lập tức, tổng tiền cập nhật |
| 3 | Kiểm tra bảng | Còn 1 dòng, không có thông báo lỗi |

---

## Flow 4 — Xóa serial đã quét

**Mục tiêu:** Tester hoặc nhân viên cần xóa serial nhập nhầm.

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Quét 3 serial hợp lệ cho 1 dòng (Qty = 3) | Danh sách serial bên phải có 3 mục |
| 2 | Click icon **X** trên serial thứ 2 trong danh sách | Serial bị xóa khỏi danh sách, chip "Đã nhập" về `2`, trạng thái đổi lại nút **"Nhập mã"** |
| 3 | Quét lại serial mới để đủ số lượng | Serial được thêm bình thường |

---

## Flow 5 — Xem danh sách phiếu nhập kho

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Truy cập `/warehouse/import-receipts` | Bảng danh sách hiển thị các cột: Mã phiếu, Ngày nhập, Nhà cung cấp, Nhân viên, Tổng tiền |
| 2 | Kiểm tra thứ tự mặc định | Phiếu mới nhất ở đầu (sắp xếp giảm dần theo ngày) |
| 3 | Kiểm tra phân trang | Có điều hướng trang nếu số phiếu > page size |
| 4 | Click vào 1 hàng trong bảng | Chuyển tới trang chi tiết phiếu (`/warehouse/import-receipts/{id}`) |

---

## Flow 6 — Tìm kiếm và lọc phiếu

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tại trang danh sách, nhập mã phiếu vào ô tìm kiếm (VD: `PN-`) | Danh sách lọc theo từ khóa mã phiếu |
| 2 | Xóa keyword, nhập tên nhà cung cấp vào ô tìm kiếm | Danh sách hiển thị các phiếu của NCC đó |
| 3 | Chọn khoảng ngày **Từ ngày / Đến ngày** khớp với phiếu đã tạo | Chỉ hiển thị phiếu trong khoảng ngày đó |
| 4 | Chọn lọc theo **Nhà cung cấp** từ dropdown | Kết quả chỉ chứa phiếu của NCC được chọn |
| 5 | Kết hợp keyword + ngày + NCC cùng lúc | Kết quả là giao của tất cả bộ lọc |
| 6 | Click **"Xóa bộ lọc"** / reset | Danh sách đầy đủ hiện lại |

---

## Flow 7 — Xem chi tiết phiếu

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Từ danh sách, click vào 1 phiếu | Trang chi tiết hiển thị: Mã phiếu, Ngày nhập, Nhà cung cấp, Nhân viên, Ghi chú, Tổng tiền |
| 2 | Kiểm tra bảng chi tiết dòng sản phẩm | Mỗi dòng hiển thị: SKU, Tên sản phẩm, Tùy chọn, Số lượng, Giá nhập, Thành tiền |
| 3 | Mở rộng (expand) 1 dòng sản phẩm hoặc xem tab serial | Danh sách serial của dòng đó được liệt kê đầy đủ |
| 4 | Click nút **"Quay lại"** | Quay về trang danh sách |

---

## Edge Cases

### EC-1: Lưu phiếu khi chưa chọn nhà cung cấp

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Để trống dropdown NCC → thêm sản phẩm, quét serial → Click "Lưu phiếu nhập hàng" | Snackbar đỏ: "Vui lòng chọn Nhà cung cấp!", phiếu không được gửi |

---

### EC-2: Lưu phiếu khi không có dòng sản phẩm nào

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Chọn NCC hợp lệ → không thêm sản phẩm → Click "Lưu phiếu nhập hàng" | Snackbar đỏ: "Phiếu nhập phải có ít nhất 1 sản phẩm!" |

---

### EC-3: Lưu phiếu khi số serial < số lượng

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Thêm sản phẩm với Qty = 3 → chỉ quét 2 serial → Click "Lưu" | Snackbar đỏ: "Chưa nhập đủ mã Serial cho: [tên sản phẩm]", chip dòng đó vẫn đỏ |
| Nếu có 2 dòng, 1 dòng thiếu serial | Snackbar liệt kê đúng tên dòng đang thiếu |

---

### EC-4: Nhấn Enter khi ô serial rỗng

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Bấm nút "Nhập mã" → ô serial được focus → nhấn Enter ngay (không nhập gì) | Không có phản ứng, danh sách serial không thay đổi |

---

### EC-5: Quét thêm serial khi dòng đã đủ số lượng

**Điều kiện:** Dòng có Qty = 1, đã quét đủ 1 serial.

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Nhập thêm 1 serial bất kỳ → nhấn Enter | Snackbar vàng: "Đã nhập đủ số lượng Serial cho sản phẩm này!", serial không được thêm |

---

### EC-6: Serial trùng trong cùng dòng

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Quét serial `ABC123` lần 1 | Serial được thêm thành công |
| Quét lại `ABC123` lần 2 trong cùng dòng | Snackbar đỏ: "Mã này vừa được quét!", serial không bị thêm lần 2 |

---

### EC-7: Serial trùng giữa 2 dòng sản phẩm khác nhau

**Điều kiện:** Phiếu có 2 dòng (2 variant khác nhau).

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Quét `XYZ999` ở dòng 1 | Thêm thành công |
| Chuyển sang quét dòng 2 → nhập `XYZ999` → Enter | Snackbar đỏ: "Mã Serial này đã được quét ở dòng sản phẩm khác!" |

---

### EC-8: Serial đã tồn tại trong DB (phiếu nhập trước)

**Điều kiện:** Serial đã được nhập trong phiếu cũ, tồn tại trong bảng `ProductSerials`.

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Nhập serial đã có trong DB → Enter | Progress bar xuất hiện (call API kiểm tra), snackbar đỏ: "LỖI: Mã Serial đã tồn tại trong hệ thống!", serial không được thêm |

---

### EC-9: Giá nhập = 0 (hàng tặng / hàng khuyến mãi)

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Thêm sản phẩm → sửa giá nhập về 0 → quét đủ serial → Lưu | Phiếu được lưu thành công, Tổng tiền = 0 đ |

---

### EC-10: Tăng số lượng sau khi đã quét đủ serial

**Kịch bản:** Quét đủ 2 serial cho Qty=2, sau đó tăng Qty lên 3.

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Quét 2 serial → Qty = 2 → chip xanh "Đã hoàn tất" | Trạng thái: hoàn tất |
| Nhấn `+` tăng Qty lên 3 | Chip đổi về đỏ `2`, trạng thái đổi lại nút **"Nhập mã"** |
| Cố lưu khi chưa quét serial thứ 3 | Snackbar đỏ báo thiếu serial |

---

### EC-11: Giảm số lượng xuống dưới số serial đã quét

**Kịch bản:** Qty = 3, đã quét 3 serial → giảm Qty xuống 2.

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Nhấn `-` để giảm Qty từ 3 xuống 2 | Qty = 2, chip "Đã nhập" vẫn hiển thị `3` (đỏ vì 3 ≠ 2) |
| Cố lưu | Snackbar đỏ: chưa nhập đủ serial (vì count ≠ qty) → cần xóa bớt 1 serial thủ công |

---

### EC-12: Thêm cùng variant 2 lần vào phiếu

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Click "Thêm sản phẩm mới" → chọn variant A | Dòng A được thêm thành công |
| Click "Thêm sản phẩm mới" → tìm và chọn lại variant A | Snackbar vàng: "Biến thể này đã có trong phiếu nhập!", dialog không đóng, không tạo dòng trùng |

---

### EC-13: Xóa dòng đang được active trong panel quét

**Điều kiện:** Đang quét serial cho dòng A (dòng A đang active ở panel phải).

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Click icon xóa (🗑) trên dòng A | Dòng A bị xóa, panel quét bên phải chuyển sang thông báo "Chọn một sản phẩm để bắt đầu quét" |

---

### EC-14: Tìm kiếm sản phẩm không tồn tại trong dialog thêm

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Mở dialog thêm sản phẩm → nhập từ khóa không khớp bất kỳ sản phẩm nào | Alert info: "Không tìm thấy sản phẩm nào.", danh sách trống |

---

### EC-15: Lọc phiếu theo ngày không có kết quả

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Tại trang danh sách, chọn khoảng ngày không có phiếu nào (VD: 01/01/2020 → 02/01/2020) | Bảng hiển thị trạng thái trống (không có bản ghi), không có lỗi |

---

### EC-16: Mã phiếu không trùng lặp khi tạo 2 phiếu trong cùng ngày

| Hành động | Kết quả mong đợi |
|-----------|-----------------|
| Tạo 2 phiếu nhập kho liên tiếp trong cùng ngày | Mã phiếu tự tăng: `PN-20260522-001`, `PN-20260522-002`, v.v., không trùng nhau |

---

## Kiểm tra UI Frontend

| Trang | Điểm cần kiểm tra |
|-------|--------------------|
| `/warehouse/import-receipts` | Bảng danh sách hiển thị đúng cột; phân trang hoạt động; click hàng chuyển đến chi tiết |
| `/warehouse/import-receipts/new` (load) | Dropdown NCC tải đủ danh sách; trường Ngày nhập read-only; bảng sản phẩm trống hiển thị thông báo "Chưa có sản phẩm nào" |
| `/warehouse/import-receipts/new` — panel quét (ẩn/hiện) | Icon toggle (`QrCodeScanner` / `ChevronRight`) ẩn/hiện panel phải; layout cột trái mở rộng khi panel ẩn |
| `/warehouse/import-receipts/new` — dialog thêm SP | Ô tìm kiếm debounce ~400ms; spinner hiện khi đang search; chọn variant → dialog đóng + snackbar xanh |
| `/warehouse/import-receipts/new` — bảng sản phẩm | Nút `+`/`-` điều chỉnh số lượng (min = 1); giá nhập chấp nhận số có format `N0`; chip "Đã nhập" đỏ khi thiếu, xanh khi đủ; nút "Nhập mã" ẩn sau khi đủ serial |
| `/warehouse/import-receipts/new` — panel quét | Khi chưa chọn dòng nào: thông báo hướng dẫn; khi active: focus tự động; progress bar khi đang kiểm tra DB; danh sách serial hiển thị mới nhất ở trên |
| `/warehouse/import-receipts/{id}` | Tất cả thông tin header hiển thị; bảng chi tiết đầy đủ; danh sách serial của từng dòng hiển thị đúng |

---

## Lưu ý khi test

- **Serial thực tế:** Để tránh trùng với DB có sẵn, dùng serial dạng `TEST-YYYYMMDD-001`, `TEST-YYYYMMDD-002`, v.v.
- **Không có nút Sửa / Xóa phiếu:** Phiếu nhập kho sau khi lưu là bất biến — không có chức năng chỉnh sửa hay xóa qua UI.
- **Làm mới trang:** Sau khi tạo phiếu thành công, nhấn F5 trên trang danh sách để đảm bảo hiển thị mới nhất.
- **Panel quét sticky:** Panel bên phải có `position: sticky`, cuộn trang không làm ô quét biến mất — tiện cho việc quét nhiều serial.
