# LoadProbe — đủ 9 kịch bản, 2 instance, sau mục 🅴

> **Vì sao có file này.** Runbook có một cảnh báo cứng: *"Chạy cả hai cấu hình. S04 ĐẠT với
> 1 instance và HỎNG với 2 — kết luận từ một cấu hình là kết luận sai."* File
> [`2026-08-31-du-9-kich-ban-sau-muc-D.md`](2026-08-31-du-9-kich-ban-sau-muc-D.md) chỉ phủ
> nửa 1 instance; đây là nửa còn lại, chạy qua nginx round-robin trên
> `devops/docker/docker-compose.multi.yml`.
>
> **Kết quả: 5 ĐẠT, 4 HỎNG, 0 KHÔNG KẾT LUẬN** — HỎNG là S01/S06/S08/S09.

- **Hạ tầng:** 2 replica API (`hushstore_api_1`, `hushstore_api_2`) + nginx, cùng DB dev
- **Round-robin đã kiểm TRƯỚC khi đo:** 10 request → **5/5** chia đều hai upstream
  (`172.21.0.2` / `172.21.0.3`), 0 lần nginx phải thử lại

> ⚠️ **Kiểm round-robin ngay trước khi đo là bắt buộc, không phải nghi thức** (bẫy #9). Và
> lần này nó bắt được một chuyện thật: đo **quá sớm** sau `compose up`, header `X-Upstream`
> trả về **hai** địa chỉ mỗi phản hồi (`172.21.0.2:8080, 172.21.0.3:8080`) — đó là
> `$upstream_addr` liệt kê **chuỗi đã thử**, tức replica thứ nhất chưa kịp khởi động, nginx
> thất bại rồi chuyển sang replica thứ hai. Đo trong cửa sổ đó thì mỗi request đi qua **cả
> hai** container, và kết luận về tính chất đa-instance là vô nghĩa. Chờ ấm rồi kiểm lại mới
> ra 5/5 sạch. **Một địa chỉ mỗi phản hồi mới là round-robin; hai địa chỉ là retry.**

---

## 🔴 S04 KHÔNG tái hiện lần này — và đó KHÔNG phải tin tốt

| Lần chạy | 1 instance | 2 instance |
|---|---|---|
| Mục C (đợt trước) | ✅ | **🔴** |
| Phiên này | ✅ | **✅** |

`HasOpenTicketForSerialAsync` **không đổi một dòng nào** giữa hai lần chạy — mục 🅴 chỉ sửa
thông báo lỗi trong `OrderService`. Nên cái ✅ này **không phải bằng chứng đã sửa**; nó là
bằng chứng S04 **phụ thuộc thời điểm**, đúng cùng loại với cảnh báo "S07 ĐẠT nhưng tín hiệu
yếu". Cửa sổ check-then-act của nó hẹp, và việc hai tiến trình có chen được vào đúng cửa sổ
đó hay không là chuyện xác suất.

**Hệ quả thực dụng:** ✅ một lần ở S04 và S07 không được tính là chứng cứ an toàn. Muốn kết
luận thì phải chạy lặp nhiều lần và đếm tỉ lệ. Ngược lại, một lần 🔴 **là** chứng cứ hỏng —
bất đối xứng này là điều cần nhớ khi đọc mọi bảng trong thư mục này.

Vì vậy con số tổng của mục 🅵 nên đọc là **"5/9 bất biến ĐÃ TỪNG sai"**, không phải
"5/9 đang sai": hợp của mọi lần chạy, chứ không phải ảnh chụp của một lần.

---

- **Thời điểm chạy:** 2026-08-31 21:25:49 (giờ máy)
- **API:** `http://localhost:8088`
- **Giãn cách giữa hai kịch bản:** 11s

> Cột **Kết luận** đọc như sau: `ĐẠT` = bất biến đúng. `HỎNG` = bất biến sai,
> có lỗi đúng đắn dữ liệu. `KHÔNG KẾT LUẬN` = phép đo bị rỗng (bị rate limiter
> chặn, seed thiếu, API không phản hồi) — **không được đọc thành "đạt"**.

| # | Kịch bản | Bất biến kiểm sau | Mã HTTP | Kết luận |
|---|---|---|---|---|
| S01 | 50 khách checkout đồng thời | Không có OrderCode trùng, đủ 50 đơn, 0 lỗi 5xx | 200×16, 400×34 | 🔴 HỎNG |
| S02 | Voucher Quantity = 1, 20 khách dùng đồng thời | Voucher.UsedCount = 1 và COUNT(VoucherUsages) = 1 | 200×1, 400×19 | ✅ ĐẠT |
| S03 | Cùng một khách, MaxUsesPerUser = 1, 10 request | COUNT(VoucherUsages WHERE UserId = khách AND VoucherId = voucher) = 1 | 200×1, 400×9 | ✅ ĐẠT |
| S04 | 10 lần tiếp nhận cùng một serial | Đúng 1 phiếu dịch vụ chưa đóng cho serial đó | 200×10 | ✅ ĐẠT |
| S05 | 10 lần duyệt cùng một báo giá | Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2 | 200×10 | ✅ ĐẠT |
| S06 | 5 lần phê duyệt cùng một phiếu kiểm kê | InventoryAdjustmentLogs không nhân đôi: đúng 1 bản ghi cho mỗi serial | 200×5 | 🔴 HỎNG |
| S07 | POS bán serial S xen kẽ kiểm kê đánh S thất thoát | Nếu serial đã bán thì Status phải khác Lost (5) | 200×1, 400×1 | ✅ ĐẠT |
| S08 | 2 lần lập báo giá song song trên một phiếu | Đúng 1 báo giá ở trạng thái Pending (0) | 200×2 | 🔴 HỎNG |
| S09 | 2 lời gọi refresh-token cùng một cặp | Đúng 1 lời gọi thành công, và hash trong DB khớp token đã trả cho lời gọi đó | 200×2 | 🔴 HỎNG |

## Chi tiết từng kịch bản

### S01 — 50 khách checkout đồng thời

**Bất biến:** Không có OrderCode trùng, đủ 50 đơn, 0 lỗi 5xx

**Kết luận:** 🔴 HỎNG — Chỉ tạo được 16/50 đơn.

- Đơn tạo được: 16/50
- Mã đơn phân biệt: 16
- Mã HTTP: 200×16, 400×34

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.","data":null}
```

*Thời gian: 15,1s*

### S02 — Voucher Quantity = 1, 20 khách dùng đồng thời

**Bất biến:** Voucher.UsedCount = 1 và COUNT(VoucherUsages) = 1

**Kết luận:** ✅ ĐẠT — Đúng 1 lượt được tiêu thụ.

- Voucher.UsedCount = 1 (kỳ vọng 1)
- COUNT(VoucherUsages) = 1 (kỳ vọng 1)
- Mã HTTP: 200×1, 400×19

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.","data":null}
```

*Thời gian: 14,4s*

### S03 — Cùng một khách, MaxUsesPerUser = 1, 10 request

**Bất biến:** COUNT(VoucherUsages WHERE UserId = khách AND VoucherId = voucher) = 1

**Kết luận:** ✅ ĐẠT — Đúng 1 lượt cho mỗi người.

- COUNT(VoucherUsages) cho cặp (khách, voucher) = 1 (kỳ vọng 1)
- Mã HTTP: 200×1, 400×9

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.","data":null}
```

*Thời gian: 5,0s*

### S04 — 10 lần tiếp nhận cùng một serial

**Bất biến:** Đúng 1 phiếu dịch vụ chưa đóng cho serial đó

**Kết luận:** ✅ ĐẠT — Đúng 1 phiếu chưa đóng.

- Phiếu chưa đóng cho serial: 1 (ST-20260831-000001)
- Mã HTTP: 200×10

*Thời gian: 13,2s*

### S05 — 10 lần duyệt cùng một báo giá

**Bất biến:** Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2

**Kết luận:** ✅ ĐẠT — Đúng một lần duyệt được ghi nhận.

- Quotation.Status = 1 (kỳ vọng 1 = Accepted)
- Bản ghi lịch sử từ trạng thái 2: 1 (kỳ vọng 1)
- Mã HTTP: 200×10

*Thời gian: 3,3s*

### S06 — 5 lần phê duyệt cùng một phiếu kiểm kê

**Bất biến:** InventoryAdjustmentLogs không nhân đôi: đúng 1 bản ghi cho mỗi serial

**Kết luận:** 🔴 HỎNG — Sổ tổn thất có 5 bản ghi cho cùng một serial trong cùng một phiếu.

- InventoryAdjustmentLogs cho (phiếu, serial) = 5 (kỳ vọng 1)
- ProductSerial.Status = 5 (kỳ vọng 5 = Lost)
- InventoryCheck.Status = 2 (kỳ vọng 2 = Completed)
- Mã HTTP: 200×5

*Thời gian: 9,7s*

### S07 — POS bán serial S xen kẽ kiểm kê đánh S thất thoát

**Bất biến:** Nếu serial đã bán thì Status phải khác Lost (5)

**Kết luận:** ✅ ĐẠT — Serial không bán được và được ghi nhận thất thoát — nhất quán.

- ProductSerial.Status = 5
- Đã gắn vào đơn bán (OrderSerials): không
- Mã HTTP: 200×1, 400×1

*Thời gian: 0,4s*

### S08 — 2 lần lập báo giá song song trên một phiếu

**Bất biến:** Đúng 1 báo giá ở trạng thái Pending (0)

**Kết luận:** 🔴 HỎNG — Phiếu có 2 báo giá cùng ở trạng thái Pending.

- Tổng báo giá trên phiếu: 2
- Báo giá đang Pending: 2 (kỳ vọng 1)
- Mã HTTP: 200×2

*Thời gian: 0,5s*

### S09 — 2 lời gọi refresh-token cùng một cặp

**Bất biến:** Đúng 1 lời gọi thành công, và hash trong DB khớp token đã trả cho lời gọi đó

**Kết luận:** 🔴 HỎNG — 1 client nhận được refresh token đã bị vô hiệu ngay lúc cấp — lần refresh kế tiếp của họ sẽ bị đá về trang đăng nhập.

- Số cặp token được cấp: 2 (kỳ vọng 1)
- Số client cầm refresh token đã chết ngay lúc nhận: 1 (kỳ vọng 0)
- Mã HTTP: 200×2

*Thời gian: 0,9s*

