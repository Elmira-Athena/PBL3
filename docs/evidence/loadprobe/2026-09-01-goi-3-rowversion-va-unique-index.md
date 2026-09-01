# Gói 3 — `RowVersion` + 3 unique index: **9/9 ✅ ở CẢ HAI cấu hình**

**Ngày:** 2026-09-01 · **Nhánh:** `fix/muc-I-client-error-leaks`
**Migration:** `20260901083028_AddConcurrencyTokensAndUniqueIndexes`

## Kết quả

| # | Kịch bản | Trước gói 3 | 1 instance | 2 instance | Số lần quan sát ở 2 inst |
|---|---|---|---|---|---|
| S01 | 50 khách checkout đồng thời | ✅ *(gói 2)* | ✅ | ✅ | 1 |
| S02 | Voucher `Quantity=1`, 20 khách | ✅ | ✅ | ✅ | 1 |
| S03 | Cùng khách, `MaxUsesPerUser=1` | 🔴 | ✅ | ✅ | **5** |
| S04 | 10 lần `intake` cùng serial | 🔴 | ✅ | ✅ | **5** |
| S05 | 10 lần `accept-quotation` | ✅ | ✅ | ✅ | 1 |
| S06 | 5 lần `approve` phiếu kiểm kê | 🔴 | ✅ | ✅ | 1 |
| S07 | POS bán S xen kẽ kiểm kê đánh Lost | ✅ | ✅ | ✅ | **5** |
| S08 | 2 lần `create-quotation` song song | 🔴 | ✅ | ✅ | **5** |
| S09 | 2 lần `refresh-token` cùng cặp | 🔴 | ✅ | ✅ | 1 |

**`0 KHÔNG KẾT LUẬN`** ở mọi lần chạy.

> ⚠️ **Vì sao cột cuối tồn tại.** S03, S04, S07, S08 **phụ thuộc thời điểm**, và luật bất
> đối xứng của runbook nói: *một lần 🔴 là bằng chứng hỏng, một lần ✅ KHÔNG là bằng chứng
> an toàn.* Nên bốn kịch bản này được chạy **5 lần** ở cấu hình 2 instance. Năm kịch bản còn
> lại đóng bằng cơ chế **tất định** (unique index / conditional update) nên một lần là đủ.
>
> Bài học này không phải lý thuyết — xem §"S03 ĐẠT rồi HỎNG" bên dưới.

## Đã thay đổi gì

**Migration** — 6 `RowVersion` (`ProductSerial`, `ServiceTicket`, `Quotation`,
`InventoryCheck`, `Order`, `RmaShipment`), 1 cột `VoucherUsage.SeqPerUser` + backfill
`ROW_NUMBER`, và 3 unique index:

| Index | Đóng | Kiểu |
|---|---|---|
| `UQ_ServiceTickets_SerialId_Open` | S04 | filtered (`Status <> 3,8,9,10 AND IsDeleted = 0`) |
| `UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId` | S06 | thường |
| `UQ_VoucherUsages_UserId_VoucherId_SeqPerUser` | S03 | thường |

**Code** — `SeqPerUser` + **kiểm lại hạn mức bên trong transaction** ở 3 chỗ tạo
`VoucherUsage`; conditional update cho rotation refresh token (S09); 10 chốt
`catch (DbUpdateConcurrencyException) { throw; }` ở tầng Service; **27 chốt ở tầng
Controller**; 2 thông báo theo ngữ cảnh (S04, S06).

Đảo chiều được: `dotnet ef database update AddDocumentCodeSequences` → 0 cột còn lại,
2 index cũ trở lại; `update` lại → xanh.

---

## 🔴 Phát hiện 1 — S08 do `RowVersion` đóng, KHÔNG do index

Kế hoạch chỉ liệt kê **ba** unique index, không có index nào cho `Quotations`. Đặt cược là
`RowVersion` trên `ServiceTicket` sẽ đủ, vì hai lời gọi `create-quotation` đều ghi
`ticket.Status = 2` nên câu `UPDATE … WHERE Id=@p AND RowVersion=@v` tuần tự hoá chúng.

**Cược đúng, và mã HTTP chứng minh chứ không phải suy luận:**

| | Mã HTTP | `Pending` |
|---|---|---|
| Trước | `200×2` | 2 🔴 |
| Sau | `200×1, **409×1**` | 1 ✅ |

Câu của kẻ thua là `"Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại trang và thử
lại."` — chính nhánh `DbUpdateConcurrencyException` của `ConflictExceptionHandler`. Không
thêm index nào cho `Quotations`.

---

## 🔴 Phát hiện 2 — S03 ĐẠT rồi HỎNG, và unique index KHÔNG đủ

Đây là chỗ tôi sai và phải sửa lại. Thiết kế đầu: `SeqPerUser = MAX+1` + unique
`(UserId, VoucherId, SeqPerUser)`, tin rằng index là chốt.

**Lần chạy 1: ✅. Lần chạy 2: 🔴 `200×2`.** Không một dòng code nào đổi giữa hai lần.

Lý do: **unique index chỉ chặn HAI INSERT CÙNG MỘT SỐ THỨ TỰ. Nó không biết
`MaxUsesPerUser`.** Có hai thứ tự xảy ra, index chỉ đóng một:

| | Diễn biến | Index |
|---|---|---|
| (a) | hai request **đọc chồng nhau** → cùng ra `seq = 1` | chặn ✅ |
| (b) | hai request **bị tuần tự hoá** → kẻ sau đọc `MAX = 1` → `seq = 2` | **cho qua** 🔴 |

Ở (b), chốt `MaxUsesPerUser` nằm **ngoài** transaction nên nó đã đọc `count = 0` từ trước
khi kẻ trước commit. Khách dùng được 2 lượt.

**Sửa:** kiểm lại hạn mức **bên trong** delegate, ngay sau câu `MAX`. Ghép với index thì
phủ kín cả (a) và (b). Chốt ngoài transaction **vẫn giữ** — nó cho câu thông báo tử tế
trong trường hợp thường và trả lỗi trước khi tạo đơn.

Sau khi sửa, **S03 chạy 5 lần liên tiếp đều ĐẠT**, và thấy rõ **cả hai** nhánh trong thông
báo trả về:

```
(b) → "Bạn đã sử dụng mã 'LP-VMU' đủ số lần cho phép (tối đa 1 lần/khách)."
(a) → 409 "Dữ liệu này vừa được người khác tạo hoặc thay đổi. Vui lòng tải lại trang…"
```

> 🚨 **Nếu tôi dừng ở lần chạy đầu, tôi đã ghi "S03 XONG" cho một lỗi vẫn còn nguyên.**
> Đây chính xác là điều luật bất đối xứng của runbook cảnh báo, gặp ngoài đời.

---

## 🔴 Phát hiện 3 — `ConflictExceptionHandler` bị CONTROLLER nuốt, không phải Service

Sau khi thêm index, dữ liệu S03 đã đúng nhưng 9 kẻ thua nhận:

```
"Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút…"
```

Trong khi log server ghi rõ `SqlException 2601 … UQ_VoucherUsages_UserId_VoucherId_SeqPerUser`.

Đây là **hồi quy UX do chính bản vá tạo ra**, và là đúng cái bẫy CLAUDE.md ghi: *"Nuốt
chúng thành câu chung là hồi quy UX nặng hơn lỗi ban đầu."* Người dùng đọc thành "server
hỏng" rồi bấm lại, trong khi thật ra họ chạm một hạn mức hợp lệ.

**Chẩn đoán ban đầu của tôi SAI.** Tôi thêm chốt `throw;` ở tầng Service và tưởng xong —
log cho thấy chốt đó **đã chạy đúng** (`"Đặt hàng thất bại do trùng khoá duy nhất"`) mà 409
vẫn không tới. Nguyên nhân thật ở
[`OrdersController.cs:52`](../../src/API/Controllers/Admin/OrdersController.cs#L52): **controller
có `catch (Exception)` riêng.**

`ConflictExceptionHandler` là `IExceptionHandler`, nên nó **chỉ thấy exception ĐÃ THOÁT khỏi
action**. Mọi `catch (Exception)` trong controller là một bức tường trước middleware.

**Sửa:** 27 chốt `throw;` ở các action **mutation** trong 4 controller. Cố ý **không** thêm
vào 8 action GET — endpoint đọc không sinh được hai loại exception này.

| | S03 | S08 |
|---|---|---|
| Trước chốt controller | `400×9` + "lỗi hệ thống" | `200×2` |
| Sau chốt controller | **`409×9`** + câu 409 | `200×1, 409×1` |

> 🔴 **Đây là mảnh ghép mà cả kế hoạch gốc và phiên trước đều bỏ sót.** Phiên trước ghi
> *"handler hiện chưa với tới được từ đường nghiệp vụ nào (service nuốt `DbUpdateException`)"* —
> **chẩn đoán đó chưa đủ**: service **và** controller, và controller mới là tường cuối.
> Trước hôm nay `ConflictExceptionHandler` là **code chết** cho mọi đường nghiệp vụ.

---

## Lựa chọn 3 của mục 🅶 — đã chạy, ở local, $0

Thay vì chạy `pre_migration_checks.sql` trên một RDS rỗng, tôi dùng LoadProbe tạo **dữ liệu
bẩn thật** rồi áp migration lên chính nó.

**Bước 1 — tạo dữ liệu bẩn.** `--scenarios S06 --keep` trên code CHƯA sửa: 5 bản ghi
`InventoryAdjustmentLogs` cho cùng `(18, 1915)`, **cả 5 request `200`**.

**Bước 2 — script kiểm chạy được, và CHECK 1 nổ với số thật:**

| Cặp trùng | Tổng bản ghi | Thừa | `CostImpact` bị tính THỪA |
|---|---|---|---|
| 1 | 5 | 4 | **3.200.000 ₫** |

Đây là con số mà cửa sổ RDS không bao giờ tạo ra được — RDS đó chưa có luồng nghiệp vụ nào
từng chạy, nên nó chỉ trả về "rỗng".

**Bước 3 — áp migration lên dữ liệu bẩn. Thất bại, ĐÚNG như dự đoán:**

```
Error Number:1505 — The CREATE UNIQUE INDEX statement terminated because a duplicate key
was found for the object name 'dbo.InventoryAdjustmentLogs' and the index name
'UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId'. The duplicate key value is (18, 1915).
```

**Bước 4 — câu hỏi quan trọng hơn: nó rollback sạch không? ✅ SẠCH HOÀN TOÀN.**

| Kiểm | Kết quả |
|---|---|
| Cột `RowVersion` / `SeqPerUser` được tạo | **0** |
| Index mới được tạo | **0** |
| `__EFMigrationsHistory` ghi nhận | **không** |
| 2 index cũ | **còn nguyên** |

EF bọc cả migration trong **một** transaction. Đây là thứ đáng biết trước khi chạy trên
production, và **không** phải điều có thể suy ra từ đọc tài liệu — nó phụ thuộc việc mọi
lệnh trong migration này đều DDL-transactional trên SQL Server.

**Bước 5 — hai chốt chặn + một backfill.** Xoá bản ghi kế toán là **quyết định nghiệp vụ**,
nên migration **không tự dọn**: nó `THROW` kèm câu chỉ thẳng script phải chạy.

```
DUNG MIGRATION: co 1 cap (AuditCheckId, SerialId) trung trong InventoryAdjustmentLogs.
Unique index UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId khong tao duoc.
Chay truoc: Infrastructure/db/fixes/dedupe_inventory_adjustment_logs.sql
(no sao luu sang InventoryAdjustmentLogs_DuplicateArchive roi moi xoa).
Doi chieu so tien o CHECK 1b cua pre_migration_checks.sql voi so ton that ke toan TRUOC KHI don.
```

Script dọn chạy thật: **4 bản ghi sao lưu + xoá, còn 1, `CostImpact` 4.000.000 → 800.000 ₫**,
0 cặp trùng còn lại. Sau đó migration chạy xanh.

**Ranh giới ngược lại — `SeqPerUser` thì migration TỰ backfill.** Khác biệt quyết định:
việc đó **không xoá gì và không mất thông tin**, chỉ đánh số các hàng đang có theo đúng thứ
tự đã xảy ra, và `ROW_NUMBER` bảo đảm tính duy nhất **tự thân cấu trúc**. Không có gì để
người chịu trách nhiệm phải quyết.

> ⚠️ **`defaultValue: 0` là bẫy do CHÍNH migration tự tạo.** Nó đặt mọi hàng cũ về 0; một
> khách đã dùng cùng một voucher 2 lần (hợp lệ khi `MaxUsesPerUser > 1`) sẽ có hai hàng
> cùng `(UserId, VoucherId, 0)` → `Error 1505`. Backfill phải nằm **trước** `CreateIndex`.

## Bốn lỗi trong công cụ của chính tôi, ghi lại vì mất thời gian

1. **`SELECT TOP(0) * INTO` kế thừa IDENTITY** → `Msg 8101` ở câu `INSERT` sau đó. Bảng lưu
   trữ phải khai `CREATE TABLE` tường minh (kèm chốt đếm cột để hỏng ồn ào nếu bảng gốc thêm cột).
2. **Subquery trong `PRINT`** → `Msg 1046`. Phải gán vào biến trước.
3. **`DELETE … WHERE Id IN (SELECT Id FROM archive)`** — bảng lưu trữ tích luỹ qua các lần
   chạy, nên lần thứ hai quét cả Id đã xoá. Dùng bảng tạm khoanh đúng một lần chạy.
4. **CTE `UPDATE` phải chiếu cột đích** → `Invalid column name 'SeqPerUser'`, nghe như cột
   chưa được tạo (nó đã tạo rồi) nên rất dễ đi sai hướng.

## Nguyên trạng đã trả về

`Products=2 · ProductSerials=0 · Orders=0 · InventoryChecks=0 · InventoryAdjustmentLogs=0 ·
VoucherUsages=0 · ServiceTickets=0 · Quotations=0 · ImportReceipts=1 · Vouchers=1` —
khớp baseline đầu phiên. Hạ tầng 2 replica đã `down`. Bảng
`InventoryAdjustmentLogs_DuplicateArchive` đã xoá (4 dòng, `ArchivedAt` hôm nay,
`AuditCheckId=18` = phiếu do probe tạo — rác của phiên này, **không** phải dữ liệu có sẵn).
