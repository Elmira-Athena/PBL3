# DI Modernization & ApiResult ErrorCode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `ApiErrorCode` enum to `ApiResult<T>`, classify all 24 Service `Fail()` calls by semantic type, replace string-matching in all 25 Controllers with a typed `ToActionResult` extension, then migrate all 44 constructors (24 Services + 20 Repositories) to C# 12 Primary Constructor syntax with `?? throw` null-checking.

**Architecture:** Two independent waves committed separately. Wave 1 fixes observable behaviour: HTTP 404/409/403/400 are now driven by a typed enum, not fragile `Message.Contains(...)` string-matching. Wave 2 is a pure syntax refactor that produces identical runtime behaviour. Each wave ends with a green `dotnet build PBL3.sln` and a commit.

**Tech Stack:** C# 12, ASP.NET Core 10, project `PBL3.Shared` (enum + ApiResult), `PBL3.API` (extension + controllers), `PBL3.Application` (services), `PBL3.Infrastructure` (repositories).

---

## Classification Rules (reference for all Wave 1 tasks)

| Rule | `ErrorCode` | When to apply |
|------|-------------|---------------|
| Primary entity not found by ID | `NotFound` | The endpoint IS about this entity — GET/PUT/DELETE by ID. Results in HTTP 404. |
| Duplicate / uniqueness violation | `Conflict` | "đã tồn tại", "SKU đã tồn tại", duplicate email/phone. Results in HTTP 409. |
| Ownership / permission check failed | `Forbidden` | "không có quyền", "chỉ chủ sở hữu mới được". Results in HTTP 403. |
| FluentValidation failure wrapped in Fail() | `Validation` | `Fail(validation.Errors.First().ErrorMessage, ...)`. Results in HTTP 400. |
| Everything else | `Business` (default) | FK ref missing during Create, business rule violation, stock errors. **No change needed** — already the default. |

> **Critical rule:** "Nhà sản xuất không tồn tại" inside `CreateAsync` (FK validation) = **Business**. Only the direct entity lookup for the endpoint's own resource → **NotFound**.

---

## WAVE 1 — Behavior Fix

---

### Task 1: Create `ApiErrorCode` enum

**Files:**
- Create: `src/Shared/DTOs/Common/ApiErrorCode.cs`

- [ ] **Step 1: Create the file**

```csharp
namespace PBL3.Shared.DTOs.Common;

public enum ApiErrorCode
{
    None       = 0,
    NotFound   = 1,
    Forbidden  = 2,
    Validation = 3,
    Business   = 4,
    Conflict   = 5,
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build PBL3.sln --no-incremental -v quiet 2>&1 | tail -5
```

Expected: `Build succeeded.` with 0 errors.

---

### Task 2: Update `ApiResult<T>`

**Files:**
- Modify: `src/Shared/DTOs/Common/ApiResult.cs`

- [ ] **Step 1: Replace the entire file**

```csharp
namespace PBL3.Shared.DTOs.Common;

/// <summary>
/// Standard API response wrapper. Tất cả API trả về format này.
/// </summary>
public class ApiResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public ApiErrorCode ErrorCode { get; set; } = ApiErrorCode.None;

    public static ApiResult<T> Ok(T data, string message = "Thao tác thành công.")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResult<T> Ok(string message = "Thao tác thành công.")
        => new() { Success = true, Message = message };

    public static ApiResult<T> Fail(string message, ApiErrorCode errorCode = ApiErrorCode.Business)
        => new() { Success = false, Message = message, ErrorCode = errorCode };
}
```

- [ ] **Step 2: Build — backward-compat check**

```bash
dotnet build PBL3.sln --no-incremental -v quiet 2>&1 | tail -5
```

Expected: green. All existing `Fail("msg")` calls without `errorCode` continue to compile because `errorCode` is optional with a default.

---

### Task 3: Update `ProductService` Fail() calls

**Files:**
- Modify: `src/Application/Products/ProductService.cs`

Apply these exact string replacements (use `replace_all: true` for patterns that appear multiple times):

| Old string | New string | Note |
|---|---|---|
| `Fail("Không tìm thấy sản phẩm yêu cầu.")` | `Fail("Không tìm thấy sản phẩm yêu cầu.", ApiErrorCode.NotFound)` | 4 occurrences: GetByIdAsync, UpdateAsync, AddVariantAsync, UpdateImagesAsync, DeleteAsync |
| `Fail($"Mã SKU '{variant.SKU}' đã tồn tại trong hệ thống.")` | `Fail($"Mã SKU '{variant.SKU}' đã tồn tại trong hệ thống.", ApiErrorCode.Conflict)` | CreateAsync |
| `Fail("Các phiên bản trong cùng sản phẩm không được trùng mã SKU.")` | `Fail("Các phiên bản trong cùng sản phẩm không được trùng mã SKU.", ApiErrorCode.Conflict)` | CreateAsync |
| `Fail($"Mã SKU '{request.SKU}' đã tồn tại trong hệ thống.")` | `Fail($"Mã SKU '{request.SKU}' đã tồn tại trong hệ thống.", ApiErrorCode.Conflict)` | AddVariantAsync |
| `Fail("Nhà sản xuất không tồn tại.")` | leave unchanged | FK validation → Business default |
| `Fail("Danh mục không tồn tại.")` | leave unchanged | FK validation → Business default |

- [ ] **Step 1: Apply all replacements to `src/Application/Products/ProductService.cs`**

- [ ] **Step 2: Build check**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

Expected: `Build succeeded.`

---

### Task 4: Update `ManufacturerService` + `SupplierService` Fail() calls

**Files:**
- Modify: `src/Application/Manufacturers/ManufacturerService.cs`
- Modify: `src/Application/Suppliers/SupplierService.cs`

**ManufacturerService** (exact replacements):

| Old string | New string |
|---|---|
| `Fail("Không tìm thấy hãng sản xuất yêu cầu.")` | `Fail("Không tìm thấy hãng sản xuất yêu cầu.", ApiErrorCode.NotFound)` |
| `Fail($"Hãng sản xuất với tên \"{request.Name}\" đã tồn tại trong hệ thống.")` | `Fail($"Hãng sản xuất với tên \"{request.Name}\" đã tồn tại trong hệ thống.", ApiErrorCode.Conflict)` |
| `Fail("Không thể xóa hãng sản xuất này vì vẫn còn sản phẩm...")` | leave unchanged — Business |

Apply with `replace_all: true` for the not-found pattern (3 occurrences across GetById/Update/Delete).

**SupplierService** — read the file first, then apply:

- [ ] **Step 1: Read SupplierService to identify Fail() messages**

```bash
grep -n "\.Fail(" src/Application/Suppliers/SupplierService.cs
```

- [ ] **Step 2: Apply — supplier not-found messages → `ApiErrorCode.NotFound`, duplicate name → `ApiErrorCode.Conflict`, "không thể xóa/ràng buộc" → leave**

- [ ] **Step 3: Apply ManufacturerService replacements**

- [ ] **Step 4: Build check**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

---

### Task 5: Update `CategoryService` + `BannerService` Fail() calls

**Files:**
- Modify: `src/Application/Categories/CategoryService.cs`
- Modify: `src/Application/Banners/BannerService.cs`

**CategoryService** (exact replacements, use `replace_all: true`):

| Old string | New string |
|---|---|
| `Fail("Không tìm thấy danh mục yêu cầu.")` | `Fail("Không tìm thấy danh mục yêu cầu.", ApiErrorCode.NotFound)` |
| `Fail("Tên danh mục đã tồn tại trong cấp này.")` | `Fail("Tên danh mục đã tồn tại trong cấp này.", ApiErrorCode.Conflict)` |
| `Fail("Slug đã tồn tại. Vui lòng chọn slug khác.")` | `Fail("Slug đã tồn tại. Vui lòng chọn slug khác.", ApiErrorCode.Conflict)` |
| `Fail("Danh mục cha không tồn tại.")` | leave — Business |
| `Fail("Lỗi tham chiếu vòng:...")` | leave — Business |
| `Fail("Không thể xóa danh mục...")` | leave — Business |

**BannerService** (exact replacements):

| Old string | New string |
|---|---|
| `Fail("Không tìm thấy banner yêu cầu.")` | `Fail("Không tìm thấy banner yêu cầu.", ApiErrorCode.NotFound)` |
| `Fail(validation.Errors.First().ErrorMessage)` (both CreateAsync + UpdateAsync) | `Fail(validation.Errors.First().ErrorMessage, ApiErrorCode.Validation)` |

- [ ] **Step 1: Apply CategoryService replacements**
- [ ] **Step 2: Apply BannerService replacements**
- [ ] **Step 3: Build check**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

---

### Task 6: Update `ProductReviewService` + `StorefrontService` + `VoucherService` Fail() calls

**Files:**
- Modify: `src/Application/Reviews/ProductReviewService.cs`
- Modify: `src/Application/Storefront/StorefrontService.cs`
- Modify: `src/Application/Vouchers/VoucherService.cs`

For each file:

- [ ] **Step 1: Read Fail() calls**

```bash
grep -n "\.Fail(" src/Application/Reviews/ProductReviewService.cs
grep -n "\.Fail(" src/Application/Storefront/StorefrontService.cs
grep -n "\.Fail(" src/Application/Vouchers/VoucherService.cs
```

- [ ] **Step 2: Apply classification**

**ProductReviewService** — known patterns:
- Line ~42: product not found → `ApiErrorCode.NotFound`
- Line ~45: "Bạn đã đánh giá sản phẩm này rồi." → leave — Business
- Line ~82: review not found (lookup by ID for delete) → `ApiErrorCode.NotFound`
- Line ~85: "chỉ chủ sở hữu" / unauthorized delete → `ApiErrorCode.Forbidden`

**StorefrontService** — known patterns:
- Line ~168: product not found → `ApiErrorCode.NotFound`
- Line ~329, ~463: category not found → `ApiErrorCode.NotFound`

**VoucherService** — known patterns:
- Line ~67, ~147: voucher not found → `ApiErrorCode.NotFound`
- Line ~89: duplicate code → `ApiErrorCode.Conflict`
- Line ~151: quantity check → leave — Business

- [ ] **Step 3: Build check**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

---

### Task 7: Update `CustomerService` + `EmployeeService` + `AuthService` Fail() calls

**Files:**
- Modify: `src/Application/Customers/CustomerService.cs`
- Modify: `src/Application/Employees/EmployeeService.cs`
- Modify: `src/Application/Auth/AuthService.cs`

- [ ] **Step 1: Read Fail() calls**

```bash
grep -n "\.Fail(" src/Application/Customers/CustomerService.cs
grep -n "\.Fail(" src/Application/Employees/EmployeeService.cs
grep -n "\.Fail(" src/Application/Auth/AuthService.cs
```

- [ ] **Step 2: Apply classification**

**CustomerService** — known patterns (lines approximate):
- Lines ~68, ~175, ~208, ~238: customer not found → `ApiErrorCode.NotFound`
- Lines ~120, ~128: duplicate email / duplicate phone → `ApiErrorCode.Conflict`
- All others (status change failures, init failures) → leave — Business

**EmployeeService** — known patterns:
- Lines ~69, ~127, ~160, ~181: employee not found → `ApiErrorCode.NotFound`
- Lines ~79, ~84: duplicate email / duplicate phone → `ApiErrorCode.Conflict`
- All others → leave — Business

**AuthService** — known patterns:
- Lines ~353, ~361: duplicate email / duplicate phone during Register → `ApiErrorCode.Conflict`
- All login failures, token failures, password change failures → leave — Business (these are auth business errors, not entity lookups)

- [ ] **Step 3: Build check**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

---

### Task 8: Update `OrderService` + `CartService` + `PosService` + `ImportReceiptService` Fail() calls

**Files:**
- Modify: `src/Application/Orders/OrderService.cs`
- Modify: `src/Application/Cart/CartService.cs`
- Modify: `src/Application/Pos/PosService.cs`
- Modify: `src/Application/ImportReceipts/ImportReceiptService.cs`

- [ ] **Step 1: Read Fail() calls**

```bash
grep -n "\.Fail(" src/Application/Orders/OrderService.cs
grep -n "\.Fail(" src/Application/Cart/CartService.cs
grep -n "\.Fail(" src/Application/Pos/PosService.cs
grep -n "\.Fail(" src/Application/ImportReceipts/ImportReceiptService.cs
```

- [ ] **Step 2: Apply classification**

**OrderService** — known patterns:
- Line ~254: product variant not found → `ApiErrorCode.NotFound`
- Lines ~494, ~612, ~642, ~664, ~680, ~693, ~709: order not found (GET/update operations on an order by ID) → `ApiErrorCode.NotFound`
- All others (address validation, stock check, voucher check, cart empty, cancel reason) → leave — Business

**CartService** — known patterns:
- Lines ~60, ~66: product / variant not found when adding to cart → `ApiErrorCode.NotFound`
- Lines ~118, ~150: item not found in cart when removing → `ApiErrorCode.NotFound`
- Lines ~71, ~84: invalid quantity / stock limit → leave — Business

**PosService** — known patterns:
- Line ~112: customer not found → `ApiErrorCode.NotFound`
- Line ~237: serial not found / already sold → `ApiErrorCode.NotFound`
- All others (voucher validation, serial status check, empty cart) → leave — Business

**ImportReceiptService** — known patterns:
- Line ~59: supplier not found → `ApiErrorCode.NotFound`
- Line ~268: receipt not found → `ApiErrorCode.NotFound`
- All others (variant check, content validation) → leave — Business

- [ ] **Step 3: Build check**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

---

### Task 9: Update `InventoryCheckService` + `InventoryExportService` + `ProductSerialService` Fail() calls

**Files:**
- Modify: `src/Application/Inventory/InventoryCheckService.cs`
- Modify: `src/Application/Inventory/InventoryExportService.cs`
- Modify: `src/Application/ProductSerials/ProductSerialService.cs`

No changes needed to:
- `src/Application/Inventory/InventorySyncService.cs` — no Fail() calls
- `src/Application/BuildPc/BuildPcService.cs` — no Fail() calls
- `src/Application/Storage/S3StorageService.cs` — no Fail() calls
- `src/Application/Analytics/AnalyticsService.cs` — all errors are data/validation Business errors, no entity lookups

- [ ] **Step 1: Read Fail() calls**

```bash
grep -n "\.Fail(" src/Application/Inventory/InventoryCheckService.cs
grep -n "\.Fail(" src/Application/Inventory/InventoryExportService.cs
grep -n "\.Fail(" src/Application/ProductSerials/ProductSerialService.cs
```

- [ ] **Step 2: Apply classification**

**InventoryCheckService** — known patterns:
- Lines ~205, ~216, ~256, ~288: inventory check not found (by ID) → `ApiErrorCode.NotFound`
- Line ~324: variant not found → `ApiErrorCode.NotFound`
- All others (scope validation, status checks, draft-only checks) → leave — Business

**InventoryExportService** — known patterns:
- Line ~54: order not found → `ApiErrorCode.NotFound`
- All others (status check, no serials scanned, serial validation) → leave — Business

**ProductSerialService** — known patterns:
- Lines ~82, ~144: serial not found (by ID) → `ApiErrorCode.NotFound`
- All others (status transition error, update error) → leave — Business

- [ ] **Step 3: Build check**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

---

### Task 10: Update `ServiceTicketService` + `ServiceInvoiceService` Fail() calls

**Files:**
- Modify: `src/Application/ServiceTickets/ServiceTicketService.cs`
- Modify: `src/Application/ServiceInvoices/ServiceInvoiceService.cs`

- [ ] **Step 1: Read all Fail() calls**

```bash
grep -n "\.Fail(" src/Application/ServiceTickets/ServiceTicketService.cs
grep -n "\.Fail(" src/Application/ServiceInvoices/ServiceInvoiceService.cs
```

- [ ] **Step 2: Apply classification rule**

For each Fail() call, determine:
- Does it look up a ServiceTicket/ServiceInvoice/related entity by its own ID for a GET/PUT/DELETE endpoint? → `ApiErrorCode.NotFound`
- Does it report a duplicate? → `ApiErrorCode.Conflict`
- Does it check ownership? → `ApiErrorCode.Forbidden`
- Everything else (status transitions, business rules, validation) → leave as default Business

- [ ] **Step 3: Build check**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

---

### Task 11: Create `ApiResultExtensions`

**Files:**
- Create: `src/API/Extensions/ApiResultExtensions.cs`

- [ ] **Step 1: Create the file**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Extensions;

public static class ApiResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this ApiResult<T> result, ControllerBase controller)
    {
        if (result.Success) return controller.Ok(result);
        return result.ErrorCode switch
        {
            ApiErrorCode.NotFound  => controller.NotFound(result),
            ApiErrorCode.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, result),
            ApiErrorCode.Conflict  => controller.Conflict(result),
            _                      => controller.BadRequest(result),
        };
    }
}
```

- [ ] **Step 2: Build check**

```bash
dotnet build src/API/API.csproj --no-incremental -v quiet 2>&1 | tail -3
```

Expected: `Build succeeded.`

---

### Task 12: Refactor all 25 Controllers to use `ToActionResult`

**Files (25 total):**

Admin (19):
- Modify: `src/API/Controllers/Admin/AnalyticsController.cs`
- Modify: `src/API/Controllers/Admin/BannersController.cs`
- Modify: `src/API/Controllers/Admin/BuildPcController.cs`
- Modify: `src/API/Controllers/Admin/CategoriesController.cs`
- Modify: `src/API/Controllers/Admin/CustomersController.cs`
- Modify: `src/API/Controllers/Admin/EmployeesController.cs`
- Modify: `src/API/Controllers/Admin/ImageController.cs`
- Modify: `src/API/Controllers/Admin/ImportReceiptsController.cs`
- Modify: `src/API/Controllers/Admin/InventoryChecksController.cs`
- Modify: `src/API/Controllers/Admin/InventoryController.cs`
- Modify: `src/API/Controllers/Admin/ManufacturersController.cs`
- Modify: `src/API/Controllers/Admin/OrdersController.cs`
- Modify: `src/API/Controllers/Admin/PosController.cs`
- Modify: `src/API/Controllers/Admin/ProductSerialsController.cs`
- Modify: `src/API/Controllers/Admin/ProductsController.cs`
- Modify: `src/API/Controllers/Admin/ServiceInvoicesController.cs`
- Modify: `src/API/Controllers/Admin/ServiceTicketsController.cs`
- Modify: `src/API/Controllers/Admin/SuppliersController.cs`
- Modify: `src/API/Controllers/Admin/VouchersController.cs`

Storefront (6):
- Modify: `src/API/Controllers/Storefront/AuthController.cs`
- Modify: `src/API/Controllers/Storefront/CartController.cs`
- Modify: `src/API/Controllers/Storefront/ProfileController.cs`
- Modify: `src/API/Controllers/Storefront/ReviewsController.cs`
- Modify: `src/API/Controllers/Storefront/StorefrontController.cs`
- Modify: `src/API/Controllers/Storefront/UserAddressesController.cs`

**For each controller file:**

- [ ] **Add `using PBL3.API.Extensions;` at the top of the file** (after existing usings)

- [ ] **Replace every service-result dispatch with `result.ToActionResult(this)`**

Standard pattern (before → after):
```csharp
// BEFORE — any of these equivalent forms:
if (!result.Success)
{
    if (result.Message.Contains("Không tìm thấy"))
        return NotFound(result);
    return BadRequest(result);
}
return Ok(result);

// OR:
if (!result.Success) return BadRequest(result);
return Ok(result);

// OR:
if (!result.Success) return NotFound(result);
return Ok(result);

// AFTER — always:
return result.ToActionResult(this);
```

`CreatedAtAction` pattern (keep the 201 return, only replace failure path):
```csharp
// BEFORE:
if (!result.Success) return BadRequest(result);
return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);

// AFTER:
if (!result.Success) return result.ToActionResult(this);
return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
```

**Do NOT change** — manual FluentValidation `BadRequest` inside controller (ReviewsController etc.):
```csharp
// This is NOT a service result — leave as-is:
if (!validation.IsValid)
    return BadRequest(ApiResult<ReviewDto>.Fail(errors));
```

**Representative example — ManufacturersController.cs Update action before/after:**

```csharp
// BEFORE:
public async Task<IActionResult> Update(int id, [FromBody] UpdateManufacturerRequest request)
{
    var result = await _manufacturerService.UpdateAsync(id, request);
    if (!result.Success)
    {
        if (result.Message.Contains("Không tìm thấy"))
            return NotFound(result);
        return BadRequest(result);
    }
    return Ok(result);
}

// AFTER:
public async Task<IActionResult> Update(int id, [FromBody] UpdateManufacturerRequest request)
{
    var result = await _manufacturerService.UpdateAsync(id, request);
    return result.ToActionResult(this);
}
```

- [ ] **After all 25 files, build the full solution:**

```bash
dotnet build PBL3.sln --no-incremental -v quiet 2>&1 | grep -E "error TS|error CS|Build succeeded"
```

Expected: `Build succeeded.` No `error CS` lines.

- [ ] **Verify zero string-matching remains:**

```bash
grep -rn "Message\.Contains" src/API/Controllers/
```

Expected: empty output.

---

### Task 13: Commit Wave 1

- [ ] **Stage all Wave 1 changes**

```bash
git add src/Shared/DTOs/Common/ApiErrorCode.cs \
        src/Shared/DTOs/Common/ApiResult.cs \
        src/Application/ \
        src/API/Extensions/ApiResultExtensions.cs \
        src/API/Controllers/
```

- [ ] **Commit**

```bash
git commit -m "$(cat <<'EOF'
feat: add ApiErrorCode enum and replace controller string-matching with ToActionResult

- Add ApiErrorCode enum (None/NotFound/Forbidden/Validation/Business/Conflict)
- Add ErrorCode property to ApiResult<T> with backward-compatible Fail() overload
- Classify all Fail() calls across 24 services with semantic error codes
- Add ApiResultExtensions.ToActionResult<T> typed HTTP dispatch helper
- Refactor all 25 controllers to use result.ToActionResult(this)
- Eliminates all Message.Contains("Không tìm thấy") fragile string-matching

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Verify**

```bash
git show --stat HEAD
grep -rn "Message\.Contains" src/API/Controllers/
```

Expected: commit shows files across Shared + Application + API; grep returns empty.

---

## WAVE 2 — Syntax Refactor

---

### Task 14: Migrate all 24 Services to C# 12 Primary Constructors

**Files (24):**
```
src/Application/Analytics/AnalyticsService.cs
src/Application/Auth/AuthService.cs
src/Application/Banners/BannerService.cs
src/Application/BuildPc/BuildPcService.cs
src/Application/Cart/CartService.cs
src/Application/Categories/CategoryService.cs
src/Application/Customers/CustomerService.cs
src/Application/Employees/EmployeeService.cs
src/Application/ImportReceipts/ImportReceiptService.cs
src/Application/Inventory/InventoryCheckService.cs
src/Application/Inventory/InventoryExportService.cs
src/Application/Inventory/InventorySyncService.cs
src/Application/Manufacturers/ManufacturerService.cs
src/Application/Orders/OrderService.cs
src/Application/Pos/PosService.cs
src/Application/ProductSerials/ProductSerialService.cs
src/Application/Products/ProductService.cs
src/Application/Reviews/ProductReviewService.cs
src/Application/ServiceInvoices/ServiceInvoiceService.cs
src/Application/ServiceTickets/ServiceTicketService.cs
src/Application/Storage/S3StorageService.cs
src/Application/Storefront/StorefrontService.cs
src/Application/Suppliers/SupplierService.cs
src/Application/Vouchers/VoucherService.cs
```

**Transformation pattern** (apply identically to every file):

```csharp
// BEFORE — old-style constructor:
public class ManufacturerService : IManufacturerService
{
    private readonly IManufacturerRepository _manufacturerRepo;
    private readonly ILogger<ManufacturerService> _logger;

    public ManufacturerService(
        IManufacturerRepository manufacturerRepo,
        ILogger<ManufacturerService> logger)
    {
        _manufacturerRepo = manufacturerRepo;
        _logger = logger;
    }
    // ... rest of class
}

// AFTER — C# 12 primary constructor:
public class ManufacturerService(
    IManufacturerRepository manufacturerRepo,
    ILogger<ManufacturerService> logger) : IManufacturerService
{
    private readonly IManufacturerRepository _manufacturerRepo =
        manufacturerRepo ?? throw new ArgumentNullException(nameof(manufacturerRepo));
    private readonly ILogger<ManufacturerService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    // ... rest of class unchanged
}
```

**Rules:**
1. Move the interface (`: IServiceName`) from after the class name to after the closing `)` of the parameter list
2. Remove the entire constructor body (the `public ClassName(...) { ... }` block)
3. Keep all private field declarations; change their assignment from inside the constructor to a field initializer with `?? throw new ArgumentNullException(nameof(param))`
4. Keep all private field names unchanged (`_camelCase`)
5. Do NOT touch any method bodies, business logic, or anything else in the class
6. All dependency types get null-checked — `IRepository`, `ILogger<T>`, `IValidator<T>`, `HushStoreDbContext`, `UserManager<T>`, `IConfiguration`, `IUnitOfWork`, etc.

**OrderService example** (complex, 7 dependencies):

```csharp
// BEFORE:
public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderRepository _orderRepo;
    private readonly IVoucherRepository _voucherRepo;
    private readonly IProductRepository _productRepo;
    private readonly ICartRepository _cartRepo;
    private readonly IUserAddressRepository _userAddressRepo;
    private readonly IProductSerialRepository _productSerialRepo;

    public OrderService(
        IUnitOfWork unitOfWork,
        IOrderRepository orderRepo,
        IVoucherRepository voucherRepo,
        IProductRepository productRepo,
        ICartRepository cartRepo,
        IUserAddressRepository userAddressRepo,
        IProductSerialRepository productSerialRepo)
    {
        _unitOfWork = unitOfWork;
        _orderRepo = orderRepo;
        _voucherRepo = voucherRepo;
        _productRepo = productRepo;
        _cartRepo = cartRepo;
        _userAddressRepo = userAddressRepo;
        _productSerialRepo = productSerialRepo;
    }
    // ...
}

// AFTER:
public class OrderService(
    IUnitOfWork unitOfWork,
    IOrderRepository orderRepo,
    IVoucherRepository voucherRepo,
    IProductRepository productRepo,
    ICartRepository cartRepo,
    IUserAddressRepository userAddressRepo,
    IProductSerialRepository productSerialRepo) : IOrderService
{
    private readonly IUnitOfWork _unitOfWork =
        unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IOrderRepository _orderRepo =
        orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
    private readonly IVoucherRepository _voucherRepo =
        voucherRepo ?? throw new ArgumentNullException(nameof(voucherRepo));
    private readonly IProductRepository _productRepo =
        productRepo ?? throw new ArgumentNullException(nameof(productRepo));
    private readonly ICartRepository _cartRepo =
        cartRepo ?? throw new ArgumentNullException(nameof(cartRepo));
    private readonly IUserAddressRepository _userAddressRepo =
        userAddressRepo ?? throw new ArgumentNullException(nameof(userAddressRepo));
    private readonly IProductSerialRepository _productSerialRepo =
        productSerialRepo ?? throw new ArgumentNullException(nameof(productSerialRepo));
    // ...
}
```

- [ ] **Process each of the 24 files in the list above: read → apply transformation → move to next**

- [ ] **Build check after all 24 files**

```bash
dotnet build src/Application/Application.csproj --no-incremental -v quiet 2>&1 | tail -3
```

Expected: `Build succeeded.`

---

### Task 15: Migrate all 20 Repositories to C# 12 Primary Constructors

**Files (20):**
```
src/Infrastructure/Repositories/Customers/CustomerRepository.cs
src/Infrastructure/Repositories/Customers/EmployeeRepository.cs
src/Infrastructure/Repositories/Customers/UserAddressRepository.cs
src/Infrastructure/Repositories/Inventory/InventoryCheckRepository.cs
src/Infrastructure/Repositories/Inventory/ImportReceiptRepository.cs
src/Infrastructure/Repositories/Inventory/ProductSerialRepository.cs
src/Infrastructure/Repositories/Misc/BannerRepository.cs
src/Infrastructure/Repositories/Misc/ProductReviewRepository.cs
src/Infrastructure/Repositories/Products/CategoryRepository.cs
src/Infrastructure/Repositories/Products/ManufacturerRepository.cs
src/Infrastructure/Repositories/Products/ProductRepository.cs
src/Infrastructure/Repositories/Sales/CartRepository.cs
src/Infrastructure/Repositories/Sales/OrderRepository.cs
src/Infrastructure/Repositories/Sales/VoucherRepository.cs
src/Infrastructure/Repositories/Sales/WarrantyRepository.cs
src/Infrastructure/Repositories/ServiceTickets/QuotationRepository.cs
src/Infrastructure/Repositories/ServiceTickets/RmaShipmentRepository.cs
src/Infrastructure/Repositories/ServiceTickets/SerialRepairLogRepository.cs
src/Infrastructure/Repositories/ServiceTickets/ServiceInvoiceRepository.cs
src/Infrastructure/Repositories/ServiceTickets/ServiceTicketRepository.cs
```

**Pattern** (all repositories inject only `HushStoreDbContext`):

```csharp
// BEFORE:
public class ManufacturerRepository : IManufacturerRepository
{
    private readonly HushStoreDbContext _context;

    public ManufacturerRepository(HushStoreDbContext context)
    {
        _context = context;
    }
    // ...
}

// AFTER:
public class ManufacturerRepository(HushStoreDbContext context) : IManufacturerRepository
{
    private readonly HushStoreDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));
    // ...
}
```

> If a repository has additional dependencies beyond `HushStoreDbContext` (check each file), apply the same `?? throw` pattern to each parameter.

- [ ] **Process each of the 20 files: read → apply transformation → move to next**

- [ ] **Build check after all 20 files**

```bash
dotnet build src/Infrastructure/Infrastructure.csproj --no-incremental -v quiet 2>&1 | tail -3
```

Expected: `Build succeeded.`

---

### Task 16: Full build check + Commit Wave 2

- [ ] **Full solution build**

```bash
dotnet build PBL3.sln --no-incremental -v quiet 2>&1 | grep -E "error CS|Build succeeded"
```

Expected: `Build succeeded.` No `error CS` lines.

- [ ] **Commit**

```bash
git add src/Application/ src/Infrastructure/Repositories/

git commit -m "$(cat <<'EOF'
refactor: migrate all services and repositories to C# 12 primary constructors

- 24 Application services: primary constructor syntax + ?? throw null-check on all fields
- 20 Infrastructure repositories: same pattern (single HushStoreDbContext dependency)
- Zero behavior changes — pure syntax modernization per Enterprise upgrade roadmap

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Verify**

```bash
git show --stat HEAD
```

Expected: ~44 changed files.

---

## Definition of Done

- [ ] `dotnet build PBL3.sln` — green, 0 errors
- [ ] `grep -rn "Message\.Contains" src/API/Controllers/` — empty (zero results)
- [ ] `grep -rn "Message\.Contains\|result\.Success" src/API/Controllers/` — empty output (all string-matching and manual if/else replaced)
- [ ] All 25 Controllers have `using PBL3.API.Extensions;` and use `result.ToActionResult(this)` for all service results
- [ ] `git log --oneline -3` — shows 2 new commits (feat Wave 1 + refactor Wave 2)
