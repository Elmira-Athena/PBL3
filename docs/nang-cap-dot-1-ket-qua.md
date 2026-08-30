# Đợt 1 — Kết quả và bàn giao

**Ngày:** 2026-08-30 · **Trạng thái:** đợt 1 xong, build sạch (`0 Error(s)`), chưa commit.
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

### 4.1 Việc đầu tiên: bật `EnableRetryOnFailure` (phần cuối của đợt 1)

18 call-site nay đã phủ hết bằng `ExecuteInTransactionAsync`, tức **điều kiện cần đã đủ**.
Nhưng **chưa bật**, và lý do phải hiểu trước khi bật:

> Khi retry, delegate chạy lại **toàn bộ**. Change Tracker **không** được clear giữa các
> lần thử — và đó là cố ý: nhiều call-site nạp entity **trước** khi mở transaction rồi sửa
> chúng bên trong; clear sẽ tháo mất những entity đó và lệnh sửa lại rơi vào hư vô (đúng
> lỗi A1 mà đợt này vừa sửa). Ngược lại, không clear thì lần thử thứ hai làm việc trên
> entity đã bị sửa dở ở lần thử thứ nhất.

Nên phải **rà từng call-site**, không bật hàng loạt. Với mỗi chỗ, trả lời: *thứ gì tính
trước khi vào transaction? nó có idempotent không? nếu không thì chuyển vào trong delegate.*

Danh sách 18 chỗ (dùng `grep -rn "ExecuteInTransactionAsync" --include='*.cs' src/Service/`):
`ServiceTicketService` ×9 · `InventoryCheckService` ×4 · `OrderService` ×2 ·
`ImportReceiptService` ×1 · `InventoryExportService` ×1 · `PosService` ×1.

### 4.2 Đợt 2 — Frontend + vá bảo mật thuần code ($0, chạy song song được)

Chưa động tới dòng nào. Thứ tự trong kế hoạch gốc:

1. Refresh + retry + single-flight ở `AuthHeaderHandler` (bốn ràng buộc ép cấu trúc —
   xem kế hoạch gốc, mỗi cái là chỗ một bản viết ngây thơ sẽ vỡ)
2. `BusyState` / `BusyScope` / `ActionButton` — chống double-submit ở 25 chỗ bằng 3 file
3. `ErrorBoundary` + `SendApiAsync` (409 phải có mặt **từ đợt 2**, dù đợt 3 mới bắt đầu trả)
4. XSS: bỏ `MarkupString` ở `ProductDetail.razor:276`
5. Viết lại rate limiter — **hiện tại nó là một DoS tự gây ra**
6. Thu hẹp bề mặt anonymous, bật HSTS
7. **Cuối cùng**, sau khi 6 bước kiểm refresh đã xanh: `AccessTokenExpirationMinutes` → 15

### 4.3 Đợt 0 còn thiếu

- `tools/LoadProbe/` — console app .NET, 9 kịch bản `IProbeScenario`
- `docker-compose` 2 replica API + nginx round-robin
- Đo `remainingResources` của container instance (cảnh báo RAM: api 512 + web 192 +
  migrator 512 = 1216 MiB vs ~950–985 MiB khả dụng trên t3.micro)

### 4.4 Đợt 3 trở đi

Giữ nguyên như kế hoạch gốc. **Chặn cứng: phải có kết quả script kiểm dữ liệu trên RDS
(mục 3.1) trước khi bắt đầu đợt 3.**

---

## 5. Tài liệu cần cập nhật (chưa làm)

- **`CLAUDE.md`**
  - Sửa khẳng định sai *"Every entity has `IsDeleted`..."* (xem 3.2)
  - Mô tả đúng cơ chế tồn kho ảo đang chạy, thay cho luồng
    `Available → Reserved → Sold` **không tồn tại**
  - Thêm quy ước: DTO phân trang mới phải kế thừa `PagedRequest`
  - Thêm quy tắc: **không cache trạng thái phân quyền/khoá tài khoản trong `MemoryCache`**
  - Thêm quy tắc: mở transaction **chỉ** qua `IUnitOfWork.ExecuteInTransactionAsync`
  - Thêm quy tắc: sinh mã chứng từ **chỉ** qua `IDocumentCodeGenerator`
- **`docs/ra-soat-ung-dung-multi-task.md`** — chuyển A1/A3/A5 sang "đã sửa, có bằng chứng";
  sửa mục A7 (production không có cả redirect lẫn HSTS vì `if (!IsProduction())`)
- **`docs/bao-mat-he-thong.md`** — bổ sung phát hiện 3.5 (lỗ hổng gói NuGet)

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
- **Chưa commit gì.** 37 file sửa, 5 đường dẫn mới:
  `Infrastructure/db/checks/`, `src/API/Filters/`, `src/Core/Interfaces/IDocumentCodeGenerator.cs`,
  `src/Service/Common/`, `src/Shared/DTOs/Common/PagedRequest.cs`.

**Gợi ý tách commit** (kế hoạch gốc yêu cầu phần transaction đi PR riêng):

1. `fix(concurrency)`: A1 + A3 voucher + A3 báo giá + A5 + seed role
2. `refactor(uow)`: `ExecuteInTransactionAsync` + 18 call-site — **riêng, không trộn**
3. `fix(inventory)`: `InventorySyncService` một câu UPDATE
4. `refactor(codegen)`: `IDocumentCodeGenerator` + 5 repository
5. `fix(security)`: bỏ cache `IsActive` + `ClampPageSizeFilter` + `PagedRequest`
