# Mục 🅴 — thông báo lỗi tiếng Anh ở `OrderService` đã bị chặn lại

> **Bất biến cần chứng minh có HAI nửa, và nửa thứ hai mới là nửa dễ làm sai.**
>
> 1. Lỗi **hạ tầng** (EF Core / SQL Server) **không** được lọt ra ngoài dưới dạng tiếng Anh.
> 2. Lỗi **nghiệp vụ** (tiếng Việt, đã soạn cho người dùng) **vẫn phải** đi ra **nguyên văn**.
>
> Cách sửa hiển nhiên nhất — thay cả khối `catch` bằng một câu tiếng Việt cố định — thoả (1)
> và **phá (2)**, vì cùng một khối `catch` cũng là đường đi của *"Mã 'X' đã hết hạn"*,
> *"Mã 'X' đã hết lượt sử dụng"*. Nuốt chúng thành "đã xảy ra lỗi" là hồi quy UX: người dùng
> mất đúng thông tin cần để tự sửa, và bấm lại thì hỏng y hệt. Vì vậy phép đo dưới đây
> **luôn kiểm cả hai nửa cùng lúc** — một nửa xanh mà thiếu nửa kia thì không kết luận được gì.

- **Ngày đo:** 2026-08-31 · **API:** `http://localhost:5222`, 1 instance, DB dev
- **Chỗ sửa:** [`src/Service/Orders/OrderService.cs`](../../../src/Service/Orders/OrderService.cs)
  (`CheckoutAsync` + `PlaceOrderAsync`), kiểu mới
  [`src/Core/Exceptions/BusinessRuleException.cs`](../../../src/Core/Exceptions/BusinessRuleException.cs)

---

## 1. TRƯỚC — chuỗi tiếng Anh lọt ra ngoài

Trích từ chốt hồi quy sau mục D ([`2026-08-31-hoi-quy-sau-muc-D.md`](2026-08-31-hoi-quy-sau-muc-D.md)),
nhánh **thua cuộc đua** của S02:

```json
{"success":false,"message":"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes. See the inner exception for details.","data":null}
```

Hai lỗi trong một dòng: **tiếng Anh** (vi phạm quy tắc user-facing message của `CLAUDE.md`) và
**lộ nội tạng EF Core** cho người ngoài. Nó bung ra đúng lúc có tải đồng thời — tức lúc người
dùng thật dễ gặp nhất.

## 2. SAU — cùng kịch bản, cùng cuộc đua

`S01` (50 khách checkout đồng thời) sinh đúng loại lỗi hạ tầng đó — đụng
`IX_Orders_OrderCode` vì `IDocumentCodeGenerator` vẫn là check-then-act:

```json
{"success":false,"message":"Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.","data":null}
```

**37/37 request lỗi đều nhận câu này.** Không còn một chuỗi tiếng Anh nào trong phản hồi HTTP.

## 3. Chi tiết KHÔNG bị mất — nó chuyển vào log

Đây là phần khiến việc sửa này không phải là "giấu lỗi đi". Đếm trên log của API:

| Đo | Số |
|---|---|
| Request checkout trả 400 trong S01 | 37 |
| Bản ghi `_logger.LogError` "Checkout thất bại cho người dùng …" | **37** |

Khớp 1:1. Và log giữ **nguyên nhân gốc thật**, thứ mà phản hồi HTTP không nên chứa:

```
Checkout thất bại cho người dùng a52785f5-…. IsBuyNow=True, BuyNowVariantId=1008, Vouchers=(không có)
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes.
 ---> Microsoft.Data.SqlClient.SqlException: Cannot insert duplicate key row in object 'dbo.Orders'
      with unique index 'IX_Orders_OrderCode'. The duplicate key value is (ORD-20260831-000002).
```

Người vận hành vẫn chẩn đoán được; người dùng cuối không phải đọc tiếng Anh. Đúng chỗ chia.

## 4. Nửa thứ hai — thông báo nghiệp vụ vẫn đi ra NGUYÊN VĂN

`S02` là phép đo đẹp nhất cho việc này, vì **một lần chạy sinh ra cả ba lớp thông báo**:

```json
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng. Vui lòng bỏ mã này và thử lại.","data":null}
{"success":false,"message":"Mã 'LP-VQ1' đã hết lượt sử dụng.","data":null}
{"success":false,"message":"Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.","data":null}
```

Hai câu đầu là `BusinessRuleException` từ hai chỗ khác nhau (`TryConsumeByCodesAsync` thấy hết
lượt bên trong transaction, và `ApplyVouchersAsync` thấy hết lượt lúc kiểm trước) — **đi ra
nguyên văn**. Câu thứ ba là nhánh hạ tầng. Cả hai lớp cùng có mặt trong **một** phép đo, nên
phép đo này không thể xanh vì lý do rỗng.

## 5. Ca đối chứng lái tay — chứng minh phép kiểm không rỗng

Chạy tay trên một khách hàng đăng ký thật (đã dọn sau khi đo), biến thể `LP-SKU-1` có 60 serial
`Available`:

| Ca | Payload | Kỳ vọng | Nhận được |
|---|---|---|---|
| **A** — mã giảm giá không tồn tại | `voucherCodes: ["KHONGCOMANAY"]` | thông báo nghiệp vụ nguyên văn | `400` · `"Mã giảm giá không tồn tại: KHONGCOMANAY"` ✅ |
| **B** — ca đối chứng, **cùng payload, bỏ voucher** | không có `voucherCodes` | `200`, đặt được đơn | `200` · `ORD-20260831-000014` ✅ |

**Ca B bắt buộc phải có.** Thiếu nó thì cái `400` của ca A có thể chỉ nghĩa là payload sai,
địa chỉ sai, hay hết tồn kho — nghĩa là luật voucher chưa từng được chạy tới và ca A không
chứng minh gì cả. Có ca B, cái `400` của A chắc chắn đến từ đúng luật cần đo. Cùng lý lẽ với
ca đối chứng "gỡ `Disabled`" của mục B và ca "hoàn tác csproj" của mục D.

## 6. Chốt hồi quy sau khi sửa

Đụng tầng Service nên phải chạy lại — xem
[`2026-08-31-hoi-quy-sau-muc-E.md`](2026-08-31-hoi-quy-sau-muc-E.md): **2 ĐẠT, 0 HỎNG,
0 KHÔNG KẾT LUẬN**. DB về đúng nguyên trạng: `Products = 2`, `ProductSerials = 0`,
`Orders = 0`, `InventoryChecks = 0`, 0 tài khoản sót.

---

## Việc này KHÔNG sửa được gì trong số 5 bất biến sai

Nói rõ để không ai đọc file này thành tiến độ đợt 3: nguyên nhân gốc của S01 — sinh mã chứng
từ đua nhau (`IDocumentCodeGenerator` đọc max rồi +1, không retry khi đụng unique index) —
**còn nguyên**. S01 vẫn 🔴 sau khi sửa, và số đơn đặt được vẫn dao động theo thời điểm
(9/50 · 18/50 · 13/50 qua ba lần chạy). Mục 🅴 chỉ đổi **thứ người dùng đọc được** khi cuộc
đua đó thua. Dữ liệu vẫn không hỏng (unique index chặn); tính khả dụng vẫn hỏng.

## Một phát hiện phụ, chưa sửa — cùng lỗi này còn ở 6 chỗ khác

Quét `ex.Message` bị chuyển thẳng cho người dùng ngoài `OrderService`:

| Chỗ | Dòng |
|---|---|
| `Service/Pos/PosService.cs` | 458, **462** (`"Lỗi khi quá trình thanh toán: " + ex.Message`) |
| `Service/Inventory/InventoryCheckService.cs` | 678, 858, 999 |
| `Service/Inventory/InventoryExportService.cs` | 192, **198** (`$"Lỗi khi xuất kho: {ex.Message}"`) |
| `API/Controllers/…` (`Cart`, `Orders`, `ServiceTickets`, `ServiceInvoices`) | ~35 chỗ `ApiResult.Fail(ex.Message)` |

`PosService.cs:462` là **bản sinh đôi y hệt** của lỗi vừa sửa, chỉ khác luồng (POS tại quầy
thay vì checkout online). Không sửa trong mục 🅴 vì runbook khoanh mục này đúng vào
`OrderService.cs:257`; ghi lại thành mục 🅷 để người quyết định phạm vi, chứ không mở rộng âm thầm.
Muốn sửa thì khuôn đã có sẵn: `BusinessRuleException` + `catch` hai tầng.
