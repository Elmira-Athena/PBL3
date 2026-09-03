# Nhật ký sửa lỗi — kế hoạch nâng cấp HushStore chịu tải & bảo mật

**Phạm vi:** toàn bộ kế hoạch nâng cấp, từ đợt 1 tới đợt nghiệm thu trước ASG EC2.
**Mục đích:** một chỗ duy nhất trả lời *"lỗi gì đã sửa, sai ở đâu, sửa thế nào"*.
Runbook vận hành ở [`bat-dau-phien-moi.md`](bat-dau-phien-moi.md); quy ước rút ra từ các lỗi này ở [`CLAUDE.md`](../CLAUDE.md).

**Cách đọc:** mỗi mục là một lỗi. Ba phần cố định — *Sai ở đâu* · *Vì sao quan trọng* · *Cách sửa*.
Phần "Vì sao quan trọng" mới là phần đáng đọc: gần như mọi lỗi ở đây đều **trả HTTP 200** và **không hiện trong log**.

> **Một nhận xét xuyên suốt, đặt lên đầu vì nó giải thích phần lớn danh sách này:**
> **một tiến trình ASP.NET Core vốn đã phục vụ nhiều thread.** "Chạy 1 task" không tuần tự hoá
> bất cứ thứ gì. Phần lớn lỗi đúng đắn dữ liệu dưới đây **đang xảy ra từ trước**, độc lập với
> chuyện scale. Scale lên 2 task chỉ **tăng xác suất** và **vô hiệu hoá các cách sửa bằng `lock`**.

---

# Đợt 1 — Sửa lỗi backend, không đổi schema

## 1.1 Ghi vào entity `AsNoTracking()` — lệnh gán rơi vào hư vô (4 vị trí)

**Sai ở đâu.** Bốn chỗ nạp entity bằng phương thức có `.AsNoTracking()` rồi gán thuộc tính và
gọi `SaveChangesAsync()`. Entity không nằm trong Change Tracker nên EF **không sinh câu UPDATE nào**.
Không lỗi, không cảnh báo, `SaveChanges` trả về bình thường.

Ba repository liên quan **đã có sẵn** phương thức tracked — call-site chỉ gọi nhầm.

| Vị trí | Việc bị mất |
|---|---|
| `ServiceTicketService` | đánh dấu báo giá cũ là Superseded |
| `ServiceTicketService` | cập nhật 4 trường RMA |
| `ServiceTicketService` ×2 | đóng bảo hành cũ |

**Vì sao quan trọng.** Comment ngay trên chỗ thứ nhất nói *"bảo đảm chỉ tồn tại duy nhất một bản
báo giá có hiệu lực"* — bất biến ấy **chưa bao giờ tồn tại**.

**Cách sửa.** Bản vá thì tầm thường; **kỹ thuật chống tái phát mới là phần đáng kể**: đổi tên các
phương thức no-tracking thành `...ReadOnlyAsync` **ngay ở interface** trong `src/Core/Interfaces/`.
Build vỡ tại đúng mọi call-site, buộc đi qua từng chỗ để quyết định đọc hay ghi. Trong repo không
có test nào, **compiler là "bộ test" rẻ nhất có thể có**.

Riêng chỗ đánh dấu báo giá cũ không dùng tracked getter mà thay bằng một câu `ExecuteUpdateAsync`
set-based — **vừa sửa lỗi này vừa đóng luôn một nhánh race** bằng một dòng.

---

## 1.2 Check-then-act trên `Voucher.UsedCount` (3 vị trí)

**Sai ở đâu.** Đọc `UsedCount` về RAM → so với `Quantity` → cộng 1 → ghi lại. Ba chỗ, không phải
một như tài liệu rà soát cũ ghi: `OrderService` ×2, `PosService` ×1.

**Vì sao quan trọng.** DB **đã có sẵn** check constraint `CK_Vouchers_Quantity`
(`Quantity IS NULL OR UsedCount <= Quantity`), nên thoạt nhìn tưởng an toàn. Nhưng nó **không bắt
được lost update**: hai request cùng đọc 5 rồi cùng ghi 6 làm `UsedCount` **đếm thiếu**, vẫn thoả
ràng buộc, và voucher được dùng **nhiều hơn số phát hành**.

Một ràng buộc đúng nhưng bảo vệ sai thứ — và chính nó là lý do lỗi này sống lâu.

**Cách sửa.** `TryConsumeAsync` / `TryConsumeByCodesAsync` sinh
`SET UsedCount = UsedCount + 1 WHERE UsedCount < Quantity`. Tăng **ở phía DB**; vị từ nằm **cùng
câu lệnh** với phép gán nên không còn khe kiểm-rồi-ghi. `ExecuteUpdateAsync` trả số dòng bị ảnh
hưởng — `0` chính là tín hiệu "người khác làm trước rồi".

Không dùng khoá, không token, không retry.

---

## 1.3 Duyệt/từ chối báo giá — nhánh từ chối **không kiểm trạng thái gì cả**

**Sai ở đâu.** `RejectQuotationAsync` không có một dòng nào kiểm `Status` trước khi ghi.

**Vì sao quan trọng.** Đây **không cần đồng thời** mới hỏng: từ chối được cả báo giá **đã duyệt**
chỉ bằng cách bấm hai lần. Nhánh duyệt thì có kiểm nhưng là check-then-act.

**Cách sửa.** Thêm chốt trạng thái, và biến cả hai nhánh thành cổng nguyên tử
`TryDecideAsync(quotationId, fromStatus, toStatus, …)` → `UPDATE … WHERE Id = @id AND Status = @from`,
trả `false` khi 0 dòng.

Hàm tách hai nhánh có/không có `note` là **cố ý**: đường duyệt không đụng `CustomerDecisionNote`,
truyền `null` vào sẽ **xoá mất ghi chú đang có**.

---

## 1.4 `.ContinueWith(t => t.Result)` — sync-over-async nuốt kiểu exception

**Sai ở đâu.** `ProductSerialRepository` dùng `.ContinueWith(t => t.Result…)`.

**Vì sao quan trọng.** Nó bọc exception vào `AggregateException`, nên `catch (SqlException)` ở tầng
service **không bắt được** — lỗi DB thành 500 vô danh.

**Cách sửa.** `await … ToListAsync()` rồi projection LINQ. Toàn repo nay còn **0** chỗ sync-over-async.

---

## 1.5 Transaction — rò rỉ, rollback trên transaction đã commit, và một nhánh bỏ dở

**Sai ở đâu.** `UnitOfWork` giữ `IDbContextTransaction` trong field, chỉ `Commit`/`Rollback`,
**không bao giờ `Dispose`**. 18 call-site tự gọi `BeginTransactionAsync()` thủ công.

Ba lỗi thật, không phải chuyện lý thuyết:

1. **Transaction không bao giờ được `Dispose`** → connection bị giữ tới khi scope DbContext kết thúc.
2. **Rollback trên transaction đã commit.** Nhiều call-site làm việc *sau* `CommitAsync` **vẫn nằm
   trong khối `try`**; chỗ đó ném thì `catch` gọi `RollbackAsync` trên transaction đã commit →
   **ném thêm exception thứ hai**, che mất lỗi thật.
3. **`InventoryCheckService.CreateAsync` bỏ dở transaction.** Nhánh "không có sản phẩm trong phạm vi"
   `return` thẳng ra ngoài — không commit, không rollback.

**Vì sao quan trọng.** Ngoài ba lỗi trên, đây là **điều kiện bắt buộc** để bật `EnableRetryOnFailure`:
EF Core **cấm** `BeginTransactionAsync()` thủ công khi có retrying strategy, và nó ném **lúc chạy,
không lúc biên dịch**. Bật retry mà chưa làm việc này thì mọi đường nghiệp vụ có transaction ném
`InvalidOperationException` — và chỉ lộ ra khi người dùng bấm nút.

**Cách sửa.** `IUnitOfWork` bỏ hẳn `BeginTransactionAsync`/`CommitAsync`/`RollbackAsync`, thay bằng
`ExecuteInTransactionAsync(Func<Task<T>>)` chạy qua `Database.CreateExecutionStrategy()`, dùng
`await using` nên transaction luôn được dispose.

Chuyển hết 18 call-site. **Phần việc làm *sau* commit** (đồng bộ tồn kho, đọc lại để map DTO, ghi log)
được đưa **ra ngoài** delegate — để trong thì nó chạy trong transaction và sẽ chạy lại khi retry.
Nhánh bỏ dở ở `InventoryCheckService` được sửa bằng cách chuyển truy vấn phạm vi (chỉ đọc) ra **trước**
transaction.

⚠️ **Change Tracker cố ý KHÔNG clear giữa các lần thử.** Nhiều call-site nạp entity **trước** khi mở
transaction rồi sửa bên trong; clear sẽ tháo mất chúng và lệnh sửa lại rơi vào hư vô — **đúng lỗi 1.1
vừa sửa**. Đây là lý do phải rà từng call-site trước khi bật retry, không bật hàng loạt.

---

## 1.6 Đồng bộ tồn kho — COUNT rồi vòng lặp UPDATE

**Sai ở đâu.** `InventorySyncService` COUNT một lần rồi lặp `foreach` gọi `ExecuteUpdateAsync` cho
từng variant.

**Vì sao quan trọng.** Hai vấn đề chồng nhau: N round-trip giữ X-lock trên `ProductVariants` suốt N
lượt; và có **khe race giữa bước COUNT và bước UPDATE** — một serial bán ra trong khoảng đó làm
`StockQuantity` bị ghi đè bằng con số đã cũ.

**Cách sửa.** Một câu duy nhất với subquery tương quan, để DB tự đánh giá COUNT **tại thời điểm ghi**:

```sql
UPDATE [p] SET [p].[StockQuantity] = (
    SELECT COUNT(*) FROM [ProductSerials] AS [p0]
    WHERE [p0].[VariantId] = [p].[Id] AND [p0].[Status] = CAST(0 AS tinyint))
FROM [ProductVariants] AS [p] WHERE [p].[Id] IN (@ids1, @ids2, @ids3)
```

**Bắt buộc đọc SQL sinh ra trước khi merge** — EF Core dịch subquery trong `SetProperty` khá kén.
Đã đọc và đúng như trên. `ProductSerials` không có cột `IsDeleted` nên không có global query filter
⇒ ngữ nghĩa giống hệt bản cũ, không có thay đổi ngầm.

---

## 1.7 Sinh mã chứng từ — 7 khối trùng lặp, và một quả bom hẹn giờ

**Sai ở đâu.** Bảy khối code giống nhau ở sáu file, mỗi khối tự "đọc mã cuối trong ngày rồi +1".
Kèm hai lỗi ẩn:

- **`{n:D3}`.** Mã cuối được tìm bằng `ORDER BY Code DESC` — tức **so sánh CHUỖI**. Quá 999 chứng từ
  một ngày thì `"...-1000"` sắp **trước** `"...-999"`, nên truy vấn **luôn** trả `-999`, số kế tiếp
  **luôn** ra 1000, và hệ thống sinh **mã trùng vĩnh viễn** kể từ đó.
- **`DateTime.Now` ở Order/POS** trong khi các loại khác dùng `UtcNow` — hai chứng từ tạo cùng lúc rơi
  vào hai **ngày** khác nhau trong mã, và ngày trong mã lệch với cột ngày (vốn luôn lưu UTC).

**Vì sao quan trọng.** Bản thân check-then-act ở đây **đã đo được**: LoadProbe S01 cho **32–41/50 đơn
hỏng** vì cùng tính ra một mã rồi đụng `IX_Orders_OrderCode`. Dữ liệu không hỏng (unique index chặn),
nhưng tính khả dụng thì có.

**Cách sửa (đợt 1 — gom code).** Gộp 7 khối thành một `IDocumentCodeGenerator`, biến 7 nơi phải sửa
thành 1. Đổi `{n:D3}` → 6 chữ số, `DateTime.Now` → `UtcNow`.

🚨 **Bẫy phát sinh, phải xử lý cùng lúc:** riêng việc đổi độ rộng là **không an toàn** nếu vẫn tìm mã
cuối bằng `ORDER BY Code DESC` — mã cũ `-001` **luôn sắp trên** mã mới `-000002` theo thứ tự chuỗi,
nên hệ thống sẽ mãi trả về mã cũ và sinh trùng. Vì vậy 5 phương thức repository đổi từ
`GetLastXxxCodeByDateAsync` → `GetCodesByDatePrefixAsync` (trả **danh sách** mã trong ngày) và
generator tự lấy max **theo số**. Cách so sánh chuỗi bị bỏ hẳn — đó mới là **nguyên nhân gốc** của
quả bom `{n:D3}`.

Đã kiểm trên DB có lẫn hai định dạng: `KK-…-001` → sinh `KK-…-000002` (không phải `000001`);
thêm `KK-…-000005` → sinh `KK-…-000006`.

*(Ruột được thay hẳn bằng SQL SEQUENCE ở đợt 3 — xem mục 4.1.)*

---

## 1.8 Cache trạng thái khoá tài khoản trong `MemoryCache`

**Sai ở đâu.** Middleware kiểm `IsActive` cache kết quả 30 giây trong `IMemoryCache`, với 4 điểm
invalidate rải ở `CustomerService`/`EmployeeService`. Ngoài ra nó gọi `FindByIdAsync` kéo **toàn bộ**
hàng `AppUsers` về chỉ để đọc 2 cột.

**Vì sao quan trọng.** `MemoryCache` nằm trong RAM của **một** tiến trình. Với 1 task, admin khoá tài
khoản thì phiên của người đó còn sống thêm tối đa 30 giây — chấp nhận được. Với 2 task sau ALB, lệnh
xoá cache chỉ chạm cache của task **nhận request khoá**; task còn lại vẫn giữ bản cũ và **vẫn cho vào**.

Điểm mấu chốt: **hướng nguy hiểm là hướng MỞ KHOÁ**, không phải hướng khoá. Đó là lỗi **bảo mật**,
không phải lỗi hiệu năng, và triệu chứng là *"lúc được lúc không tuỳ ALB định tuyến"* — loại lỗi
không tái hiện được.

**Cách sửa.** Bỏ hẳn cache, đọc thẳng DB bằng projection đúng 2 cột (`IsActive`, `LockReason`) —
vừa đúng luật, vừa **rẻ hơn** cách cũ kể cả khi còn cache. Xoá cả 4 điểm invalidate và bỏ
`IMemoryCache` khỏi constructor **cùng lúc** — để lại thì người đọc sau tưởng cache vẫn chạy.

**Đây chính là thứ làm cho việc chạy nhiều task không cần Redis.**

Đo được: khoá tài khoản trong DB rồi gọi lại **ngay** → `403` + `X-Account-Status: locked`; mở khoá
gọi lại ngay → `200`. Trước đó cả hai chiều đều phải chờ tới 30 giây.

---

## 1.9 `pageSize` không chặn trần ở 25/26 controller

**Sai ở đâu.** `pageSize` nhận thẳng từ query string, không chặn trần.

**Vì sao quan trọng.** Đường DoS rẻ nhất trong hệ thống: tìm kiếm sản phẩm — **anonymous**, chưa có
rate limit, và `?pageSize=1000000` ép server vật hoá một triệu dòng.

**Cách sửa — hai lớp, đi kèm chứ không thay thế nhau:**

- **`ClampPageSizeFilter`** (`IAsyncActionFilter`, đăng ký global): clamp mọi tham số tên
  `pageSize`/`take`/`limit`/`size` về `[1, 100]` và `page`/`pageNumber`/`pageIndex` về `>= 1` — cả tham
  số rời lẫn thuộc tính bên trong model. Một file, 0 thay đổi chữ ký, phủ **cả 148 endpoint kể cả
  những cái viết sau này**. Đây là lớp bảo vệ **thật**: nó phủ cả `curl`.
- **`PagedRequest`** (`src/Shared/DTOs/Common/`) với setter tự clamp, 11 DTO kế thừa. Đặt ở `Shared`
  nên **Blazor client dùng chung** ⇒ `MudTable` không gửi nổi số lớn ngay từ phía gửi.

Chọn thêm base class thay vì chỉ dựa vào filter vì quy ước trong repo **không nhất quán**
(`OrderFilterRequest` dùng `PageIndex`, storefront mặc định 20) — filter dựa trên quy ước sẽ âm thầm
bỏ sót đúng những chỗ lệch chuẩn nếu đứng một mình.

Đo được: `?pageSize=1000000` → `100`; `?pageSize=-5` → `1`; `?pageSize=20` → `20` (không đụng giá trị hợp lệ).

---

## 1.10 Seed role `Technician` giết task lúc boot

**Sai ở đâu.** Role `Technician`/`KTV` chỉ được tạo bởi khối seed trong `Program.cs`, chạy ở
top-level statement **không có try/catch**.

**Vì sao quan trọng.** Hai ECS task cold-start cùng lúc (tức **đúng lúc deploy**) thì task thua cuộc
đua vi phạm **hai** unique index (`RoleNameIndex` + `IX_AppRoles_RoleCode`). Vi phạm ở tầng DB **ném
exception** chứ không trả `IdentityResult` thất bại, nên kiểm `result.Succeeded` không cứu được.
Exception chưa bắt ở top-level ⇒ **process exit ≠ 0** ⇒ **task chết lúc boot**. Ngoài ra nó làm
startup phụ thuộc vào việc DB đang sống và ghi được.

**Cách sửa — tách đôi, và thứ tự là bắt buộc.**

1. Thêm `Technician`/`KTV` vào `seed_data.sql` kèm chốt kiểm đủ 4 role (`THROW` nếu thiếu).
2. **Rồi mới** bọc khối trong `Program.cs` bằng try/catch, giữ làm lưới an toàn.

Đảo thứ tự thì `EmployeeService.AddToRoleAsync(user, "Technician")` vô hiệu **im lặng**.

⚠️ `NormalizedName = 'TECHNICIAN'` phải **ghi tay**: `RoleManager.CreateAsync` tự sinh nó qua
`UpperInvariantLookupNormalizer`, INSERT tay thì không. Thiếu nó thì `FindByNameAsync`/`RoleExistsAsync`
**không bao giờ** tìm thấy role.

---

# Đợt 2 — Frontend và bảo mật thuần code

## 2.1 Gặp 401 là xoá token rồi đá về `/login` — hạ tầng refresh có sẵn mà không ai nối vào

**Sai ở đâu.** `AuthHeaderHandler` gặp 401 thì xoá **cả access lẫn refresh token** rồi
`NavigateTo("/login")`. Không refresh, không gửi lại.

**Vì sao quan trọng.** Nghịch lý: hạ tầng refresh **đã tồn tại và hoạt động đầy đủ ở cả hai đầu** —
chỉ là không ai nối nó vào nhánh 401. Và nó **chặn** việc hạ `AccessTokenExpirationMinutes`: hạ xuống
15 khi chưa sửa chỗ này sẽ đăng xuất người dùng mỗi 15 phút và **mất trắng dữ liệu form đang nhập dở**.

**Cách sửa.** Bốn ràng buộc ép cấu trúc, mỗi cái là chỗ một bản viết ngây thơ sẽ vỡ:

- Không thể inject `AuthClientService` vào handler — nó nhận chính `HttpClient` có gắn handler đó
  ⇒ **vòng tròn DI và đệ quy**. Cần named client **không có handler**.
- `HttpRequestMessage` **không dùng lại được sau khi đã gửi** ⇒ phải `LoadIntoBufferAsync()` trước lần
  gửi đầu và dựng message mới khi gửi lại. Bỏ qua là `InvalidOperationException`.
- **Single-flight** bằng `SemaphoreSlim` ở client là **hợp lệ** (một tab WASM là một tiến trình đơn
  luồng — khác hẳn backend). Mấu chốt: sau khi vào semaphore phải **đọc lại token và so với token
  caller đã dùng**, để N request 401 cùng lúc chỉ sinh **một** lời gọi mạng.
- **`NavigateTo` không được gọi từ trong `SendAsync`** — component gọi API bị dispose giữa lúc request
  đang bay. Thay bằng notifier mà layout subscribe.

`JwtAuthenticationStateProvider` dùng **chung** coordinator với handler, nếu không lúc trang load sẽ có
hai lời gọi refresh song song và **rotation vô hiệu hoá lẫn nhau**.

Gửi lại cả POST là an toàn **ở đây và chỉ ở đây**: 401 phát ra bởi middleware JWT **trước khi**
controller chạy, server chưa làm gì cả.

**Đo bằng trình duyệt thật:** 12 request nhận 401 **cùng lúc** → **đúng 1** lời gọi refresh → 12 request
gửi lại đều thành công. Không có single-flight thì 12 lời gọi refresh, mà refresh token **xoay vòng**
mỗi lần dùng nên 11 cái sau cầm token đã bị thu hồi — chúng thất bại **và vô hiệu hoá kết quả của cái
đầu tiên**, tức người dùng bị đăng xuất **đúng lúc hệ thống đang cố giữ họ đăng nhập**.

---

## 2.2 Double-submit trên diện rộng, và một bug tinh vi hơn cả danh sách

**Sai ở đâu.** 25 nút mutation không có chống double-submit, không idempotency key. Nghiêm trọng nhất
là `Pos/Index.razor` `ProcessPayment` — không cờ, không dialog xác nhận, không `Disabled`, và là nút
**duy nhất trừ tồn kho mà không hỏi lại**.

**Vì sao quan trọng — và đây là phần đáng đọc.** Trang kiểm kê **có** cờ `_isActioning`, **có** bind
`Disabled`, và **vẫn không hoạt động**: cờ được gán *sau* `await ShowMessageBox` và thiếu
`StateHasChanged()`. `ComponentBase.HandleEventAsync` chỉ tự `StateHasChanged()` sau phần **đồng bộ**
của handler. **Bốn người đã viết đúng ý định và vẫn sai.**

**Cách sửa.** Ba file dùng chung thay vì sửa tay 25 chỗ: `BusyState` (POCO, đặt cờ và raise event
**trước mọi `await`**), `BusyScope` (cascading — một hành động đang chạy thì khoá cả vùng, đúng nhu cầu
màn hình phiếu dịch vụ với 14 nút), `ActionButton` (cờ thuộc về **nút** chứ không thuộc về page).

Vì cờ được đặt **trước khi** handler chạy, nó sửa luôn bug tinh vi ở 4 chỗ kiểm kê **mà không cần sửa
dòng nào trong thân handler**.

🚨 **Ngoại lệ đã đo:** nút `ButtonType.Submit` bên trong `<EditForm OnValidSubmit>` thì `ActionButton`
**vô hiệu** — cú click submit form, không đi qua `OnClick`. Đã đo: 3 click → 3 request. Hai chỗ như vậy
cố ý giữ cờ thủ công.

⚠️ **Phải nói rõ:** cơ chế này **không bảo vệ server**. Hai tab, F5 giữa chừng, hay `curl` vẫn
double-submit. Phòng tuyến thật là conditional update + unique index ở tầng DB.

**Đo được, kèm ca đối chứng âm:** 6/6 nút hỏng thật đều khoá đúng; hạ `ActionButton` → `MudButton` trần
thì 3 click → **3 dialog**, `disabled=false` — tức phép đo thật sự đo được thứ nó tuyên bố.

---

## 2.3 Nuốt lỗi thành "bảng rỗng", và `ErrorBoundary` biến một trang hỏng thành cả app hỏng

**Sai ở đâu.** `OrderClientService` nuốt lỗi rồi trả danh sách rỗng. `ErrorBoundary` không phục hồi.

**Vì sao quan trọng.** Nuốt lỗi thành bảng rỗng là **loại lỗi tệ nhất vì nó nói dối** — người dùng
tưởng mình không có đơn hàng nào. Còn `ErrorBoundary` không giữ `@ref` và không gọi `Recover()` khi
`LocationChanged` thì nó biến *"một trang hỏng"* thành *"cả app hỏng cho tới khi F5"* — **tệ hơn hiện
trạng**.

**Cách sửa.** `ErrorBoundary` giữ `@ref` + `Recover()` khi điều hướng. Helper `ApiCall` ánh xạ status
code sang thông báo **có tính hành động**, trong đó **409 có mặt từ đợt 2** dù chưa ai trả về nó —
đợt 3 sẽ bắt đầu trả, client phải sẵn sàng trước.

---

## 2.4 Stored XSS

**Sai ở đâu.** `ProductDetail.razor` render mô tả sản phẩm bằng `MarkupString`. Không sanitizer nào
trong toàn repo.

**Vì sao quan trọng.** Kết hợp với token trong `localStorage` → **chuỗi tấn công đầy đủ tới refresh
token sống 7 ngày**. Nguồn ghi là một `MudTextField Lines="5"` — tức **textarea thuần, không có
rich-text editor nào trong hệ thống**, nên không có lý do nghiệp vụ nào cần HTML.

**Cách sửa.** Sanitizer áp ở **tầng API trên đường GHI** — vô hiệu hoá dữ liệu **tại chỗ lưu** nên mọi
consumer hiện tại và tương lai đều an toàn, một chỗ thay vì mọi chỗ render.

---

## 2.5 Rate limiter là một DoS tự gây ra

**Sai ở đâu.** `options.AddFixedWindowLimiter("LoginRateLimit", …)` — overload đó tạo **một limiter duy
nhất, không phân vùng, cho toàn bộ endpoint**.

**Vì sao quan trọng.** Tức 5 lần đăng nhập mỗi phút cho **tất cả người dùng cộng lại**. Một kẻ tấn công
đốt hết hạn mức là **khoá đăng nhập của mọi khách hàng**. Tệ hơn hẳn vấn đề "2 task = 2× hạn mức" mà
tài liệu cũ đang lo, và nó **đang xảy ra ngay bây giờ với 1 task**.

**Cách sửa.** `GlobalLimiter` phân vùng theo IP, cộng 4 policy riêng (đăng ký, refresh, hai endpoint
tra cứu, đọc công khai). `OnRejected` trả body **tiếng Việt** (trước đó 429 body rỗng).

🚨 **Miễn trừ `/health/*` là bắt buộc, không phải tối ưu.** Container chạy `bridge` nên source IP là
gateway docker cho mọi request; `UseForwardedHeaders` viết lại thành IP thật cho traffic qua ALB, **nhưng
health check của ALB gọi thẳng vào instance và không mang `X-Forwarded-For`**. Nếu health check rơi cùng
phân vùng với traffic thật, một đợt tải làm nó bị 429 → ALB kết luận unhealthy sau 45 giây → **ECS giết
task đang chạy tốt, đúng lúc tải cao**. Đây là chế độ chết tệ nhất có thể nghĩ ra cho một hệ thống đang
phải chứng minh chịu tải.

Rate limiter chỉ đáng tin **nhờ** `ForwardedHeadersOptions.ForwardLimit = 1`; đổi thành ≥2 là mở đường
lách bằng cách bơm `X-Forwarded-For`.

---

## 2.6 Bề mặt ẩn danh, HSTS

**Sai ở đâu.** Oracle liệt kê serial và dò mã voucher mở cho anonymous; `available-for-order` liệt kê
toàn bộ khuyến mãi cho người chưa đăng nhập; `[AllowAnonymous]` thừa trên `GetTechnicians`; và
`ServiceTicketsController` trả `"Lỗi khi kiểm tra Serial: " + ex.Message` **thẳng cho client anonymous**
— đi vòng qua `UseExceptionHandler` vốn được viết ra chính để giấu nội tình.

Về HSTS, bối cảnh **khác tài liệu cũ mô tả**: `UseHttpsRedirection()` nằm **bên trong**
`if (!IsProduction())`. Production **không có cả redirect lẫn HSTS** — ép HTTPS hoàn toàn do ALB và
Cloudflare gánh, tầng ứng dụng không đóng góp gì.

**Cách sửa.** Thu hẹp bề mặt; **chuẩn hoá phản hồi** cho `intake` và `validate-code` — chính **sự khác
biệt** giữa "không tồn tại" và "hết hạn" là oracle. Bật HSTS ở nhánh Production, `max-age` 1 ngày,
`Preload = false`.

⚠️ `UseHsts()` **phải đứng sau `UseForwardedHeaders`** — nó chỉ phát header khi `Request.IsHttps`, mà
production container nhận HTTP thuần từ ALB. Đảo thứ tự là **no-op im lặng**: không lỗi, không header.
Và **không** thêm `UseHttpsRedirection` (ALB đã 301 ở listener, bật ở đây gây redirect loop).

---

## 2.7 `AccessTokenExpirationMinutes` 10080 → 15

**Làm cuối cùng của đợt 2, cố ý** — chỉ hạ sau khi luồng refresh đã xanh (mục 2.1).

Vòng đời access token giảm **672 lần**. Đây là biện pháp bù **định lượng được** cho quyết định giữ token
trong `localStorage`.

**Không đổi sang cookie HttpOnly trong đợt này:** nó biến CSRF từ "không áp dụng" thành "phải xử lý",
buộc `AllowCredentials()` trong CORS, và mất state in-memory mỗi lần F5. Ghi là **giới hạn đã biết** kèm
bốn biện pháp bù: vector phát tán XSS đã bị loại · vòng đời 7 ngày → 15 phút · đã có thu hồi phía server
khi khoá tài khoản · refresh token xoay mỗi lần dùng.

📌 **Ghi chú để sau này không ai bối rối:** `ClockSkew = 1 phút` nên vòng đời thực tế là **15 + 1**.
Đo "15 phút" rồi thấy token còn dùng được ở phút thứ 16 là **đúng**, không phải lỗi.

---

# Các mục rà soát bổ sung

## 3.1 🅰 — 14 call-site transaction còn lại chưa retry-safe

Mục 1.5 phủ 18 chỗ dùng `IUnitOfWork`; mục này rà nốt phần còn lại. Sau đó mới **bật
`EnableRetryOnFailure`**. Kết quả: **18/18 call-site transaction retry-safe**.

## 3.2 🅱 — quét nốt chống double-submit

**23/23 nút mutation** dùng `ActionButton`/`BusyScope`, trong đó **6/6 nút hỏng thật đã đo, có ca đối
chứng âm**.

## 3.3 🅲 — `tools/LoadProbe/` và hạ tầng 2 replica

Repo không có test tự động. Đây là thứ thay thế, và **nó không phải công cụ đo hiệu năng**: nó bắn
request song song rồi **khẳng định bất biến bằng LINQ trên DB**.

**Lý do phải kiểm ở DB, không ở mã HTTP:** mọi lỗi đúng đắn dữ liệu tìm thấy ở repo này đều **trả 200**.
Voucher vượt hạn mức, sổ tổn thất nhân đôi, hai phiếu cùng một serial — tất cả đều "thành công" ở tầng HTTP.

🚨 **`KHÔNG KẾT LUẬN` ≠ `ĐẠT`.** Kịch bản bị rate limiter chặn sẽ **thoả mọi bất biến** vì code cần đo
chưa chạy. Đó là **bằng chứng an toàn giả, nguy hiểm hơn không có bằng chứng**.

## 3.4 🅳 — 10 lỗ hổng NuGet mức High + cổng chặn ở CI

**Cách sửa.** Vá 10 advisory, thêm `devops/scripts/check-vulnerable-packages.sh` chặn ở bước
`dotnet build`.

Hai điều rút ra:

- ⚠️ **Đừng "kiểm nhanh" bằng `dotnet list package --vulnerable` rồi tin mã thoát** — lệnh đó trả `0`
  **kể cả khi tìm thấy lỗ hổng**. Đã đo.
- **`AutoMapper` bị gỡ hẳn thay vì nâng phiên bản.** Nó nằm trong `API.csproj` và `Service.csproj` mà
  **không một dòng code nào dùng** (0 `CreateMap`, 0 `IMapper`, 0 `AddAutoMapper`, 0 lớp `: Profile`),
  trong khi có 22 chỗ projection thủ công. Một gói không ai dùng mang một lỗ hổng High — cách sửa đúng
  là gỡ, và sửa CLAUDE.md vốn đang mô tả sai là repo dùng AutoMapper.

## 3.5 🅴 🅷 🅸 — rò rỉ `ex.Message` của hạ tầng, ba tầng

**Sai ở đâu.** `throw new Exception("Lỗi hệ thống: " + ex.Message, ex)` — chuỗi của EF Core/SQL Server
là **tiếng Anh** và lộ nội tạng ORM, nên câu này **vi phạm luật ngay cả khi nửa đầu là tiếng Việt**.
LoadProbe tái hiện được nó ở nhánh thua cuộc đua sinh mã chứng từ.

🚨 **Nhưng "thay cả khối `catch` bằng một câu cố định" là SAI — đó là cái bẫy.** Cùng khối
`catch (Exception)` đó thường **cũng** là đường đi của thông báo **nghiệp vụ** đã soạn cho người dùng
(`"Mã 'X' đã hết lượt sử dụng."`). Nuốt chúng thành câu chung là **hồi quy UX nặng hơn lỗi ban đầu**:
người dùng mất đúng thông tin cần để tự sửa, bấm lại thì hỏng y hệt.

**Cách sửa — phân loại tại nguồn, không bằng cách dò nội dung chuỗi:**

```csharp
throw new BusinessRuleException($"Mã '{code}' đã hết lượt sử dụng.");   // an toàn để hiển thị
…
catch (BusinessRuleException) { throw; }            // PHẢI đứng trước catch (Exception)
catch (Exception ex)
{
    _logger.LogError(ex, "Checkout thất bại cho người dùng {UserId}.", userId);
    throw new Exception("Không thể hoàn tất đặt hàng do lỗi hệ thống. …", ex);
}
```

Bắt `InvalidOperationException` thay thế là **không** an toàn vì EF Core dùng chính kiểu đó cho chuyện
khác — vì vậy `grep -rn 'catch (InvalidOperationException' src/` **phải luôn rỗng**.

Ở **tầng Client**, 124 chỗ đổi sang câu tiếng Việt cố định **có tính hành động**, và **18 client service
được bổ sung `ILogger<T>`** — vì "thay chuỗi" mà không log là **vứt sạch chẩn đoán**.

**Cổng chống hồi quy:** `devops/scripts/check-error-message-leaks.sh`.
🚨 **Đừng thay nó bằng `grep 'ex.Message'`** — grep không biết dòng đó nằm trong khối `catch` **nào**,
nên nó đếm cả 41 chỗ relay **đúng** (từ `catch` nghiệp vụ) thành lỗi. Đã đo: cách đếm bằng grep phóng
đại 2 chỗ rò rỉ thật ở tầng Service thành 7. **Phân loại phải theo ngữ cảnh.**

*(Cổng này về sau bị phát hiện mù với `.razor` — xem mục 6.4.)*

---

# Đợt 3 — Sửa lỗi cần đổi schema

## 4.1 SEQUENCE thay ruột sinh mã chứng từ

**Sai ở đâu.** Thuật toán "đọc mã cuối trong ngày rồi +1" vẫn là check-then-act, dù đợt 1 đã gom về một
chỗ. Đo được: **32–41/50 đơn hỏng**.

**Vì sao không chọn "bắt unique-violation rồi retry" làm phương án chính.** Với bộ cấp phát `SELECT MAX`,
N request đồng thời **đều tính ra cùng một giá trị**, nên mỗi vòng retry chỉ cho **đúng một người** qua
⇒ cần O(N) vòng, mỗi vòng một round-trip. Ở kịch bản 50 request đó là hàng nghìn round-trip. **Retry là
công cụ đúng cho va chạm *hiếm*, không phải va chạm *chắc chắn*.**

**Cách sửa.** SQL `SEQUENCE`, khai bằng `modelBuilder.HasSequence<long>()`, lấy giá trị bằng
`SELECT NEXT VALUE FOR` qua `IDocumentSequence`.

**Chốt thiết kế: sequence TOÀN CỤC, không reset theo ngày.** Thứ *gây ra* toàn bộ race chính là yêu cầu
reset mỗi ngày — nó **bắt buộc** phải có câu `SELECT MAX` để biết hôm nay đã tới đâu. Bỏ việc reset là
**bỏ nguyên nhân** chứ không vá triệu chứng. Giữ phần ngày trong mã để vẫn đọc được và vẫn sắp đúng thời gian.

**Hai hệ quả phải biết trước khi động vào:**

- `NEXT VALUE FOR` **không mang tính giao dịch**: giá trị bị tiêu thụ dù transaction rollback, nên **dãy
  mã có lỗ**. Đừng "sửa". Số trong mã **không** còn là "chứng từ thứ N" — muốn đếm thì `COUNT(*)`.
  (Bù lại: khi transaction retry, lần thử sau lấy mã **mới** — đúng điều cần.)
- Cột mã là `nvarchar(20)`, tiền tố 3 ký tự chịu tối đa **7 chữ số**. `ORD` và `POS` **dùng chung** một
  sequence vì cùng ghi vào `Orders.OrderCode`.

**Kết quả đo:** S01 chuyển 🔴 → ✅, **50/50 đơn**.

## 4.2 Ánh xạ xung đột sang HTTP 409

**Sai ở đâu.** Không có gì ánh xạ `DbUpdateConcurrencyException` hay vi phạm unique index sang 409 —
chúng thành 500.

**Vì sao quan trọng.** 500 nói *"server hỏng"*; 409 nói *"bạn thử lại đi"*. Client đã sẵn sàng từ đợt 2.

**Cách sửa.** `ConflictExceptionHandler` (`IExceptionHandler`) đăng ký trước handler tổng.

🚨 **Nhưng handler là `IExceptionHandler` nên nó CHỈ THẤY exception ĐÃ THOÁT khỏi action.** Mọi
`catch (Exception)` trong controller là một **bức tường** trước middleware — và trước khi sửa, nó làm
handler 409 thành **code chết cho MỌI đường nghiệp vụ**.

**Sửa ở tầng Service là KHÔNG ĐỦ** — đã đo: chốt `throw;` ở `OrderService` chạy đúng (log ghi "trùng khoá
duy nhất") mà **409 vẫn không tới**, vì `OrdersController` bắt trước. Vì vậy phải thêm **27 chốt** ở 4
controller, chỉ đặt ở action **mutation**:

```csharp
catch (BusinessRuleException ex) { return ApiResult<T>.Fail(ex.Message); }
catch (DbUpdateConcurrencyException) { throw; }                    // → 409
catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
{ throw; }                                                        // → 409
catch (Exception ex) { … }                                        // PHẢI đứng cuối
```

Nhận diện bằng **số lỗi** (2601/2627), **tuyệt đối không dò `ex.Message`** — chuỗi đó tiếng Anh và đổi
theo phiên bản SQL Server.

## 4.3 `RowVersion` trên 6 entity

**Sai ở đâu.** Toàn hệ thống **không có concurrency token nào**. Ví dụ rõ nhất ở
`InventoryCheckService.ApproveAsync`: EF sinh `UPDATE ProductSerials SET Status=5 WHERE Id=@p`, **không
có mệnh đề trạng thái nào** — một serial bị POS bán mất trong khoảng đó bị ghi đè thẳng từ `Sold` sang
`Lost`.

**Cách sửa.** `byte[] RowVersion` với `IsRowVersion()` cho `ProductSerial`, `ServiceTicket`, `Quotation`,
`InventoryCheck`, `Order`, `RmaShipment`. Sau đó EF sinh `… WHERE Id=@p AND RowVersion=@v` → 0 dòng →
`DbUpdateConcurrencyException` → rollback → **all-or-nothing cho cả lần phê duyệt**, đúng điều mong muốn.

**Không** đặt lên `Voucher` (đã dùng atomic increment, và `ExecuteUpdateAsync` bỏ qua token) và **không**
lên `AppUser` (Identity đã có `ConcurrencyStamp`).

🚨 **`ExecuteUpdateAsync` BỎ QUA HOÀN TOÀN token** (nó không qua Change Tracker), nên chuyển một đường ghi
từ tracked-write sang `ExecuteUpdate` là **âm thầm gỡ mất** lớp bảo vệ — phải tự đưa vị từ trạng thái vào
`Where`. Đã rà: 3 đường ghi `Quotation` đều đã làm đúng.

## 4.4 Ba unique index

| Index | Lỗi nó chặn |
|---|---|
| `UQ_ServiceTickets_SerialId_Open` (filtered) | hai phiếu dịch vụ mở trên cùng một serial |
| `UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId` | phê duyệt hai lần ghi trùng log ⇒ **kế toán tổn thất nhân đôi** |
| `UQ_VoucherUsages_UserId_VoucherId_SeqPerUser` | vượt `MaxUsesPerUser` |

**Trên SQL Server, filtered index không cho `NOT IN`** nên phải bung thành
`Status <> 3 AND Status <> 8 AND Status <> 9 AND Status <> 10 AND IsDeleted = 0`.

🚨 **Vị từ index và vị từ trong code phải LUÔN khớp.** Thêm một trạng thái terminal mà quên index thì bất
biến **âm thầm nới ra** — không có gì báo lỗi. Hai chỗ comment chéo nhau. Đã kiểm trên DB đang chạy: vị từ
của `sys.indexes` khớp chính xác `terminalStates = {3,8,9,10}` trong `ServiceTicketRepository`.

🚨 **Unique index KHÔNG tự bảo vệ một hạn mức đếm được.** Nó chỉ chặn hai bản ghi cùng khoá.
`UQ_VoucherUsages_…_SeqPerUser` chặn được hai insert cùng `SeqPerUser`, nhưng **không biết
`Voucher.MaxUsesPerUser`** — kẻ thua bị tuần tự hoá sẽ đọc `MAX = 1`, dùng `SeqPerUser = 2` và **đi qua
index**. Vì vậy hạn mức phải được kiểm **LẠI bên trong transaction**, sau câu `MAX`. Giữ **cả hai** chốt.

## 4.5 Chốt chặn trong migration

Migration **`THROW`** nếu DB đích có dữ liệu xung đột, kèm câu chỉ thẳng script phải chạy. Đó là **cố ý**:
**xoá bản ghi kế toán là quyết định nghiệp vụ**, migration không quyết thay người chịu trách nhiệm.

Ngược lại, backfill `SeqPerUser` thì migration **tự làm** — nó không xoá gì và `ROW_NUMBER()` bảo đảm tính
duy nhất **tự thân cấu trúc**.

---

# Gói 4 (nửa CODE) — chuẩn bị chạy nhiều task

## 5.1 `ICacheService`

Bọc `IDistributedCache` để ngày chuyển sang Redis chỉ đổi **một** dòng đăng ký DI.

⚠️ **Mọi lỗi cache bị nuốt + log Warning**, kể cả `RemoveAsync` — nghĩa là xoá thất bại thì dữ liệu **cũ
còn tới khi TTL hết**. Với thứ gì mà "cũ" là SAI, đọc thẳng DB.
🚨 Exception từ `factory` của `GetOrCreateAsync` **không** bị nuốt: nuốt nó là biến *"DB sập"* thành
*"danh mục rỗng"* — loại lỗi tệ nhất vì **nó nói dối**.

## 5.2 `ShutdownTimeout = 45`

Số **giữa** của bộ ba 30/45/90: `deregistration_delay + ShutdownTimeout < stopTimeout`.
*(Ba số này hiện chưa khớp nhau — xem mục 7.1.)*

## 5.3 DataProtection qua SSM Parameter Store

**Sai ở đâu.** Mặc định ASP.NET Core sinh key ring vào ổ đĩa **của từng container**, nên link đặt lại mật
khẩu / xác nhận email do task A phát hành thì task B **không giải mã được**.

**Cách sửa.** Persist key ring vào SSM, **có điều kiện** theo `DataProtection:SsmPrefix` — thiếu cấu hình
⇒ hành vi như cũ (để `dotnet run` ở local vẫn chạy).

`SetApplicationName("HushStore")` **bắt buộc**: thiếu nó thì purpose string lấy theo tên assembly, hai task
ra khác nhau, và key ring dùng chung mà **vẫn** không giải mã được cho nhau — bug **ngược lại** với điều
đang sửa, và im lặng hơn.

🚨 **Env var và chính sách IAM phải vào CÙNG một lần deploy.** Đã đo: đặt env var mà task role chưa có
quyền thì app **vẫn khởi động**, `health/live` xanh, ECS coi task healthy — nhưng `IDataProtector.Protect`
ném `CryptographicException`. Chỉ vài đường 500, không dashboard nào đỏ.

---

# Đợt nghiệm thu trước ASG EC2 (2026-09-03) — 4 chốt chặn

Năm reviewer độc lập rà lại `c5e0215..7bc61a4` trước khi chạm `infra/tf/`. **Cả 4 chốt chặn tìm được đều
nằm ở tầng code, không ở hạ tầng.**

> **Điều rút ra, đáng hơn cả bốn lỗi:** cả bốn đều là chỗ được nghiệm thu bằng **hình dạng** (grep, build
> sạch, "đã viết dòng đó"), và phép đo hành vi duy nhất được chạy thì **rơi trúng ca hoạt động được** —
> gói 4 đo cache bằng một kiểu **tham chiếu**; cổng rò rỉ quét đúng phần `.cs`; 409 được đo ở **tầng HTTP**
> chứ không ở tầng người dùng; DataProtection đo với `AWS_PROFILE` ở local.
> Đây chính là luật của repo *("grep chỉ chứng minh hình dạng code")* **không được áp lên chính đợt vừa làm**.

## 6.1 DataProtection ném ngay lần deploy đầu, với mọi tín hiệu sức khoẻ đều xanh

**Sai ở đâu.** `PersistKeysToAWSSystemsManager(prefix)` được gọi **không có region**. Thư viện lấy
`IAmazonSimpleSystemsManagement` từ DI và **fallback `new AmazonSimpleSystemsManagementClient()`** khi
không thấy; constructor không tham số phải tự phân giải region: env `AWS_REGION` → file config → **IMDS**.

| Dữ kiện | Đo được |
|---|---|
| Đăng ký `IAmazonSimpleSystemsManagement` | **0** |
| `AWS_REGION` trong `taskdef.tf` | **không có** |
| IMDS | `hop_limit = 1` — comment ngay trên nó nói *"container (bridge network = thêm 1 hop) không tự gọi được"* |
| `AmazonS3Client` cùng file, ngay dưới | **có** truyền `RegionEndpoint` tường minh |

⇒ chuỗi cạn đường ⇒ `AmazonClientException: No RegionEndpoint or ServiceURL configured`.

**Vì sao quan trọng.** Credential thì **vẫn ổn** (đi qua `AWS_CONTAINER_CREDENTIALS_RELATIVE_URI`), nên
đây **không phải** lỗi IAM — nhưng nó **trông hệt** ca thiếu quyền IAM đã ghi trong CLAUDE.md. Ai chẩn
đoán nhầm sẽ đi **nới rộng IAM policy** để đuổi một triệu chứng không do IAM, tức **phá một deliverable
bảo mật vì một lỗi cấu hình region**.

**Cách sửa.** Dựng client SSM tường minh với region, đúng khuôn `AmazonS3Client` đã dùng từ trước:

```csharp
var ssmRegion = builder.Configuration["AwsSettings:Region"] ?? "ap-southeast-1";
builder.Services.AddSingleton<IAmazonSimpleSystemsManagement>(_ =>
    new AmazonSimpleSystemsManagementClient(RegionEndpoint.GetBySystemName(ssmRegion)));
```

Kèm **chuẩn hoá prefix** (`TrimEnd('/') + "/"`): thiếu dấu `/` cuối thì `GetParametersByPath` và
`PutParameter` ghép tên khác nhau ⇒ key ring **luôn rỗng** ⇒ mỗi task tự sinh key riêng — im lặng y hệt.
Và khai `AWSSDK.SimpleSystemsManagement` **tường minh** trong `API.csproj` vì code nay dùng trực tiếp.

⚠️ **Code không thể tự bảo vệ yêu cầu "prefix phải hẹp"** — `/hushstore/prod/` chạy y hệt
`/hushstore/prod/dataprotection/`. Ranh giới đó **chỉ tồn tại trong IAM policy**.

## 6.2 Mọi phản hồi lỗi cắt ngang không giải tuần tự được ở 49 chỗ phía client

**Sai ở đâu.** Bốn đường lỗi cắt ngang đều ghi `ApiResult<object>.Fail(...)` ⇒ thân có `"data": null`:

| Đường | Mã |
|---|---|
| Rate limit | 429 |
| Exception tổng | 500 |
| **Khoá tài khoản** | 403 + `X-Account-Status: locked` |
| **Xung đột đồng thời** | 409 |

Client đọc chúng bằng `ReadFromJsonAsync<ApiResult<bool>>` ở **49 call-site** (47 `bool` + 2 `int`, trong
18 file). `ApiResult<T>.Data` khai `T?`, nhưng **`T` không ràng buộc** nên `T?` chỉ là chú thích — ở
runtime với `T = bool` nó vẫn là `bool` không-nullable, và `System.Text.Json` **ném**.

Đo được:
```
ApiResult<bool>   -> JsonException: The JSON value could not be converted to System.Boolean
ApiResult<int>    -> JsonException
ApiResult<string> -> OK
```

**Vì sao quan trọng.** Đây là **chỗ toàn bộ công của đợt 3 dừng lại**: chuỗi 27 chốt controller →
`ConflictExceptionHandler` → `RowVersion` → unique index đều nhằm đưa một câu tiếng Việt có ích tới người
dùng, và nó **chết ở bước cuối cùng**. Rộng hơn nữa: **403 khoá tài khoản cũng chết** — đó là tính năng
bảo mật, người dùng phải thấy lý do khoá.

Chưa ai phát hiện vì 409 được đo ở **tầng HTTP**, không ai đo ở **tầng người dùng**.

**Cách sửa.** Một tuỳ chọn JSON dùng chung (`ErrorResponseJson.Options`) với
`DefaultIgnoreCondition = WhenWritingNull`, áp cho cả 4 đường. Bỏ khoá `data` khỏi thân lỗi thì `bool`
nhận `default(false)` và không ném.

Sửa ở **server** là 4 chỗ và đóng vĩnh viễn; sửa ở client là 49 chỗ và mọi client viết sau phải nhớ.
Ngoài ra `data: null` vốn không mang thông tin gì trong một phản hồi lỗi.

⚠️ Chỉ áp cho phản hồi **lỗi**. Đường thành công phải giữ `data` kể cả khi null — ở đó "null" là một câu
trả lời có nghĩa.

**Đo lại qua HTTP thật:** body 403 và 429 nay không còn khoá `data`; `ApiResult<bool>`, `<int>`, `<string>`
đều đọc được câu tiếng Việt.

## 6.3 `GetOrCreateAsync` với kiểu giá trị không bao giờ gọi `factory`

**Sai ở đâu.** `var cached = await GetAsync<T>(key); if (cached is not null) return cached;`
`GetAsync<T>` trả `default(T)` khi miss. Với `T` là kiểu **giá trị**, `default(T)` là `0`/`false` —
**không phải null** — nên `cached is not null` **luôn đúng**.

**Đo được trên cache HOÀN TOÀN RỖNG:**
```
T = int    -> trả 0     | factory gọi 0 lần   (kỳ vọng 42 / 1)
T = bool   -> trả False | factory gọi 0 lần   (kỳ vọng True / 1)
T = List<> -> trả 2     | factory gọi 1 lần   ✓
```

`GetOrCreateAsync<int>("ton-kho", …)` trả `0` mà **không hề chạm DB**, mãi mãi.

**Vì sao quan trọng.** Đúng lớp lỗi mà XML doc của chính interface tuyên bố sẽ không phạm — trả `0`/`false`
như một câu trả lời hợp lệ, tức **nó nói dối**. File bằng chứng gói 4 đo `factory = 1` nhưng **với một kiểu
tham chiếu**, nên ca này chưa từng được chạm tới.

**Cách sửa.** Tách `TryGetAsync<T>` trả `(bool Found, T? Value)` — phân biệt hit/miss **ở tầng byte**
(`byte[]` null hoặc rỗng = miss), tức tín hiệu đáng tin **duy nhất**, trước khi giải tuần tự.

⚠️ **Đừng "sửa" bằng cách so với `default(T)`** — làm thế thì một giá trị `0` **hợp lệ** bị coi là miss mãi mãi.

Đo lại, gồm cả hai ca hồi quy cố ý thêm: `int`/`bool`/`List` đều factory 1 lần · đọc lại là **cache HIT**
(factory 0 lần, không phá cache) · giá trị **`0` hợp lệ** được coi là hit.

## 6.4 Cổng chống rò rỉ mù với 108 file `.razor`

**Sai ở đâu.** Glob là `src/Client/**/*.cs`. Tầng Client là **Blazor** — logic xử lý sự kiện nằm trong
khối `@code` của file `.razor`, và **108 file không bao giờ được quét**.

Còn **15 chỗ** `Snackbar.Add($"…{ex.Message}")` trong `catch (Exception)`, tất cả bắn thẳng ra người dùng.
`ImportReceiptPage.razor:508` — `"Lỗi hệ thống: " + ex.Message` — là **đúng nguyên văn ví dụ SAI** mà
CLAUDE.md dùng để dạy chính luật này.

Và chúng **chạm tới được thật**: `OrderClientService.CancelOrderAsync`/`CompleteOrderAsync` không có
`try/catch` nào, nên `HttpRequestException` xuyên thẳng lên `OrderDetail.razor:287`.

**Vì sao quan trọng.** Một chốt chống hồi quy **mù nửa phạm vi mà báo "Sạch" thì tệ hơn không có chốt** —
nó **dập tắt nghi ngờ**. Đúng loại "bằng chứng an toàn giả" mà CLAUDE.md xếp là nguy hiểm hơn không có
bằng chứng. Dòng `Sạch [all]: 140 file` đang được đọc như một khẳng định về **cả tầng Client**, trong khi
nó chỉ đo 58/166 file.

**Cách sửa.** Thêm `('Client', 'src/Client/**/*.razor')` vào glob, và sửa 15 chỗ theo đúng khuôn đã dùng
cho 124 chỗ ở tầng service: câu tiếng Việt cố định **có tính hành động**, **kèm log** — trang `.razor`
không có `ILogger` sẵn nên phải `@inject Microsoft.Extensions.Logging.ILogger<T>` vào 14 file.

**Đo lại:** cổng nay quét **249 file** (trước 140); riêng client **166** (trước 58) — tức 108 file `.razor`
đã vào. `0 chỗ`, exit `0` ở cả ba phạm vi.

---

## 6.5 Deadlock 1205 bị nuốt ở đúng 27 action — và một đường bọc khó thấy hơn

**Sai ở đâu.** Ba khoảng trống cùng một nguyên nhân: **hai danh sách độc lập cho cùng một câu hỏi.**

| | Nhận diện được |
|---|---|
| 27 chốt controller — quyết định cái gì **thoát ra** | `DbUpdateConcurrencyException`, `SqlException 2601/2627` |
| `ConflictExceptionHandler` — quyết định cái gì thành **409** | ba loại trên **+ `1205`** **+ `ConcurrentModificationException`** |

Giao của hai danh sách mới là thứ chạy. Phần dôi ra là **code chết**: `grep -rn "1205" src/` ra đúng
**một** dòng — chính dòng khai báo trong handler.

Khoảng trống thứ hai khó thấy hơn nhiều: **`EnableRetryOnFailure` đã bật** (`Program.cs:212`). Deadlock
nằm trong danh sách transient của SQL Server nên nó **bị thử lại**; hết lượt thì EF bọc nguyên nhân gốc
vào `RetryLimitExceededException`. Phép so khớp cũ là so khớp **một tầng** ⇒ không khớp cái nào ⇒ **500**.
`grep -rn "RetryLimitExceeded" src/` ra **0 chỗ**.

Khoảng trống thứ ba: **8 action `GET` không có chốt nào.** Tiền đề trong `CLAUDE.md` — *"GET không sinh
được hai loại này"* — đúng cho `2601/2627` và `RowVersion`, nhưng **sai cho `1205`**: một `SELECT` bị SQL
Server chọn làm nạn nhân là chuyện bình thường.

**Vì sao quan trọng.** Cả ba chỉ hiện ra **dưới tải**, và cả ba đều trả sai loại thông báo: `500` nói
"server hỏng" trong khi sự thật là "thử lại đi". Hai instance đánh nhau trên cùng bảng thì deadlock
**tăng lên** — tức lỗi này nặng dần đúng lúc đợt 5 bắt đầu, và nó phá thẳng luận điểm mà đợt 6 định vẽ
(*"vượt năng lực thì nhận 429/409 sạch thay vì 500"*).

**Cách sửa.** `ConflictClassifier` (`src/Infrastructure/Concurrency/`) làm nguồn sự thật duy nhất.
Controller hỏi `IsConflict`, handler hỏi `Classify` — **cùng một hàm**, nên chúng không lệch được nữa.
Nó **đi hết chuỗi `InnerException`** (chặn ở 16 tầng) thay vì so khớp một tầng, nên không cần nhắc tên
`RetryLimitExceededException` và vẫn đúng cho mọi lớp bọc viết sau.

**35 chốt** ở 4 controller (27 mutation + **8 đọc**) + 2 chốt ở `OrderService`.

⚠️ **Hai chỗ CỐ Ý không đổi:** `ServiceTicketService` và `InventoryCheckService` **dịch** vi phạm unique
thành câu nghiệp vụ riêng. Đổi chúng sang `IsConflict` là **lỗi** — deadlock sẽ được báo là *"Sản phẩm này
đã có phiếu sửa chữa chưa đóng."*, sai sự thật, và người dùng đi tìm một phiếu không tồn tại. Luật: dùng
`IsConflict` ở chỗ **rethrow**, không dùng ở chỗ **dịch nghĩa**.

**Đo bằng deadlock THẬT** (hai kết nối khoá hai bảng theo thứ tự ngược nhau) — 12/12 đúng, **0 hồi quy**,
với cột đối chứng âm chạy logic cũ trên cùng những exception đó và **để lọt 3 ca**. LoadProbe 9/9 không hồi
quy; 409 đầu-cuối còn nguyên và câu nghiệp vụ 400 vẫn giữ nguyên văn.
[Bằng chứng đầy đủ](evidence/2026-09-03-conflict-classifier.md).

---

# Còn lại — chưa sửa, ghi để không rơi

## 7.1 Bộ ba 30/45/90 hiện mâu thuẫn với chính nó

| Số | Nơi khai | Hiện tại | Comment giả định |
|---|---|---|---|
| `deregistration_delay` | `alb.tf:58, :83` | **5** | 30 |
| ECS `stopTimeout` | chưa khai | **không có** | 90 |
| `ECS_CONTAINER_STOP_TIMEOUT` | `user_data.sh.tftpl:26` | **30s** | 90s |

⇒ `ShutdownTimeout(45) > stopTimeout thật(30)`. Và `alb.tftest.hcl` **khẳng định ngược lại** rằng nó phải
bằng `5`. **Hai tiêu chí XONG không thể cùng đúng — phải quyết trước khi sửa Terraform.**

## 7.2 Cold-start 2 task trên key ring SSM rỗng tái tạo đúng bug đang sửa

Hai task cùng thấy prefix rỗng, mỗi task tự sinh key riêng và `PutParameter` tên khác nhau — **không xung
đột, không lỗi**. `KeyRingProvider` làm mới theo chu kỳ **24 giờ**.

Cách tránh **không suy ra được từ code**: chạy 1 task, gọi `Protect` một lần, xác nhận SSM có ≥ 1 parameter,
rồi mới scale.

## 7.3 Các mục nhỏ hơn

- `HushStoreDbContext:491` — `HasIndex(t => t.SerialId)` là **dòng chết** (EF gộp nó vào index filtered ở
  dòng 505). Xác minh trên DB: `IX_ServiceTickets_SerialId` **không tồn tại**. Người sau xoá dòng 505 "cho
  hết trùng lặp" là mất bất biến.
- `Quotation.RowVersion` là **bẫy đã lên nòng**: `GetByIdWithTrackingAsync` đưa `Quotation` vào Change
  Tracker với `RowVersion = V1`, `TryDecideAsync` làm nó thành V2 dưới DB, rồi `SaveChangesAsync()`. Hôm
  nay an toàn vì không ai gán thuộc tính lên nó; ngày ai đó thêm `quotation.Note = …` sẽ có **409 cho một
  người dùng không hề đua với ai**.
- Migration cần **cửa sổ bảo trì**: 6 × `ALTER TABLE ADD rowversion` là thao tác **size-of-data**, và
  `dotnet ef database update` dùng command timeout mặc định 30s.
- `ProductVariantService` còn 2 chỗ nhận diện unique violation bằng `Message.Contains("duplicate")` — đúng
  bẫy CLAUDE.md cấm.
- `ICacheService` có **0 call-site**; `Serilog.AspNetCore` có **0 dòng code dùng** (cùng hình dạng với sự
  cố AutoMapper).
- Không có `Max Pool Size` ở bất kỳ đâu — mặc định .NET là 100/tiến trình; 2 task = 200, cộng migrator +
  seeder lúc deploy, trên `sqlserver-ex`.
- `/health/ready` chạm DB ⇒ một cú chớp của RDS làm ALB rút **cả hai** target cùng lúc.
- Mục 🅹: **35 lời gọi `GetFromJsonAsync`** (40 nếu tính cả `Pages/`) vẫn **vứt thân phản hồi** vì hàm đó
  tự gọi `EnsureSuccessStatusCode` bên trong.

## 7.4 Sai lệch giữa tài liệu và thực tế

- **"S03/S04/S07/S08 quan sát 5 lần ở 2 instance" — đếm được 1.** LoadProbe ghi một file mỗi lần chạy;
  repo có **đúng một** báo cáo ở cấu hình 2 instance, `git log --diff-filter=D` rỗng. Theo luật của chính
  repo (*một ✅ không là bằng chứng an toàn*), bốn kịch bản này đang đứng trên **một** ✅. Riêng ✅ của S07
  là ca POS **thua** đua — nhánh nguy hiểm chưa hề chạy.
- **CLAUDE.md tự mâu thuẫn** ở dòng 393–394 với 359–362 của chính nó. Commit đúng, hai dòng đó là bản cũ sót.
- **Gói 4 chưa đo thứ chính:** *task A phát hành → task B giải mã được*. Cả ba ca đo đều chạy trên **một**
  tiến trình, trong khi bug cần sửa theo định nghĩa chỉ tồn tại khi có hai.
- **Hai chốt `THROW` của migration chưa bao giờ được quan sát nổ** — bằng chứng gói 3 ghi `Error 1505`
  (lỗi ở bước `CREATE UNIQUE INDEX`), mà nếu chốt đã có mặt thì migration phải dừng ở `50001` **trước đó**.
  Suy ra chúng được thêm **sau** như phản ứng với lỗi ấy, và chỉ có build sạch chống lưng.
- **Số lệch:** `139` → **140** (nay là **249** sau khi thêm `.razor`) · `18/18 client service` → **18/24** ·
  `35 GetFromJsonAsync` → 35 trong `Services/` nhưng **40** toàn `src/Client`.

---

# Cách nghiệm thu lại

```bash
dotnet build PBL3.sln                                     # 0 Error
bash devops/scripts/check-vulnerable-packages.sh          # exit 0
bash devops/scripts/check-error-message-leaks.sh all      # exit 0 — exit 2 = KHÔNG KẾT LUẬN
dotnet run --project tools/LoadProbe -- --scenarios S02,S05   # chốt hồi quy rẻ nhất
```

**Bốn phép đo còn nợ, phải làm trước khi chạm `infra/tf/`:**

1. **DataProtection xuyên task** trên `docker-compose.multi.yml`: `Protect` ở `:8081`, `Unprotect` ở
   `:8082`, kèm ca đối chứng âm (bỏ `SetApplicationName` → phải hỏng).
2. **Chạy lại `--scenarios S03,S04,S07,S08` ở cấu hình 2 instance** cho đủ số lần đã tuyên bố, kiểm
   `X-Upstream` **trước** khi đo. Nếu không định chạy thì **sửa con số xuống đúng số quan sát được**.
3. **Ép hai chốt `THROW` của migration nổ thật** trên bản migration hiện tại.
4. **Quyết `deregistration_delay`** (5 hay 30).
