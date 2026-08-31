# LoadProbe — chốt hồi quy sau mục D (vá NuGet)

> **Vì sao có file này.** Mục D chỉ đổi `.csproj` + CI, không đổi một dòng code
> tầng Service nào — nhưng nó CÓ đổi đồ thị phụ thuộc của `Service` (ghim
> `System.Security.Cryptography.Xml` dưới chân EPPlus, và gỡ hẳn `AutoMapper`).
> S02/S05 là hai kịch bản đo đúng thứ đợt 1 đã sửa (`ExecuteUpdateAsync` có vị từ
> và `TryDecideAsync`), nên chúng là chốt hồi quy rẻ nhất cho câu hỏi "việc vá gói
> có làm hỏng thứ gì không". Kết quả: **2 ĐẠT, 0 HỎNG, 0 KHÔNG KẾT LUẬN**.


- **Thời điểm chạy:** 2026-08-31 20:50:17 (giờ máy)
- **API:** `http://localhost:5222`
- **Giãn cách giữa hai kịch bản:** 11s

> Cột **Kết luận** đọc như sau: `ĐẠT` = bất biến đúng. `HỎNG` = bất biến sai,
> có lỗi đúng đắn dữ liệu. `KHÔNG KẾT LUẬN` = phép đo bị rỗng (bị rate limiter
> chặn, seed thiếu, API không phản hồi) — **không được đọc thành "đạt"**.

| # | Kịch bản | Bất biến kiểm sau | Mã HTTP | Kết luận |
|---|---|---|---|---|
| S02 | Voucher Quantity = 1, 20 khách dùng đồng thời | Voucher.UsedCount = 1 và COUNT(VoucherUsages) = 1 | 200×1, 400×19 | ✅ ĐẠT |
| S05 | 10 lần duyệt cùng một báo giá | Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2 | 200×10 | ✅ ĐẠT |

## Chi tiết từng kịch bản

### S02 — Voucher Quantity = 1, 20 khách dùng đồng thời

**Bất biến:** Voucher.UsedCount = 1 và COUNT(VoucherUsages) = 1

**Kết luận:** ✅ ĐẠT — Đúng 1 lượt được tiêu thụ.

- Voucher.UsedCount = 1 (kỳ vọng 1)
- COUNT(VoucherUsages) = 1 (kỳ vọng 1)
- Mã HTTP: 200×1, 400×19

Mẫu thân phản hồi thất bại:

```json
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng.","data":null}
```
```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```
```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```

*Thời gian: 7,9s*

### S05 — 10 lần duyệt cùng một báo giá

**Bất biến:** Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2

**Kết luận:** ✅ ĐẠT — Đúng một lần duyệt được ghi nhận.

- Quotation.Status = 1 (kỳ vọng 1 = Accepted)
- Bản ghi lịch sử từ trạng thái 2: 1 (kỳ vọng 1)
- Mã HTTP: 200×10

*Thời gian: 2,9s*

