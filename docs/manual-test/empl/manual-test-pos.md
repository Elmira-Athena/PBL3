# Manual Test Guide — Bán hàng tại quầy / POS (UC009)

> Truy cập trang POS tại `https://localhost:7107` bằng tài khoản **Employee** hoặc **Admin**.
> Mỗi flow bắt đầu từ trạng thái màn hình POS **sạch** (giỏ trống, chưa có khách, chưa có voucher).

---

## Thiết lập trước khi test

| Bước | Hành động |
|------|-----------|
| 1 | Khởi động hệ thống (API + Client + DB) |
| 2 | Đăng nhập bằng tài khoản Employee |
| 3 | Vào trang **Bán hàng tại quầy** |
| 4 | Chuẩn bị sẵn các mã serial hợp lệ và không hợp lệ (xem bảng bên dưới) |

### Dữ liệu cần chuẩn bị sẵn

| Ký hiệu | Trạng thái cần có |
|---------|------------------|
| `serial_A` | Sản phẩm có bảo hành (WarrantyMonth > 0), đang Available |
| `serial_B` | **Cùng variant** với serial_A, đang Available |
| `serial_C` | **Khác variant** với serial_A, đang Available |
| `serial_D` | Sản phẩm **không có bảo hành** (WarrantyMonth = 0), đang Available |
| `serial_SOLD` | Đã bán (Sold) |
| `serial_RESERVED` | Đang giữ cho đơn online (Reserved) |
| `serial_DEFECTIVE` | Hàng lỗi (Defective) |
| `phone_customer` | SĐT của tài khoản Customer đã đăng ký trong hệ thống |
| `voucher_FIXED` | Giảm cố định (VD: 50.000đ), còn hiệu lực, còn lượt dùng, ApplyFor = POS hoặc Cả hai |
| `voucher_PCT` | Giảm phần trăm (VD: 10%), có MaxDiscountAmount, còn hiệu lực |
| `voucher_EXPIRED` | Đã hết hạn |
| `voucher_FUTURE` | Chưa đến ngày bắt đầu |
| `voucher_USED_UP` | Đã dùng hết lượt (UsedCount >= Quantity) |
| `voucher_MIN_ORDER` | MinOrderValue cao hơn giá 1 sản phẩm đơn lẻ |
| `voucher_ONLINE_ONLY` | ApplyFor = Online only |
| `voucher_INACTIVE` | Đã bị tắt (IsActive = false) |

---

## NHÓM 1 — Happy Paths

---

### Flow 1 — Bán 1 sản phẩm cho khách vãng lai, tiền mặt

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Gõ `serial_A` vào ô barcode, nhấn **Enter** | Sản phẩm xuất hiện trong giỏ hàng: tên, serial, giá đúng |
| 2 | Không nhập số điện thoại khách hàng | Khu vực khách hàng không hiển thị thông tin |
| 3 | Chọn phương thức **Tiền mặt** | Radio button Tiền mặt được chọn |
| 4 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thông báo thành công, màn hình trở về trạng thái sạch |

---

### Flow 2 — Tìm khách hàng theo số điện thoại trước khi thanh toán

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Gõ `serial_A` vào ô barcode, nhấn **Enter** | Sản phẩm vào giỏ |
| 2 | Nhập `phone_customer` vào ô số điện thoại, nhấn tìm kiếm | Tên và email khách hiện ra bên dưới ô SĐT |
| 3 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công, đơn hàng được liên kết với khách hàng đó |

---

### Flow 3 — Bán nhiều sản phẩm khác variant, thanh toán chuyển khoản

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Gõ `serial_A`, nhấn **Enter** | Sản phẩm A vào giỏ |
| 2 | Gõ `serial_C`, nhấn **Enter** | Sản phẩm C vào giỏ — giỏ có 2 dòng khác nhau |
| 3 | Kiểm tra tổng tiền hiển thị | Bằng giá A + giá C |
| 4 | Chọn phương thức **Chuyển khoản / QR** | — |
| 5 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công |

---

### Flow 4 — Bán 2 sản phẩm cùng variant

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Gõ `serial_A`, nhấn **Enter** | Sản phẩm A vào giỏ |
| 2 | Gõ `serial_B` (cùng variant với A), nhấn **Enter** | Sản phẩm B vào giỏ |
| 3 | Kiểm tra giỏ hàng | 2 dòng riêng biệt (mỗi serial 1 dòng) hoặc gộp thành 1 dòng số lượng 2 — tùy UI render |
| 4 | Tổng tiền = giá A + giá B | — |
| 5 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công |

---

### Flow 5 — Áp dụng voucher giảm giá cố định

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Gõ `serial_A`, nhấn **Enter** | Sản phẩm vào giỏ, SubTotal hiển thị |
| 2 | Nhập `voucher_FIXED` vào ô mã giảm giá, nhấn **Áp dụng** | Dòng "Giảm giá" xuất hiện với số tiền giảm đúng; "Cần thanh toán" giảm tương ứng |
| 3 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công, tổng tiền sau giảm đúng |

---

### Flow 6 — Áp dụng voucher giảm phần trăm (có giới hạn tối đa)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Thêm sản phẩm có giá cao vào giỏ sao cho 10% × giá > MaxDiscountAmount | SubTotal hiển thị |
| 2 | Nhập `voucher_PCT`, nhấn **Áp dụng** | Dòng giảm giá hiển thị **đúng bằng MaxDiscountAmount** (bị cap), không phải 10% thực tế |
| 3 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công |

---

### Flow 7 — Thanh toán quẹt thẻ

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Thêm sản phẩm vào giỏ | — |
| 2 | Chọn phương thức **Quẹt thẻ** | Radio button Quẹt thẻ được chọn |
| 3 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công |

---

### Flow 8 — Xóa sản phẩm khỏi giỏ trước khi thanh toán

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Thêm `serial_A` và `serial_C` vào giỏ | Giỏ có 2 sản phẩm |
| 2 | Nhấn nút xóa (🗑) trên dòng `serial_A` | Dòng serial_A biến mất, chỉ còn serial_C |
| 3 | Tổng tiền cập nhật đúng | Bằng giá serial_C |
| 4 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công với 1 sản phẩm |

---

### Flow 9 — Nhập ghi chú nhân viên

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Thêm sản phẩm vào giỏ | — |
| 2 | Nhập ghi chú vào ô "Ghi chú" (nếu có trên UI) | — |
| 3 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công |

---

## NHÓM 2 — Serial Edge Cases

---

### Flow 10 — Quét serial không tồn tại

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập chuỗi bất kỳ không phải serial hợp lệ vào ô barcode, nhấn **Enter** | Thông báo lỗi (toast/snackbar): "Không tìm thấy..." |
| 2 | Giỏ hàng | Không thay đổi, không thêm dòng mới |

---

### Flow 11 — Quét serial đã bán

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `serial_SOLD` vào ô barcode, nhấn **Enter** | Thông báo lỗi tiếng Việt (đã bán / không khả dụng) |
| 2 | Giỏ hàng | Không thay đổi |

---

### Flow 12 — Quét serial đang được giữ cho đơn online (Reserved)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `serial_RESERVED` vào ô barcode, nhấn **Enter** | Thông báo lỗi tiếng Việt |
| 2 | Giỏ hàng | Không thay đổi |

---

### Flow 13 — Quét serial hàng lỗi (Defective)

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `serial_DEFECTIVE` vào ô barcode, nhấn **Enter** | Thông báo lỗi tiếng Việt |
| 2 | Giỏ hàng | Không thay đổi |

---

### Flow 14 — Quét trùng serial đã có trong giỏ

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `serial_A`, nhấn **Enter** | Thêm vào giỏ |
| 2 | Nhập lại đúng `serial_A`, nhấn **Enter** | Cảnh báo hoặc không thêm — giỏ vẫn chỉ có 1 dòng serial_A |

---

## NHÓM 3 — Customer Edge Cases

---

### Flow 15 — Tìm khách hàng không có trong hệ thống

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập số điện thoại không tồn tại vào ô SĐT, nhấn tìm kiếm | Thông báo "Không tìm thấy khách hàng" |
| 2 | Ô thông tin khách | Trống, không hiển thị tên |
| 3 | Vẫn có thể tiến hành checkout bình thường | Đơn được tạo dưới dạng khách vãng lai |

---

### Flow 16 — Xóa thông tin khách sau khi đã tìm thấy

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Tìm `phone_customer` thành công — tên khách hiện ra | — |
| 2 | Xóa nội dung ô SĐT | Thông tin khách biến mất hoặc không còn liên kết |
| 3 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Đơn được tạo như khách vãng lai |

---

## NHÓM 4 — Voucher Edge Cases

---

### Flow 17 — Nhập sai mã voucher

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Thêm sản phẩm vào giỏ | — |
| 2 | Nhập mã ngẫu nhiên không tồn tại, nhấn **Áp dụng** | Thông báo lỗi tiếng Việt, không áp dụng discount |
| 3 | "Cần thanh toán" | Không thay đổi, bằng SubTotal |

---

### Flow 18 — Voucher đã hết hạn

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `voucher_EXPIRED`, nhấn **Áp dụng** | Thông báo lỗi "đã hết hạn" |

---

### Flow 19 — Voucher chưa đến ngày áp dụng

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `voucher_FUTURE`, nhấn **Áp dụng** | Thông báo lỗi "chưa đến thời gian sử dụng" |

---

### Flow 20 — Voucher đã hết lượt sử dụng

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `voucher_USED_UP`, nhấn **Áp dụng** | Thông báo lỗi "đã hết lượt sử dụng" |

---

### Flow 21 — Đơn hàng chưa đạt giá trị tối thiểu của voucher

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Thêm 1 sản phẩm có giá thấp hơn MinOrderValue của `voucher_MIN_ORDER` | SubTotal hiển thị |
| 2 | Nhập `voucher_MIN_ORDER`, nhấn **Áp dụng** | Thông báo lỗi "chưa đạt giá trị tối thiểu" |

---

### Flow 22 — Voucher chỉ dành cho kênh Online

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `voucher_ONLINE_ONLY`, nhấn **Áp dụng** | Thông báo lỗi (không áp dụng được tại quầy) |

---

### Flow 23 — Voucher bị vô hiệu hóa

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Nhập `voucher_INACTIVE`, nhấn **Áp dụng** | Thông báo lỗi tiếng Việt |

---

### Flow 24 — Bỏ voucher sau khi đã áp dụng

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Áp dụng `voucher_FIXED` thành công — dòng giảm giá hiện ra | — |
| 2 | Xóa nội dung ô mã giảm giá (hoặc nhấn nút bỏ voucher nếu có) | Dòng giảm giá biến mất, "Cần thanh toán" trở về bằng SubTotal |
| 3 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Thành công, không có discount |

---

## NHÓM 5 — Cart Edge Cases

---

### Flow 25 — Nhấn thanh toán khi giỏ trống

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Không quét serial nào, giỏ hàng đang trống | — |
| 2 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Cảnh báo "Chưa có sản phẩm trong giỏ" — **không tạo đơn** |

---

### Flow 26 — Thêm nhiều sản phẩm rồi xóa hết trước khi checkout

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Thêm `serial_A` và `serial_C` vào giỏ | 2 dòng trong giỏ |
| 2 | Xóa từng dòng | Giỏ trống, tổng tiền về 0 |
| 3 | Nhấn **HOÀN TẤT ĐƠN HÀNG** | Cảnh báo, không tạo đơn |

---

## NHÓM 6 — Warranty (Kiểm tra kết quả)

---

### Flow 27 — Sản phẩm có bảo hành sau khi bán thành công

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Bán `serial_A` (sản phẩm có bảo hành) thành công | Thông báo thành công |
| 2 | Vào trang quản lý bảo hành (nếu có) | Xuất hiện bản ghi bảo hành mới cho serial_A: ngày bắt đầu = hôm nay, ngày kết thúc đúng theo số tháng bảo hành |

---

### Flow 28 — Sản phẩm không có bảo hành

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Bán `serial_D` (sản phẩm không có bảo hành) thành công | Thông báo thành công |
| 2 | Vào trang quản lý bảo hành | Không có bản ghi bảo hành nào cho serial_D |

---

## NHÓM 7 — Authorization

---

### Flow 29 — Tài khoản Customer cố truy cập trang POS

| # | Hành động | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Đăng xuất, đăng nhập lại bằng tài khoản **Customer** | — |
| 2 | Truy cập URL trang POS trực tiếp | Bị chuyển hướng về trang đăng nhập hoặc trang lỗi 403 — không vào được giao diện POS |
