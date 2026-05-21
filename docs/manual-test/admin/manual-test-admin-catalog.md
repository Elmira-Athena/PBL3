# Manual Test: Admin — Danh mục / Hãng sản xuất / Nhà cung cấp

Tài liệu này liệt kê tất cả các flow cần kiểm thử thủ công trên UI cho 3 module quản trị: Danh mục, Hãng sản xuất, Nhà cung cấp.

**Yêu cầu trước khi test:**
- Đăng nhập tài khoản có role `Admin`
- Ứng dụng đang chạy (API + Client)

**Ký hiệu kết quả:** ✅ Pass &nbsp;|&nbsp; ❌ Fail &nbsp;|&nbsp; ⬜ Chưa test

---

## 1. Quản lý Danh mục

### Happy Path

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-CAT-01 | Thêm danh mục gốc đầy đủ thông tin | 1. Vào trang Danh mục<br>2. Nhấn **Thêm danh mục**<br>3. Nhập tên "Laptop Gaming"<br>4. Xác nhận slug tự sinh: `laptop-gaming`<br>5. Không chọn danh mục cha<br>6. Nhập Sort Order = 1<br>7. Upload ảnh (jpg/png hợp lệ)<br>8. Bật toggle **Hiển thị**<br>9. Nhấn **Lưu** | Thêm thành công, danh mục xuất hiện ở cấp 0 trong cây; snackbar thành công hiện | ⬜ |
| TC-CAT-02 | Thêm danh mục con | 1. Hover vào danh mục cha trong cây<br>2. Nhấn icon **+** (AddCircleOutline)<br>3. Dialog mở với danh mục cha đã được chọn sẵn<br>4. Nhập tên "Laptop Gaming ASUS"<br>5. Nhấn **Lưu** | Danh mục con xuất hiện dưới danh mục cha; chip "Cấp 1" hiển thị đúng | ⬜ |
| TC-CAT-03 | Sửa danh mục | 1. Hover vào một danh mục<br>2. Nhấn icon **Edit** (bút xanh)<br>3. Đổi tên sang "Laptop Gaming Pro"<br>4. Kiểm tra slug tự cập nhật thành `laptop-gaming-pro` khi blur khỏi field tên<br>5. Đổi Sort Order = 2<br>6. Nhấn **Lưu** | Tên và slug cập nhật ngay trong cây; snackbar thành công | ⬜ |
| TC-CAT-04 | Xóa danh mục lá (không có con) | 1. Hover vào danh mục lá (icon Label, không có con)<br>2. Nhấn icon **Xóa** (đỏ)<br>3. Confirm dialog hiện — nhấn **Xác nhận** | Danh mục biến mất khỏi cây; snackbar xóa thành công | ⬜ |
| TC-CAT-05 | Tìm kiếm — cây tự mở rộng | 1. Nhập từ khóa vào ô tìm kiếm (VD: "Gaming")<br>2. Chờ debounce ~300ms | Chỉ hiện các node khớp; tất cả node cha của kết quả tự động expand; tên khớp hiển thị **đậm** màu primary | ⬜ |
| TC-CAT-06 | Expand All / Collapse All | 1. Nhấn **Mở rộng tất cả** → toàn bộ cây expand<br>2. Nhấn **Thu gọn** → toàn bộ cây collapse về cấp 0 | Trạng thái cây thay đổi đúng | ⬜ |
| TC-CAT-07 | Thêm con từ nút inline trong cây | 1. Hover một node gốc<br>2. Nhấn icon **+** xanh lá bên phải tên<br>3. Dialog Thêm danh mục mở, dropdown cha đã chọn sẵn node đó<br>4. Nhập tên và lưu | Con mới xuất hiện dưới node đã chọn | ⬜ |

---

### Edge Cases & Validation

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-CAT-08 | Tên rỗng | 1. Mở dialog Thêm danh mục<br>2. Để trống field **Tên**<br>3. Nhấn **Lưu** | Lỗi validation: *"Tên danh mục không được để trống."*; form không submit | ⬜ |
| TC-CAT-09 | Slug chứa ký tự không hợp lệ | 1. Mở dialog Thêm<br>2. Nhập slug tay: `Laptop Gaming!` (chữ hoa + dấu !)<br>3. Nhấn **Lưu** | Lỗi: *"Slug chỉ chấp nhận chữ thường, số và dấu gạch ngang."* | ⬜ |
| TC-CAT-10 | Tên tiếng Việt → slug tự sinh | 1. Nhập tên: `Máy Tính Xách Tay`<br>2. Click ra ngoài field Tên (blur) | Slug tự điền: `may-tinh-xach-tay` (bỏ dấu, lowercase, khoảng trắng thành `-`) | ⬜ |
| TC-CAT-11 | Tên quá dài (>100 ký tự) | 1. Nhập tên 101 ký tự liên tiếp<br>2. Nhấn **Lưu** | Lỗi: *"Tên danh mục không được vượt quá 100 ký tự."* | ⬜ |
| TC-CAT-12 | Sort order âm | 1. Nhập Sort Order = -1<br>2. Nhấn **Lưu** | Lỗi: *"Thứ tự hiển thị phải >= 0."* | ⬜ |
| TC-CAT-13 | Upload file không phải ảnh | 1. Trong dialog, thử upload file .pdf hoặc .exe vào ô ảnh danh mục | File bị từ chối; ô upload không thay đổi | ⬜ |
| TC-CAT-14 | Xóa ảnh đã upload trong dialog | 1. Upload ảnh thành công (preview hiện)<br>2. Nhấn nút xóa ảnh trong dialog | Preview biến mất; trường ImageUrl về null | ⬜ |
| TC-CAT-15 | Tắt hiển thị → badge "Ẩn" | 1. Thêm hoặc sửa danh mục<br>2. Tắt toggle **Hiển thị** (IsVisible = false)<br>3. Lưu | Danh mục vẫn xuất hiện trong cây admin nhưng có badge chip **Ẩn** màu warning bên cạnh tên | ⬜ |
| TC-CAT-16 | Không thể chọn chính mình làm cha | 1. Mở dialog **Sửa** danh mục "Laptop"<br>2. Mở dropdown **Danh mục cha** | Danh mục "Laptop" và tất cả con cháu của nó không xuất hiện trong dropdown | ⬜ |
| TC-CAT-17 | Xóa danh mục có con | 1. Chọn danh mục cha có ít nhất 1 con<br>2. Nhấn icon Xóa → Confirm | Quan sát hành vi: hoặc báo lỗi không cho xóa, hoặc xóa cascade — ghi lại kết quả thực tế | ⬜ |
| TC-CAT-18 | Tìm kiếm không có kết quả | 1. Nhập từ khóa không tồn tại (VD: "xyzabcdef")<br>2. Chờ debounce | Cây trống hoặc hiện thông báo không tìm thấy | ⬜ |
| TC-CAT-19 | Đóng dialog bằng Esc | 1. Mở dialog Thêm/Sửa<br>2. Nhập một số dữ liệu<br>3. Nhấn phím **Esc** | Dialog đóng; không có thay đổi nào được lưu | ⬜ |
| TC-CAT-20 | Cancel xác nhận xóa | 1. Nhấn icon Xóa trên một danh mục<br>2. Confirm dialog xuất hiện → nhấn **Hủy** | Dialog đóng; danh mục không bị xóa | ⬜ |

---

## 2. Quản lý Hãng sản xuất

### Happy Path

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-MFR-01 | Thêm hãng đầy đủ thông tin | 1. Vào trang Hãng sản xuất<br>2. Nhấn **Thêm hãng**<br>3. Tên: `ASUS`<br>4. Upload logo (ảnh hợp lệ)<br>5. Website: `https://www.asus.com`<br>6. Email hỗ trợ: `support@asus.com`<br>7. Nhấn **Lưu** | Hãng xuất hiện trong bảng với logo, website là link clickable, email là mailto link; snackbar thành công | ⬜ |
| TC-MFR-02 | Thêm hãng chỉ có tên | 1. Mở dialog Thêm<br>2. Chỉ nhập Tên: `MSI`<br>3. Để trống logo, website, email<br>4. Nhấn **Lưu** | Thêm thành công; avatar hiển thị initials "MS"; website và email ô trống / dash | ⬜ |
| TC-MFR-03 | Sửa hãng | 1. Nhấn icon **Edit** trên hàng hãng<br>2. Đổi tên, thay logo mới, cập nhật website<br>3. Nhấn **Lưu** | Bảng cập nhật ngay sau khi dialog đóng; snackbar thành công | ⬜ |
| TC-MFR-04 | Xóa hãng | 1. Nhấn icon **Xóa** trên hàng hãng<br>2. Confirm dialog: *"Bạn có chắc chắn muốn xóa hãng ... không?"*<br>3. Nhấn **Xác nhận** | Hãng biến khỏi bảng; tổng đếm chip giảm 1; snackbar thành công | ⬜ |
| TC-MFR-05 | Tìm kiếm theo tên | 1. Nhập "ASUS" vào ô tìm kiếm<br>2. Chờ debounce ~400ms | Bảng lọc chỉ còn hãng tên chứa "ASUS" | ⬜ |
| TC-MFR-06 | Tìm kiếm theo website | 1. Nhập "asus.com" vào ô tìm kiếm | Bảng hiển thị hãng có website chứa "asus.com" | ⬜ |
| TC-MFR-07 | Phân trang | 1. Thêm đủ >10 hãng (hoặc đổi page size về 5)<br>2. Nhấn sang trang 2<br>3. Đổi page size sang 25 | Trang chuyển đúng; info *"X-Y / Z hãng"* cập nhật; toàn bộ data load | ⬜ |
| TC-MFR-08 | Sắp xếp theo tên | 1. Nhấn header **Tên hãng sản xuất** lần 1 → A→Z<br>2. Nhấn lần 2 → Z→A | Bảng sort đúng chiều; icon mũi tên trên header đổi chiều | ⬜ |
| TC-MFR-09 | Sắp xếp theo ngày thêm | 1. Nhấn header **Ngày thêm** | Bảng sort theo ngày tạo mới nhất / cũ nhất | ⬜ |

---

### Edge Cases & Validation

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-MFR-10 | Tên rỗng | 1. Mở dialog Thêm<br>2. Để trống field **Tên**<br>3. Nhấn **Lưu** | Lỗi validation; form không submit | ⬜ |
| TC-MFR-11 | Website thiếu http/https | 1. Nhập Website: `google.com` (không có protocol)<br>2. Nhấn **Lưu** | Lỗi: website phải bắt đầu bằng `http://` hoặc `https://` | ⬜ |
| TC-MFR-12 | Website hợp lệ → link trong bảng | 1. Thêm hãng với website: `https://www.lenovo.com`<br>2. Kiểm tra bảng | Cột Website hiển thị `lenovo.com` (bỏ protocol); click mở tab mới đúng URL | ⬜ |
| TC-MFR-13 | Email sai định dạng | 1. Nhập Email hỗ trợ: `support-asus` (không có @)<br>2. Nhấn **Lưu** | Lỗi định dạng email | ⬜ |
| TC-MFR-14 | Tên >100 ký tự | 1. Paste 101 ký tự vào field Tên<br>2. Nhấn **Lưu** | Lỗi max length | ⬜ |
| TC-MFR-15 | Upload file không phải ảnh làm logo | 1. Trong dialog, thử upload .pdf hoặc .docx vào ô Logo | File bị từ chối; logo không thay đổi | ⬜ |
| TC-MFR-16 | Thay/xóa logo trong dialog sửa | 1. Mở dialog Sửa hãng đã có logo<br>2. Nhấn **Thay logo khác** hoặc nút xóa logo<br>3. Upload ảnh mới (hoặc để trống)<br>4. Lưu | Logo cập nhật đúng trong bảng | ⬜ |
| TC-MFR-17 | Không có logo → initials avatar | 1. Thêm hãng tên "Dell Technologies" không upload logo | Avatar hiển thị chữ "DE" (initials) thay vì ảnh | ⬜ |
| TC-MFR-18 | Cancel xóa | 1. Nhấn Xóa trên một hãng<br>2. Confirm dialog → nhấn **Hủy** | Hãng không bị xóa | ⬜ |
| TC-MFR-19 | Tìm kiếm không có kết quả | 1. Nhập từ khóa "xyzxyzxyz" | Empty state: icon Factory + *"Không tìm thấy hãng sản xuất nào."* | ⬜ |
| TC-MFR-20 | Snackbar sau xóa thành công | 1. Xóa một hãng (xác nhận) | Snackbar màu success hiện ở góc màn hình | ⬜ |

---

## 3. Quản lý Nhà cung cấp

### Happy Path

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-SUP-01 | Thêm nhà cung cấp đầy đủ thông tin | 1. Vào trang Nhà cung cấp<br>2. Nhấn **Thêm nhà cung cấp**<br>3. Tên: `Công ty TNHH Phân phối ABC`<br>4. SĐT: `0912345678`<br>5. Người liên hệ: `Nguyễn Văn A`<br>6. Email: `contact@abc.vn`<br>7. Mã số thuế: `0123456789`<br>8. Địa chỉ: `123 Nguyễn Văn Linh, Đà Nẵng`<br>9. Nhấn **Lưu** | Nhà cung cấp xuất hiện trong bảng với đầy đủ thông tin; snackbar thành công | ⬜ |
| TC-SUP-02 | Thêm nhà cung cấp chỉ điền bắt buộc | 1. Chỉ nhập Tên và Số điện thoại<br>2. Để trống tất cả field còn lại<br>3. Nhấn **Lưu** | Thêm thành công; các cột tùy chọn hiển thị **—** trong bảng | ⬜ |
| TC-SUP-03 | Sửa nhà cung cấp | 1. Nhấn icon **Edit** trên hàng<br>2. Cập nhật Người liên hệ, Email, Địa chỉ<br>3. Nhấn **Lưu** | Bảng cập nhật ngay; snackbar thành công | ⬜ |
| TC-SUP-04 | Xóa nhà cung cấp | 1. Nhấn icon **Xóa**<br>2. Confirm dialog: *"Bạn có chắc chắn muốn ngừng hợp tác với nhà cung cấp ... không?"*<br>3. Nhấn **Xác nhận** | NCC biến khỏi bảng; tổng đếm giảm 1 | ⬜ |
| TC-SUP-05 | Tìm kiếm theo tên | 1. Nhập "ABC" vào ô tìm kiếm<br>2. Chờ debounce ~400ms | Bảng chỉ hiện NCC tên chứa "ABC" | ⬜ |
| TC-SUP-06 | Tìm kiếm theo số điện thoại | 1. Nhập "0912" vào ô tìm kiếm | Bảng hiện NCC có SĐT bắt đầu bằng "0912" | ⬜ |
| TC-SUP-07 | Phân trang | 1. Đổi page size về 5 (hoặc có >10 NCC)<br>2. Chuyển trang 2<br>3. Thử page size 25 | Phân trang hoạt động; info *"X-Y / Z nhà cung cấp"* đúng | ⬜ |
| TC-SUP-08 | Sắp xếp theo tên | 1. Nhấn header **Tên nhà cung cấp** lần 1 → A→Z<br>2. Nhấn lần 2 → Z→A | Bảng sort đúng chiều | ⬜ |

---

### Edge Cases & Validation

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|---------------|------------------|----|
| TC-SUP-09 | Tên rỗng | 1. Mở dialog Thêm<br>2. Để trống Tên<br>3. Nhấn **Lưu** | Lỗi: *"Tên nhà cung cấp không được để trống."* | ⬜ |
| TC-SUP-10 | Số điện thoại rỗng | 1. Điền Tên nhưng để trống SĐT<br>2. Nhấn **Lưu** | Lỗi: *"Số điện thoại không được để trống."* | ⬜ |
| TC-SUP-11 | SĐT chứa chữ cái | 1. Nhập SĐT: `0912abc345`<br>2. Nhấn **Lưu** | Lỗi: *"Số điện thoại chỉ được chứa chữ số."* | ⬜ |
| TC-SUP-12 | SĐT chứa dấu đặc biệt | 1. Nhập SĐT: `+84912345678` (có dấu +)<br>2. Nhấn **Lưu** | Lỗi regex; không cho submit | ⬜ |
| TC-SUP-13 | Email tùy chọn — bỏ trống vs sai định dạng | 1. Để trống Email → Lưu → OK<br>2. Nhập email `contact-abc` (không có @) → Lưu | Trường hợp 1: thành công. Trường hợp 2: lỗi *"Email không đúng định dạng."* | ⬜ |
| TC-SUP-14 | Tên >200 ký tự | 1. Paste 201 ký tự vào Tên<br>2. Nhấn **Lưu** | Lỗi: *"Tên nhà cung cấp không được vượt quá 200 ký tự."* | ⬜ |
| TC-SUP-15 | SĐT >20 ký tự | 1. Nhập 21 chữ số liên tiếp vào SĐT<br>2. Nhấn **Lưu** | Lỗi: *"Số điện thoại không được vượt quá 20 ký tự."* | ⬜ |
| TC-SUP-16 | Mã số thuế >20 ký tự | 1. Nhập 21 ký tự vào Mã số thuế<br>2. Nhấn **Lưu** | Lỗi: *"Mã số thuế không được vượt quá 20 ký tự."* | ⬜ |
| TC-SUP-17 | Field tùy chọn null → hiển thị dash | 1. Tạo NCC chỉ có tên + SĐT<br>2. Xem hàng trong bảng | Cột Người liên hệ, Email, Mã số thuế đều hiển thị **—** | ⬜ |
| TC-SUP-18 | Cancel xóa | 1. Nhấn Xóa → Confirm dialog → **Hủy** | NCC không bị xóa | ⬜ |
| TC-SUP-19 | Tìm kiếm không có kết quả | 1. Nhập từ khóa không tồn tại | Alert: *"Không tìm thấy nhà cung cấp nào."* | ⬜ |
| TC-SUP-20 | Đóng dialog bằng Esc | 1. Mở dialog Thêm/Sửa bất kỳ<br>2. Nhấn **Esc** | Dialog đóng; không có thay đổi được lưu | ⬜ |
