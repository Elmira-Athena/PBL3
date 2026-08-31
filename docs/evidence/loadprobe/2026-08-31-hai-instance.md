# LoadProbe — kết quả đo tính đúng đắn dưới tải đồng thời

- **Thời điểm chạy:** 2026-08-31 16:57:30 (giờ máy)
- **API:** `http://localhost:8088`
- **Giãn cách giữa hai kịch bản:** 11s

> Cột **Kết luận** đọc như sau: `ĐẠT` = bất biến đúng. `HỎNG` = bất biến sai,
> có lỗi đúng đắn dữ liệu. `KHÔNG KẾT LUẬN` = phép đo bị rỗng (bị rate limiter
> chặn, seed thiếu, API không phản hồi) — **không được đọc thành "đạt"**.

| # | Kịch bản | Bất biến kiểm sau | Mã HTTP | Kết luận |
|---|---|---|---|---|
| S01 | 50 khách checkout đồng thời | Không có OrderCode trùng, đủ 50 đơn, 0 lỗi 5xx | 200×13, 400×37 | 🔴 HỎNG |
| S02 | Voucher Quantity = 1, 20 khách dùng đồng thời | Voucher.UsedCount = 1 và COUNT(VoucherUsages) = 1 | 200×1, 400×19 | ✅ ĐẠT |
| S03 | Cùng một khách, MaxUsesPerUser = 1, 10 request | COUNT(VoucherUsages WHERE UserId = khách AND VoucherId = voucher) = 1 | 200×1, 400×9 | ✅ ĐẠT |
| S04 | 10 lần tiếp nhận cùng một serial | Đúng 1 phiếu dịch vụ chưa đóng cho serial đó | 200×10 | 🔴 HỎNG |
| S05 | 10 lần duyệt cùng một báo giá | Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2 | 200×10 | ✅ ĐẠT |
| S06 | 5 lần phê duyệt cùng một phiếu kiểm kê | InventoryAdjustmentLogs không nhân đôi: đúng 1 bản ghi cho mỗi serial | 200×5 | 🔴 HỎNG |
| S07 | POS bán serial S xen kẽ kiểm kê đánh S thất thoát | Nếu serial đã bán thì Status phải khác Lost (5) | 200×1, 400×1 | ✅ ĐẠT |
| S08 | 2 lần lập báo giá song song trên một phiếu | Đúng 1 báo giá ở trạng thái Pending (0) | 200×2 | 🔴 HỎNG |
| S09 | 2 lời gọi refresh-token cùng một cặp | Đúng 1 lời gọi thành công, và hash trong DB khớp token đã trả cho lời gọi đó | 200×2 | 🔴 HỎNG |

## Chi tiết từng kịch bản

### S01 — 50 khách checkout đồng thời

**Bất biến:** Không có OrderCode trùng, đủ 50 đơn, 0 lỗi 5xx

**Kết luận:** 🔴 HỎNG — Chỉ tạo được 13/50 đơn.

- Đơn tạo được: 13/50
- Mã đơn phân biệt: 13
- Mã HTTP: 200×13, 400×37

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```

*Thời gian: 9,2s*

### S02 — Voucher Quantity = 1, 20 khách dùng đồng thời

**Bất biến:** Voucher.UsedCount = 1 và COUNT(VoucherUsages) = 1

**Kết luận:** ✅ ĐẠT — Đúng 1 lượt được tiêu thụ.

- Voucher.UsedCount = 1 (kỳ vọng 1)
- COUNT(VoucherUsages) = 1 (kỳ vọng 1)
- Mã HTTP: 200×1, 400×19

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: Mã 'LP-VQ1' đã hết lượt sử dụng. Vui lòng bỏ mã này và thử lại.","data":null}
```
```json
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng.","data":null}
```
```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```

*Thời gian: 2,9s*

### S03 — Cùng một khách, MaxUsesPerUser = 1, 10 request

**Bất biến:** COUNT(VoucherUsages WHERE UserId = khách AND VoucherId = voucher) = 1

**Kết luận:** ✅ ĐẠT — Đúng 1 lượt cho mỗi người.

- COUNT(VoucherUsages) cho cặp (khách, voucher) = 1 (kỳ vọng 1)
- Mã HTTP: 200×1, 400×9

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```

*Thời gian: 0,6s*

### S04 — 10 lần tiếp nhận cùng một serial

**Bất biến:** Đúng 1 phiếu dịch vụ chưa đóng cho serial đó

**Kết luận:** 🔴 HỎNG — Một serial có 2 phiếu chưa đóng.

- Phiếu chưa đóng cho serial: 2 (ST-20260831-000001, ST-20260831-000002)
- Mã HTTP: 200×10

*Thời gian: 2,4s*

### S05 — 10 lần duyệt cùng một báo giá

**Bất biến:** Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2

**Kết luận:** ✅ ĐẠT — Đúng một lần duyệt được ghi nhận.

- Quotation.Status = 1 (kỳ vọng 1 = Accepted)
- Bản ghi lịch sử từ trạng thái 2: 1 (kỳ vọng 1)
- Mã HTTP: 200×10

*Thời gian: 2,2s*

### S06 — 5 lần phê duyệt cùng một phiếu kiểm kê

**Bất biến:** InventoryAdjustmentLogs không nhân đôi: đúng 1 bản ghi cho mỗi serial

**Kết luận:** 🔴 HỎNG — Sổ tổn thất có 5 bản ghi cho cùng một serial trong cùng một phiếu.

- InventoryAdjustmentLogs cho (phiếu, serial) = 5 (kỳ vọng 1)
- ProductSerial.Status = 5 (kỳ vọng 5 = Lost)
- InventoryCheck.Status = 2 (kỳ vọng 2 = Completed)
- Mã HTTP: 200×5

*Thời gian: 1,9s*

### S07 — POS bán serial S xen kẽ kiểm kê đánh S thất thoát

**Bất biến:** Nếu serial đã bán thì Status phải khác Lost (5)

**Kết luận:** ✅ ĐẠT — Serial không bán được và được ghi nhận thất thoát — nhất quán.

- ProductSerial.Status = 5
- Đã gắn vào đơn bán (OrderSerials): không
- Mã HTTP: 200×1, 400×1

*Thời gian: 0,2s*

### S08 — 2 lần lập báo giá song song trên một phiếu

**Bất biến:** Đúng 1 báo giá ở trạng thái Pending (0)

**Kết luận:** 🔴 HỎNG — Phiếu có 2 báo giá cùng ở trạng thái Pending.

- Tổng báo giá trên phiếu: 2
- Báo giá đang Pending: 2 (kỳ vọng 1)
- Mã HTTP: 200×2

*Thời gian: 0,3s*

### S09 — 2 lời gọi refresh-token cùng một cặp

**Bất biến:** Đúng 1 lời gọi thành công, và hash trong DB khớp token đã trả cho lời gọi đó

**Kết luận:** 🔴 HỎNG — 1 client nhận được refresh token đã bị vô hiệu ngay lúc cấp — lần refresh kế tiếp của họ sẽ bị đá về trang đăng nhập.

- Số cặp token được cấp: 2 (kỳ vọng 1)
- Số client cầm refresh token đã chết ngay lúc nhận: 1 (kỳ vọng 0)
- Mã HTTP: 200×2

*Thời gian: 0,6s*

