# Design Spec: Đồng bộ hóa DI & Hiện đại hóa API Response

**Ngày:** 2026-05-29  
**Dự án:** HushStore — PBL3  
**Phạm vi:** Bước 1 trong lộ trình Enterprise Upgrade (xem `docs/enterprise_design_pattern_review_report.md`)

---

## Mục tiêu

1. Xóa bỏ hoàn toàn cơ chế `result.Message.Contains("Không tìm thấy")` fragile ở tất cả Controllers bằng cách thêm `ErrorCode` enum vào `ApiResult<T>`.
2. Phân loại ngữ nghĩa cho toàn bộ `Fail()` calls trong 24 Services.
3. Hiện đại hóa cú pháp constructor của 44 lớp (24 Services + 20 Repositories) sang Primary Constructor C# 12 kèm null-check.

---

## Chiến lược: 2 Wave

### Wave 1 — Behavior Fix (ApiResult + Services + Controllers)
Sửa lỗi thiết kế thực sự. Commit độc lập. Build phải xanh.

### Wave 2 — Syntax Refactor (Primary Constructors)
Pure cú pháp, không thay đổi hành vi. Commit độc lập. Build phải xanh.

---

## Wave 1: ApiResult<T> + ErrorCode

### 1.1 File mới: `src/Shared/DTOs/Common/ApiErrorCode.cs`

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

### 1.2 Sửa: `src/Shared/DTOs/Common/ApiResult.cs`

Thêm property `ErrorCode` và overload `Fail()` có tham số errorCode:

```csharp
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

**Backward-compatibility:** Mọi `Fail()` call hiện tại không truyền `errorCode` sẽ nhận default `Business` — build không bị vỡ.

---

## Wave 1: Service Layer — phân loại `Fail()` calls

Toàn bộ 24 Services phải cập nhật `Fail()` calls theo bảng phân loại:

| Loại lỗi | ErrorCode | Ví dụ message pattern |
|---|---|---|
| Entity không tồn tại | `ApiErrorCode.NotFound` | `"Không tìm thấy X."`, `"X không tồn tại."` |
| Kiểm tra ownership | `ApiErrorCode.Forbidden` | `"Bạn không có quyền..."` |
| Trùng lặp dữ liệu | `ApiErrorCode.Conflict` | `"X đã tồn tại."`, `"SKU đã được sử dụng."` |
| Vi phạm nghiệp vụ | `ApiErrorCode.Business` | `"Đơn hàng không thể hủy."` — **default, không cần sửa** |

**Danh sách Services cần cập nhật (toàn bộ):**
- `AnalyticsService`, `AuthService`, `BannerService`, `BuildPcService`
- `CartService`, `CategoryService`, `CustomerService`, `EmployeeService`
- `ImportReceiptService`, `InventoryCheckService`, `InventoryExportService`, `InventorySyncService`
- `ManufacturerService`, `OrderService`, `PosService`, `ProductSerialService`
- `ProductService`, `ProductReviewService`, `ServiceInvoiceService`, `ServiceTicketService`
- `S3StorageService`, `StorefrontService`, `SupplierService`, `VoucherService`

---

## Wave 1: Controller Layer — `ToActionResult` extension

### File mới: `src/API/Extensions/ApiResultExtensions.cs`

```csharp
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

### Áp dụng cho tất cả 25 Controllers

**Trước:**
```csharp
if (!result.Success)
{
    if (result.Message.Contains("Không tìm thấy"))
        return NotFound(result);
    return BadRequest(result);
}
return Ok(result);
```

**Sau:**
```csharp
return result.ToActionResult(this);
```

**Danh sách 25 Controllers:**

Admin (19):
- `AnalyticsController`, `BannersController`, `BuildPcController`, `CategoriesController`
- `CustomersController`, `EmployeesController`, `ImageController`, `ImportReceiptsController`
- `InventoryChecksController`, `InventoryController`, `ManufacturersController`, `OrdersController`
- `PosController`, `ProductSerialsController`, `ProductsController`, `ServiceInvoicesController`
- `ServiceTicketsController`, `SuppliersController`, `VouchersController`

Storefront (6):
- `AuthController`, `CartController`, `ProfileController`, `ReviewsController`
- `StorefrontController`, `UserAddressesController`

---

## Wave 2: Primary Constructor Migration

### Pattern chuẩn — Service (nhiều dependencies)

```csharp
// Trước:
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepo;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository productRepo, ILogger<ProductService> logger)
    {
        _productRepo = productRepo;
        _logger = logger;
    }
}

// Sau:
public class ProductService(
    IProductRepository productRepo,
    ILogger<ProductService> logger) : IProductService
{
    private readonly IProductRepository _productRepo =
        productRepo ?? throw new ArgumentNullException(nameof(productRepo));
    private readonly ILogger<ProductService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
}
```

### Pattern chuẩn — Repository (1 dependency)

```csharp
// Sau:
public class ProductRepository(HushStoreDbContext context) : IProductRepository
{
    private readonly HushStoreDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));
}
```

### Quy tắc áp dụng
- Xóa toàn bộ constructor body cũ (fields assignment)
- Giữ nguyên tên private fields (`_camelCase`) — không đổi tên
- Mọi parameter đều có `?? throw new ArgumentNullException(nameof(param))`
- Không thay đổi bất kỳ logic nghiệp vụ nào

**Danh sách 44 file:**

Services (24): toàn bộ files trong `src/Application/`  
Repositories (20): toàn bộ files trong `src/Infrastructure/Repositories/`

---

## Thứ tự thực thi

```
Wave 1:
  1. Tạo ApiErrorCode.cs (Shared)
  2. Sửa ApiResult.cs (Shared)
  3. Cập nhật Fail() trong 24 Services (Application)
  4. Tạo ApiResultExtensions.cs (API)
  5. Refactor 25 Controllers dùng ToActionResult (API)
  6. dotnet build → phải xanh
  7. Commit: "feat: add ApiErrorCode enum and replace string-matching in controllers"

Wave 2:
  8. Migrate 24 Services sang Primary Constructor
  9. Migrate 20 Repositories sang Primary Constructor
  10. dotnet build → phải xanh
  11. Commit: "refactor: migrate all services and repositories to C# 12 primary constructors"
```

---

## Định nghĩa hoàn thành

- [ ] `dotnet build PBL3.sln` không có warning hay error
- [ ] Không còn `Message.Contains(` trong bất kỳ Controller nào
- [ ] Không còn constructor cũ (dạng `public Foo(X x) { _x = x; }`) trong bất kỳ Service hay Repository nào
- [ ] Tất cả `Fail()` calls có `errorCode` phù hợp (không phải chỉ default Business cho NotFound)
- [ ] Tất cả 25 Controllers dùng `result.ToActionResult(this)`
