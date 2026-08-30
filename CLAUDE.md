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
| **Service** | Business logic, AutoMapper profiles, orchestration of repositories. |
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
4. **Service** — add `IXyzService` interface + `XyzService` implementation, register AutoMapper profile
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
- Never return entities from API endpoints — always map to DTOs via AutoMapper
- Use `ILogger<T>` (Serilog) for logging; no sensitive data in logs
- RESTful URLs: `GET /api/products`, `GET /api/products/{id}`, `POST /api/products`, `PUT /api/products/{id}`, `DELETE /api/products/{id}`
- **Mọi thông báo lỗi trả về cho người dùng (user-facing messages) phải bằng tiếng Việt có dấu.**
  - Sai: `return BadRequest("Product not found");`
  - Đúng: `return BadRequest("Không tìm thấy sản phẩm yêu cầu.");`

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
  Cấm viết lại khối "đọc mã cuối trong ngày rồi +1". Cũng cấm tìm mã cuối bằng
  `ORDER BY Code DESC` — đó là so sánh **chuỗi**, và nó chính là nguyên nhân quả bom
  `{n:D3}` (quá 999/ngày thì `-1000` sắp trước `-999`).

- **Không cache trạng thái phân quyền hay khoá tài khoản trong `MemoryCache`.**
  `IsActive`, role, quyền — đọc thẳng DB bằng projection. `MemoryCache` nằm trong RAM của
  **một** tiến trình; với nhiều task, hướng nguy hiểm là hướng **mở khoá**: cache nói tài
  khoản còn hoạt động trong khi DB đã khoá. Đó là lỗi bảo mật, không phải lỗi hiệu năng, và
  triệu chứng là "lúc được lúc không tuỳ ALB định tuyến" — không tái hiện được.
  (Cache dữ liệu **công khai, ít đổi** như danh mục/menu thì vẫn khuyến khích.)

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

## AI Context Files

Detailed specifications in `/AI_context/`:
- `tech_contex.md` — full technical spec, conventions, business logic rules
- `security_context.md` — JWT flow, authorization patterns, IDOR prevention requirements
- `database_optimization.md` — DB performance strategies
- `srs/` — per-module Software Requirements Specifications
- `implementation_plans/` — in-progress feature implementation plans
