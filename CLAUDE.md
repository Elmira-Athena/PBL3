# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IT Hardware E-commerce & Management System ("HushStore") — a full-stack C# application with ASP.NET Core 10 Web API backend and Blazor WebAssembly frontend, backed by SQL Server 2025.

## Build & Run Commands

```bash
# Build entire solution
dotnet build PBL3.sln

# Run API (https://localhost:7010)
dotnet run --project src/API/API.csproj

# Run Blazor client (https://localhost:7107)
dotnet run --project src/Client/Client.csproj

# Start SQL Server container (from Infrastructure/db/)
docker-compose up -d

# Create a new EF Core migration
dotnet ef migrations add <MigrationName> --project src/Infrastructure --startup-project src/API

# Apply pending migrations
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

There are no automated tests in this project yet.

## Solution Architecture

Six projects in strict dependency order (arrows = "depends on"):

```
Core ← Infrastructure ← Service ← API
 ↑                                  ↑
 └────────── Shared ────────────────┘
             ↑
           Client
```

| Project | Role |
|---------|------|
| **Core** | Domain entities (POCOs), repository interfaces. No dependencies on other projects. |
| **Shared** | DTOs, Enums, `ApiResult<T>`, `PagedResult<T>`, FluentValidation validators. Used by both API and Client. |
| **Infrastructure** | EF Core `HushStoreDbContext`, migration files, repository implementations. |
| **Service** | Business logic, ánh xạ entity → DTO bằng projection LINQ, orchestration of repositories. |
| **API** | ASP.NET Core controllers — receive request → call service → return `ApiResult<T>`. Program.cs wires all DI. |
| **Client** | Blazor WASM pages, MudBlazor components, HttpClient-based client services. |

## Key Patterns

### API Response Shape
All endpoints return `ApiResult<T>` from `/src/Shared/`:
```csharp
ApiResult<T>.Ok(data, message)   // success
ApiResult<T>.Fail(message)       // failure
```
Paginated lists return `PagedResult<T>` wrapped in `ApiResult<PagedResult<T>>`.

### Adding a New Feature
Follow this checklist in order:
1. **Core** — add entity and `IXyzRepository` interface
2. **Shared** — add request/response DTOs, enums, FluentValidation validator
3. **Infrastructure** — add `DbSet`, Fluent API config in `HushStoreDbContext`, implement `XyzRepository`
4. **Service** — add `IXyzService` interface + `XyzService` implementation, ánh xạ sang DTO bằng `.Select(...)` trong LINQ
5. **API** — add `XyzController`, register `IXyzRepository`→`XyzRepository` and `IXyzService`→`XyzService` in `Program.cs`
6. **Client** — add client service calling the API, add Blazor pages/components

### Data Access
- Repository pattern: `IXyzRepository` in Core, `XyzRepository` in Infrastructure
- `IUnitOfWork` / `UnitOfWork` for multi-repository transactions (orders, import receipts)
- Use `AsNoTracking()` on read-only queries
- Use eager loading (`Include`) for related entities rather than lazy loading

### Soft Deletes & Audit
Phần lớn entity có `IsDeleted` (+ một phần có `DeletedDate`, `CreatedDate`, `ModifiedDate`).
Global EF query filter loại bỏ bản ghi soft-deleted tự động **cho những entity có khai filter**.

⚠️ **KHÔNG phải mọi entity đều có `IsDeleted`.** Đã kiểm trên schema thật:
`Orders`, `InventoryChecks`, `OrderSerials`, `VoucherUsages`, `ProductSerials` **không có** cột này;
`ImportReceipts` không có `CreatedDate`. Viết truy vấn hay script SQL thì kiểm cột trước,
đừng giả định — chính giả định này làm bản đầu của `Infrastructure/db/checks/pre_migration_checks.sql` chạy lỗi.

### Validation
FluentValidation validators live in `/src/Shared/Validators/`. The same validator class is used on both the API (server) and Blazor client (via Blazored.FluentValidation).

### Authentication
JWT Bearer: 15-min access token + 7-day refresh token. Three roles: `Admin`, `Employee`, `Customer`. Controllers use `[Authorize(Roles = "...")]`; public endpoints use `[AllowAnonymous]`.

## Domain Model Highlights

- **Product hierarchy**: `Category` (recursive parent/child) → `Product` → `ProductVariant` (SKU) → `ProductSerial` (physical unit)
- **Stock sync**: `ProductVariant.StockQuantity` is a DB column kept in sync by `InventoryService` by counting `ProductSerial` records with `Available` status — do not set it manually
- **Order flow**: Cart → Order (OrderDetail + OrderSerial) → Voucher deduction
- **POS flow**: Direct in-store sale without cart
- **JSON specs**: `ProductVariant.Specifications` is a `Dictionary<string, string>` stored as a JSON column

## Coding Conventions

- **Classes/Methods**: `PascalCase`; **variables/params**: `camelCase`; **private fields**: `_camelCase`
- All async methods end with `Async`
- Never return entities from API endpoints — luôn ánh xạ sang DTO bằng **projection LINQ**
  (`.Select(p => new ProductDto { ... })`), khớp với luật DTO Projection ở mục Query & Performance.
  ⚠️ **Repo KHÔNG dùng AutoMapper.** Bản trước của file này ghi "map to DTOs via AutoMapper"
  ở ba chỗ, nhưng đã kiểm trên toàn repo: **0** `CreateMap`, **0** `IMapper`, **0** `AddAutoMapper`,
  **0** lớp `: Profile` — trong khi có **22** chỗ projection thủ công. Gói `AutoMapper` từng nằm
  trong `API.csproj` và `Service.csproj` mà không một dòng code nào dùng; nó đã bị gỡ ở mục D
  vì đang mang một lỗ hổng High (`GHSA-rvv3-g6hj-g44x`). Đừng thêm lại rồi viết code dựa vào nó
  mà không đổi quy ước một cách có ý thức.
- Use `ILogger<T>` (Serilog) for logging; no sensitive data in logs
- RESTful URLs: `GET /api/products`, `GET /api/products/{id}`, `POST /api/products`, `PUT /api/products/{id}`, `DELETE /api/products/{id}`
- **Mọi thông báo lỗi trả về cho người dùng (user-facing messages) phải bằng tiếng Việt có dấu.**
  - Sai: `return BadRequest("Product not found");`
  - Đúng: `return BadRequest("Không tìm thấy sản phẩm yêu cầu.");`
- **Không bao giờ nối `ex.Message` của hạ tầng vào thông báo cho người dùng.**
  Chuỗi của EF Core / SQL Server là **tiếng Anh** và lộ nội tạng ORM, nên
  `throw new Exception("Lỗi hệ thống: " + ex.Message, ex)` **vi phạm luật ngay trên** dù nửa đầu
  câu là tiếng Việt. Đây là lỗi đã có thật ở `OrderService`, đã sửa ở mục 🅴; LoadProbe tái hiện
  được nó ở nhánh thua cuộc đua sinh mã chứng từ. Chi tiết `ex` đi vào `ILogger<T>`, người dùng
  nhận một câu tiếng Việt cố định.
  🚨 **Nhưng "thay cả khối `catch` bằng một câu cố định" là sai — đó là cái bẫy.** Cùng khối
  `catch (Exception)` đó thường **cũng** là đường đi của thông báo **nghiệp vụ** đã soạn cho
  người dùng (`"Mã 'X' đã hết lượt sử dụng."`). Nuốt chúng thành câu chung là hồi quy UX nặng
  hơn lỗi ban đầu: người dùng mất đúng thông tin cần để tự sửa, bấm lại thì hỏng y hệt.
  Khuôn đúng — phân loại **tại nguồn**, không bằng cách dò nội dung chuỗi:
  ```csharp
  // throw nghiệp vụ: message BẮT BUỘC là tiếng Việt, an toàn để hiển thị
  throw new BusinessRuleException($"Mã '{code}' đã hết lượt sử dụng.");
  …
  catch (BusinessRuleException) { throw; }            // PHẢI đứng trước catch (Exception)
  catch (Exception ex)
  {
      _logger.LogError(ex, "Checkout thất bại cho người dùng {UserId}.", userId);
      throw new Exception("Không thể hoàn tất đặt hàng do lỗi hệ thống. …", ex);
  }
  ```
  `BusinessRuleException` ở `src/Core/Exceptions/`. Cùng lý lẽ với `ConcurrentModificationException`:
  bắt `InvalidOperationException` thay thế là **không** an toàn vì EF Core dùng chính kiểu đó cho
  chuyện khác — vì vậy `grep -rn 'catch (InvalidOperationException' src/` **phải luôn rỗng**.
  ✅ **Cả ba tầng đã sạch:** Service + API (mục 🅷) và **Client** (mục 🅸, xong 2026-09-01) —
  `0` chỗ chở `ex.Message` của hạ tầng ra cho người dùng. Ở tầng Client, 124 chỗ đã đổi sang câu
  tiếng Việt cố định **có tính hành động**, và **18/18 client service nay inject `ILogger<T>`**
  (trước đó là `0/18`) — vì "thay chuỗi" mà không log là **vứt sạch chẩn đoán**. Khuôn:
  ```csharp
  catch (Exception ex)
  {
      _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách nhà cung cấp");
      return ApiResult<T>.Fail("Không tải được danh sách nhà cung cấp. Vui lòng thử lại.");
  }
  ```
  ⚠️ **Còn một biến thể CHƯA đóng ở tầng Client** (mục 🅹): **`GetFromJsonAsync` tự gọi
  `EnsureSuccessStatusCode` bên trong**, nên **35** lời gọi GET vẫn **vứt thân phản hồi** — câu
  tiếng Việt server soạn mất trắng. Mục 🅸 chỉ làm chúng đỡ hơn (câu cố định thay chuỗi EF), không
  đóng được. **Viết client service mới thì dùng `ApiCall.SendAsync`**
  (`src/Client/Services/Common/`) — nó đọc thân phản hồi để lấy đúng câu đó, phân biệt
  `HttpRequestException` với `TaskCanceledException`, và có sẵn ánh xạ `409`.
- **Chốt chống hồi quy — chạy `devops/scripts/check-error-message-leaks.sh`** cho **cả ba tầng**
  (kỳ vọng `Sạch [all]: 139 file, 0 chỗ`, mã thoát `0`; mã thoát `2` = **KHÔNG KẾT LUẬN**, không
  phải sạch). Tham số `server` / `client` để soi từng tầng — **cả hai nay đều phải ra `0`**.
  🚨 **Đừng thay nó bằng `grep 'ex.Message'`.** `grep` không biết dòng đó nằm trong khối `catch`
  **nào**, nên nó đếm cả **41** chỗ relay **đúng** (từ `catch` nghiệp vụ) thành lỗi. Đã đo: cách
  đếm bằng grep phóng đại 2 chỗ rò rỉ thật ở tầng Service thành 7. Phân loại phải theo **ngữ cảnh**.
  Bản mẫu làm đúng từ đầu: `InventoryCheckService` — copy khuôn của nó.

## Query & Performance Rules

Các quy tắc này bắt buộc, không được bỏ qua:

- **`.AsNoTracking()`** — bắt buộc cho mọi query GET/read-only. EF Core không cần Change Tracker → tăng tốc 2-5 lần.
- **DTO Projection** — không bao giờ `SELECT *`. Dùng `.Select(p => new ProductDto {...})` thẳng trong LINQ thay vì lấy entity rồi mới map.
- **N+1 Prevention** — khi cần dữ liệu bảng con, dùng `.Include()` hoặc projection. Tuyệt đối không dùng vòng lặp foreach gọi DB thêm.
- **Pagination bắt buộc** — mọi API danh sách phải có `Skip()` + `Take()`. Không được trả về toàn bộ bảng.
- **Async only** — dùng `ToListAsync()`, `FirstOrDefaultAsync()`, `AnyAsync()`. Cấm dùng `.Result` hoặc `.Wait()` gây deadlock.
- **PLINQ** — chỉ dùng `.AsParallel()` sau khi đã `ToList()` về RAM (CPU-bound tasks như kiểm tra tương thích Build PC). Tuyệt đối không gọi `.AsParallel()` trực tiếp trên `DbSet` hay `IQueryable`.
- **Caching** — dùng `MemoryCache` cho dữ liệu ít thay đổi (danh mục, menu) để đảm bảo API listing < 2 giây.

## Bắt buộc dùng lớp trừu tượng có sẵn

Bốn quy tắc này sinh ra từ lỗi có thật đã sửa ở đợt 1 — vi phạm sẽ tái tạo đúng lỗi cũ.

- **Transaction — chỉ qua `IUnitOfWork.ExecuteInTransactionAsync`.**
  Cấm gọi `_context.Database.BeginTransactionAsync()` thủ công: EF Core cấm nó khi có
  retrying execution strategy và sẽ ném **lúc chạy, không lúc biên dịch**. Phần việc làm
  *sau khi* commit (đồng bộ tồn kho, đọc lại để map DTO, ghi log) phải nằm **ngoài**
  delegate — để trong thì nó chạy trong transaction, và khi retry sẽ chạy lại.

- **Sinh mã chứng từ — chỉ qua `IDocumentCodeGenerator`.**
  Cấm viết lại khối "đọc mã cuối trong ngày rồi +1" — **đó là check-then-act**, và LoadProbe S01
  đã đo: 32–41/50 đơn hỏng vì cùng tính ra một mã rồi đụng `IX_Orders_OrderCode`. Cũng cấm tìm mã
  cuối bằng `ORDER BY Code DESC` — đó là so sánh **chuỗi**, nguyên nhân quả bom `{n:D3}`
  (quá 999/ngày thì `-1000` sắp trước `-999`).
  ✅ **Ruột đã thay bằng SQL SEQUENCE (đợt 3 phần 1, 2026-09-01): S01 chuyển 🔴 → ✅, 50/50 đơn.**
  Số thứ tự do `SELECT NEXT VALUE FOR` cấp qua `IDocumentSequence` (`src/Core/Interfaces/`), khai
  bằng `modelBuilder.HasSequence<long>()`. **Sequence TOÀN CỤC, không reset theo ngày** — chính
  yêu cầu "reset mỗi ngày" là thứ bắt buộc phải có `SELECT MAX`, tức là nguyên nhân của race.
  Hai hệ quả phải biết trước khi động vào:
  - `NEXT VALUE FOR` **không mang tính giao dịch**: giá trị bị tiêu thụ dù transaction rollback,
    nên **dãy mã có lỗ**. Đừng "sửa". Số trong mã **không** còn là "chứng từ thứ N" — muốn đếm
    thì `COUNT(*)`. (Bù lại: khi transaction retry, lần thử sau lấy mã MỚI — đúng điều cần.)
  - Cột mã là `nvarchar(20)`, nên tiền tố 3 ký tự (`ORD`/`POS`/`SRV`) chịu tối đa **7 chữ số**.
    `ORD` và `POS` **dùng chung** một sequence vì cùng ghi vào `Orders.OrderCode`.

- **Không cache trạng thái phân quyền hay khoá tài khoản trong `MemoryCache`.**
  `IsActive`, role, quyền — đọc thẳng DB bằng projection. `MemoryCache` nằm trong RAM của
  **một** tiến trình; với nhiều task, hướng nguy hiểm là hướng **mở khoá**: cache nói tài
  khoản còn hoạt động trong khi DB đã khoá. Đó là lỗi bảo mật, không phải lỗi hiệu năng, và
  triệu chứng là "lúc được lúc không tuỳ ALB định tuyến" — không tái hiện được.
  (Cache dữ liệu **công khai, ít đổi** như danh mục/menu thì vẫn khuyến khích.)

- **Không thêm gói NuGet dính lỗ hổng High/Critical.** Cổng CI
  (`devops/scripts/check-vulnerable-packages.sh`) chặn ở bước `dotnet build`. Chạy trước khi
  mở PR. ⚠️ Đừng "kiểm nhanh" bằng `dotnet list package --vulnerable` rồi tin mã thoát: lệnh
  đó trả về **`0` kể cả khi tìm thấy lỗ hổng** — đã đo. Gói **transitive** dính lỗi thì vá bằng
  cách thêm `PackageReference` **ghim thẳng** vào `.csproj`, kèm comment nói rõ chuỗi phụ thuộc
  và khi nào xoá được (xem `src/Service/Service.csproj` để lấy mẫu). Chọn bản **nhỏ nhất đóng
  hết** advisory của gói đó — một gói có thể dính nhiều advisory với ngưỡng vá khác nhau.

- **DTO có phân trang — kế thừa `PagedRequest`** (`src/Shared/DTOs/Common/`).
  Nó tự clamp `PageSize` về `[1, 100]` và `PageNumber` về `>= 1` ngay trong setter, nên
  Blazor client dùng chung cũng không gửi nổi số lớn. Tầng API còn có `ClampPageSizeFilter`
  đăng ký global phủ mọi endpoint — hai lớp dùng **cùng** một trần, đổi một bên phải đổi
  bên kia.

## Security Rules

- **IDOR/BOLA protection** — khi customer truy cập resource của chính họ, bắt buộc kiểm tra ownership:
  ```csharp
  if (order.UserId != currentUserId) return Forbid();
  ```
- **JWT payload** — không nhét dữ liệu nhạy cảm (mật khẩu, số thẻ) vào Claims vì bất kỳ ai cũng decode được Base64.
- **JWT Secret** — phải là chuỗi ngẫu nhiên tối thiểu 256-bit, lưu trong Environment Variables hoặc Secret Manager, không hardcode.
- **ASP.NET Core Identity** — không tự viết bảng Users/Roles hay thuật toán hash mật khẩu thủ công.
- **Frontend không bảo vệ dữ liệu** — `<AuthorizeView>`, `AuthenticationStateProvider` chỉ phục vụ UX (ẩn/hiện nút). API Backend phải luôn validate token và quyền độc lập, bất kể UI có ẩn hay không.
- **Policy-Based Authorization** — khi logic phân quyền phức tạp hơn role đơn thuần (VD: "chỉ người tạo mới được xóa"), dùng Policy thay vì `[Authorize(Roles="...")]` cứng.

## Business Logic Rules

### Serial Status Flow
`SerialStatus` enum: `Available`, `Reserved`, `Sold`, `Defective`, `Returned`, `Lost`.

⚠️ **Trạng thái `Reserved` hiện KHÔNG được dùng.** Mô tả cũ ở đây (đơn online tạo ra rồi
nhân viên kho "Xác nhận đóng gói" mới chuyển `Available` → `Reserved`) là **thiết kế mong
muốn, chưa cài đặt**. Thực tế đang chạy: đơn online **không giữ chỗ serial nào**; chống bán
vượt bằng **tồn kho ảo** — `Available thực tế − số lượng đang nằm trong đơn online chưa
xuất kho (Pending/Confirmed/Shipping)`. Serial chỉ đổi `Available` → `Sold` lúc xuất kho.

Đây là **giới hạn đã biết, cố ý hoãn**. Đừng viết code dựa trên giả định `Reserved` tồn tại.

### Category Rules
- Không xóa danh mục nếu có danh mục con hoặc sản phẩm đang thuộc nó.
- Khi cập nhật `ParentId`, phải kiểm tra circular dependency (A là cha B → B không được phép đặt làm cha A).
- API tree endpoint phải trả về dạng JSON đệ quy để Frontend render TreeView/MegaMenu.

### Inventory Sync
`ProductVariant.StockQuantity` = `COUNT(ProductSerials WHERE Status = Available)` — không được hardcode hay cập nhật thủ công.

### Async vs Sync Decision
- **Đồng bộ (blocking):** Thanh toán, trừ tồn kho, login — cần nhất quán dữ liệu ngay lập tức.
- **Bất đồng bộ (background):** Gửi email/SMS, tạo PDF, resize ảnh — phải tách ra Background Job, không được chặn luồng chính.

## Frontend (Blazor) Patterns

- **Danh sách lớn** — dùng `<MudTable>` với `ServerData` (phân trang phía server).
- **Form** — dùng `<MudForm>` kết hợp `<FluentValidationValidator>`.
- **Thông báo** — dùng `ISnackbar` inject vào component để hiện toast Success/Error.
- **Dialog** — dùng `IDialogService` cho form Thêm/Sửa dạng popup, không chuyển trang.
- **Loading** — khi API > 2s, phải hiển thị Skeleton Loading hoặc Spinner, không để màn hình trắng.
- **Nút gọi mutation — dùng `<ActionButton>`, không dùng `<MudButton>`.**
  (`src/Client/Shared/Components/Common/`.) Nó đặt cờ bận **trước mọi `await`** và tự vẽ lại,
  nên không tái tạo được lỗi "cờ đặt sau `await`, UI không bao giờ nhận". Đừng thêm
  `Disabled="_isSaving"` hay spinner thủ công — `ActionButton` lo cả hai; `Disabled` chỉ dành
  cho lý do **nghiệp vụ** (sai trạng thái, thiếu quyền). Khi nhiều nút cùng tác động lên **một**
  đối tượng và có thể cùng hiện, bọc chúng trong `<BusyScope>` — khoá riêng từng nút là chưa đủ
  vì mỗi nút tự thấy mình rảnh. Nút điều hướng / đóng dialog / lọc thì giữ `<MudButton>`.
  ⚠️ Đây là UX, **không bảo vệ server**: hai tab hay `curl` vẫn double-submit. Phòng tuyến thật
  là conditional update + unique index ở tầng DB.
  🚨 **Ngoại lệ — nút `ButtonType="ButtonType.Submit"` bên trong `<EditForm OnValidSubmit="...">`
  thì `ActionButton` VÔ HIỆU.** Cú click submit form, không đi qua `OnClick` của nút, nên cờ bận
  `TryBegin()`/`End()` tức thì trong khi handler mới bắt đầu chạy. Đã đo: 3 click → 3 request.
  Hai chỗ như vậy (`Admin/Customers/CustomerDialog`, `Admin/Employees/EmployeeDialog`) cố ý giữ
  cờ thủ công. Muốn dùng `ActionButton` thì phải bỏ `ButtonType.Submit` và chuyển handler sang
  `OnClick` — đó là sửa cấu trúc form, không phải đổi tag.

## Đang làm dở — đọc trước khi viết code

🔴 **[`docs/bat-dau-phien-moi.md`](docs/bat-dau-phien-moi.md)** — điểm vào cho một phiên mới.
Nó ghi: việc kế tiếp (kèm `file:dòng` cụ thể), cách chạy môi trường local, công thức kiểm
chứng, và **mười ba cái bẫy im lặng** đã gặp. Đọc file đó trước khi sửa bất cứ thứ gì thuộc
tầng Service, auth, hay rate limiting.

Tóm tắt trạng thái: đợt 1 + đợt 2 + mục A + mục B + mục C + mục D + mục 🅴 + mục 🅷 +
**mục 🅸** đã xong (18/18 call-site transaction retry-safe; 23/23 nút mutation dùng
`ActionButton`/`BusyScope`, trong đó **6/6 nút hỏng thật đã đo có ca đối chứng âm**; bộ đo
`tools/LoadProbe/` + hạ tầng 2 replica đã chạy ra số ở **cả hai** cấu hình; 10/10 lỗ hổng NuGet
High đã vá và có cổng chặn ở CI; rò rỉ `ex.Message` đã chặn hết ở **cả ba tầng**, có cổng
`check-error-message-leaks.sh`), và **nợ kiểm thử 🧪 ưu tiên 1 + 2 đã trả** — bốn luồng cuối
(POS · nhập kho · xuất kho · phiếu dịch vụ) **đã chạy thật tới DB**, 4/4 ĐẠT, 0 bản ghi nhân đôi.

Còn lại: **mục 🅹** (35 lời gọi GET chuyển sang `ApiCall.SendAsync`) và **đợt 3** — đợt 3 **bị
chặn** tới khi chạy được `Infrastructure/db/checks/pre_migration_checks.sql` trên RDS.

🧪 **Nợ kiểm thử — đọc mục 🧪 của runbook trước khi tin dòng "XONG" nào.** `grep` chỉ chứng
minh **hình dạng code**, không chứng minh hành vi. Nợ của mục D **đã trả** (OpenAPI sinh được
sau khi ghim `Microsoft.OpenApi` 2.7.5; đủ 9 kịch bản LoadProbe chạy lại ở **cả hai** cấu hình,
không hồi quy). Nợ còn lại chia **không đều giữa hai nửa**:
- **Tầng Service:** Checkout **đã chạy thật tới DB**; POS · xuất/nhập kho · phiếu dịch vụ chưa.
- **Giao diện:** **cả 6 nút double-submit hỏng thật của mục B vẫn chưa nút nào được đo** — bất
  biến cần đo là "nút có khoá trong cùng một tick render", chỉ tồn tại trong trình duyệt, `curl`
  không đo được.

Chốt chặn "DB local không có `ProductSerials`" **đã tháo** và đường tháo đã chạy thật:
`dotnet run --project tools/LoadProbe -- --scenarios S01 --keep` để lại **60 serial `Available`**
(không script `.sql` nào seed bảng đó — đừng đi tìm). Lưu ý khách hàng do probe seed **không**
đăng nhập được bằng mật khẩu (token mint trong RAM) — muốn lái tay thì tự `POST /api/auth/register`
rồi thêm địa chỉ qua `POST /api/storefront/user-addresses`, và **tự dọn** sau khi đo.

🔴 **LoadProbe đã đo: 5/9 bất biến ĐÃ TỪNG SAI** (hợp của 4 lần chạy; mỗi lần chạy riêng lẻ
cho 4). Đọc mục 🅵 của runbook trước khi động vào tầng Service — nó nói rõ chỗ nào còn
check-then-act và chỗ nào đã an toàn. Bốn điều rút ra:
- Chỗ nào đã chuyển sang **conditional update** (`ExecuteUpdateAsync` có vị từ,
  `TryDecideAsync`) thì ĐẠT. Chỗ nào còn **check-then-act** thì HỎNG. Không có ngoại lệ.
- **S04 ĐẠT với 1 instance, HỎNG với 2.** Kết luận từ một cấu hình là kết luận sai.
- **S04 và S07 phụ thuộc thời điểm:** một lần 🔴 **là** bằng chứng hỏng, một lần ✅ **không** là
  bằng chứng an toàn. S04 ra ✅ với 2 instance ở lần chạy sau mà không ai sửa gì — đừng đọc
  thành "đã sửa".
- Sinh mã chứng từ vẫn đua nhau: 32–41/50 đơn đặt hỏng vì đụng `IX_Orders_OrderCode`.
  Dữ liệu không hỏng (unique index chặn), nhưng tính khả dụng thì có.

## Đo tính đúng đắn dưới tải — dùng `tools/LoadProbe/`

Repo không có test tự động. `tools/LoadProbe/` là thứ thay thế, và nó **không phải công cụ
đo hiệu năng**: nó bắn request song song rồi khẳng định bất biến bằng LINQ trên DB.

- **Lý do phải kiểm ở DB, không ở mã HTTP:** mọi lỗi đúng đắn dữ liệu tìm thấy ở repo này
  đều trả **200**. Voucher vượt hạn mức, sổ tổn thất nhân đôi, hai phiếu cùng một serial —
  tất cả đều "thành công" ở tầng HTTP.
- **`KHÔNG KẾT LUẬN` ≠ `ĐẠT`.** Kịch bản bị rate limiter chặn sẽ **thoả mọi bất biến** vì
  code cần đo chưa chạy. Đó là bằng chứng an toàn giả, nguy hiểm hơn không có bằng chứng.
- **Sửa tầng Service xong thì chạy lại `--scenarios S02,S05`** — hai kịch bản này đo đúng
  thứ đợt 1 đã sửa, nên chúng là chốt hồi quy rẻ nhất.
- **Thêm kịch bản mới thì phải bổ sung `ProbeFixture.CleanupAsync`** — đơn hàng và phiếu
  do API tạo ra trong lúc đo mang mã thật (`ORD-…`), không mang tiền tố `LP-`.

Hạ tầng 2 replica + nginx: `devops/docker/docker-compose.multi.yml`. Dùng nó cho mọi tính
chất **chỉ sai khi có nhiều hơn một tiến trình** — khoá tài khoản, seed lúc boot, phiên
đăng nhập nhảy instance.

## AI Context Files

Detailed specifications in `/AI_context/`:
- `tech_contex.md` — full technical spec, conventions, business logic rules
- `security_context.md` — JWT flow, authorization patterns, IDOR prevention requirements
- `database_optimization.md` — DB performance strategies
- `srs/` — per-module Software Requirements Specifications
- `implementation_plans/` — in-progress feature implementation plans
