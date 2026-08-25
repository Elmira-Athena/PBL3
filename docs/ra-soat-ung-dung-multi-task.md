# Rà soát ứng dụng trước khi chạy nhiều task — HushStore

> **Trạng thái:** ĐÃ RÀ SOÁT, **CHƯA SỬA**. Tài liệu này là *kết quả điều tra*, không phải nhật ký sửa lỗi.
> **Quyết định ngày 2026-08-25:** hoãn toàn bộ việc sửa sang sau khi kết thúc dự án hạ tầng AWS (Đề tài 513).
> Hạ tầng hiện tại chạy **đúng 1 task**, nên phần lớn các lỗi ở nhóm B chưa thể xảy ra. Nhóm A thì **đang xảy ra ngay bây giờ**.

## Vì sao có tài liệu này

Câu hỏi ban đầu rất hẹp: *"nâng ASG lên 2 instance thì có sinh deadlock, tranh chấp tài nguyên hay sai lệch dữ liệu không?"*

Trả lời thẳng: **có, nhưng đó không phải phát hiện đáng kể nhất.** Đáng kể hơn là — cuộc rà soát tìm ra **nhiều lỗi đang tồn tại ngay bây giờ, với đúng một task, hơn là lỗi do việc scale gây ra.**

Lý do kỹ thuật của điều nghe có vẻ nghịch lý ấy: **một tiến trình ASP.NET Core phục vụ request trên nhiều thread.** "Một instance" không tuần tự hoá bất cứ thứ gì. Hai request đồng thời vào cùng một tiến trình đã đủ đạp lên nhau ở mọi chỗ dùng mẫu *đọc rồi ghi* mà không có khoá. Scale lên 2 task chỉ **tăng xác suất** và **vô hiệu hoá các cách sửa bằng `lock`**, chứ không tạo ra lớp lỗi mới.

## Phạm vi đã đọc

5 subagent chạy song song, mỗi agent một tầng, đọc **toàn văn** chứ không grep:

| Agent | Phạm vi | Kết quả |
|---|---|---|
| 1 | `InventoryCheckService`, `ImportReceiptService`, `InventoryExportService`, `PosService`, `OrderService` + repository tương ứng | 17 hazard, 2 CRITICAL |
| 2 | `ServiceTicketService`, `QuotationService`, `WarrantyService` + repository | 14 hazard, 4 CRITICAL |
| 3 | Toàn bộ state per-instance trong RAM (166 file, 4 project) | 1 cache duy nhất, 4 điểm invalidate |
| 4 | Startup / background job / ghi file / log | 1 điểm duy nhất chạy tự động lúc boot, và nó có lỗi |
| 5 | Tầng truy cập dữ liệu: pool, DbContext lifetime, sync-over-async, `AsNoTracking` | pool 100/process, không `EnableRetryOnFailure` |
| 6 | Graceful shutdown, thời gian rút target, deployment percentage | ngân sách in-flight 35 giây, biên an toàn = 0 |

Các con số nền đã **xác minh trực tiếp**, không suy đoán:

- `grep -rn RowVersion src/` → **0 kết quả**. Toàn hệ thống không có concurrency token nào.
- `grep -rn "SemaphoreSlim|lock (|Interlocked|sp_getapplock" src/` → **0 kết quả**. Không có khoá nào, cả trong tiến trình lẫn phân tán.
- [`Program.cs:81`](../src/API/Program.cs#L81) — `UseSqlServer(...)` **không** có `EnableRetryOnFailure`, **không** truyền `IsolationLevel`.
- [`UnitOfWork.cs:20-23`](../src/Infrastructure/Data/UnitOfWork.cs#L20-L23) — `BeginTransactionAsync()` dùng mức mặc định = **READ COMMITTED**.
- Connection string thật (từ [`modules/data/main.tf:126-134`](../infra/tf/modules/data/main.tf#L126-L134)) **không** có `Max Pool Size` → mặc định .NET = **100 connection/tiến trình**.

### READ COMMITTED nghĩa là gì ở đây

Đây là mấu chốt của gần như mọi lỗi bên dưới, nên nói rõ một lần:

Dưới READ COMMITTED, một câu `SELECT` lấy shared lock rồi **nhả ngay khi đọc xong** — không giữ tới cuối transaction. Nên đoạn code này:

```csharp
if (await _repo.ExistsAsync(x))   // đọc — lock nhả ngay
    return Fail("đã tồn tại");
await _repo.AddAsync(x);          // ghi — lúc này điều kiện trên có thể đã sai
```

**không** được transaction bảo vệ. Hai request cùng chạy qua dòng `if` trước khi bất kỳ ai chạm dòng `Add`. Mở `BeginTransactionAsync()` bao quanh nó **không sửa được gì** — đây là hiểu lầm phổ biến nhất trong code hiện tại: nhiều chỗ đã mở transaction và tưởng rằng thế là an toàn.

Mẫu này có tên: **check-then-act**, và hệ quả của nó là **lost update**.

---

## A. Lỗi ĐANG TỒN TẠI ngay bây giờ (1 task cũng vỡ)

Sửa những mục này không liên quan gì tới AWS, không tốn tiền hạ tầng, và nên làm trước tiên khi quay lại app.

### A1 — Ghi vào entity `AsNoTracking()`: lệnh gán không bao giờ vào DB

**Mức độ: nghiêm trọng nhất trong toàn bộ danh sách**, vì nó sai kể cả khi hoàn toàn không có đồng thời.

[`ServiceTicketService.cs:341-344`](../src/Service/ServiceTickets/ServiceTicketService.cs#L341-L344) đọc báo giá cũ rồi đánh dấu "bị thay thế":

```csharp
var oldQuotations = await _quotationRepository.GetByTicketIdAsync(ticketId);
foreach (var q in oldQuotations.Where(q => q.Status == (byte)0))
{
    q.Status = (byte)3;   // ← không bao giờ được lưu
}
```

Nhưng [`QuotationRepository.cs:37`](../src/Infrastructure/Repositories/QuotationRepository.cs#L37) có `.AsNoTracking()`. Entity trả về **không nằm trong Change Tracker**, nên `SaveChangesAsync()` không sinh câu `UPDATE` nào. Comment ngay phía trên nói *"bảo đảm chỉ tồn tại duy nhất một bản báo giá có hiệu lực"* — bất biến ấy **chưa bao giờ tồn tại**.

Cùng lỗi ở hai chỗ nữa: [`ServiceTicketService.cs:608-611`](../src/Service/ServiceTickets/ServiceTicketService.cs#L608-L611), nguồn `AsNoTracking` là [`RmaShipmentRepository.cs:20`](../src/Infrastructure/Repositories/RmaShipmentRepository.cs#L20) và [`WarrantyRepository.cs:24`](../src/Infrastructure/Repositories/WarrantyRepository.cs#L24).

Đây là mặt trái của quy tắc `.AsNoTracking()` bắt buộc trong `CLAUDE.md`: quy tắc đúng cho đường **đọc**, nhưng repository không phân biệt được caller định đọc hay định ghi. Cần tách phương thức `GetXxxTrackedAsync` riêng cho đường ghi — mẫu này đã có sẵn ở `OrderRepository.GetByIdWithDetailsTrackedAsync`.

### A2 — Sinh mã tự tăng theo ngày: 7 vị trí, cùng một mẫu sai

| Tiền tố | Vị trí | Cột unique |
|---|---|---|
| `ORD-` | [`OrderService.cs:145-155`](../src/Service/Orders/OrderService.cs#L145-L155), [`:283-293`](../src/Service/Orders/OrderService.cs#L283-L293) | `OrderCode` |
| `ORD-` | [`PosService.cs:281-291`](../src/Service/Pos/PosService.cs#L281-L291) | `OrderCode` |
| `PN-` | [`ImportReceiptService.cs:316-336`](../src/Service/ImportReceipts/ImportReceiptService.cs#L316-L336) | `ReceiptCode` |
| `KK-` | [`InventoryCheckService.cs:918-931`](../src/Service/Inventory/InventoryCheckService.cs#L918-L931) | `CheckCode` |
| `ST-` | [`ServiceTicketService.cs:161-169`](../src/Service/ServiceTickets/ServiceTicketService.cs#L161-L169) | `TicketCode` |
| `SRV-` | [`ServiceTicketService.cs:1041-1052`](../src/Service/ServiceTickets/ServiceTicketService.cs#L1041-L1052) | `InvoiceCode` |

Mẫu chung: `SELECT TOP 1 ... ORDER BY Code DESC` → cắt hậu tố → `int.Parse` → `+1` → ghi. Hai request đồng thời đọc cùng `ORD-20260825-005`, cả hai tính `006`, cột có unique index nên **người thua nhận SQL violation** → `UseExceptionHandler` ([`Program.cs:291-302`](../src/API/Program.cs#L291-L302)) trả HTTP 500 *"Lỗi máy chủ nội bộ."*

Khách hàng thấy: **bấm Đặt hàng báo lỗi máy chủ, giỏ hàng vẫn nguyên, thử lại thì được.**

Một lỗi ẩn kèm theo: định dạng `{nextIndex:D3}`. Quá 999 phiếu/ngày, `ST-...-1000` sắp **trước** `ST-...-999` trong phép so sánh chuỗi → hàm sinh mã vĩnh viễn trả về mã trùng. Không phải lỗi đồng thời, nhưng là quả bom hẹn giờ trên cùng dòng code.

**Cách sửa đúng:** SQL Server `SEQUENCE`, hoặc bắt `DbUpdateException` unique-violation rồi thử lại 2-3 lần, hoặc bỏ mã tuần tự (dùng ngày + `Id` IDENTITY). **Không dùng `lock`/`SemaphoreSlim`** — vô nghĩa qua hai tiến trình, và không nên viết một cách sửa mà ta biết trước sẽ phải vứt.

### A3 — Check-then-act trên tiền và tồn kho

Bốn vị trí có hậu quả tài chính hoặc tồn kho:

| Vị trí | Bất biến bị vi phạm | Hậu quả |
|---|---|---|
| [`OrderService.cs:415`](../src/Service/Orders/OrderService.cs#L415) → [`:209`](../src/Service/Orders/OrderService.cs#L209) | `Voucher.UsedCount < UsageLimit` | Voucher dùng **quá lượt** — mất tiền thật |
| [`ServiceTicketService.cs:420`](../src/Service/ServiceTickets/ServiceTicketService.cs#L420) → [`:433-437`](../src/Service/ServiceTickets/ServiceTicketService.cs#L433-L437) | `quotation.Status == 0` mới được duyệt | Một báo giá **được duyệt hai lần**, phiếu rơi vào trạng thái mâu thuẫn với lịch sử |
| [`ServiceTicketService.cs:147`](../src/Service/ServiceTickets/ServiceTicketService.cs#L147) → [`:203`](../src/Service/ServiceTickets/ServiceTicketService.cs#L203) | 1 phiếu mở/serial | **Không có unique index** hỗ trợ → hai phiếu cùng tồn tại, **im lặng**, không exception |
| [`InventoryCheckService.cs:645-651`](../src/Service/Inventory/InventoryCheckService.cs#L645-L651) → [`:748`](../src/Service/Inventory/InventoryCheckService.cs#L748) | 1 lần duyệt/phiếu kiểm kê | **Không có unique index** trên `(AuditCheckId, SerialId)` → ghi trùng `InventoryAdjustmentLog`, **kế toán tổn thất bị nhân đôi** |

Hai dòng cuối nguy hiểm hơn hai dòng đầu: khi **có** unique index, race biến thành exception ồn ào (500) — xấu nhưng nhìn thấy được. Khi **không có** index, race biến thành dữ liệu sai âm thầm.

Một thiếu sót riêng, không cần đồng thời mới lộ: [`ServiceTicketService.cs:472-476`](../src/Service/ServiceTickets/ServiceTicketService.cs#L472-L476) — `RejectQuotationAsync` đọc báo giá nhưng **không kiểm `quotation.Status` gì cả** (khác hẳn nhánh Accept ở `:420`). Từ chối được cả báo giá đã duyệt, chỉ cần bấm hai lần.

### A4 — Ghi đè trạng thái Serial một cách mù quáng

[`InventoryCheckService.cs:669-674`](../src/Service/Inventory/InventoryCheckService.cs#L669-L674) và [`:720-724`](../src/Service/Inventory/InventoryCheckService.cs#L720-L724):

```csharp
if (currentStatus == (byte)SerialStatus.Available)
{
    ...
    row.Serial.Status = (byte)SerialStatus.Lost;
}
```

`currentStatus` đọc ở dòng 660, `SaveChanges` mãi tận dòng 752. Không có RowVersion, nên EF sinh `UPDATE ProductSerials SET Status=5 WHERE Id=@p` — **không có mệnh đề nào kiểm trạng thái**. Một serial được `PosService` bán mất trong khoảng thời gian ấy sẽ bị ghi đè thẳng từ `Sold` sang `Lost`.

Comment ngay trên dòng đó ghi *"QUY TẮC NGHIỆP VỤ CỐT LÕI (BR1)"* — nhưng câu `if` đánh giá trên bộ nhớ cũ, nên nó **không bảo vệ gì cả**.

Cách sửa rẻ nhất và đúng nhất: thêm `RowVersion` (`IsRowVersion()`) vào `ProductSerial`. EF sẽ tự thêm predicate vào `WHERE`, và ném `DbUpdateConcurrencyException` khi 0 dòng bị ảnh hưởng — biến ghi đè im lặng thành lỗi nhìn thấy được.

### A5 — Những mục nhỏ hơn nhưng đã xác minh

- [`EmployeesController.cs:103`](../src/API/Controllers/Admin/EmployeesController.cs#L103) — `[AllowAnonymous]` trên `GetTechnicians()`. Một endpoint trong khu vực `Admin/`, trả danh sách nhân viên, **không cần đăng nhập**, và có truy vấn DB. Vừa là rò rỉ thông tin nhân sự vừa là điểm gọi rẻ để làm cạn pool.
- [`ProductSerialRepository.cs:159-167`](../src/Infrastructure/Repositories/ProductSerialRepository.cs#L159-L167) — `.ToListAsync().ContinueWith(t => t.Result...)`, chỗ **duy nhất** trong repo dùng `.Result`. Không deadlock, nhưng exception bị bọc thành `AggregateException` nên khối `catch (SqlException)` ở tầng service **không bắt được** — lỗi DB rơi thẳng thành 500 vô danh đúng lúc RDS đang quá tải.
- [`UnitOfWork.cs:20-39`](../src/Infrastructure/Data/UnitOfWork.cs#L20-L39) — `_transaction` không bao giờ được gán `null` sau commit/rollback; `RollbackAsync` ném lỗi nếu `_transaction == null`.
- `PageSize` không bị chặn trần ở tầng API — một request `?pageSize=100000` kéo nguyên bảng về RAM.
- [`InventorySyncService.cs:38-53`](../src/Service/Inventory/InventorySyncService.cs#L38-L53) — vòng lặp phát **N câu UPDATE riêng lẻ**, và được gọi *bên trong* transaction đang mở ở `InventoryCheckService.cs:757`, giữ X-lock trên `ProductVariants` tới tận commit. Thứ tự khoá `ProductSerials → ProductVariants`; nếu ở đâu đó có đường khoá ngược lại thì đây là công thức deadlock.
- Không có `EnableRetryOnFailure` → mọi lỗi transient (1205 deadlock, TCP reset lúc RDS `modifying`) đều thành HTTP 500 trần, và **thông báo lỗi gốc bị `UseExceptionHandler` che mất**.

---

### A6 — Access token JWT sống 7 ngày, làm refresh token thành vô nghĩa

Phát hiện ngày 2026-08-25 khi kiểm chứng các giá trị TTL để viết mục *Chứng chỉ TLS
và TTL* của [bao-mat-he-thong.md](bao-mat-he-thong.md).

[`appsettings.json:16`](../src/API/appsettings.json#L16) đặt:

```json
"AccessTokenExpirationMinutes": 10080,   // = 7 ngày
"RefreshTokenExpirationDays": 7
```

Và production **không ghi đè** giá trị đó: task definition ở
[`modules/ecs/taskdef.tf`](../infra/tf/modules/ecs/taskdef.tf) chỉ inject
`JwtSettings__SecretKey`, không inject `JwtSettings__AccessTokenExpirationMinutes`.
Nên 10080 phút là giá trị **thật đang chạy**.

**Vì sao đây là lỗi, không phải một lựa chọn.** Mô hình access token + refresh
token chỉ có ý nghĩa khi hai thời hạn **lệch nhau**: access token ngắn để giới hạn
thiệt hại nếu bị lộ, refresh token dài để người dùng không phải đăng nhập lại.
Đặt hai cái bằng nhau thì refresh token không mua được gì — nó chỉ thêm một đường
tấn công (một bí mật nữa phải lưu, một endpoint nữa phải bảo vệ) mà không giảm
được rủi ro nào.

**Hệ quả cụ thể.** JWT là không trạng thái: cấp rồi thì không thu lại được. Nên
một access token bị lộ (log, devtools, máy dùng chung) dùng được **7 ngày**. Thứ
duy nhất còn chặn là middleware kiểm cờ `IsActive` với cache 30 giây. Nghĩa là
middleware đó **không phải lớp bổ sung cho chắc — nó đang là lớp phòng thủ chính**,
và [B2](#b2--cache-khoá-tài-khoản-không-xuyên-task-và-hướng-mở-khoá-mới-là-hướng-nguy-hiểm)
nói rõ lớp đó còn có vấn đề riêng khi chạy nhiều task.

**Đáng chú ý: `CLAUDE.md` mô tả đúng ý định.** Nó viết *"15-min access token +
7-day refresh token"*. Nên đây là code **lệch khỏi thiết kế**, không phải thiết kế
sai — và cách sửa là kéo code về đúng `CLAUDE.md`, không phải sửa `CLAUDE.md` theo
code.

**Sửa:** đặt `AccessTokenExpirationMinutes = 15`. Một dòng. Nhưng phải kiểm luôn
phía client: Blazor WASM có xử lý 401 bằng cách gọi refresh rồi thử lại request
hay không. Nếu chưa có, hạ thời hạn xuống 15 phút sẽ làm người dùng bị đăng xuất
mỗi 15 phút — tức lỗi hiện tại đang **che** một thiếu sót ở tầng client. Việc kiểm
đó thuộc [D1](#d1--tầng-frontend-blazor-chưa-rà-soát-dòng-nào), chưa làm.

### A7 — Không bật HSTS

[`Program.cs:311`](../src/API/Program.cs#L311) gọi `app.UseHttpsRedirection()`
nhưng **không** gọi `app.UseHsts()`. Nên response không có header
`Strict-Transport-Security`.

Điều HSTS làm mà redirect 301 không làm được: nó dặn trình duyệt **tự đổi sang
HTTPS trước khi gửi request**, cho những lần sau. Không có nó, request **đầu tiên**
của mỗi phiên vẫn đi bằng HTTP và bị chặn giữa đường được — redirect 301 tới quá
muộn, vì lúc đó request đã bay qua mạng rồi.

Ở hệ thống này rủi ro nhỏ hơn bình thường: Cloudflare đứng trước và đang bật
Full (strict), nên đoạn người dùng ↔ Cloudflare đã mã hoá. Nhưng **bản thân ứng
dụng** thì chưa có lớp này, và đó là một khác biệt đáng ghi: bảo vệ đang đến từ
cấu hình của một dịch vụ bên ngoài, không đến từ code.

**Sửa:** thêm `app.UseHsts()` ở nhánh Production. Lưu ý `max-age` mặc định của
.NET là 30 ngày, và HSTS **khó lùi**: trình duyệt đã nhớ thì trong `max-age` đó nó
từ chối HTTP cho tên miền này, kể cả khi bạn muốn quay lại. Nên bật với `max-age`
ngắn trước, xác nhận không có subdomain nào cần HTTP, rồi mới nâng.

---

## B. Lỗi chỉ vỡ khi có từ 2 task trở lên

### B1 — Seed role `Technician` lúc khởi động làm chết task

[`Program.cs:271-278`](../src/API/Program.cs#L271-L278): `RoleExistsAsync("Technician")` rồi `CreateAsync(...)`, **không try/catch**, nằm trong top-level statement.

Hai task cold-start cùng lúc — tức **đúng lúc deploy** — cả hai thấy role chưa có, cả hai INSERT. Task thứ hai vi phạm **hai** unique index cùng lúc: `RoleNameIndex` trên `NormalizedName` và index trên `RoleCode` ([`HushStoreDbContext.cs:86`](../src/Infrastructure/Data/HushStoreDbContext.cs#L86)). Vi phạm unique ở tầng DB **ném exception**, không trả về `IdentityResult` thất bại — nên kiểm `result.Succeeded` cũng không cứu được. Exception chưa bắt trong top-level statement → tiến trình thoát khác 0 → **ECS task chết ngay lúc boot**.

Triệu chứng khó chịu: lần khởi động lại sẽ thành công (role đã tồn tại), nên biểu hiện là *một trong hai task chết đúng một lần rồi tự lành* — rất dễ trôi qua trong log deploy.

**Bẫy khi sửa:** `seed_data.sql` chỉ tạo 3 role `ADMIN`/`EMPLOYEE`/`CUSTOMER`, **không có** `Technician`. Dòng `Program.cs:276` hiện là nơi **duy nhất** trong toàn hệ thống tạo role này. Xoá block mà chưa thêm vào SQL trước thì `EmployeeService.cs:116` và `:141` (`AddToRoleAsync(user, "Technician")`) sẽ vỡ. **Thêm vào SQL trước, xoá sau.**

Rủi ro thứ hai của cùng block, đúng cả với 1 task: nó làm startup của API phụ thuộc vào việc DB *đang sống và cho ghi*. RDS chớp tắt lúc task đang lên → crash loop. Trong khi `/health/ready` tồn tại chính là để xử lý DB chết một cách mềm mại.

### B2 — Cache khoá tài khoản không xuyên task, và hướng MỞ KHOÁ mới là hướng nguy hiểm

Toàn backend có **đúng một** cache key: `user_isactive_{userId}`, TTL 30 giây ([`Program.cs:328-336`](../src/API/Program.cs#L328-L336)). Bốn điểm invalidate:

| Vị trí | Đường |
|---|---|
| [`CustomerService.cs:228`](../src/Service/Customers/CustomerService.cs#L228) | khoá |
| [`CustomerService.cs:249`](../src/Service/Customers/CustomerService.cs#L249) | **mở khoá** |
| [`EmployeeService.cs:171`](../src/Service/Employees/EmployeeService.cs#L171) | khoá |
| [`EmployeeService.cs:192`](../src/Service/Employees/EmployeeService.cs#L192) | **mở khoá** |

Hướng **khoá** chậm 30 giây là nới lỏng — biết rồi, chấp nhận được.

Hướng **mở khoá** chậm 30 giây thì tệ hơn hẳn, và đây là điều cuộc rà soát mới phát hiện: admin bấm "Mở khoá", request rơi vào task A → chỉ cache của A bị xoá. Người dùng vừa được mở khoá đăng nhập lại, request đi vào task B → B vẫn giữ entry `IsActive=false` → trả **403 kèm header `X-Account-Status: locked`**. Frontend đọc header đó và **đá session ra ngay**. Người dùng thấy: mở khoá xong vẫn bị đá ra, *lúc được lúc không* tuỳ ALB định tuyến.

Sửa: hoặc bỏ MemoryCache và đọc `IsActive` thẳng mỗi request (một index-seek theo PK, rẻ), hoặc chuyển sang `IDistributedCache`. Sửa **cả 4 vị trí cùng lúc**.

### B3 — Rate limiter đăng nhập chỉ còn một nửa hiệu lực

`AddFixedWindowLimiter("LoginRateLimit", PermitLimit = 5, Window = 1 phút)` ([`Program.cs:217-221`](../src/API/Program.cs#L217-L221)) đếm trong RAM của từng tiến trình. Hai task → 10 lần thử/phút.

**Không phải lỗ hổng brute-force**, và cần nói rõ điều này vì báo cáo bảo mật có nhắc: `Lockout.MaxFailedAccessAttempts = 5` ([`Program.cs:101`](../src/API/Program.cs#L101)) là cơ chế của ASP.NET Core Identity, **đếm trong DB** (`AccessFailedCount`, `LockoutEnd`), nên nó xuyên task nguyên vẹn. Rate limiter chỉ là phòng thủ chiều sâu ở trên. Scale làm nó **suy giảm**, không làm nó **thủng**.

### B4 — Graceful shutdown: ngân sách 35 giây, biên an toàn bằng 0

| Đại lượng | Giá trị | Nguồn |
|---|---|---|
| `deregistration_delay` | **5s** | [`alb.tf:83`](../infra/tf/modules/alb/alb.tf#L83) |
| ECS `stopTimeout` | **không khai** → về mặc định 30s | [`user_data.sh.tftpl:26`](../infra/tf/modules/ecs/user_data.sh.tftpl#L26) |
| .NET `HostOptions.ShutdownTimeout` | **30s** mặc định, không nơi nào ghi đè | (đo trực tiếp bằng `new HostOptions()` trên net10.0) |
| Ngân sách request đang bay | **35s**, biên = **0** | |

Hai đồng hồ 30 giây chạy song song mà không ai nhường ai: `SIGTERM` khởi động cả `stopTimeout` của ECS lẫn `ShutdownTimeout` của .NET **cùng một lúc**. Request dài bị cắt giữa chừng khi rolling deploy.

Quy tắc thứ tự phải giữ: **`deregistration_delay` + `ShutdownTimeout` < `stopTimeout`**.
Bộ số đề xuất: **30 / 45 / 90**.

### B5 — Connection pool

100 connection/tiến trình (mặc định .NET, không override ở đâu). 2 task = **200**. Cộng task migrator chạy chồng lúc deploy thì đỉnh lý thuyết ~300.

RDS `db.t3.micro` chỉ có 2 vCPU. Ngưỡng thật của instance **không đọc được từ code** — repo không có parameter group nào set `user connections`. Muốn biết phải chạy trên instance thật:

```sql
SELECT @@MAX_CONNECTIONS, value FROM sys.configurations WHERE name = 'max worker threads';
```

Đặt `Max Pool Size=30; Min Pool Size=2; Connect Timeout=15` vào connection string ở [`modules/data/main.tf:126-134`](../infra/tf/modules/data/main.tf#L126-L134) **ngay lúc chuyển sang 2 task**: 2×30 = 60, dư cho 2 vCPU mà vẫn chặn được bão connection.

---

## C. Những thứ KHÔNG phải vấn đề — đã kiểm và loại trừ

Ghi lại để lần sau không phải rà lại:

- **Không có background job nào.** Grep `AddHostedService`, `IHostedService`, `BackgroundService`, `PeriodicTimer`, `Timer`, `Task.Run`, `Hangfire`, `Quartz`, `Coravel` trên toàn `src/` → **0 kết quả**. Chỉ có đúng block seed role ở B1 chạy tự động lúc boot.
- **Không ghi file xuống đĩa local.** Upload ảnh stream thẳng vào S3 (`ImageController.cs:44-45` → `S3StorageService.cs:35`), key dùng `Guid.NewGuid()` nên hai task không đè nhau. Export Excel hoàn toàn trong RAM (`new ExcelPackage()` không truyền `FileInfo`). Không có PDF generator.
- **Log ra stdout.** `Serilog.AspNetCore` có trong csproj nhưng **chưa từng được cấu hình** (`UseSerilog`, `Log.Logger`, `LoggerConfiguration` → 0 kết quả) → vẫn là console provider mặc định → CloudWatch. Không có file sink. Đúng cho ECS.
- **Không có migration lúc runtime.** `MigrateAsync`/`EnsureCreated` → 0 kết quả. Đã tách sang task migrator riêng.
- **Không có DbContext nào bị inject vào singleton.** Singleton duy nhất là `AddSingleton<IAmazonS3>` — stateless.
- **Không có DbContext nào dùng song song trên nhiều thread.** `Task.WhenAll`, `Parallel.*`, `AsParallel`, `async void` trên `Infrastructure`+`Service`+`API` → 0 kết quả.
- **Không có endpoint cronjob** nào để scheduler gọi định kỳ.

---

## D. CHƯA KIỂM — việc còn phải làm khi quay lại app

Đây là phần quan trọng nhất của tài liệu này: những gì cuộc rà soát **không** đụng tới, để lần sau không nhầm tưởng là đã sạch.

### D1 — Tầng frontend Blazor: chưa rà soát dòng nào

Toàn bộ cuộc rà soát dừng ở backend. `src/Client/` chưa được đọc. Cụ thể cần kiểm:

- Component nào giữ state qua nhiều lần render mà giả định server có bộ nhớ.
- Xử lý HTTP 403 kèm header `X-Account-Status` — logic kick session (liên quan trực tiếp tới B2).
- Có chỗ nào bấm nút hai lần gửi hai request không (double-submit) — vì mọi lỗi check-then-act ở mục A3 đều **kích hoạt được chỉ bằng một người dùng bấm hai lần**, không cần hai người.
- Xử lý lỗi 500: hiện thông báo có nói được cho người dùng biết "thử lại" không.

### D2 — Chưa chạy test nào

Repo **không có test tự động** (đã ghi trong `CLAUDE.md`). Toàn bộ phát hiện trên đến từ đọc code, không từ chạy. Nghĩa là:

- Chưa có bằng chứng thực nghiệm nào cho bất kỳ race nào ở mục A.
- Cách rẻ nhất để có bằng chứng: một script bắn N request đồng thời vào `POST /api/orders` và đếm số 500 — dựng được trong vài chục phút, chạy ở $0 nếu chạy local với SQL Server trong Docker.
- **Nên làm điều này TRƯỚC khi sửa**, để có con số "trước/sau".

### D3 — Ngưỡng thật của RDS chưa đo

Xem B5. Cần chạy `SELECT @@MAX_CONNECTIONS ...` trên instance thật một lần. Miễn phí (chỉ cần RDS đang chạy vì việc khác).

### D4 — Ba lớp tấn công web chưa kiểm bao giờ

Báo cáo bảo mật ([`security-validation-report.md`](security-validation-report.md)) kiểm 12 kịch bản ở tầng **hạ tầng** — cổng, rule, IAM, NACL. Ba thứ ở tầng **ứng dụng** chưa kiểm và đang được ghi rõ là "chưa kiểm" trong tài liệu:

- **SQL injection** — EF Core parameter hoá mặc định, nhưng chưa grep hết `FromSqlRaw`/`ExecuteSqlRaw`.
- **XSS** — Blazor mã hoá output mặc định, nhưng chưa kiểm `MarkupString`.
- **CSRF** — API dùng JWT Bearer chứ không dùng cookie, nên về lý thuyết miễn nhiễm; cần xác nhận không có endpoint nào nhận cookie auth.

### D5 — `SerialStatus.Reserved` không được gán ở đâu cả

`CLAUDE.md` mô tả luồng `Available → Reserved → Sold | Defective`, và nói rõ: *"Khi tạo đơn hàng online: chưa gán Serial. Chỉ khi nhân viên kho Xác nhận đóng gói → quét mã Serial → chuyển Available → Reserved."*

Đã xác minh: **`Reserved` không được gán ở bất kỳ đâu trong `src/`.** Serial đi thẳng `Available → Sold`.

Nghĩa là một trong hai điều sau đúng, và cần quyết định là điều nào:
1. Bước "Xác nhận đóng gói" **chưa được implement** → thiếu tính năng, và tồn kho bị bán trùng vì không có trạng thái giữ chỗ.
2. Luồng đã đổi và `CLAUDE.md` **lỗi thời** → cần sửa tài liệu.

Không được đoán. Đây là câu hỏi nghiệp vụ, phải hỏi người quyết định.

### D6 — CVE trong package NuGet

Đo lại ngày **2026-08-25** bằng `dotnet list PBL3.sln package --vulnerable --include-transitive`.
Tất cả đều mức **High**, chia hai nhóm:

| Package | Kiểu | Version | Advisory |
|---|---|---|---|
| `AutoMapper` | **top-level**, khai trong `Service` | 16.0.0 | [GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x) |
| `System.Security.Cryptography.Xml` | **transitive** | 9.0.0 (Infrastructure) và 10.0.0 (Service) | 8 advisory: [GHSA-37gx-xxp4-5rgx](https://github.com/advisories/GHSA-37gx-xxp4-5rgx), [GHSA-w3x6-4m5h-cxqf](https://github.com/advisories/GHSA-w3x6-4m5h-cxqf), [GHSA-cvvh-rhrc-wg4q](https://github.com/advisories/GHSA-cvvh-rhrc-wg4q), [GHSA-g8r8-53c2-pm3f](https://github.com/advisories/GHSA-g8r8-53c2-pm3f), [GHSA-23rf-6693-g89p](https://github.com/advisories/GHSA-23rf-6693-g89p), [GHSA-8q5v-6pqq-x66h](https://github.com/advisories/GHSA-8q5v-6pqq-x66h), [GHSA-mmjf-rqrv-855v](https://github.com/advisories/GHSA-mmjf-rqrv-855v), [GHSA-6588-8gv4-xfgh](https://github.com/advisories/GHSA-6588-8gv4-xfgh) |

`Shared` sạch.

**Hai nhóm này sửa khác nhau, đừng gộp làm một:**

- `AutoMapper` là **top-level** — nâng thẳng version trong `Service.csproj`. Rủi ro
  là AutoMapper hay đổi API giữa các major version, nên sau khi nâng phải build
  lại và chạy thử các đường có mapping phức tạp (`ProductVariant.Specifications`
  là `Dictionary<string,string>`, đây là chỗ dễ vỡ nhất).
- `System.Security.Cryptography.Xml` là **transitive** — không khai ở đâu cả, nó
  đến từ package khác kéo vào. Nâng đúng cách là tìm package cha rồi nâng cha
  (`dotnet nuget why PBL3.sln System.Security.Cryptography.Xml`). Ghim thẳng
  version bằng cách thêm `PackageReference` top-level cũng chạy được, nhưng đó
  là vá chứ không phải sửa: lần restore sau nếu cha nâng lên version khác thì
  cái ghim tay trở thành nguồn xung đột.

**Vì sao không sửa cùng lúc với dự án hạ tầng:** nâng package làm đổi image, tức
phải build + deploy lại toàn bộ, tức phải bật hạ tầng lên (~$0.1954/giờ). Và nếu
build vỡ thì nó vỡ đúng lúc mọi thứ khác đang xanh. Tách ra làm riêng, có cửa sổ
riêng để thử.

**Việc cần làm khi quay lại:**

1. `dotnet nuget why PBL3.sln System.Security.Cryptography.Xml` — tìm package cha.
2. Nâng cha (hoặc nâng `AutoMapper`), `dotnet build PBL3.sln -c Release`.
3. Chạy lại `dotnet list PBL3.sln package --vulnerable --include-transitive` cho tới
   khi ra "has no vulnerable packages" ở cả 4 project.
4. Deploy trong một cửa sổ riêng, không gộp với thay đổi hạ tầng nào khác.

---

## E. Thứ tự đề xuất khi quay lại

| Vòng | Nội dung | Chi phí hạ tầng | Điều kiện tiên quyết |
|---|---|---|---|
| **0** | Viết script bắn request đồng thời, đo số 500 hiện tại (D2) | $0 (chạy local) | không |
| **1** | Sửa nhóm **A** — A1 trước (mất trắng dữ liệu), rồi A3 (tiền + tồn kho), rồi A2, A4, A5 | $0 | vòng 0 để có con số đối chứng |
| **1b** | **A6** (`AccessTokenExpirationMinutes = 15`) và **A7** (`UseHsts`) — mỗi cái một dòng code | $0 | A6 phụ thuộc D1: phải biết client có tự refresh khi gặp 401 hay không |
| **2** | Rà soát frontend (D1) + quyết định D5 | $0 | hỏi người quyết định về D5 |
| **3** | Sửa nhóm **B** — chỉ khi thật sự chuyển sang ≥2 task | $0 phần code | vòng 1 xong |
| **4** | Thay đổi Terraform: mở trần task, `distinctInstance`, bộ số shutdown 30/45/90, `Max Pool Size=30` | tăng theo giờ chạy | vòng 3 xong |
| **5** | CVE NuGet (D6) — `AutoMapper` + `System.Security.Cryptography.Xml` | $0 để sửa, ~$0.1954/giờ để deploy lại | làm trong cửa sổ riêng, không gộp với thay đổi hạ tầng |

Lý do đặt vòng 1 trước vòng 4, dù câu hỏi ban đầu là về scale: **A1 làm mất trắng kết quả xử lý RMA và A3 làm voucher dùng quá lượt — ngay bây giờ, với đúng một task, hoàn toàn độc lập với chuyện scale.** Scale lên 2 task khi tầng dữ liệu còn những lỗi này chỉ làm chúng xảy ra thường xuyên hơn.

---

## Một ghi chú về `distinctInstance`

Khi tới vòng 4: cản trở kỹ thuật cứng của việc scale-out **không** phải code app, mà là **static host port**. Hai task không thể cùng bind `hostPort 80/8080` trên một instance.

Cách đúng là `placement_constraints { type = "distinctInstance" }` — nó bảo đảm đúng 1 task/instance một cách tường minh. Cách "cho task chiếm ~75% RAM instance" cũng chạy được và đã được cân nhắc, nhưng nó vỡ im lặng nếu ai đó đổi cỡ instance sau này, còn `distinctInstance` thì không.

Và một điều dễ hiểu nhầm, cần ghi lại: **trần chi phí của IAM phụ thuộc `managed_scaling = DISABLED`, không phụ thuộc `max_size`.** Chừng nào managed scaling còn tắt và Terraform còn giữ `desired_capacity`, thì `ecs:UpdateService` một mình **không tạo được máy**. Nâng `max_size` từ 1 lên 2 không phá bảo đảm ấy. Bật managed scaling thì phá — âm thầm.
