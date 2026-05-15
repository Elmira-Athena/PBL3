# Controllers & Validators Reorganization

**Date:** 2026-05-15
**Scope:** `src/API/Controllers/` and `src/Shared/Validators/`

## Goal

Bring Controllers and Validators into consistent folder structure:
- Controllers grouped by **access area** (Admin vs Storefront)
- Validators grouped by **domain** matching existing `DTOs/` structure

## Controllers Reorganization

### Strategy: Area-based (Admin / Storefront)

All 21 flat controllers move into two subfolders. The existing `Storefront/` subfolder stays; a new `Admin/` subfolder is created.

### Final Structure

```
src/API/Controllers/
  Admin/
    AnalyticsController.cs
    AuthController.cs
    BannersController.cs
    BuildPcController.cs
    CategoriesController.cs
    CustomersController.cs
    EmployeesController.cs
    ImageController.cs
    ImportReceiptsController.cs
    InventoryController.cs
    ManufacturersController.cs
    OrdersController.cs
    PosController.cs
    ProductsController.cs
    ProductSerialsController.cs
    ReviewsController.cs
    ServiceInvoicesController.cs
    ServiceTicketsController.cs
    SuppliersController.cs
    VouchersController.cs
  Storefront/
    CartController.cs          ← moved from flat
    ProfileController.cs       ← already here
    StorefrontController.cs    ← already here
    UserAddressesController.cs ← already here
```

### Namespace Changes

| Old namespace | New namespace |
|---|---|
| `PBL3.API.Controllers` | `PBL3.API.Controllers.Admin` |
| `PBL3.API.Controllers` (Cart) | `PBL3.API.Controllers.Storefront` |

**Routing is unaffected** — ASP.NET Core discovers controllers via assembly scan; all `[Route("api/...")]` attributes remain unchanged.

## Validators Reorganization

### Strategy: Domain-based (matching DTOs/)

All flat validator files move into domain subfolders. `ProductValidators.cs` is also split.

### Final Structure

```
src/Shared/Validators/
  Banners/
    BannerValidators.cs
  Categories/
    CategoryValidators.cs
  Customers/
    CustomerValidators.cs
  Employees/
    EmployeeValidators.cs
  Inventory/
    ImportReceiptValidators.cs
  Products/
    ProductValidators.cs     ← Create/Update Product validators only
    VariantValidators.cs     ← CreateVariant + SaveVariant validators (split from ProductValidators.cs)
  Reviews/
    ReviewValidators.cs
  Sale/
    CreateOrderRequestValidator.cs  ← already here
    ExportOrderValidators.cs        ← moved from flat
  ServiceTickets/
    ServiceTicketValidators.cs
  Suppliers/
    SupplierValidators.cs
  Vouchers/
    VoucherValidators.cs
```

### Namespace Changes

| File | Old namespace | New namespace |
|---|---|---|
| BannerValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Banners` |
| CategoryValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Categories` |
| CustomerValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Customers` |
| EmployeeValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Employees` |
| ImportReceiptValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Inventory` |
| ProductValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Products` |
| VariantValidators.cs | _(new file)_ | `PBL3.Shared.Validators.Products` |
| ReviewValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Reviews` |
| ExportOrderValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Sale` |
| ServiceTicketValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.ServiceTickets` |
| SupplierValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Suppliers` |
| VoucherValidators.cs | `PBL3.Shared.Validators` | `PBL3.Shared.Validators.Vouchers` |

### Side Effects

**`src/API/Program.cs`** — replace `using PBL3.Shared.Validators;` with:
```csharp
using PBL3.Shared.Validators.Banners;
using PBL3.Shared.Validators.Reviews;
```
(Only these two namespaces are referenced by name in Program.cs for explicit `IValidator<T>` registrations.)

**`src/Client/Program.cs`** — no changes needed. Uses `AddValidatorsFromAssemblyContaining<>` which scans the whole assembly regardless of namespace.

## ProductValidators.cs Split Detail

### `Products/ProductValidators.cs`
- `CreateProductRequestValidator`
- `UpdateProductRequestValidator`

### `Products/VariantValidators.cs`
- `CreateVariantRequestValidator`
- `SaveVariantRequestValidator`

Both files use namespace `PBL3.Shared.Validators.Products`.

## Build Verification

After all moves, run:
```bash
dotnet build PBL3.sln
```
Zero errors expected — no business logic changes, only file location and namespace updates.
