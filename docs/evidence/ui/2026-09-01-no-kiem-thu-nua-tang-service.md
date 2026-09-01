# Nợ 🧪 — nửa tầng Service: POS · nhập kho · xuất kho · phiếu dịch vụ **đã chạy thật tới DB**

> **Bốn luồng này trước phiên 2026-09-01 chưa từng chạy hết một lần nghiệp vụ nào** — mới chỉ
> được rà bằng đọc code + build sạch. Nay cả bốn đã chạy hết luồng, và **mọi khẳng định dưới đây
> đọc thẳng DB**, không đọc mã HTTP.
>
> **Vì sao phải đọc DB.** Hai chế độ hỏng mà mục 🅰 sinh ra đều trả **`200`**:
> - **`SaveChanges` không sinh `UPDATE`** → API báo thành công, dữ liệu không đổi.
> - **entity `Add` hai lần sau retry** → API báo thành công, bản ghi nhân đôi.
>
> Không phép đo nào ở tầng HTTP nhìn thấy được chúng.
>
> **Kết quả: 4/4 luồng ĐẠT. 0 bản ghi nhân đôi. 0 chỗ lệch tồn kho.**

- **Ngày đo:** 2026-09-01 · API `http://localhost:5222` · DB `HushStoreDb` (SQL Server container)
- **Seed:** `dotnet run --project tools/LoadProbe -- --scenarios S01 --keep` → 60 serial `Available`
- **Tài khoản:** `admin@hushstore.com` (JWT thật, qua `POST /api/auth/login`)
- **Nguyên trạng đã trả về đủ sau khi đo** — xem §6

---

## ⚠️ Phép đo này đo TẦNG SERVICE, không đo trang Razor

Công thức gói 1 ghi *"bấm tay trên giao diện"*. Phép đo thật sự chạy là **gọi API bằng JWT
admin thật**, không click qua Blazor. Ghi rõ ở đây để không ai đọc file này rộng hơn nó chứng minh.

**Vì sao vẫn đúng thứ cần đo:** công thức gói 1 tự nói mục tiêu là
*"🎯 Cái đang thật sự được đo là **mục 🅰** (transaction retry-safe), **không phải giao diện**."*
Mục 🅰 nằm trong `*Service.cs`; đường `Controller → Service → Repository → DB` giống hệt nhau dù
request đến từ Blazor hay từ `curl`. Nửa giao diện thì **đã có bằng chứng riêng** ở
[`2026-08-31-6-nut-double-submit.md`](2026-08-31-6-nut-double-submit.md) (6/6 nút, có ca đối chứng âm).

**Cái phép đo này KHÔNG chứng minh:** rằng các trang `Pos/Index`, `Inventory/*`,
`ServiceTickets/*` bind đúng DTO và hiện đúng dữ liệu. Đó vẫn là nợ, chỉ là nợ **giao diện**,
không còn là nợ **tầng Service**.

**Đổi lại được một thứ mà click tay không cho:** toàn bộ phép đo là lệnh `curl` + `sqlcmd`,
**chạy lại được**. Một phiên click tay chỉ dùng được đúng một lần.

---

## 1. Nhập kho — `POST /api/import-receipts`

```json
{"supplierId":1,"details":[{"variantId":1,"quantity":3,"importPrice":3500000,
  "serialNumbers":["MT-TEST-A01","MT-TEST-A02","MT-TEST-A03"]}]}
→ 200  "Tạo phiếu nhập kho thành công."  ReceiptCode = PN-20260901-000001
```

| Bất biến | Kỳ vọng | Đo được | |
|---|---|---|---|
| `ImportReceipts` | 2 → 3 | **3** | ✅ |
| `ProductSerials` | 60 → 63 | **63** | ✅ |
| 3 serial mới `Available` (`Status=0`), gắn `ImportReceiptId=16` | đúng | **đúng cả 3** | ✅ |
| `StockQuantity == COUNT(serial Available)` **cho cả 4 variant** | khớp | **4/4 KHỚP** | ✅ |
| `ReceiptCode` trùng | 0 | **0** | ✅ |
| `SerialNumber` trùng | 0 | **0** | ✅ |

---

## 2. POS — `POST /api/pos/checkout`

Bán tại quầy 1 serial `LP-` (id 1688), không khách hàng (khách vãng lai).

```
→ 200  "Thanh toán thành công."  OrderCode = POS-20260901-000001
```

| Bất biến | Kỳ vọng | Đo được | |
|---|---|---|---|
| `Orders` | 12 → 13, **1 mã `POS-`** | **13 / 1** | ✅ |
| `OrderDetails` của đơn 675 | 1 | **1** | ✅ |
| `OrderSerials` | 0 → 1 | **1** | ✅ |
| `Warranties` | 0 → 1 | **1** (`2026-09-01 → 2027-09-01`) | ✅ |
| Serial → `Sold` (`Status=2`) + `OrderId` + `SoldDate` | đúng | **`Status=2 OrderId=675 SoldDate=…07:05:17`** | ✅ |
| `StockQuantity` variant 1015 | 60 → 59, khớp `COUNT` | **59 / KHỚP** | ✅ |
| **`OrderSerial` nhân đôi** | 0 | **0** | ✅ |
| **`Warranty` nhân đôi trên cùng serial** | 0 | **0** | ✅ |
| `OrderCode` `POS-` trùng | 0 | **0** | ✅ |

🎯 Đây là luồng mang **hai** trong bốn bẫy của mục 🅰: `PosService` từng `new` sẵn
`Order`/`OrderDetail`/`Warranty` **ngoài** delegate, và tích luỹ `newWarranties` **ngoài** delegate.
Cả hai chế độ hỏng đều hiện ra dưới dạng bản ghi nhân đôi — **đo được 0**.

---

## 3. Xuất kho — `POST /api/inventory/export-order`

**Ca đối chứng âm (chạy trước, cố ý):** xuất khi đơn còn `Pending` →

```
→ 200 {"success": false, "message": "Đơn hàng không ở trạng thái Đã xác nhận (Confirmed). Không thể xuất kho."}
```

Chốt nghiệp vụ chặn đúng, **và câu từ chối là tiếng Việt** — không rò rỉ chuỗi EF.
Rồi `PUT /api/orders/625/confirm` → `POST export-order` →
`"Xuất kho thành công. Đơn hàng chuyển sang trạng thái Đã xuất kho."`

| Bất biến | Kỳ vọng | Đo được | |
|---|---|---|---|
| Serial → `Sold` + `OrderId=625` + `SoldDate` | đúng | **`Status=2 OrderId=625 SoldDate=…07:06:47`** | ✅ |
| `Orders.Status` đơn 625 | đã xuất kho | **`2`** | ✅ |
| **`OrderSerial` của `OrderDetail 144`** (`Quantity=1`) | **đúng 1** | **1** | ✅ |
| `OrderSerial` trùng `(OrderDetailId, SerialId)` | 0 | **0** | ✅ |
| Một serial nằm trong >1 `OrderSerial` | 0 | **0** | ✅ |
| `StockQuantity` variant 1015 | 59 → 58, khớp `COUNT` | **58 / KHỚP** | ✅ |

🎯 Đây là bẫy **"collection đã `Include` rồi `.Add`"** của
`InventoryExportService.ExportOrderAsync`: `orderDetail.OrderSerials` giữ luôn bản ghi `Add` của
lần thử trước → sinh `OrderSerial` **trùng**. Đo được **đúng 1**, không trùng.

---

## 4. Phiếu dịch vụ — hai nhánh, vì một nhánh không đủ

Nhánh bảo hành **không có báo giá** (đúng nghiệp vụ), nên đo một mình nó thì bất biến
*"đúng 1 `Quotation` `Accepted`"* **không bao giờ chạy tới**. Phải đo cả hai.

### 4a. Nhánh trong bảo hành — `InternalRepair`

`intake` → `create` → `assign` → `diagnosis` → `branch(1)` → `start-repair` → `waiting-parts`
→ `resume-repair` → `complete`. Tất cả `200`.

**Ca đối chứng âm:** xin báo giá trên nhánh bảo hành →
`"Chỉ phiếu sửa tính phí mới có báo giá."` — chặn đúng.

Đáng chú ý: `intake` đọc đúng bảo hành **do luồng POS ở §2 vừa tạo ra**
(`warrantySource: "WarrantyRow"`, `isInWarranty: true`) — hai luồng nối được vào nhau.

### 4b. Nhánh ngoài bảo hành — `PaidRepair`

Chuẩn bị: bán `MT-TEST-A01` qua POS thật, rồi **chỉ lùi ngày** `Warranties.EndDate` /
`ProductSerials.SoldDate` về quá khứ (không đợi được 12 tháng; **không sửa dòng code nào đang đo**).
`intake` xác nhận: `isInWarranty: false`, `allowedBranches: ["PaidRepair"]`.

`create` → `assign` → `diagnosis` → `branch(4)` → `quotation` → `accept(nextStatus=5)` → `complete`.

| Bất biến | Kỳ vọng | Đo được | |
|---|---|---|---|
| `ServiceTickets` | 0 → 2, cả hai `Status=9` (hoàn tất) | **2, cả hai `Status=9`** | ✅ |
| `Quotations` của phiếu 66 | **đúng 1** | **1** | ✅ |
| `Quotation.Status` | `1` (Accepted) + `CustomerDecidedAt` **không NULL** | **`Status=1`, `DecidedAt=07:08:59`** | ✅ |
| Phiếu có >1 `Quotation` `Accepted` | 0 | **0** | ✅ |
| `ServiceTicketStatusHistory` **bước lặp** | 0 | **0** | ✅ |

🎯 `Quotation.Status=1` **kèm** `CustomerDecidedAt` khác NULL chính là thứ bác bỏ chế độ hỏng
**"`SaveChanges` không sinh `UPDATE`"**: nếu nó cắn, phiếu vẫn `Status=0` và `DecidedAt=NULL`
trong khi API đã trả `200`.

Chuỗi lịch sử ghi được — mạch lạc, không đứt, không lặp:

```
65:  0→0   0→1   1→5   5→4   4→5   5→9      (6 bước, nhánh bảo hành có waiting-parts)
66:  0→0   0→1   1→2   2→5   5→9            (5 bước, nhánh tính phí qua báo giá)
```

⚠️ Bảng là `ServiceTicketStatusHistory` — **số ít** trong DB (bẫy #10), và khoá ngoại tên
`TicketId`, **không** phải `ServiceTicketId`. Cột của `Quotations` cũng là `TicketId`, và tổng
tiền là `GrandTotal`, **không** phải `TotalAmount`.

---

## 5. Chốt hồi quy sau khi đụng vùng tầng Service

```
dotnet run --project tools/LoadProbe -- --scenarios S02,S05 --pace 11
```

```
── S02: Voucher Quantity = 1, 20 khách dùng đồng thời
   ✅ ĐẠT — Đúng 1 lượt được tiêu thụ.   UsedCount=1 · COUNT(VoucherUsages)=1 · 200×1, 400×19
── S05: 10 lần duyệt cùng một báo giá
   ✅ ĐẠT — Đúng một lần duyệt được ghi nhận.   Quotation.Status=1 · lịch sử từ trạng thái 2: 1

Tổng kết: 2 đạt, 0 hỏng, 0 không kết luận.
```

`0 KHÔNG KẾT LUẬN` — phép đo thật sự chạy, không bị rate limiter nuốt (bẫy #8).

---

## 6. Nguyên trạng đã trả về

| Bảng | Trước | Sau |
|---|---|---|
| `Products` | 2 | **2** |
| `ProductSerials` | 0 | **0** |
| `Orders` · `OrderDetails` · `OrderSerials` | 0 | **0** |
| `Warranties` | 0 | **0** |
| `ServiceTickets` · `Quotations` · `ServiceTicketStatusHistory` | 0 | **0** |
| `ImportReceipts` | 1 | **1** |
| `InventoryChecks` | 0 | **0** |
| `StockQuantity` cả 3 variant | 0 | **0** |

Probe **chỉ** dọn thứ mang tiền tố `LP-`; bốn luồng trên tạo ra dữ liệu mang mã thật
(`PN-`, `POS-`, `ST-`, `MT-TEST-`) nên phải dọn tay, theo đúng thứ tự khoá ngoại
(`ServiceInvoices → QuotationItems → Quotations → RmaShipments → SerialRepairLogs →
ServiceTicketStatusHistory → ServiceTickets → Warranties → OrderSerials → VoucherUsages →
OrderDetails → Orders → InventoryAdjustmentLogs → InventoryCheckDetailSerials → ProductSerials
→ ImportReceiptDetails → ImportReceipts`), rồi tính lại `StockQuantity` từ `COUNT`.

**Không đụng tới** 4 dòng `Suppliers` id 1001–1004: kiểm `CreatedDate` cho thấy chúng sinh
**2026-04-20**, tức tồn dư của một phiên cũ, không phải của phiên này (3/4 đã `IsDeleted=1`).
Lần đếm đầu dùng `TOP 3` nên không thấy chúng — suýt xoá nhầm dữ liệu mình không tạo ra.

---

## 7. Giới hạn — đọc trước khi đánh dấu nợ 🧪 là hết

1. **Không ép được retry.** Bốn luồng chạy **đường thuận**, mỗi luồng một lần. Nó bác bỏ chắc
   chắn chế độ hỏng *"`SaveChanges` không sinh `UPDATE`"*, và bác bỏ *"`Add` hai lần"* **trong
   phạm vi các lần chạy đã thực hiện** — nhưng chế độ hỏng thứ hai chỉ **chắc chắn** lộ ra khi
   execution strategy **thật sự retry**, mà retry cần lỗi transient của SQL Server. Muốn đóng
   hẳn thì phải bơm lỗi transient, chưa làm.
2. **Nửa giao diện của bốn luồng vẫn chưa bấm.** Xem khối ⚠️ ở đầu file.
3. **S01 vẫn 🔴** (32–41/50 đơn hỏng vì đụng `IX_Orders_OrderCode`) — lần chạy seed của phiên này
   ra **12/50**. Đây là lỗi **đã biết**, do sinh mã chứng từ đua nhau, và là việc của **gói 2**
   (SEQUENCE). Không phải hồi quy của phiên này.
