# Manual Test: Admin — Quản lý Sản phẩm & Biến thể

Tài liệu này liệt kê tất cả các flow kiểm thử thủ công cho module quản lý sản phẩm trên trang Admin (`/admin/products`).

**Yêu cầu trước khi test:**
- Đăng nhập tài khoản có role `Admin`
- Ứng dụng đang chạy (API + Client)

**Ký hiệu kết quả:** ✅ Pass &nbsp;|&nbsp; ❌ Fail &nbsp;|&nbsp; ⬜ Chưa test

---

## Dữ liệu cần chuẩn bị sẵn

| Ký hiệu | Trạng thái cần có |
|---------|------------------|
| `cat_A` | Danh mục đã tồn tại trong hệ thống |
| `mfr_A` | Hãng sản xuất đã tồn tại trong hệ thống |
| `prod_existing` | Sản phẩm đã tồn tại, có ít nhất 1 biến thể, tồn kho > 0 (có serial Available) |
| `prod_no_stock` | Sản phẩm đã tồn tại, tồn kho = 0 (không có serial Available), trạng thái Đang bán |
| `sku_existing` | Mã SKU đã được dùng bởi một biến thể của `prod_existing` |
| `img_valid` | File ảnh JPG / PNG / WebP ≤ 5 MB |
| `img_large` | File ảnh > 5 MB |
| `file_invalid` | File không phải ảnh (VD: `.pdf`, `.exe`) |

---

## NHÓM 1 — Happy Paths

---

### Flow 1 — Xem danh sách và lọc sản phẩm

> Điểm bắt đầu: Vào `/admin/products`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Vào `/admin/products` | Bảng tải đúng với đủ các cột: Tên sản phẩm, Danh mục, Hãng, Giá, Tồn kho, Biến thể, Trạng thái, Thao tác; pagination hiện dạng "X–Y / Z sản phẩm" | ⬜ |
| 2 | Nhập "Laptop" vào ô **Tìm kiếm sản phẩm...** | Bảng cập nhật sau debounce (~400ms) — chỉ hiện sản phẩm có tên chứa "Laptop" | ⬜ |
| 3 | Chọn **Trạng thái** = "Đang bán" | Bảng chỉ hiện sản phẩm có chip xanh "Đang bán"; bộ lọc keyword vẫn còn hiệu lực | ⬜ |
| 4 | Chọn **Hãng sản xuất** = `mfr_A` | Cả 3 bộ lọc (keyword + trạng thái + hãng) cùng hoạt động | ⬜ |
| 5 | Chọn **Danh mục** = `cat_A` | Bốn bộ lọc kết hợp, kết quả thu hẹp thêm | ⬜ |
| 6 | Xóa lần lượt từng bộ lọc | Bảng mở rộng kết quả tương ứng sau mỗi lần xóa | ⬜ |
| 7 | Nhấn vào tiêu đề cột **Tên sản phẩm** | Bảng sắp xếp A → Z; nhấn lần 2 → Z → A | ⬜ |
| 8 | Nhấn vào tiêu đề cột **Giá** | Bảng sắp xếp theo giá tăng dần; nhấn lần 2 → giảm dần | ⬜ |
| 9 | Chuyển sang trang 2 (khi có > 10 sản phẩm) | Trang 2 tải đúng; pagination cập nhật số trang hiện tại | ⬜ |

---

### Flow 2 — Tạo sản phẩm mới (1 biến thể, đầy đủ thông tin)

> Điểm bắt đầu: Trang danh sách sản phẩm `/admin/products`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Nhấn **Thêm sản phẩm** | Chuyển đến `/admin/products/create`; tiêu đề trang "Thêm sản phẩm mới" | ⬜ |
| 2 | Nhập **Tên sản phẩm** = "Laptop Dell XPS 15" | Ô tên hiện đúng, không lỗi tức thì | ⬜ |
| 3 | Nhập **Mô tả ngắn** = "Laptop cao cấp cho doanh nhân" | Ô mô tả ngắn hiện đúng | ⬜ |
| 4 | Nhập **Mô tả chi tiết** (vài dòng) trong rich text editor | Editor chấp nhận nội dung | ⬜ |
| 5 | Chọn **Danh mục** = `cat_A` | Dropdown hiển thị phân cấp đúng; tên danh mục xuất hiện sau khi chọn | ⬜ |
| 6 | Chọn **Hãng sản xuất** = `mfr_A` | Logo hãng (nếu có) và tên hãng hiển thị trong dropdown | ⬜ |
| 7 | Nhấn **Thêm biến thể** | Khối "Biến thể 1" mở ra với các trường: Mã SKU, Tên phiên bản, Giá bán, Giá gốc, Bảo hành | ⬜ |
| 8 | Nhập **Mã SKU** = "DELL-XPS15-i7-512" | Ô hiện đúng; không lỗi tức thì | ⬜ |
| 9 | Nhập **Tên phiên bản** = "Core i7 / 16GB / 512GB SSD" | Ô hiện đúng | ⬜ |
| 10 | Nhập **Giá bán** = 35000000 | Hiển thị số tiền đúng (có thể format "35.000.000 đ") | ⬜ |
| 11 | Nhập **Giá gốc** = 38000000 | Hiện đúng | ⬜ |
| 12 | Nhập **Bảo hành (tháng)** = 12 | Ô hiện đúng | ⬜ |
| 13 | Nhấn vào vùng **"Click để chọn ảnh (tối đa 5 ảnh)"**, chọn `img_valid` | Ảnh preview xuất hiện; badge **Main** tự gán cho ảnh đầu tiên | ⬜ |
| 14 | Nhấn **Thêm thông số**; nhập Key = "Hãng CPU" / Value = "Intel" | Hàng spec xuất hiện với đúng nội dung | ⬜ |
| 15 | Nhấn **Tạo sản phẩm** | Snackbar "Tạo sản phẩm thành công!"; chuyển về danh sách; sản phẩm "Laptop Dell XPS 15" xuất hiện với chip **Nháp** | ⬜ |

---

### Flow 3 — Tạo sản phẩm với nhiều biến thể

> Điểm bắt đầu: Trang `/admin/products/create`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Điền **Tên sản phẩm**, chọn Danh mục và Hãng sản xuất | Các field không báo lỗi | ⬜ |
| 2 | Nhấn **Thêm biến thể** lần 1; điền SKU = "SKU-001", Tên = "Phiên bản 8GB", Giá = 10000000 | Khối "Biến thể 1" hiện đúng | ⬜ |
| 3 | Nhấn **Thêm biến thể** lần 2; điền SKU = "SKU-002", Tên = "Phiên bản 16GB", Giá = 12000000 | Khối "Biến thể 2" xuất hiện bên dưới; 2 biến thể độc lập | ⬜ |
| 4 | Upload `img_valid` vào Biến thể 1; upload ảnh khác vào Biến thể 2 | Mỗi biến thể có ảnh riêng, không bị lẫn sang nhau | ⬜ |
| 5 | Nhấn **Tạo sản phẩm** | Tạo thành công; trong danh sách cột **Biến thể** hiển thị "2" | ⬜ |

---

### Flow 4 — Sửa thông tin cơ bản sản phẩm

> Điểm bắt đầu: Danh sách sản phẩm, có `prod_existing`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Nhấn icon **Chỉnh sửa** (bút xanh) trên hàng `prod_existing` | Chuyển đến `/admin/products/edit/{id}`; tiêu đề "Chỉnh sửa sản phẩm"; tất cả field được điền sẵn dữ liệu hiện tại | ⬜ |
| 2 | Quan sát phần **Thông tin** (Quick Info) bên phải | Hiện đúng: ID sản phẩm, Ngày tạo, Số biến thể | ⬜ |
| 3 | Đổi **Tên sản phẩm** thành tên mới bất kỳ | Ô cập nhật | ⬜ |
| 4 | Đổi **Danh mục** sang danh mục khác | Dropdown phân cấp hiện đúng; lựa chọn mới được ghi nhận | ⬜ |
| 5 | Đổi **Hãng sản xuất** sang hãng khác | Dropdown cập nhật | ⬜ |
| 6 | Nhấn **Cập nhật** | Snackbar "Cập nhật sản phẩm thành công!"; quay về danh sách; hàng sản phẩm hiện tên, danh mục và hãng mới | ⬜ |

---

### Flow 5 — Sửa biến thể đã tồn tại (giá, bảo hành, thông số)

> Điểm bắt đầu: Trang edit `prod_existing`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Mở trang edit `prod_existing`; kéo xuống phần **Danh sách biến thể** | Thấy biến thể hiện có với dữ liệu đúng (SKU, tên, giá, bảo hành, specs, ảnh) | ⬜ |
| 2 | Thay đổi **Giá bán** của biến thể | Ô cập nhật ngay | ⬜ |
| 3 | Thay đổi **Bảo hành (tháng)** | Ô cập nhật ngay | ⬜ |
| 4 | Trong phần thông số kỹ thuật của biến thể: sửa Value của 1 spec | Ô cập nhật | ⬜ |
| 5 | Nhấn **Cập nhật** | Thành công; mở lại trang edit và kiểm tra: giá bán, bảo hành và spec đã lưu đúng | ⬜ |

---

### Flow 6 — Quản lý ảnh biến thể (upload, kéo thả, đặt ảnh chính, xóa)

> Điểm bắt đầu: Trang tạo mới hoặc edit sản phẩm, trong khối Biến thể

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Nhấn vào vùng **"Click để chọn ảnh (tối đa 5 ảnh)"** | File picker hệ thống mở | ⬜ |
| 2 | Chọn 3 file `img_valid` cùng lúc | 3 ảnh preview xuất hiện; ảnh đầu tiên tự có badge **Main** | ⬜ |
| 3 | Kéo (drag & drop) ảnh thứ 3 lên vị trí thứ 1 | Thứ tự ảnh thay đổi; badge Main vẫn theo ảnh được gán ban đầu | ⬜ |
| 4 | Nhấn vào badge **Main** trên ảnh ở vị trí thứ 2 | Badge Main chuyển sang ảnh đó; các ảnh còn lại không còn badge Main | ⬜ |
| 5 | Nhấn nút xóa (✕) trên ảnh ở vị trí thứ 3 | Ảnh đó biến khỏi preview; còn 2 ảnh | ⬜ |
| 6 | Drag & drop file `img_valid` trực tiếp vào vùng upload | Ảnh mới được thêm vào cuối danh sách preview | ⬜ |
| 7 | Lưu (Cập nhật / Tạo sản phẩm) | Snackbar thành công; mở lại trang edit → ảnh lưu đúng thứ tự và đúng ảnh Main | ⬜ |

---

### Flow 7 — Thêm / sửa / xóa thông số kỹ thuật

> Điểm bắt đầu: Trang tạo mới hoặc edit sản phẩm

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Kéo xuống phần **Thông số kỹ thuật** (chưa có spec nào) | Hiện: *"Chưa có thông số nào. Nhấn 'Thêm thông số' để bắt đầu."* | ⬜ |
| 2 | Nhấn **Thêm thông số** | Hàng mới xuất hiện với 2 ô: Key (placeholder "VD: Hãng CPU") và Value (placeholder "VD: Intel") | ⬜ |
| 3 | Nhập Key = "CPU" / Value = "Intel Core i7-13700H" | Hàng hiện đúng nội dung | ⬜ |
| 4 | Nhấn **Thêm thông số** thêm 2 lần; nhập "RAM" / "16GB" và "Ổ cứng" / "512GB NVMe" | 3 hàng spec hiện đủ | ⬜ |
| 5 | Sửa Value của hàng "RAM" thành "32GB" | Ô cập nhật ngay không cần thao tác khác | ⬜ |
| 6 | Nhấn icon xóa trên hàng "Ổ cứng" | Hàng đó biến mất; còn 2 spec | ⬜ |
| 7 | Lưu sản phẩm | Thành công; mở lại trang edit → 2 spec "CPU" và "RAM" hiện đúng | ⬜ |

---

### Flow 8 — Thêm biến thể mới cho sản phẩm đã tồn tại

> Điểm bắt đầu: Trang edit `prod_existing` (đang có 1 biến thể)

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Kéo xuống phần **Danh sách biến thể** | Thấy biến thể hiện có với dữ liệu đúng | ⬜ |
| 2 | Nhấn **Thêm biến thể** | Khối "Biến thể 2" xuất hiện bên dưới, các ô đều trống | ⬜ |
| 3 | Điền SKU mới (khác `sku_existing`), Tên phiên bản và Giá bán | Không có lỗi | ⬜ |
| 4 | Upload ảnh cho biến thể mới | Ảnh preview xuất hiện trong khối Biến thể 2; không ảnh hưởng ảnh của Biến thể 1 | ⬜ |
| 5 | Nhấn **Cập nhật** | Thành công; trong danh sách cột **Biến thể** tăng thêm 1 | ⬜ |

---

### Flow 9 — Đổi trạng thái sản phẩm

> Điểm bắt đầu: Có sản phẩm với trạng thái **Nháp**

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Mở trang edit sản phẩm trạng thái Nháp | Phần **Trạng thái** hiển thị dropdown đang chọn "Nháp" | ⬜ |
| 2 | Đổi dropdown **Trạng thái** → "Đang bán" | Dropdown cập nhật | ⬜ |
| 3 | Nhấn **Cập nhật** | Thành công; trong danh sách chip đổi sang xanh **Đang bán** | ⬜ |
| 4 | Mở lại trang edit, đổi → "Ngừng kinh doanh" → **Cập nhật** | Chip đổi sang đỏ **Ngừng kinh doanh** | ⬜ |
| 5 | Mở lại trang edit, đổi → "Nháp" → **Cập nhật** | Chip trở về **Nháp** (outlined, màu mặc định) | ⬜ |

---

### Flow 10 — Xóa sản phẩm

> Điểm bắt đầu: Trang danh sách sản phẩm

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Nhấn icon **Xóa** (đỏ) trên hàng một sản phẩm | Dialog xác nhận hiện: *"Bạn có chắc chắn muốn xóa sản phẩm '{tên}'?"* với 2 nút **Xóa** và **Hủy** | ⬜ |
| 2 | Nhấn **Hủy** | Dialog đóng; sản phẩm vẫn còn trong danh sách | ⬜ |
| 3 | Nhấn icon Xóa lần nữa → nhấn **Xóa** | Sản phẩm biến khỏi danh sách; tổng đếm pagination giảm 1; snackbar xóa thành công | ⬜ |

---

## NHÓM 2 — Edge Cases & Validation

---

### 2.1 Validation — Thông tin sản phẩm

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-PROD-E01 | Tên sản phẩm rỗng | 1. Vào form tạo mới<br>2. Để trống **Tên sản phẩm**<br>3. Nhấn **Tạo sản phẩm** | Lỗi: *"Tên sản phẩm không được để trống."*; form không submit | ⬜ |
| TC-PROD-E02 | Chưa chọn danh mục | 1. Điền tên sản phẩm và hãng<br>2. Bỏ trống **Danh mục**<br>3. Submit | Lỗi: *"Vui lòng chọn danh mục."* | ⬜ |
| TC-PROD-E03 | Chưa chọn hãng sản xuất | 1. Điền tên và danh mục<br>2. Bỏ trống **Hãng sản xuất**<br>3. Submit | Lỗi: *"Vui lòng chọn hãng sản xuất."* | ⬜ |
| TC-PROD-E04 | Không có biến thể | 1. Điền đủ tên, danh mục, hãng<br>2. Không nhấn "Thêm biến thể"<br>3. Submit | Lỗi: *"Sản phẩm phải có ít nhất 1 biến thể."*; form không submit | ⬜ |
| TC-PROD-E05 | Nhấn **Hủy** ở form tạo mới | 1. Vào `/admin/products/create`<br>2. Điền một số dữ liệu<br>3. Nhấn **Hủy** | Chuyển về danh sách; không có sản phẩm mới nào được tạo | ⬜ |
| TC-PROD-E06 | Nhấn **Hủy** ở form chỉnh sửa | 1. Vào trang edit<br>2. Thay đổi tên sản phẩm<br>3. Nhấn **Hủy** | Chuyển về danh sách; sản phẩm giữ nguyên tên cũ | ⬜ |

---

### 2.2 Validation — Biến thể

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-VAR-E01 | Mã SKU rỗng | 1. Nhấn **Thêm biến thể**<br>2. Để trống **Mã SKU**<br>3. Submit | Lỗi: *"Mã SKU của biến thể không được để trống."* | ⬜ |
| TC-VAR-E02 | Tên phiên bản rỗng | 1. Nhấn **Thêm biến thể**<br>2. Điền SKU nhưng để trống **Tên phiên bản**<br>3. Submit | Lỗi: *"Tên phiên bản không được để trống."* | ⬜ |
| TC-VAR-E03 | Giá bán = 0 hoặc bỏ trống | 1. Điền SKU và Tên phiên bản<br>2. Để Giá bán = 0 hoặc trống<br>3. Submit | Quan sát: lỗi validation hay cho phép lưu — **ghi lại kết quả thực tế** | ⬜ |
| TC-VAR-E04 | Giá gốc thấp hơn giá bán | 1. Nhập Giá bán = 20.000.000, Giá gốc = 15.000.000<br>2. Submit | Quan sát: cảnh báo hay cho phép lưu — **ghi lại kết quả thực tế** | ⬜ |
| TC-VAR-E05 | Xóa biến thể duy nhất | 1. Form đang có đúng 1 biến thể<br>2. Nhấn nút xóa biến thể đó | Bị ngăn hoặc hiện cảnh báo *"Sản phẩm phải có ít nhất 1 biến thể."* | ⬜ |
| TC-VAR-E06 | SKU trùng trong cùng sản phẩm | 1. Thêm 2 biến thể<br>2. Nhập cùng SKU cho cả 2<br>3. Submit | Lỗi trùng SKU (client-side validation hoặc từ server) | ⬜ |
| TC-VAR-E07 | SKU trùng với sản phẩm khác | 1. Nhập **Mã SKU** = `sku_existing`<br>2. Submit | Server trả lỗi; snackbar thất bại hiện; không tạo được sản phẩm | ⬜ |
| TC-VAR-E08 | Bảo hành âm | 1. Nhập **Bảo hành (tháng)** = -1<br>2. Submit | Trường không cho nhập số âm hoặc lỗi validation | ⬜ |

---

### 2.3 Validation — Ảnh

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-IMG-E01 | Upload file > 5 MB | 1. Click vùng upload ảnh<br>2. Chọn `img_large` (> 5 MB) | Thông báo lỗi file quá lớn; ảnh không thêm vào preview; upload ảnh khác vẫn hoạt động bình thường | ⬜ |
| TC-IMG-E02 | Upload file không phải ảnh | 1. Click vùng upload<br>2. Chọn `file_invalid` (.pdf hoặc .exe) | File bị từ chối; thông báo lỗi định dạng không hợp lệ; preview không thay đổi | ⬜ |
| TC-IMG-E03 | Upload quá 5 ảnh | 1. Upload 5 `img_valid` thành công<br>2. Thử upload thêm ảnh thứ 6 | Upload bị chặn hoặc hiện cảnh báo đã đạt giới hạn 5 ảnh | ⬜ |
| TC-IMG-E04 | Lưu biến thể không có ảnh | 1. Điền đủ SKU, Tên phiên bản, Giá bán<br>2. Không upload ảnh nào<br>3. Submit | Quan sát: cho phép lưu (ảnh không bắt buộc) hay báo lỗi — **ghi lại kết quả thực tế** | ⬜ |
| TC-IMG-E05 | Xóa toàn bộ ảnh của biến thể đã có ảnh | 1. Trang edit: xóa tất cả ảnh hiện có của 1 biến thể<br>2. Nhấn **Cập nhật** | Quan sát: cho phép lưu biến thể không ảnh hay báo lỗi — **ghi lại kết quả thực tế** | ⬜ |

---

### 2.4 Validation — Thông số kỹ thuật

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-SPEC-E01 | Key rỗng, có Value | 1. Nhấn **Thêm thông số**<br>2. Để trống Key, nhập Value = "Intel"<br>3. Submit | Quan sát: lỗi validation hay hàng bị bỏ qua — **ghi lại kết quả thực tế** | ⬜ |
| TC-SPEC-E02 | Cả Key và Value đều rỗng | 1. Nhấn **Thêm thông số**<br>2. Không nhập gì vào cả 2 ô<br>3. Submit | Hàng trống bị bỏ qua hoặc báo lỗi | ⬜ |
| TC-SPEC-E03 | Key trùng lặp trong 1 biến thể | 1. Thêm 2 spec đều có Key = "CPU"<br>2. Submit | Quan sát: báo lỗi trùng key hay ghi đè lên nhau — **ghi lại kết quả thực tế** | ⬜ |

---

### 2.5 Edge Cases — Danh sách & Bộ lọc

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-LIST-E01 | Tìm kiếm không có kết quả | 1. Nhập từ khóa không tồn tại: "xyzabcdef999"<br>2. Chờ debounce | Bảng hiện *"Không tìm thấy sản phẩm nào."* | ⬜ |
| TC-LIST-E02 | Xóa từ khóa sau khi đã lọc | 1. Tìm "Laptop" → bảng lọc<br>2. Xóa hết nội dung ô tìm kiếm | Bảng tải lại đầy đủ không lọc | ⬜ |
| TC-LIST-E03 | Sản phẩm tồn kho = 0 | Tìm `prod_no_stock` trong danh sách | Cột **Tồn kho** hiển thị "0"; chip trạng thái không tự đổi thành "Ngừng kinh doanh" | ⬜ |
| TC-LIST-E04 | Nhấn icon **Xem kho Serial** | Nhấn icon kho Serial trên bất kỳ sản phẩm nào | Chuyển đến `/inventory/serials` đã được lọc sẵn theo sản phẩm đó | ⬜ |
| TC-LIST-E05 | Lọc kết hợp trả về 0 kết quả | 1. Chọn Hãng và Danh mục không có sản phẩm nào giao nhau<br>2. Chờ reload | Hiện *"Không tìm thấy sản phẩm nào."*; pagination ẩn hoặc hiện "0–0 / 0 sản phẩm" | ⬜ |

---

### 2.6 Hành vi đặc biệt

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-MISC-E01 | Submit form nhanh 2 lần (double-click) | 1. Điền form hợp lệ đầy đủ<br>2. Double-click **Tạo sản phẩm** rất nhanh | Chỉ 1 sản phẩm được tạo; không bị duplicate; nút bị disable sau lần click đầu | ⬜ |
| TC-MISC-E02 | Truy cập URL edit không tồn tại | Nhập `/admin/products/edit/999999` vào thanh địa chỉ trình duyệt | Hiện thông báo không tìm thấy hoặc redirect về danh sách; không crash toàn trang | ⬜ |
| TC-MISC-E03 | Xóa sản phẩm đang có serial Available | 1. Nhấn icon Xóa trên `prod_existing` (có serial Active)<br>2. Confirm | Quan sát: cho phép soft-delete hay báo lỗi do còn serial — **ghi lại kết quả thực tế** | ⬜ |
| TC-MISC-E04 | Tên sản phẩm chứa ký tự đặc biệt | 1. Nhập tên: `Laptop "Dell" & <XPS> 15`<br>2. Tạo sản phẩm | Tạo thành công; tên hiển thị đúng trong danh sách (không bị HTML-encode lỗi) | ⬜ |
| TC-MISC-E05 | Tên sản phẩm tiếng Việt đầy dấu | 1. Nhập tên: "Máy Tính Bảng Samsung Galaxy"<br>2. Tạo sản phẩm | Tạo thành công; tên Unicode hiển thị đúng trong danh sách | ⬜ |
| TC-MISC-E06 | Xung đột concurrent edit | 1. Mở trang edit sản phẩm X ở Tab 1<br>2. Xóa sản phẩm X từ Tab 2 → confirm<br>3. Quay lại Tab 1 → nhấn **Cập nhật** | API trả lỗi (404 hoặc thông báo phù hợp); snackbar thất bại hiển thị ở Tab 1; không crash | ⬜ |
| TC-MISC-E07 | Skeleton loading khi API chậm | 1. Throttle mạng xuống "Slow 3G" trong DevTools<br>2. Vào `/admin/products` | Skeleton loading hoặc spinner hiện trong khi chờ; không để màn hình trắng | ⬜ |
