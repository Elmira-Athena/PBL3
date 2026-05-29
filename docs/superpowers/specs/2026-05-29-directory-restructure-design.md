# Design: Enterprise Directory Restructure

**Date:** 2026-05-29  
**Project:** HushStore (PBL3)  
**Based on:** `docs/enterprise_directory_structure_review.md`

---

## Goal

Bring the solution structure to enterprise .NET Clean Architecture standards by resolving five categories of anti-patterns identified in the review:

1. God files (multiple classes per file) in `Core/Entities` and `Core/Interfaces`
2. Flat 21-file repository directory in `Infrastructure/Repositories`
3. Root-level clutter (`infra/`, `Infrastructure/`, scripts scattered)
4. Missing `tests/` directory
5. Misleading project name `Service` (should be `Application`)

---

## Approach: 3 Phases (Low-risk → High-risk)

Each phase produces a green `dotnet build` before proceeding to the next.

---

## Phase 1 — Structural Cleanup (Zero compilation risk)

### 1.1 Create `devops/` and consolidate root clutter

Move the following into a single `devops/` tree:

| Source (current) | Destination |
|---|---|
| `Infrastructure/db/` | `devops/db/` |
| `Infrastructure/scripts/` | `devops/scripts/` |
| `infra/setup.sh`, `infra/start.sh`, `infra/stop.sh`, `infra/teardown.sh` | `devops/scripts/` |
| `infra/config.example.json`, `infra/resources.env` | `devops/` |
| `local-up.sh`, `local-down.sh`, `local-import-azure.sh`, `deploy.sh` | `devops/scripts/` |
| `docker-compose.yml`, `docker-compose.local.yml` | `devops/docker/` |
| `nginx/` | `devops/nginx/` |

> **Do NOT move `local-dev/`** — it is the published Blazor WASM output referenced by the nginx static file config. Moving it would break the running deployment.

Delete the now-empty root directories: `Infrastructure/`, `infra/`.

### 1.2 Delete empty folders in `src/`

- `src/Infrastructure/Services/` — empty, never used
- `src/Service/Mappings/` — empty, abandoned AutoMapper setup

### 1.3 Create `tests/` skeleton

Create two xUnit projects at the solution root level:

```
tests/
├── PBL3.UnitTests/
│   ├── PBL3.UnitTests.csproj   (xUnit + FluentAssertions, no src/ dependencies yet)
│   └── PlaceholderTest.cs      (one passing test so the runner does not error)
└── PBL3.IntegrationTests/
    ├── PBL3.IntegrationTests.csproj
    └── PlaceholderTest.cs
```

Both projects are added to `PBL3.sln`. No references to `src/` projects yet.

**Verify:** `dotnet build PBL3.sln` green.

---

## Phase 2 — Code Splitting (Medium risk, no namespace change in API consumers)

### 2.1 Split `Core/Entities` god files

Each class gets its own file, organized by domain subfolder. Namespace updated to match subfolder (e.g., `PBL3.Core.Entities.Products`). Because namespace changes, every file in `Infrastructure`, `Service`, and `API` that references these entities must add the new `using` (e.g., `using PBL3.Core.Entities.Products;`). The safest approach is to add a `global using` per domain in the `Core` project's root, so downstream projects do not need changes.

#### `ProductEntities.cs` → `Core/Entities/Products/`

| Class | File |
|---|---|
| `Manufacturer` | `Products/Manufacturer.cs` |
| `Category` | `Products/Category.cs` |
| `Product` | `Products/Product.cs` |
| `ProductVariant` | `Products/ProductVariant.cs` |
| `ProductImage` | `Products/ProductImage.cs` |

#### `SaleEntities.cs` → `Core/Entities/Sales/`

| Class | File |
|---|---|
| `Voucher` | `Sales/Voucher.cs` |
| `VoucherCategory` | `Sales/VoucherCategory.cs` |
| `VoucherUsage` | `Sales/VoucherUsage.cs` |
| `Order` | `Sales/Order.cs` |
| `OrderDetail` | `Sales/OrderDetail.cs` |
| `OrderSerial` | `Sales/OrderSerial.cs` |
| `Cart` | `Sales/Cart.cs` |
| `Warranty` | `Sales/Warranty.cs` |
| `UserAddress` | `Sales/UserAddress.cs` |
| `ProductReview` | `Sales/ProductReview.cs` |

#### `AuthEntities.cs` → `Core/Entities/Auths/`

| Class | File |
|---|---|
| `AppUser` | `Auths/AppUser.cs` |
| `UserProfile` | `Auths/UserProfile.cs` |
| `AppRole` | `Auths/AppRole.cs` |
| `RefreshToken` | `Auths/RefreshToken.cs` |

#### `InventoryEntities.cs` → `Core/Entities/Inventory/`

| Class | File |
|---|---|
| `Supplier` | `Inventory/Supplier.cs` |
| `ImportReceipt` | `Inventory/ImportReceipt.cs` |
| `ImportReceiptDetail` | `Inventory/ImportReceiptDetail.cs` |
| `ProductSerial` | `Inventory/ProductSerial.cs` |
| `InventoryCheck` | `Inventory/InventoryCheck.cs` |
| `InventoryCheckDetail` | `Inventory/InventoryCheckDetail.cs` |
| `InventoryCheckDetailSerial` | `Inventory/InventoryCheckDetailSerial.cs` |
| `InventoryAdjustmentLog` | `Inventory/InventoryAdjustmentLog.cs` |

#### `ServiceEntities.cs` → `Core/Entities/ServiceTickets/`

| Class | File |
|---|---|
| `ServiceTicket` | `ServiceTickets/ServiceTicket.cs` |
| `ServiceTicketStatusHistory` | `ServiceTickets/ServiceTicketStatusHistory.cs` |
| `Quotation` | `ServiceTickets/Quotation.cs` |
| `QuotationItem` | `ServiceTickets/QuotationItem.cs` |
| `RmaShipment` | `ServiceTickets/RmaShipment.cs` |
| `ServiceInvoice` | `ServiceTickets/ServiceInvoice.cs` |
| `ServiceInvoiceItem` | `ServiceTickets/ServiceInvoiceItem.cs` |
| `SerialRepairLog` | `ServiceTickets/SerialRepairLog.cs` |

#### `Banner.cs` — no change (already one class, one file)

### 2.2 Split `Core/Interfaces` god files

#### `IRepositories.cs` (642 lines, 16 interfaces) → `Core/Interfaces/Repositories/`

| Interface | File |
|---|---|
| `IManufacturerRepository` | `Repositories/Products/IManufacturerRepository.cs` |
| `ICategoryRepository` | `Repositories/Products/ICategoryRepository.cs` |
| `IProductRepository` | `Repositories/Products/IProductRepository.cs` |
| `ISupplierRepository` | `Repositories/Inventory/ISupplierRepository.cs` |
| `IImportReceiptRepository` | `Repositories/Inventory/IImportReceiptRepository.cs` |
| `IProductSerialRepository` | `Repositories/Inventory/IProductSerialRepository.cs` |
| `IInventoryCheckRepository` | `Repositories/Inventory/IInventoryCheckRepository.cs` |
| `IVoucherRepository` | `Repositories/Sales/IVoucherRepository.cs` |
| `IOrderRepository` | `Repositories/Sales/IOrderRepository.cs` |
| `ICartRepository` | `Repositories/Sales/ICartRepository.cs` |
| `IWarrantyRepository` | `Repositories/Sales/IWarrantyRepository.cs` |
| `ICustomerRepository` | `Repositories/Customers/ICustomerRepository.cs` |
| `IEmployeeRepository` | `Repositories/Customers/IEmployeeRepository.cs` |
| `IUserAddressRepository` | `Repositories/Customers/IUserAddressRepository.cs` |
| `IBannerRepository` | `Repositories/Misc/IBannerRepository.cs` |
| `IProductReviewRepository` | `Repositories/Misc/IProductReviewRepository.cs` |

#### `IServiceRepositories.cs` (5 interfaces) → `Core/Interfaces/Repositories/ServiceTickets/`

| Interface | File |
|---|---|
| `IServiceTicketRepository` | `Repositories/ServiceTickets/IServiceTicketRepository.cs` |
| `IQuotationRepository` | `Repositories/ServiceTickets/IQuotationRepository.cs` |
| `IRmaShipmentRepository` | `Repositories/ServiceTickets/IRmaShipmentRepository.cs` |
| `IServiceInvoiceRepository` | `Repositories/ServiceTickets/IServiceInvoiceRepository.cs` |
| `ISerialRepairLogRepository` | `Repositories/ServiceTickets/ISerialRepairLogRepository.cs` |

#### `IInventorySyncService.cs`, `IUnitOfWork.cs` — no change (already single-interface files)

### 2.3 Group `Infrastructure/Repositories` flat files into domain subfolders

| Current (flat) | New location |
|---|---|
| `ProductRepository.cs` | `Repositories/Products/` |
| `CategoryRepository.cs` | `Repositories/Products/` |
| `ManufacturerRepository.cs` | `Repositories/Products/` |
| `ImportReceiptRepository.cs` | `Repositories/Inventory/` |
| `InventoryCheckRepository.cs` | `Repositories/Inventory/` |
| `ProductSerialRepository.cs` | `Repositories/Inventory/` |
| `SupplierRepository.cs` | `Repositories/Inventory/` |
| `OrderRepository.cs` | `Repositories/Sales/` |
| `CartRepository.cs` | `Repositories/Sales/` |
| `VoucherRepository.cs` | `Repositories/Sales/` |
| `WarrantyRepository.cs` | `Repositories/Sales/` |
| `CustomerRepository.cs` | `Repositories/Customers/` |
| `EmployeeRepository.cs` | `Repositories/Customers/` |
| `UserAddressRepository.cs` | `Repositories/Customers/` |
| `ServiceTicketRepository.cs` | `Repositories/ServiceTickets/` |
| `QuotationRepository.cs` | `Repositories/ServiceTickets/` |
| `RmaShipmentRepository.cs` | `Repositories/ServiceTickets/` |
| `ServiceInvoiceRepository.cs` | `Repositories/ServiceTickets/` |
| `SerialRepairLogRepository.cs` | `Repositories/ServiceTickets/` |
| `BannerRepository.cs` | `Repositories/Misc/` |
| `ProductReviewRepository.cs` | `Repositories/Misc/` |

**Verify:** `dotnet build PBL3.sln` green.

---

## Phase 3 — Rename `Service` → `Application` (High risk)

### 3.1 Physical rename

```
src/Service/            →  src/Application/
src/Service/Service.csproj  →  src/Application/Application.csproj
```

### 3.2 Update `PBL3.sln`

Two lines change:
- Project display name: `"PBL3.Service"` → `"PBL3.Application"`
- Relative path: `"src\Service\Service.csproj"` → `"src\Application\Application.csproj"`

### 3.3 Update `src/API/API.csproj`

```xml
<!-- Before -->
<ProjectReference Include="..\Service\Service.csproj" />

<!-- After -->
<ProjectReference Include="..\Application\Application.csproj" />
```

### 3.4 Global namespace replacement

Across the entire solution, two text replacements:

| Find | Replace |
|---|---|
| `namespace PBL3.Service` | `namespace PBL3.Application` |
| `using PBL3.Service` | `using PBL3.Application` |

Affected files: all `*.cs` in `src/Application/` (~20-25 files) and `src/API/Program.cs` + any controller that imports a service type directly.

**Verify:** `dotnet build PBL3.sln` green, then `dotnet run --project src/API/API.csproj` starts without errors.

---

## Out of Scope

- `Shared/DTOs/` — already well-organized into per-module subfolders. No change needed.
- `Service/` feature folders — already organized by feature (Products/, Orders/, etc.). No change needed.
- Writing actual unit/integration tests — skeleton only in Phase 1.
- Adding SeedWork (`Entity.cs`, `ValueObject.cs`, `IAggregateRoot.cs`) — not in scope for this refactor.
- API versioning (`v1/Admin/`, `v1/Storefront/`) — out of scope.
