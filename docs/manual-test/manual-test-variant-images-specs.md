# Manual Test: Ảnh & Thông số kỹ thuật theo từng Biến thể

Tài liệu này bao gồm tất cả các flow kiểm thử thủ công cho tính năng **mỗi biến thể có ảnh và thông số kỹ thuật riêng**, trải dài trên Admin, Storefront và POS.

**Yêu cầu trước khi test:**
- Đăng nhập đúng role tương ứng với từng nhóm (Admin / Customer / Employee)
- API + Client đang chạy đồng thời
- Trình duyệt có DevTools để kiểm tra URL và mạng khi cần

**Ký hiệu kết quả:** ✅ Pass &nbsp;|&nbsp; ❌ Fail &nbsp;|&nbsp; ⬜ Chưa test

---

## Dữ liệu cần chuẩn bị sẵn

| Ký hiệu | Mô tả |
|---------|-------|
| `prod_2var` | Sản phẩm có đúng 2 biến thể, mỗi biến thể đã có ảnh **khác nhau** và specs **khác nhau** (tạo thủ công trong bước chuẩn bị) |
| `prod_1var` | Sản phẩm chỉ có đúng 1 biến thể |
| `var_A` | Biến thể thứ nhất của `prod_2var` (SKU = "VAR-A", có ≥ 2 ảnh, có ≥ 1 spec) |
| `var_B` | Biến thể thứ hai của `prod_2var` (SKU = "VAR-B", có ảnh **khác** `var_A`, có spec **khác** `var_A`) |
| `prod_noimg` | Sản phẩm có 1 biến thể nhưng biến thể đó **không có ảnh** nào |
| `img_valid_A` | File ảnh JPG/PNG/WebP ≤ 5 MB (dùng cho `var_A`) |
| `img_valid_B` | File ảnh **khác** `img_valid_A`, ≤ 5 MB (dùng cho `var_B`) |
| `img_large` | File ảnh > 5 MB |
| `file_invalid` | File không phải ảnh (VD: `.pdf`) |
| `serial_A` | Mã serial thuộc `var_A` của `prod_2var` |

---

## NHÓM A — Admin: Tạo sản phẩm với ảnh & specs cấp biến thể

---

### A-1 — Tạo sản phẩm 2 biến thể, mỗi biến thể có ảnh và specs riêng

> Điểm bắt đầu: `/admin/products/create`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Điền Tên sản phẩm, chọn Danh mục và Hãng | Không lỗi tức thì | ✅ |
| 2 | Nhấn **Thêm biến thể** lần 1; điền SKU = "VAR-A", Tên = "Phiên bản A", Giá = 10.000.000 | Khối Biến thể 1 xuất hiện | ⬜ |
| 3 | Trong khối Biến thể 1, nhấn vùng upload ảnh → chọn `img_valid_A` | Ảnh preview xuất hiện **trong khối Biến thể 1**; badge **Main** tự gán; khối Biến thể 2 (chưa có) không bị ảnh hưởng | ⬜ |
| 4 | Trong khối Biến thể 1, nhấn **Thêm thông số** → nhập Key = "CPU" / Value = "Intel i5" | Hàng spec xuất hiện **trong khối Biến thể 1** | ✅ |
| 5 | Nhấn **Thêm biến thể** lần 2; điền SKU = "VAR-B", Tên = "Phiên bản B", Giá = 12.000.000 | Khối Biến thể 2 xuất hiện; ảnh và spec của Biến thể 1 không bị xáo trộn | ✅(Không confirm về ảnh) |
| 6 | Trong khối Biến thể 2, upload `img_valid_B` | Ảnh xuất hiện **trong khối Biến thể 2**; khối Biến thể 1 vẫn hiện `img_valid_A` | ⬜ |
| 7 | Trong khối Biến thể 2, thêm spec Key = "CPU" / Value = "AMD Ryzen 7" | Spec xuất hiện **trong khối Biến thể 2**; Biến thể 1 vẫn giữ "Intel i5" | ✅ |
| 8 | Nhấn **Tạo sản phẩm** | Snackbar thành công; chuyển về danh sách; cột **Biến thể** hiện "2" | ✅ |
| 9 | Vào trang edit sản phẩm vừa tạo | Biến thể 1 hiện đúng ảnh `img_valid_A` + spec "Intel i5"; Biến thể 2 hiện đúng ảnh `img_valid_B` + spec "AMD Ryzen 7"; **không bị lẫn** | ✅(Không confirm về ảnh) |

---

### A-2 — Tạo biến thể không có ảnh và không có specs

> Điểm bắt đầu: `/admin/products/create`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Điền thông tin sản phẩm và thêm 1 biến thể | Không lỗi | ✅ |
| 2 | Điền SKU, Tên, Giá cho biến thể nhưng **không upload ảnh** và **không thêm spec** | Không có lỗi tức thì trong UI | ✅ |
| 3 | Nhấn **Tạo sản phẩm** | Tạo thành công (ảnh và specs không bắt buộc) | ✅ |
| 4 | Vào trang edit | Phần ảnh của biến thể trống; phần specs hiện "*Chưa có thông số nào*" | ✅ |

---

## NHÓM B — Admin: Chỉnh sửa ảnh biến thể (độc lập giữa các biến thể)

---

### B-1 — Thay ảnh của Biến thể A, kiểm tra Biến thể B không bị ảnh hưởng

> Điểm bắt đầu: Trang edit `prod_2var`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Mở trang edit `prod_2var` | Biến thể A hiện ảnh cũ của nó; Biến thể B hiện ảnh cũ của nó | ⬜ |
| 2 | Trong khối `var_A`, xóa ảnh hiện tại (nhấn ✕) | Ảnh biến mất **chỉ** trong khối `var_A`; khối `var_B` không thay đổi | ⬜ |
| 3 | Trong khối `var_A`, upload ảnh mới (`img_valid_A`) | Ảnh preview xuất hiện trong khối `var_A`; `var_B` vẫn có ảnh cũ | ⬜ |
| 4 | Nhấn **Cập nhật** | Snackbar thành công | ⬜ |
| 5 | Reload trang edit (`F5` hoặc nhấn lại từ danh sách) | `var_A` hiện ảnh mới; `var_B` hiện đúng ảnh của nó — **không bị ghi đè** | ⬜ |

---

### B-2 — Đặt ảnh chính (Main) cho biến thể

> Điểm bắt đầu: Trang edit `prod_2var`, `var_A` có ≥ 2 ảnh

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Quan sát khối `var_A` | Ảnh đầu tiên có badge **Main** màu xanh; các ảnh còn lại có icon ⭐ để đặt làm chính | ⬜ |
| 2 | Nhấn icon ⭐ trên ảnh thứ 2 | Badge **Main** chuyển sang ảnh thứ 2; ảnh thứ 2 tự động lên vị trí đầu; ảnh thứ 1 mất badge | ⬜ |
| 3 | Nhấn **Cập nhật** | Thành công | ⬜ |
| 4 | Reload trang edit | Ảnh từng được chọn làm Main vẫn ở vị trí đầu với badge **Main** | ⬜ |

---

### B-3 — Upload nhiều ảnh cùng lúc cho một biến thể

> Điểm bắt đầu: Trang edit `prod_2var`, khối `var_A` đang trống ảnh

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Click vùng upload của `var_A` → chọn 3 file `img_valid_A` (multi-select) | 3 ảnh preview xuất hiện; ảnh đầu tự có badge **Main** | ⬜ |
| 2 | Xóa ảnh thứ 2 | Preview còn 2 ảnh; badge **Main** không bị ảnh hưởng nếu không xóa ảnh Main | ⬜ |
| 3 | Nhấn **Cập nhật** | Thành công | ⬜ |
| 4 | Reload trang edit | Đúng 2 ảnh; đúng ảnh Main | ⬜ |

---

## NHÓM C — Admin: Chỉnh sửa thông số kỹ thuật biến thể (độc lập)

---

### C-1 — Sửa specs của Biến thể A, kiểm tra Biến thể B không bị ảnh hưởng

> Điểm bắt đầu: Trang edit `prod_2var`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Mở trang edit `prod_2var` | `var_A` hiện spec "CPU: Intel i5"; `var_B` hiện spec "CPU: AMD Ryzen 7" | ✅ |
| 2 | Trong khối `var_A`, sửa Value của "CPU" thành "Intel i9" | Chỉ ô của `var_A` thay đổi | ✅ |
| 3 | Trong khối `var_A`, thêm spec mới Key = "RAM" / Value = "16GB" | Hàng xuất hiện **chỉ** trong khối `var_A` | ✅ |
| 4 | Nhấn **Cập nhật** | Thành công | ✅ |
| 5 | Reload trang edit | `var_A` có "CPU: Intel i9" + "RAM: 16GB"; `var_B` vẫn chỉ có "CPU: AMD Ryzen 7" — **không bị lẫn** | ✅ |

---

### C-2 — Xóa toàn bộ specs của một biến thể

> Điểm bắt đầu: Trang edit, `var_A` có 2 specs

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Xóa từng spec của `var_A` bằng icon xóa | Mỗi hàng biến mất sau khi xóa; `var_B` không bị ảnh hưởng | ✅ |
| 2 | Nhấn **Cập nhật** | Thành công | ✅ |
| 3 | Reload trang edit | `var_A` hiện "*Chưa có thông số nào*"; `var_B` vẫn giữ specs cũ | ✅ |

---

## NHÓM D — Admin: Quản lý biến thể (thêm, xóa)

---

### D-1 — Thêm biến thể mới cho sản phẩm đã tồn tại

> Điểm bắt đầu: Trang edit `prod_2var` (đang có 2 biến thể)

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Nhấn **Thêm biến thể** | Khối "Biến thể 3" xuất hiện; ảnh và specs của 2 biến thể cũ không bị xáo trộn | ✅(Không confirm về ảnh) |
| 2 | Điền SKU = "VAR-C", Tên = "Phiên bản C", Giá = 15.000.000 | Không lỗi | ✅ |
| 3 | Upload ảnh cho biến thể mới | Ảnh chỉ xuất hiện trong khối Biến thể 3 | ⬜ |
| 4 | Nhấn **Cập nhật** | Thành công; cột Biến thể trong danh sách hiện "3" | ✅(Không confirm về ảnh) |

---

### D-2 — Xóa biến thể không phải biến thể duy nhất

> Điểm bắt đầu: Trang edit `prod_2var` (đang có 2 biến thể)

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Nhấn icon xóa trên khối `var_B` | Dialog xác nhận xuất hiện hoặc khối biến mất khỏi UI | ✅ |
| 2 | Xác nhận xóa | `var_B` biến mất; `var_A` vẫn giữ nguyên ảnh + specs | ✅(Không confirm về ảnh) |
| 3 | Nhấn **Cập nhật** | Thành công; cột Biến thể giảm xuống 1 | ✅ |

---

### D-3 — Edge case: Xóa biến thể duy nhất

> Điểm bắt đầu: Trang edit `prod_1var` (chỉ có 1 biến thể)

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Nhấn icon xóa trên biến thể duy nhất | Bị ngăn: cảnh báo *"Sản phẩm phải có ít nhất 1 biến thể."* hoặc nút xóa bị vô hiệu hóa | ⬜ |
| 2 | Thử submit / cập nhật | Không có gì bị xóa; biến thể vẫn còn | ⬜ |

---

## NHÓM E — Storefront: Gallery & Specs swap khi đổi biến thể

---

### E-1 — Kiểm tra gallery thay đổi khi chọn biến thể khác

> Điểm bắt đầu: Trang storefront detail của `prod_2var` (đang ở biến thể mặc định `var_A`)

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Vào trang detail `prod_2var` | Ảnh lớn (main) và thumbnails bên dưới là ảnh của **`var_A`** | ⬜ |
| 2 | Nhấn chip biến thể **VAR-B** | Ảnh lớn chuyển sang ảnh main của `var_B`; thumbnails cũng cập nhật thành ảnh của `var_B` | ⬜ |
| 3 | Nhấn chip biến thể **VAR-A** trở lại | Ảnh lớn và thumbnails quay về ảnh của `var_A` | ⬜ |
| 4 | Click vào thumbnail thứ 2 của `var_B` | Ảnh lớn đổi thành thumbnail đó | ⬜ |

---

### E-2 — Kiểm tra thông số kỹ thuật thay đổi khi đổi biến thể

> Điểm bắt đầu: Trang detail `prod_2var`, đang ở `var_A`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Quan sát bảng **Thông số kỹ thuật** | Hiện specs của `var_A` (VD: "CPU: Intel i5") | ✅ |
| 2 | Nhấn chip biến thể **VAR-B** | Bảng specs cập nhật thành specs của `var_B` (VD: "CPU: AMD Ryzen 7") — **không lẫn** với `var_A` | ✅ |
| 3 | Nhấn lại chip **VAR-A** | Bảng specs quay về của `var_A` | ✅ |

---

### E-3 — Kiểm tra giá, bảo hành thay đổi — mô tả không đổi

> Điểm bắt đầu: Trang detail `prod_2var`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Quan sát giá bán, số tháng bảo hành của `var_A` | Hiện đúng giá và bảo hành của `var_A` | ✅ |
| 2 | Nhấn chip **VAR-B** | Giá và bảo hành cập nhật theo `var_B`; **mô tả sản phẩm (Description) không thay đổi** | ✅ |
| 3 | Nhấn lại **VAR-A** | Giá và bảo hành trở về của `var_A`; Description vẫn giữ nguyên | ✅ |

---

### E-4 — Thumbnail nhỏ trên chip biến thể

> Điểm bắt đầu: Trang detail `prod_2var`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Quan sát khu vực chip chọn biến thể | Mỗi chip hiện ảnh thumbnail nhỏ (28×28px) tương ứng với ảnh main của biến thể đó | ⬜ |
| 2 | So sánh thumbnail chip `var_A` và chip `var_B` | 2 thumbnail **khác nhau** (phản ánh ảnh riêng của từng biến thể) | ⬜ |

---

### E-5 — Fallback khi biến thể không có ảnh

> Điểm bắt đầu: Trang detail `prod_noimg`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Vào trang detail `prod_noimg` | Gallery không crash; hiện placeholder ảnh hoặc ảnh mặc định thay vì màn hình trống/vỡ layout | ✅ |
| 2 | Bảng thông số kỹ thuật | Hiện "*Chưa có thông số nào*" hoặc ẩn bảng — không crash | ✅ |
| 3 | Nút **Thêm vào giỏ** vẫn khả dụng (nếu có tồn kho) | Người dùng vẫn có thể mua sản phẩm dù biến thể thiếu ảnh | ✅ |

---

## NHÓM F — Storefront: URL Deep Linking theo SKU biến thể

---

### F-1 — URL cập nhật khi đổi biến thể

> Điểm bắt đầu: Trang detail `prod_2var`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Vào trang detail `prod_2var` (không có query param) | Biến thể mặc định được chọn; URL chưa có `?variant=` hoặc có `?variant=VAR-A` | ✅ |
| 2 | Nhấn chip **VAR-B** | URL cập nhật thành `…?variant=VAR-B` (kiểm tra thanh địa chỉ) | ✅ |
| 3 | Nhấn chip **VAR-A** | URL đổi thành `…?variant=VAR-A` | ✅ |

---

### F-2 — Mở link với ?variant=SKU (deep link)

> Điểm bắt đầu: Copy URL từ F-1 bước 2 (`…?variant=VAR-B`)

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Mở URL `…?variant=VAR-B` trong tab mới | Trang load; chip **VAR-B** được chọn sẵn; gallery hiện ảnh của `var_B`; specs hiện của `var_B` | ✅ (không confirm về ảnh) |
| 2 | Nhấn F5 (refresh trang) | Sau refresh vẫn giữ đúng `var_B` được chọn | ✅ |

---

### F-3 — Deep link với SKU không tồn tại

> Điểm bắt đầu: Trình duyệt

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Nhập thủ công URL `…?variant=INVALID-SKU-999` vào thanh địa chỉ | Trang không crash; fallback về biến thể mặc định (thường là biến thể rẻ nhất) | ✅ |
| 2 | Quan sát gallery và specs | Gallery và specs của biến thể mặc định được hiển thị bình thường | ✅ |

---

## NHÓM G — POS: Ảnh biến thể thật

---

### G-1 — Thumbnail hiện đúng ảnh biến thể khi quét serial

> Điểm bắt đầu: Trang POS (`/pos`), đăng nhập Employee hoặc Admin

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Quét / nhập mã serial `serial_A` (thuộc `var_A` có ảnh) | Hàng sản phẩm xuất hiện trong bảng POS; cột ảnh hiển thị **thumbnail thật** của `var_A` (không phải placeholder) | ⬜ |
| 2 | So sánh thumbnail POS với ảnh Main trên trang storefront detail `var_A` | Cùng URL ảnh | ⬜ |

---

### G-2 — Fallback khi biến thể không có ảnh

> Điểm bắt đầu: Trang POS, cần serial thuộc `prod_noimg`

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Quét serial của biến thể **không có ảnh** | Hàng xuất hiện; cột ảnh hiện placeholder (VD: ô xám hoặc icon camera) — không crash, không URL gãy | ✅ |

---

## NHÓM H — Tính độc lập đầu cuối (End-to-end Independence)

> Các test trong nhóm này xác minh rằng thay đổi ảnh/specs của biến thể A **không bao giờ** ảnh hưởng đến biến thể B, từ lúc lưu trên Admin đến khi hiển thị trên Storefront.

---

### H-1 — Thay ảnh var_A trên Admin → Storefront var_B không bị đổi

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Trên Admin, vào edit `prod_2var`, upload ảnh mới cho `var_A` → Cập nhật | Thành công | ⬜ |
| 2 | Trên Storefront, vào trang detail `prod_2var` → chọn chip **VAR-B** | Gallery hiện ảnh của `var_B` — **không hiện ảnh mới vừa upload cho `var_A`** | ⬜ |
| 3 | Chuyển sang chip **VAR-A** | Gallery cập nhật thành ảnh mới của `var_A` | ⬜ |

---

### H-2 — Thay specs var_B trên Admin → Storefront var_A không bị đổi

| # | Hành động | Kết quả mong đợi | KQ |
|---|-----------|------------------|----|
| 1 | Trên Admin, sửa spec của `var_B` (đổi giá trị CPU) → Cập nhật | Thành công | ✅ |
| 2 | Trên Storefront, vào trang detail, chọn chip **VAR-A** | Bảng specs hiện giá trị cũ của `var_A` — **không bị ghi đè bởi `var_B`** | ✅ |
| 3 | Chuyển sang chip **VAR-B** | Bảng specs hiện giá trị mới vừa sửa của `var_B` | ✅(Sau khi reload lại trang) |

---

## NHÓM I — Edge Cases & Validation

---

### I-1 — Upload ảnh không hợp lệ

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-IMG-E01 | File > 5 MB | Upload `img_large` vào vùng ảnh của biến thể | Thông báo lỗi file quá lớn; ảnh không được thêm; upload ảnh khác vẫn hoạt động | ⬜ |
| TC-IMG-E02 | File không phải ảnh | Upload `file_invalid` (.pdf) | File bị từ chối; thông báo lỗi định dạng; preview không thay đổi | ⬜ |
| TC-IMG-E03 | Vượt quá 5 ảnh/biến thể | Upload 5 ảnh thành công → thử upload ảnh thứ 6 | Upload bị chặn hoặc cảnh báo đạt giới hạn; tổng không vượt 5 | ⬜ |
| TC-IMG-E04 | Đặt Main khi chỉ có 1 ảnh | Biến thể chỉ có 1 ảnh; icon ⭐ không xuất hiện (hoặc ảnh duy nhất đã là Main) | Badge **Main** luôn hiện; không có icon ⭐ (vì không có ảnh nào khác để chọn) | ⬜ |
| TC-IMG-E05 | Upload trong khi đang uploading | Trong lúc spinner "Đang upload..." đang hiện, thử click vùng upload lần nữa | Click bị chặn (vùng upload vô hiệu hóa); chỉ 1 batch upload chạy tại 1 thời điểm | ⬜ |

---

### I-2 — Thông số kỹ thuật

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-SPEC-E01 | Key rỗng, có Value | Thêm spec: Key trống, Value = "Intel" → Submit | Lỗi hoặc hàng bị bỏ qua — **ghi lại kết quả thực tế** | ✅(Hàng bị bỏ qua) |
| TC-SPEC-E02 | Key và Value đều rỗng | Thêm spec: để trống cả 2 ô → Submit | Hàng bị bỏ qua hoặc báo lỗi; không lưu spec rỗng | ✅(Hàng bị bỏ qua) |
| TC-SPEC-E03 | Key trùng trong cùng biến thể | Thêm 2 spec đều có Key = "CPU" → Submit | Ghi đè lên nhau hoặc báo lỗi trùng key — **ghi lại kết quả thực tế** | ✅(Tự xóa hàng sau khi nhập CPU) |
| TC-SPEC-E04 | Spec ở biến thể khác nhau có Key trùng | `var_A` có Key = "CPU"; `var_B` cũng có Key = "CPU" | Hợp lệ — mỗi biến thể có namespace specs riêng; cả 2 lưu thành công | ✅ |

---

### I-3 — Biến thể

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-VAR-E01 | Xóa biến thể duy nhất | Trên trang edit `prod_1var`, nhấn xóa biến thể duy nhất | Bị ngăn: cảnh báo *"Sản phẩm phải có ít nhất 1 biến thể."* | ⬜ |
| TC-VAR-E02 | SKU trùng trong cùng sản phẩm | Form tạo mới với 2 biến thể có cùng SKU → Submit | Lỗi trùng SKU (client-side hoặc server) | ✅ |
| TC-VAR-E03 | Xóa biến thể rồi hủy (không lưu) | Trang edit: xóa `var_B` khỏi UI → nhấn **Hủy** hoặc rời trang | Biến thể không bị xóa thật; reload lại trang edit vẫn thấy đủ 2 biến thể | ✅ |

---

### I-4 — Storefront edge cases

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-SF-E01 | Sản phẩm chỉ có 1 biến thể | Vào detail sản phẩm có 1 biến thể | Không hiện khu vực chọn biến thể (hoặc hiện 1 chip đã chọn sẵn); gallery và specs hiển thị bình thường | ✅(Hiện 1 chip chọn sẵn) |
| TC-SF-E02 | Biến thể không có specs | Chọn chip biến thể không có thông số kỹ thuật | Bảng specs ẩn hoặc hiện "*Không có thông số kỹ thuật.*" — không crash, không hiện bảng rỗng gây mất thẩm mỹ | ✅ |
| TC-SF-E03 | Ảnh main bị xóa, biến thể còn ảnh khác | Admin xóa ảnh Main của `var_A`, để lại ảnh không phải Main → Cập nhật | Storefront hiện ảnh còn lại (ảnh đầu theo sort order) làm main; không crash | ⬜ |
| TC-SF-E04 | Thumbnail trên card danh mục vẫn đúng | Vào trang danh mục/tìm kiếm sau khi thay ảnh biến thể đầu tiên | Card sản phẩm hiện thumbnail của biến thể đầu tiên có ảnh — không bị vỡ | ⬜ |

---

### I-5 — Concurrent & refresh

| ID | Mô tả | Bước thực hiện | Kết quả mong đợi | KQ |
|----|-------|----------------|------------------|----|
| TC-CONC-E01 | Admin thay ảnh trong lúc user đang xem storefront | Tab 1 (Admin): đổi ảnh `var_A` → Cập nhật. Tab 2 (Storefront): đang xem detail `prod_2var` → nhấn F5 | Sau F5, storefront hiện ảnh mới của `var_A` | ✅ |
| TC-CONC-E02 | Admin xóa biến thể trong lúc user xem deep link biến thể đó | Tab 1 (Admin): xóa `var_B`. Tab 2 (Storefront): reload URL `…?variant=VAR-B` | Fallback về biến thể mặc định; không crash | ✅ |
