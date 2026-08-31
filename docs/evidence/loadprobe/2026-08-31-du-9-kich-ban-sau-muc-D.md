# LoadProbe — đủ 9 kịch bản sau mục D (trả nợ kiểm thử 🧪 ưu tiên 1)

> **Vì sao có file này.** Mục 🧪 của runbook ghi một món nợ do chính mục D tạo ra: sau khi
> vá gói NuGet, người ta **chỉ chạy lại S02/S05** — hai kịch bản đo đúng thứ đợt 1 đã sửa —
> chứ chưa chạy đủ 9. S02/S05 không phủ luồng POS, xuất kho hay phiếu dịch vụ, nên chúng
> không trả lời được câu hỏi "việc ghim `System.Security.Cryptography.Xml` 10.0.10, ghim
> `Microsoft.OpenApi` 2.7.5 và gỡ hẳn `AutoMapper` có làm hỏng thứ gì ở nơi khác không".
> File này là câu trả lời: **chạy cả 9 kịch bản, 1 instance, trên code `main` chưa sửa gì.**
>
> **Kết quả: 5 ĐẠT, 4 HỎNG, 0 KHÔNG KẾT LUẬN — trùng khít cột "1 instance" của mục 🅵.**
> Bốn cái HỎNG đúng là S01/S06/S08/S09; S02/S03/S04/S05/S07 vẫn ĐẠT. Không có hồi quy nào
> từ việc vá gói.
>
> ⚠️ **Sửa một chỗ đọc sai trong runbook.** Mục 🧪 viết kỳ vọng là *"vẫn đúng 5/9 HỎNG như
> trước"*. Con số **5/9 là HỢP của hai cấu hình**, không phải của một cấu hình: S04 ĐẠT với
> 1 instance và chỉ HỎNG với 2. Với 1 instance thì kỳ vọng đúng là **4/9 HỎNG**. Ai chạy
> lệnh này rồi thấy 4 mà tưởng "đã sửa được một cái" là đọc sai — và ngược lại, thấy 5 mà
> yên tâm "đúng như kỳ vọng" thì đã bỏ qua một hồi quy thật ở S04.
>
> ⚠️ **`0 KHÔNG KẾT LUẬN` là phần quan trọng nhất của dòng tổng kết**, không phải phần phụ.
> Nó nói không có kịch bản nào bị rate limiter làm rỗng, tức 4 cái ĐẠT là ĐẠT thật chứ không
> phải "code cần đo chưa từng chạy" (bẫy #8).
>
> **Chưa phủ:** cấu hình 2 instance — xem
> [`2026-08-31-hai-instance-sau-muc-E.md`](2026-08-31-hai-instance-sau-muc-E.md).
> **Cùng phiên:** [`2026-08-31-muc-E-thong-bao-loi-tieng-viet.md`](2026-08-31-muc-E-thong-bao-loi-tieng-viet.md).

---

- **Thời điểm chạy:** 2026-08-31 21:14:24 (giờ máy)
- **API:** `http://localhost:5222`
- **Giãn cách giữa hai kịch bản:** 11s

> Cột **Kết luận** đọc như sau: `ĐẠT` = bất biến đúng. `HỎNG` = bất biến sai,
> có lỗi đúng đắn dữ liệu. `KHÔNG KẾT LUẬN` = phép đo bị rỗng (bị rate limiter
> chặn, seed thiếu, API không phản hồi) — **không được đọc thành "đạt"**.

| # | Kịch bản | Bất biến kiểm sau | Mã HTTP | Kết luận |
|---|---|---|---|---|
| S01 | 50 khách checkout đồng thời | Không có OrderCode trùng, đủ 50 đơn, 0 lỗi 5xx | 200×18, 400×32 | 🔴 HỎNG |
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

**Kết luận:** 🔴 HỎNG — Chỉ tạo được 18/50 đơn.

- Đơn tạo được: 18/50
- Mã đơn phân biệt: 18
- Mã HTTP: 200×18, 400×32

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```

*Thời gian: 16,7s*

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
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```
```json
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng.","data":null}
```

*Thời gian: 3,8s*

### S03 — Cùng một khách, MaxUsesPerUser = 1, 10 request

**Bất biến:** COUNT(VoucherUsages WHERE UserId = khách AND VoucherId = voucher) = 1

**Kết luận:** ✅ ĐẠT — Đúng 1 lượt cho mỗi người.

- COUNT(VoucherUsages) cho cặp (khách, voucher) = 1 (kỳ vọng 1)
- Mã HTTP: 200×1, 400×9

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```

*Thời gian: 0,4s*

### S04 — 10 lần tiếp nhận cùng một serial

**Bất biến:** Đúng 1 phiếu dịch vụ chưa đóng cho serial đó

**Kết luận:** ✅ ĐẠT — Đúng 1 phiếu chưa đóng.

- Phiếu chưa đóng cho serial: 1 (ST-20260831-000001)
- Mã HTTP: 200×10

*Thời gian: 2,3s*

### S05 — 10 lần duyệt cùng một báo giá

**Bất biến:** Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2

**Kết luận:** ✅ ĐẠT — Đúng một lần duyệt được ghi nhận.

- Quotation.Status = 1 (kỳ vọng 1 = Accepted)
- Bản ghi lịch sử từ trạng thái 2: 1 (kỳ vọng 1)
- Mã HTTP: 200×10

*Thời gian: 0,9s*

### S06 — 5 lần phê duyệt cùng một phiếu kiểm kê

**Bất biến:** InventoryAdjustmentLogs không nhân đôi: đúng 1 bản ghi cho mỗi serial

**Kết luận:** 🔴 HỎNG — Sổ tổn thất có 5 bản ghi cho cùng một serial trong cùng một phiếu.

- InventoryAdjustmentLogs cho (phiếu, serial) = 5 (kỳ vọng 1)
- ProductSerial.Status = 5 (kỳ vọng 5 = Lost)
- InventoryCheck.Status = 2 (kỳ vọng 2 = Completed)
- Mã HTTP: 200×5

*Thời gian: 1,4s*

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

*Thời gian: 0,4s*

### S09 — 2 lời gọi refresh-token cùng một cặp

**Bất biến:** Đúng 1 lời gọi thành công, và hash trong DB khớp token đã trả cho lời gọi đó

**Kết luận:** 🔴 HỎNG — 1 client nhận được refresh token đã bị vô hiệu ngay lúc cấp — lần refresh kế tiếp của họ sẽ bị đá về trang đăng nhập.

- Số cặp token được cấp: 2 (kỳ vọng 1)
- Số client cầm refresh token đã chết ngay lúc nhận: 1 (kỳ vọng 0)
- Mã HTTP: 200×2

*Thời gian: 0,5s*

