# Đợt 1 + Đợt 2 — Kết quả và bàn giao

**Ngày:** 2026-08-30 · **Trạng thái:** **đợt 1 xong, mục 4.1 xong, đợt 2 xong**;
build sạch (`0 Error(s)`); **đã commit thành 9 commit trên `main`**.
**Kế hoạch gốc:** `/Users/ml/.claude/plans/hi-n-t-i-t-i-ang-memoized-quill.md`
**Trục chính:** sửa lỗi chương trình → autoscale EC2 → mở rộng (PostgreSQL, Redis).

---

## 1. Đã sửa xong trong đợt 1

### A1 — Ghi vào entity `AsNoTracking()`, lệnh gán rơi vào hư vô

Bốn vị trí. Kỹ thuật chống tái phát quan trọng hơn bản vá: **đổi tên các phương thức
no-tracking thành `...ReadOnlyAsync`** ngay ở interface trong `src/Core/Interfaces/`.
Build vỡ tại đúng mọi call-site, buộc phải đi qua từng chỗ để quyết định đọc hay ghi.
Trong repo không có test nào, compiler là "bộ test" rẻ nhất có thể có.

| Chỗ ghi | Cách sửa |
|---|---|
| `ServiceTicketService` — đánh dấu báo giá cũ là Superseded | `QuotationRepository.MarkPendingAsSupersededAsync` (một câu `ExecuteUpdateAsync`) |
| `ServiceTicketService` — cập nhật 4 trường RMA | `GetByTicketIdTrackedAsync` |
| `ServiceTicketService` ×2 — đóng bảo hành cũ | `GetActiveBySerialIdTrackedAsync` |

Chỗ đầu không dùng tracked getter mà thay bằng câu `UPDATE ... WHERE Status = 0` set-based:
**vừa sửa A1 vừa đóng luôn khe race**. Comment ngay trên đó nói "bảo đảm chỉ tồn tại duy
nhất một bản báo giá có hiệu lực" — bất biến ấy trước đây **chưa bao giờ tồn tại**.

### A3 — Check-then-act trên voucher

Có **ba** chỗ tăng `UsedCount` (tài liệu rà soát cũ ghi một chỗ): `OrderService` ×2,
`PosService` ×1. Tất cả chuyển sang `TryConsumeAsync` / `TryConsumeByCodesAsync`,
sinh `SET UsedCount = UsedCount + 1 WHERE UsedCount < Quantity` — tăng ở phía DB,
vị từ nằm cùng câu lệnh với phép gán, không còn khe kiểm-rồi-ghi.

> **Phát hiện đáng đưa vào báo cáo:** DB đã có sẵn check constraint `CK_Vouchers_Quantity`
> (`Quantity IS NULL OR UsedCount <= Quantity`), nên thoạt nhìn tưởng an toàn. Nhưng nó
> **không** bắt được lost update: hai request cùng đọc 5 rồi cùng ghi 6 làm `UsedCount`
> **đếm thiếu**, vẫn thoả ràng buộc, và voucher được dùng nhiều hơn số phát hành.
> Một ràng buộc đúng nhưng bảo vệ sai thứ.

### A3 — Duyệt / từ chối báo giá

Nhánh **từ chối** trước đây **không kiểm `Status` gì cả** — từ chối được cả báo giá đã
duyệt chỉ bằng cách bấm hai lần, không cần đồng thời. Đã thêm chốt, và biến cả hai nhánh
thành cổng nguyên tử `TryDecideAsync(quotationId, fromStatus, toStatus, ...)`
(`UPDATE ... WHERE Id = @id AND Status = @from`, trả `false` khi 0 dòng bị ảnh hưởng).

Hàm tách hai nhánh có/không có `note` là cố ý: đường duyệt không đụng tới
`CustomerDecisionNote`, truyền null vào sẽ xoá mất ghi chú đang có.

### A5 — `.ContinueWith(t => t.Result)`

Bỏ trong `ProductSerialRepository`. Nó bọc exception vào `AggregateException` nên
`catch (SqlException)` ở tầng service **không bắt được**, lỗi DB thành 500 vô danh.
Toàn repo nay còn **0** chỗ sync-over-async.

### Transaction — `ExecuteInTransactionAsync` phủ hết 18 call-site

`IUnitOfWork` bỏ hẳn `BeginTransactionAsync`/`CommitAsync`/`RollbackAsync`,
thay bằng `ExecuteInTransactionAsync(Func<Task<T>>)` chạy qua
`Database.CreateExecutionStrategy()`. Đây là **điều kiện bắt buộc** để sau này bật
`EnableRetryOnFailure`: EF Core cấm `BeginTransactionAsync()` thủ công khi có retrying
strategy, và nó ném **lúc chạy, không lúc biên dịch**.

Ba lỗi có thật được sửa kèm, không phải chuyện lý thuyết:

1. **Transaction không bao giờ được `Dispose`.** Bản cũ giữ nó trong field và chỉ
   `Commit`/`Rollback`. Nay dùng `await using` nên connection được trả về pool ngay.
2. **Rollback trên transaction đã commit.** Nhiều call-site làm việc *sau* `CommitAsync`
   vẫn nằm trong khối `try`; nếu chỗ đó ném thì `catch` gọi `RollbackAsync` trên
   transaction đã commit → **ném thêm exception thứ hai**, che mất lỗi thật.
   Nay phần sau commit nằm ngoài lambda nên không thể xảy ra.
3. **`InventoryCheckService.CreateAsync` bỏ dở transaction.** Nhánh "không có sản phẩm
   trong phạm vi" `return` thẳng ra ngoài, không commit không rollback. Truy vấn phạm vi
   vốn chỉ đọc nên đã chuyển ra **trước** transaction.

⚠️ **Chưa bật `EnableRetryOnFailure`** — xem mục 4.

### Đồng bộ tồn kho — gộp về một câu UPDATE

`InventorySyncService` trước đây COUNT rồi lặp `foreach` gọi `ExecuteUpdateAsync` từng
variant. Nay là **một** câu với subquery tương quan. Lợi ích kép: N round-trip → 1
(thu hẹp cửa sổ giữ X-lock trên `ProductVariants`), và xoá khe race giữa bước COUNT và
bước UPDATE.

**Đã xác minh SQL sinh ra** (bắt buộc theo kế hoạch — EF Core dịch subquery trong
`SetProperty` khá kén):

```sql
UPDATE [p]
SET [p].[StockQuantity] = (
    SELECT COUNT(*) FROM [ProductSerials] AS [p0]
    WHERE [p0].[VariantId] = [p].[Id] AND [p0].[Status] = CAST(0 AS tinyint))
FROM [ProductVariants] AS [p]
WHERE [p].[Id] IN (@ids1, @ids2, @ids3)
```

`ProductSerials` **không có** cột `IsDeleted` nên không có global query filter — ngữ nghĩa
giống hệt bản cũ, không có thay đổi ngầm.

### Sinh mã chứng từ — gom 7 khối về `IDocumentCodeGenerator`

7 khối trùng lặp ở 6 file → một `DocumentCodeGenerator` (`src/Service/Common/`).
Đợt 3 thay ruột bằng SEQUENCE sẽ chỉ phải sửa **một** file.

Cùng lúc sửa hai thứ:

- **`{n:D3}` → 6 chữ số.** Quá 999 chứng từ/ngày thì `-1000` sắp **trước** `-999` khi so
  chuỗi, nên truy vấn "mã cuối" luôn trả `-999`, số kế tiếp luôn ra 1000 → **sinh mã trùng
  vĩnh viễn**. Mã dài nhất `ORD-yyyyMMdd-NNNNNN` = 19 ký tự, cột rộng 20 → vừa.
- **`DateTime.Now` → `UtcNow`** ở Order/POS. Trước đây chúng dùng giờ địa phương còn các
  loại khác dùng UTC, nên ngày trong mã lệch với cột ngày (vốn luôn lưu UTC).

> **Bẫy phát sinh, đã xử lý:** riêng việc đổi độ rộng là **không an toàn** nếu vẫn tìm mã
> cuối bằng `ORDER BY Code DESC` — mã cũ `-001` luôn sắp trên mã mới `-000002`, nên hệ
> thống sẽ mãi trả về mã cũ và sinh trùng. Vì vậy 5 phương thức repository đổi từ
> `GetLastXxxCodeByDateAsync` → `GetCodesByDatePrefixAsync` (trả danh sách mã trong ngày),
> và generator tự lấy max **theo số**. Cách so sánh chuỗi bị bỏ hẳn — đó mới là nguyên
> nhân gốc của quả bom `{n:D3}`.
>
> Đã kiểm thực tế trên DB có lẫn hai định dạng:
> `KK-20260830-001` → sinh ra `KK-20260830-000002` (không phải `000001`);
> thêm `KK-20260830-000005` → sinh ra `KK-20260830-000006`.

### Bỏ hẳn `MemoryCache` cho `IsActive`

Middleware kiểm khoá tài khoản nay **đọc thẳng DB** bằng projection đúng 2 cột
(`IsActive`, `LockReason`) thay vì `FindByIdAsync` kéo toàn bộ hàng `AppUsers`.
Xoá cả 4 điểm invalidate và bỏ `IMemoryCache` khỏi constructor của
`CustomerService`/`EmployeeService`.

**Đây là thứ làm cho việc chạy 2 task không cần Redis.** Điểm mấu chốt: hướng nguy hiểm là
hướng **mở khoá** (cache nói còn hoạt động trong khi DB đã khoá), không phải hướng khoá.
Với 1 task đó là "chậm 30 giây"; với 2 task đó là "lúc được lúc không tuỳ ALB định tuyến"
— loại lỗi không tái hiện được, và là lỗi **bảo mật** chứ không phải lỗi hiệu năng.

### Chặn trần `pageSize`

`ClampPageSizeFilter` (`IAsyncActionFilter`, đăng ký global) clamp mọi tham số tên
`pageSize`/`take`/`limit`/`size` về `[1, 100]` và `page`/`pageNumber`/`pageIndex` về `>= 1`
— cả tham số rời lẫn thuộc tính bên trong model. Một file, 0 thay đổi chữ ký, phủ **cả 148
endpoint kể cả những cái viết sau này**.

Lớp thứ hai: `PagedRequest` ở `src/Shared/DTOs/Common/` với setter tự clamp, **11 DTO đã kế
thừa**. Đặt ở `Shared` nên Blazor client dùng chung → `MudTable` không gửi nổi số lớn ngay
từ phía gửi. Chọn base class thay vì chỉ dựa vào filter vì quy ước trong repo **không nhất
quán** (`OrderFilterRequest` dùng `PageIndex`, storefront mặc định 20).

### Seed role Technician — tách đôi, đúng thứ tự

Thêm `Technician`/`KTV` vào `seed_data.sql` (kèm `NormalizedName='TECHNICIAN'` ghi tay —
`RoleManager.CreateAsync` tự sinh nó qua `UpperInvariantLookupNormalizer`, INSERT tay thì
không; thiếu nó thì `FindByNameAsync`/`RoleExistsAsync` **không bao giờ** tìm thấy role),
kèm chốt kiểm đủ 4 role (`THROW` nếu thiếu). Rồi mới bọc block seed trong `Program.cs` bằng
try/catch.

**Thứ tự này bắt buộc.** Đảo lại thì `EmployeeService.AddToRoleAsync(user, "Technician")`
vô hiệu **im lặng**. Block trong `Program.cs` vẫn giữ làm lưới an toàn, **xoá hẳn ở đợt 5**
sau khi xác nhận role có trên production.

Lỗi nó chặn: hai ECS task cold-start cùng lúc (tức đúng lúc deploy) thì một task vi phạm
**hai** unique index (`RoleNameIndex` + `IX_AppRoles_RoleCode`); vi phạm ở tầng DB **ném
exception** chứ không trả `IdentityResult` thất bại, nên kiểm `result.Succeeded` không cứu
được; exception chưa bắt ở top-level statement → **process exit ≠ 0 → task chết lúc boot**.

---

## 2. Bằng chứng đã chạy được (local, $0)

Chạy trên container `hushstore_sqlserver_dev`, API ở `http://localhost:5111`.

| Kiểm | Kết quả |
|---|---|
| Build toàn solution | `0 Error(s)` |
| API khởi động ở `Development` | OK — tức DI graph đã qua `ValidateOnBuild` |
| `?pageSize=1000000` | trả về `pageSize = 100` |
| `?pageSize=-5` | trả về `pageSize = 1` |
| `?pageSize=20` | trả về `pageSize = 20` (không đụng vào giá trị hợp lệ) |
| **Khoá tài khoản trong DB rồi gọi lại ngay (0 giây chờ)** | **403 + `X-Account-Status: locked`** (trước đây phải chờ tới 30 giây) |
| **Mở khoá rồi gọi lại ngay** | **200** (trước đây cũng phải chờ 30 giây) |
| Sinh mã 6 loại chứng từ | `ORD/POS/PN/KK/ST/SRV-20260830-000001` |
| Lẫn định dạng cũ `-001` | → `-000002` ✓ (so chuỗi sẽ ra `-000001`, sai) |
| SQL của `SyncStockBatchAsync` | một câu `UPDATE ... SET = (SELECT COUNT(*) ...)` |
| `seed_data.sql` | 4 role đầy đủ, `KTV / Technician / TECHNICIAN` ✓ |

Cặp khoá/mở khoá chính là hình **before/after** thuyết phục nhất cho báo cáo, và tốn $0.

### Bằng chứng bổ sung — mục 4.1 và đợt 2 (cũng $0)

| Kiểm | Kết quả |
|---|---|
| `POST /api/inventory-checks` (đường **có transaction**) sau khi bật retry | 200, sinh `KK-20260830-000001` — tức `strategy.ExecuteAsync` bọc `BeginTransactionAsync` hợp lệ, đúng thứ EF Core **cấm** nếu làm sai |
| 8 lần đăng nhập sai liên tiếp | `400,400,400,400,400,429,429,429` — rate limit nay **theo IP** |
| Thân phản hồi 429 | `{"success":false,"message":"Bạn thao tác quá nhanh..."}` + `Retry-After: 60` (bản cũ trả **thân rỗng**) |
| 150 request tới `/health/live` | **0 lần** khác 200 → miễn trừ health check hoạt động |
| 130 request tới `/api/products` | **11 lần 429** → global limiter *có* chạy, nên miễn trừ health là **có ý nghĩa** |
| `POST /api/vouchers/available-for-order` ẩn danh | **401** (trước: 200 + liệt kê toàn bộ khuyến mãi) |
| `GET /api/employees/technicians` ẩn danh | **401** (trước: 200 + lộ danh sách nhân sự) |
| XSS: `<script>`, `onerror=`, `javascript:`, `<iframe>` gửi qua `PUT /api/products/1` | **bị loại sạch**; `<h2>`, `<p>`, `<a href="https://...">` **giữ nguyên** |

**Kiểm luồng refresh bằng trình duyệt thật** (Chrome DevTools, API `localhost:5222`
+ Blazor WASM `localhost:5214`, token TTL đặt 1 phút để quan sát được):

| Kịch bản | Kết quả |
|---|---|
| Token hết hạn lúc **điều hướng** (đường `JwtAuthenticationStateProvider`) | 1 lời gọi `/api/auth/refresh-token` chạy **trước** mọi lời gọi dữ liệu; 6 lời gọi analytics cách nhau 17ms **không** sinh refresh thứ hai |
| Token hết hạn **giữa phiên** (đường 401 của `AuthHeaderHandler`) | 12 lời gọi mang token cũ → **tất cả 401** → **đúng 1** lời gọi refresh → 12 lời gọi lại **đều thành công** |

Kịch bản thứ hai là bằng chứng quyết định cho **single-flight**: không có nó thì sẽ
có 12 lời gọi refresh, mà refresh token **xoay vòng mỗi lần dùng**, nên 11 cái sau
cầm token đã bị thu hồi — chúng thất bại **và** vô hiệu hoá kết quả của cái đầu
tiên, và người dùng bị đăng xuất **đúng lúc hệ thống đang cố giữ họ đăng nhập**.

> **Bẫy gặp khi kiểm, ghi lại để lần sau khỏi mất thời gian:** lần thử đầu ở kịch
> bản 2 **không** sinh 401 nào dù token đã quá hạn 45 giây. Lý do:
> `TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(1)` — server vẫn chấp
> nhận token quá hạn tối đa 60 giây. Vòng đời thực tế vì thế là **15 + 1 phút**.

---

## 3. Phát hiện mới trong lúc làm (chưa có trong tài liệu nào)

Xếp theo mức độ cần xử lý.

### 3.1 🔴 Hai câu hỏi nghiệp vụ **vẫn chưa trả lời được**

Đã chạy `Infrastructure/db/checks/pre_migration_checks.sql` trên DB local. **Mọi truy vấn
đều rỗng, nhưng kết quả đó vô nghĩa**: DB local gần như trống.

```
Orders = 0            OrderDetails = 0       ProductSerials = 0
ServiceTickets = 0    InventoryChecks = 0    InventoryAdjustmentLogs = 0
Quotations = 0        VoucherUsages = 0      Vouchers = 1
AppUsers = 6          Products = 2           ProductVariants = 3
```

Ngoài ra DB local trước đó **chậm 4 migration** — `AddInventoryAuditFeature` chưa từng chạy,
nên bảng `InventoryAdjustmentLogs` còn không tồn tại. (Đã `dotnet ef database update` để
đưa schema lên hiện tại; bảng nay tồn tại và rỗng.)

**Việc phải làm ở phiên sau:** bật RDS lên, chạy script này ở đó. Script **đã được sửa cho
khớp schema thật** và chạy sạch, nên chỉ cần trỏ connection string.

Kết quả quyết định công sức của đợt 3:
1. `MaxUsesPerUser` có giá trị `> 1` không → unique index đơn giản, hay phải thêm cột
   `SeqPerUser` (chênh khoảng một ngày công).
2. `InventoryAdjustmentLogs` đã có bản ghi trùng chưa → nếu có thì phát sinh **việc nghiệp
   vụ** (dọn dữ liệu + đối chiếu sổ tổn thất), không phải việc kỹ thuật.

### 3.2 CLAUDE.md đang mô tả sai schema

CLAUDE.md viết *"Every entity has `IsDeleted`, `DeletedDate`, `CreatedDate`, `CreatedBy`,
`ModifiedDate`, `ModifiedBy`"*. Thực tế trên DB:

| Bảng | `IsDeleted` |
|---|---|
| `Orders` | **KHÔNG có** |
| `InventoryChecks` | **KHÔNG có** |
| `OrderSerials`, `VoucherUsages` | **KHÔNG có** |
| `ProductSerials` | **KHÔNG có** |
| `ImportReceipts`, `ServiceTickets`, `Vouchers` | có |

`ImportReceipts` cũng không có `CreatedDate`. Đây là lý do bản đầu của script kiểm dữ liệu
chạy lỗi. Cần sửa CLAUDE.md — xem mục 5.

### 3.3 `seed_data.sql` cần `QUOTED_IDENTIFIER ON`

Chạy bằng `sqlcmd` không có cờ `-I` thì hỏng ngay câu INSERT đầu tiên:

```
Msg 1934: INSERT failed because the following SET options have incorrect settings:
'QUOTED_IDENTIFIER'.
```

DB có filtered index / computed column nên bắt buộc. **Phải kiểm task `seeder` trên ECS
gọi script bằng đường nào** — nếu cũng qua `sqlcmd` không cờ `-I` thì seed đang hỏng im
lặng trên production.

### 3.4 `seed_data.sql` idempotent theo `Id`, không theo `UserName`

Khối seed admin guard bằng `IF NOT EXISTS (... WHERE Id = @AdminUserId)`. Trên DB local đã
có sẵn một user `admin` với `Id` khác → INSERT chạy → vi phạm `UserNameIndex` → script chết
giữa chừng. Nên guard theo `NormalizedUserName` thay vì `Id`.

### 3.5 Cảnh báo lỗ hổng gói NuGet (liên quan trực tiếp phần "an ninh bảo mật" của đồ án)

`dotnet build` báo `NU1903` mức **high severity**:

- `AutoMapper` 16.0.0 — GHSA-rvv3-g6hj-g44x
- `System.Security.Cryptography.Xml` 9.0.0 và 10.0.0 — 8 advisory

Đây là điểm dễ ghi và dễ sửa cho phần bảo mật của báo cáo (nâng phiên bản gói), và là bằng
chứng cho luận điểm "kiểm soát chuỗi cung ứng phụ thuộc".

---

## 4. Việc còn lại — bắt đầu phiên sau từ đây

### 4.1 ✅ XONG — `EnableRetryOnFailure` đã bật, kèm hợp đồng retry

Commit `feat(resilience): bật EnableRetryOnFailure kèm hợp đồng retry cho 18 call-site`.

**Phát hiện lớn khi rà: 14/18 call-site KHÔNG chạy lại được** — nhiều hơn hẳn dự
kiến của kế hoạch gốc. Cơ chế hỏng, viết ra vì nó không hiển nhiên:

> Nếu lỗi transient rơi đúng lúc `CommitAsync` thì mọi `SaveChangesAsync` **bên
> trong** đã thành công rồi. EF đánh dấu entity là `Unchanged` **và** cập nhật
> snapshot giá trị gốc thành giá trị **mới**. Lần thử thứ hai gán lại đúng giá trị
> đó (`ticket.Status = 2`) thì EF thấy **không có thay đổi** → không sinh câu
> `UPDATE` nào → hàng dữ liệu (vừa bị rollback về giá trị cũ) **giữ nguyên giá trị
> cũ**. Không exception, không log. **Mất dữ liệu âm thầm.**

**Giải pháp: opt-in thay vì refactor 14 chỗ ngay.** `ExecuteInTransactionAsync`
nhận thêm `retrySafe`, **mặc định `false`**. Khi `false`, lần thử thứ hai **ném lỗi
rõ ràng kèm tên call-site** (qua `CallerMemberName`/`CallerFilePath`/`CallerLineNumber`)
thay vì làm hỏng dữ liệu. Hành vi người dùng thấy **giống hệt** trước khi bật retry.

Vì sao vẫn đáng bật cờ dù 14/18 chưa retry: giá trị lớn nhất **không** nằm ở 18 chỗ
có transaction mà ở **toàn bộ phần còn lại** — mọi query đọc, mọi `SaveChanges` đơn
lẻ, health check — tức gần như toàn bộ lưu lượng, nay tự chịu được lỗi transient.
Đó đúng là thứ xảy ra khi RDS failover, khi rolling deploy, và khi pool cạn.

**4 chỗ đã rà và bật `retrySafe: true`:** `ImportReceiptService.CreateAsync`,
`InventoryCheckService.CreateAsync`, `ServiceTicketService.CreateTicketFromSerialScanAsync`,
`ServiceTicketService.IssueServiceInvoiceAsync`.

**14 chỗ còn lại** đều có comment ⚠️ ghi **đích danh** lý do chưa bật được. Việc còn
lại ở mỗi chỗ cùng một hình dạng: **chuyển phần nạp entity vào bên trong delegate.**

### 4.2 ✅ XONG — Đợt 2 (frontend + vá bảo mật thuần code)

| # | Việc | Trạng thái |
|---|---|---|
| 1 | Refresh + retry + single-flight ở `AuthHeaderHandler` | ✅ xong, đã kiểm bằng trình duyệt thật |
| 2 | `BusyState`/`BusyScope`/`ActionButton` chống double-submit | ⚠️ **xong một phần** — xem dưới |
| 3 | `ErrorBoundary` + `ApiCall` (409 có mặt từ đợt 2) | ✅ xong |
| 4 | XSS: bỏ `MarkupString` ở `ProductDetail.razor` | ✅ xong — **đổi phương án**, xem dưới |
| 5 | Viết lại rate limiter | ✅ xong |
| 6 | Thu hẹp bề mặt anonymous, bật HSTS | ✅ xong |
| 7 | `AccessTokenExpirationMinutes` → 15 | ✅ xong, làm cuối cùng đúng như kế hoạch |

**Hai chỗ lệch khỏi kế hoạch gốc, và lệch vì dữ liệu thật nói khác:**

- **Việc 4 (XSS).** Kế hoạch chọn "bỏ `MarkupString`, render text thuần", với giả
  định định dạng duy nhất UI tạo ra được là xuống dòng. Kế hoạch cũng yêu cầu
  **kiểm dữ liệu thật trước** — đã kiểm, và kết quả **lật ngược lựa chọn**: 2/2
  sản phẩm có mô tả là **HTML thật** (`<h2>`, `<p>`, `<img>`). Render text thuần sẽ
  hiện nguyên thẻ ra cho khách. Nên chuyển sang đúng **"kế hoạch B"** mà tài liệu
  đã dự trù: `HtmlSanitizer` (namespace `Ganss.Xss`) áp ở **tầng API trên đường ghi**.
- **Việc 6 (bề mặt anonymous).** Kế hoạch bảo "xoá `[AllowAnonymous]` ở
  `EmployeesController:103`". Nhưng class là `[Authorize(Roles="Admin")]` nên xoá
  suông sẽ thành **Admin-only**, trong khi endpoint gán kỹ thuật viên
  `PUT {id}/assign` là `"Admin, Employee"` → Employee vẫn gán được nhưng **không
  tải nổi danh sách để chọn**. Đặt tường minh `[Authorize(Roles = "Admin, Employee")]`.

**Việc 2 mới xong một phần — phần còn lại, ghi rõ để không bị nhầm là đã phủ:**
`MarkAsPaid`, `OrderDetail`, và các form CRUD admin **vẫn dùng `MudButton` trần**.
Ba file cơ chế (`BusyState`/`BusyScope`/`ActionButton`) đã có sẵn và đã chứng minh
chạy đúng ở POS + 4 chỗ kiểm kê + 16 nút phiếu dịch vụ; việc còn lại thuần tuý là
quét nốt các call-site.

### 4.3 Đợt 0 còn thiếu (chưa động)

- `tools/LoadProbe/` — console app .NET, 9 kịch bản `IProbeScenario`
- `docker-compose` 2 replica API + nginx round-robin
- Đo `remainingResources` của container instance (cảnh báo RAM: api 512 + web 192 +
  migrator 512 = 1216 MiB vs ~950–985 MiB khả dụng trên t3.micro)

### 4.4 Đợt 3 trở đi

Giữ nguyên như kế hoạch gốc. **Chặn cứng: phải có kết quả script kiểm dữ liệu trên
RDS (mục 3.1) trước khi bắt đầu đợt 3.**

Bổ sung một việc mới sinh ra từ đợt 2: **rà nốt 14 call-site chưa retry-safe** (mục
4.1). Không chặn đợt 3, nhưng nên làm trước khi chạy nhiều task thật.

---

## 5. Tài liệu — ✅ ĐÃ CẬP NHẬT

Cả ba đã làm xong.

- **`CLAUDE.md`** ✅
  - Sửa khẳng định sai *"Every entity has `IsDeleted`..."* (xem 3.2)
  - Mô tả đúng cơ chế tồn kho ảo đang chạy, thay cho luồng
    `Available → Reserved → Sold` **không tồn tại**
  - Thêm quy ước: DTO phân trang mới phải kế thừa `PagedRequest`
  - Thêm quy tắc: **không cache trạng thái phân quyền/khoá tài khoản trong `MemoryCache`**
  - Thêm quy tắc: mở transaction **chỉ** qua `IUnitOfWork.ExecuteInTransactionAsync`
  - Thêm quy tắc: sinh mã chứng từ **chỉ** qua `IDocumentCodeGenerator`
- **`docs/ra-soat-ung-dung-multi-task.md`** ✅ — A1/A3/A5 đã chuyển sang "đã sửa, có
  bằng chứng"; **A6 và A7 nay đánh dấu ĐÃ SỬA** kèm bằng chứng và kèm chốt "khối HSTS
  phải đứng sau `UseForwardedHeaders`, đảo thứ tự là no-op im lặng".
- **`docs/bao-mat-he-thong.md`** ✅ — thêm mục **5.1** liệt kê đích danh 3 gói còn lỗ
  hổng mức High kèm số hiệu advisory, và nói rõ vấn đề thật không phải ba gói đó mà
  là **cảnh báo đã hiện sẵn ở mỗi lần build mà quy trình không có chỗ nào bắt buộc xử lý**.

---

## 6. Trạng thái môi trường local

- Container `hushstore_sqlserver_dev` **đang chạy**, DB `HushStoreDb` đã ở migration mới nhất.
- Mật khẩu `sa` trong volume trước đây lệch với `Infrastructure/db/.env`; đã reset bằng
  `docker run ... /opt/mssql/bin/sqlservr --reset-sa-password` trên chính volume đó nên
  **dữ liệu còn nguyên**. Mật khẩu nay khớp `.env`.
- Đã đặt lại mật khẩu user `admin` về `Admin@123` để chạy smoke test.
- Chuỗi kết nối dùng khi chạy local:
  ```
  Server=localhost,1433;Database=HushStoreDb;User Id=sa;Password=<SA_PASSWORD trong .env>;TrustServerCertificate=True;MultipleActiveResultSets=True
  ```
- **Đã commit hết**, working tree sạch. 9 commit trên `main`:

| # | Commit |
|---|---|
| 1 | `fix(inventory)`: đồng bộ tồn kho một câu UPDATE |
| 2 | `fix(security)`: bỏ cache `IsActive` + `ClampPageSizeFilter` + `PagedRequest` |
| 3 | `fix(concurrency)`: A1 + A3 + A5 + transaction + sinh mã + seed role |
| 4 | `docs`: sửa khẳng định sai về schema, script kiểm dữ liệu |
| 5 | `feat(resilience)`: bật `EnableRetryOnFailure` + hợp đồng retry *(mục 4.1)* |
| 6 | `feat(auth)`: refresh + retry + single-flight *(đợt 2 việc 1)* |
| 7 | `fix(security)`: vá stored XSS bằng sanitizer *(việc 4)* |
| 8 | `fix(security)`: rate limiter theo IP + bề mặt ẩn danh + HSTS *(việc 5, 6)* |
| 9 | `feat(ui)`: chống double-submit *(việc 2)* · `feat(ui)`: ErrorBoundary + ApiCall *(việc 3)* · `feat(security)`: token 15 phút *(việc 7)* |

> **Lệch khỏi "gợi ý tách 5 commit" của bản trước, và lý do đáng ghi:** ba nhóm
> `fix(concurrency)` + `refactor(uow)` + `refactor(codegen)` **không tách được**.
> Bọc thân phương thức vào `ExecuteInTransactionAsync` làm **thụt lề lại toàn bộ
> khối**, nên bản vá A1 và lời gọi `IDocumentCodeGenerator` nằm **đúng trên những
> dòng** mà refactor transaction đã viết lại — cùng hunk, không phải cùng file.
> Tách ra sẽ phải dựng tay các trạng thái trung gian **không build được**. Hai
> nhóm tách được (`fix(inventory)`, `fix(security)`) thì đã tách.

- **Gói mới thêm:** `HtmlSanitizer 9.2.1039` (namespace `Ganss.Xss`) ở `Service`,
  cho việc 4 của đợt 2.
