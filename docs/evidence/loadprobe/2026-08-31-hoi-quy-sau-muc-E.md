# LoadProbe — chốt hồi quy sau mục 🅴 (sửa thông báo lỗi ở `OrderService`)

> **Vì sao có file này.** Mục 🅴 đụng vào tầng Service, và runbook có quy tắc cứng: sửa tầng
> Service xong thì chạy lại `--scenarios S02,S05`. Hai kịch bản này đo đúng thứ đợt 1 đã sửa
> (`ExecuteUpdateAsync` có vị từ, `TryDecideAsync`), nên nếu ai đó vô tình đưa check-then-act
> quay lại thì chúng chuyển sang HỎNG.
>
> **Kết quả: 2 ĐẠT, 0 HỎNG, 0 KHÔNG KẾT LUẬN.** Đợt 1 không hồi quy.
>
> ⚠️ **Việc sửa của mục 🅴 có một rủi ro hồi quy riêng mà S02/S05 KHÔNG đo được**, đừng đọc
> file này thay cho nó: mục 🅴 chèn thêm một `catch (BusinessRuleException)` đứng **trước**
> `catch (Exception)`, nên nếu phân loại sai thì thông báo nghiệp vụ bị nuốt thành câu chung.
> Đó là bất biến về *nội dung thông báo*, không phải về *tính đúng đắn dữ liệu*, nên nó được
> đo riêng ở [`2026-08-31-muc-E-thong-bao-loi-tieng-viet.md`](2026-08-31-muc-E-thong-bao-loi-tieng-viet.md)
> §4–§5. Trớ trêu là chính S02 lại là phép đo tốt nhất cho nó — một lần chạy sinh ra cả ba
> lớp thông báo, xem mẫu phản hồi lỗi của S02 ở dưới.

---

- **Thời điểm chạy:** 2026-08-31 21:21:34 (giờ máy)
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
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng. Vui lòng bỏ mã này và thử lại.","data":null}
```
```json
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng.","data":null}
```
```json
{"success":false,"message":"Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.","data":null}
```

*Thời gian: 4,3s*

### S05 — 10 lần duyệt cùng một báo giá

**Bất biến:** Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2

**Kết luận:** ✅ ĐẠT — Đúng một lần duyệt được ghi nhận.

- Quotation.Status = 1 (kỳ vọng 1 = Accepted)
- Bản ghi lịch sử từ trạng thái 2: 1 (kỳ vọng 1)
- Mã HTTP: 200×10

*Thời gian: 1,3s*

