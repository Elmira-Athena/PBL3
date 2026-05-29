# Enterprise Directory Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the HushStore solution from god-file anti-patterns to clean per-class file organization, consolidate devops assets, add a test skeleton, and rename the `Service` project to `Application`.

**Architecture:** Three sequential phases — Phase 1 (pure filesystem, zero compilation risk), Phase 2 (split entity/interface god files and group repo files; namespace unchanged), Phase 3 (rename `src/Service` → `src/Application` including `.sln`, `.csproj`, and all namespaces). Each phase ends with a green `dotnet build`.

**Tech Stack:** .NET 10 / C# 13, xUnit 2.x, FluentAssertions, git

---

## Phase 1 — Structural Cleanup

### Task 1: Create `devops/` tree and move root-level assets

**Files:**
- Create dir: `devops/db/`, `devops/scripts/`, `devops/docker/`, `devops/nginx/`
- Delete: `Infrastructure/` (root, after move), `infra/` (after move)

- [ ] **Step 1.1: Create devops directory structure**

```bash
mkdir -p devops/db devops/scripts devops/docker devops/nginx
```

- [ ] **Step 1.2: Move Infrastructure/db and Infrastructure/scripts**

```bash
mv Infrastructure/db/* devops/db/
mv Infrastructure/scripts/* devops/scripts/
```

- [ ] **Step 1.3: Move infra shell scripts and config**

```bash
mv infra/setup.sh infra/start.sh infra/stop.sh infra/teardown.sh devops/scripts/
mv infra/config.example.json infra/resources.env devops/
```

- [ ] **Step 1.4: Move root-level shell scripts**

```bash
mv local-up.sh local-down.sh local-import-azure.sh deploy.sh devops/scripts/
```

- [ ] **Step 1.5: Move docker-compose files**

```bash
mv docker-compose.yml docker-compose.local.yml devops/docker/
```

- [ ] **Step 1.6: Move nginx config**

```bash
mv nginx/* devops/nginx/
rmdir nginx
```

- [ ] **Step 1.7: Remove now-empty root directories**

```bash
rmdir Infrastructure/db Infrastructure/scripts Infrastructure
rmdir infra
```

> **Do NOT move `local-dev/`** — it is the published Blazor WASM output. The nginx config serves static files from it. Moving it would break the running deployment.

- [ ] **Step 1.8: Commit**

```bash
git add -A
git commit -m "chore: consolidate devops assets into devops/ directory"
```

---

### Task 2: Delete empty folders in `src/`

**Files:**
- Delete: `src/Infrastructure/Services/` (empty)
- Delete: `src/Service/Mappings/` (empty)

- [ ] **Step 2.1: Verify the folders are empty**

```bash
ls src/Infrastructure/Services/
ls src/Service/Mappings/
```

Expected: both commands return no output (empty).

- [ ] **Step 2.2: Delete them**

```bash
rmdir src/Infrastructure/Services/
rmdir src/Service/Mappings/
```

- [ ] **Step 2.3: Commit**

```bash
git add -A
git commit -m "chore: remove empty src/Infrastructure/Services and src/Service/Mappings folders"
```

---

### Task 3: Create `tests/` skeleton and add to solution

**Files:**
- Create: `tests/PBL3.UnitTests/PBL3.UnitTests.csproj`
- Create: `tests/PBL3.UnitTests/PlaceholderTest.cs`
- Create: `tests/PBL3.IntegrationTests/PBL3.IntegrationTests.csproj`
- Create: `tests/PBL3.IntegrationTests/PlaceholderTest.cs`
- Modify: `PBL3.sln`

- [ ] **Step 3.1: Create the unit test project**

```bash
mkdir -p tests/PBL3.UnitTests
```

Create `tests/PBL3.UnitTests/PBL3.UnitTests.csproj` with this exact content:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3.2: Create placeholder test for unit tests**

Create `tests/PBL3.UnitTests/PlaceholderTest.cs`:

```csharp
namespace PBL3.UnitTests;

public class PlaceholderTest
{
    [Fact]
    public void Placeholder_AlwaysPasses() => Assert.True(true);
}
```

- [ ] **Step 3.3: Create the integration test project**

```bash
mkdir -p tests/PBL3.IntegrationTests
```

Create `tests/PBL3.IntegrationTests/PBL3.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3.4: Create placeholder test for integration tests**

Create `tests/PBL3.IntegrationTests/PlaceholderTest.cs`:

```csharp
namespace PBL3.IntegrationTests;

public class PlaceholderTest
{
    [Fact]
    public void Placeholder_AlwaysPasses() => Assert.True(true);
}
```

- [ ] **Step 3.5: Add both projects to the solution**

```bash
dotnet sln PBL3.sln add tests/PBL3.UnitTests/PBL3.UnitTests.csproj
dotnet sln PBL3.sln add tests/PBL3.IntegrationTests/PBL3.IntegrationTests.csproj
```

- [ ] **Step 3.6: Verify build is green**

```bash
dotnet build PBL3.sln
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3.7: Commit**

```bash
git add -A
git commit -m "chore: add tests/ skeleton with PBL3.UnitTests and PBL3.IntegrationTests (xUnit)"
```

---

## Phase 2 — Code Splitting

> **Namespace strategy:** All split files keep their original namespace (`PBL3.Core.Entities` for entities, `PBL3.Core.Interfaces` for interfaces). Only the *folder* structure changes. This means zero downstream `using` changes needed anywhere in Infrastructure, Service, or API.

### Task 4: Split `Core/Entities/ProductEntities.cs`

**Files:**
- Create: `src/Core/Entities/Products/Manufacturer.cs`
- Create: `src/Core/Entities/Products/Category.cs`
- Create: `src/Core/Entities/Products/Product.cs`
- Create: `src/Core/Entities/Products/ProductVariant.cs`
- Create: `src/Core/Entities/Products/ProductImage.cs`
- Delete: `src/Core/Entities/ProductEntities.cs`

- [ ] **Step 4.1: Create `Products/` subfolder**

```bash
mkdir -p src/Core/Entities/Products
```

- [ ] **Step 4.2: Create `Manufacturer.cs`**

Create `src/Core/Entities/Products/Manufacturer.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("Manufacturers")]
    public class Manufacturer
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? LogoUrl { get; set; }
        [MaxLength(255)]
        public string? Website { get; set; }
        [MaxLength(100)]
        public string? SupportEmail { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
```

- [ ] **Step 4.3: Create `Category.cs`**

Create `src/Core/Entities/Products/Category.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("Categories")]
    public class Category
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        [Required]
        [MaxLength(150)]
        public string Slug { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public int Level { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        [ForeignKey("ParentId")]
        public virtual Category? Parent { get; set; }
        public virtual ICollection<Category> Children { get; set; } = new List<Category>();
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
```

- [ ] **Step 4.4: Create `Product.cs`**

Create `src/Core/Entities/Products/Product.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("Products")]
    public class Product
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)]
        public string Slug { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }

        public int ManufacturerId { get; set; }
        public int CategoryId { get; set; }

        public byte Status { get; set; } = 1;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        [ForeignKey("ManufacturerId")]
        public virtual Manufacturer Manufacturer { get; set; } = null!;
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
        public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    }
}
```

- [ ] **Step 4.5: Create `ProductVariant.cs`**

Create `src/Core/Entities/Products/ProductVariant.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("ProductVariants")]
    public class ProductVariant
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }

        [Required]
        [MaxLength(50)]
        public string SKU { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        public string VariantName { get; set; } = string.Empty;
        [Required]
        [MaxLength(250)]
        public string Slug { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int WarrantyMonth { get; set; }
        public Dictionary<string, string> Specifications { get; set; } = new();

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public virtual ICollection<ProductSerial> Serials { get; set; } = new List<ProductSerial>();

        /// <summary>
        /// Số lượng tồn kho — cột vật lý, được đồng bộ bởi InventorySyncService.
        /// KHÔNG tự đếm on-the-fly. Luồng Read chỉ đọc giá trị này.
        /// </summary>
        public int StockQuantity { get; set; }
    }
}
```

- [ ] **Step 4.6: Create `ProductImage.cs`**

Create `src/Core/Entities/Products/ProductImage.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("ProductImages")]
    public class ProductImage
    {
        [Key]
        public int Id { get; set; }
        public int VariantId { get; set; }

        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }

        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
    }
}
```

- [ ] **Step 4.7: Delete the god file**

```bash
rm src/Core/Entities/ProductEntities.cs
```

- [ ] **Step 4.8: Verify build**

```bash
dotnet build src/Core/Core.csproj
```

Expected: `Build succeeded`.

---

### Task 5: Split `Core/Entities/SaleEntities.cs`

**Files:**
- Create: `src/Core/Entities/Sales/Voucher.cs`, `VoucherCategory.cs`, `VoucherUsage.cs`, `Order.cs`, `OrderDetail.cs`, `OrderSerial.cs`, `Cart.cs`, `Warranty.cs`, `UserAddress.cs`, `ProductReview.cs`
- Delete: `src/Core/Entities/SaleEntities.cs`

- [ ] **Step 5.1: Create `Sales/` subfolder**

```bash
mkdir -p src/Core/Entities/Sales
```

- [ ] **Step 5.2: Create each file**

For each class below, create a new file in `src/Core/Entities/Sales/` with `namespace PBL3.Core.Entities` and the full class body copied exactly from `src/Core/Entities/SaleEntities.cs`:

| File to create | Class to extract | Source lines |
|---|---|---|
| `Voucher.cs` | `Voucher` | Lines 9–49 |
| `VoucherCategory.cs` | `VoucherCategory` | Lines 51–62 |
| `Order.cs` | `Order` | Lines 64–122 |
| `OrderDetail.cs` | `OrderDetail` | Lines 124–145 |
| `OrderSerial.cs` | `OrderSerial` | Lines 147–160 |
| `Cart.cs` | `Cart` | Lines 162–177 |
| `VoucherUsage.cs` | `VoucherUsage` | Lines 179–204 |
| `Warranty.cs` | `Warranty` | Lines 206–226 |
| `UserAddress.cs` | `UserAddress` | Lines 228–253 |
| `ProductReview.cs` | `ProductReview` | Lines 255–280 |

Each file follows this template:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // [paste exact class body from SaleEntities.cs]
}
```

- [ ] **Step 5.3: Delete the god file**

```bash
rm src/Core/Entities/SaleEntities.cs
```

- [ ] **Step 5.4: Verify build**

```bash
dotnet build src/Core/Core.csproj
```

Expected: `Build succeeded`.

---

### Task 6: Split `Core/Entities/AuthEntities.cs`

**Files:**
- Create: `src/Core/Entities/Auths/AppUser.cs`, `UserProfile.cs`, `AppRole.cs`, `RefreshToken.cs`
- Delete: `src/Core/Entities/AuthEntities.cs`

- [ ] **Step 6.1: Create `Auths/` subfolder**

```bash
mkdir -p src/Core/Entities/Auths
```

- [ ] **Step 6.2: Create each file**

| File | Class | Source lines in `AuthEntities.cs` |
|---|---|---|
| `AppUser.cs` | `AppUser` | Lines 10–29 |
| `UserProfile.cs` | `UserProfile` | Lines 31–54 |
| `AppRole.cs` | `AppRole` | Lines 56–64 |
| `RefreshToken.cs` | `RefreshToken` | Lines 66–89 |

Each file uses this template (note: `AppUser` and `AppRole` need the Identity using):

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PBL3.Core.Entities
{
    // [paste exact class body]
}
```

For `UserProfile.cs` and `RefreshToken.cs`, omit the `using Microsoft.AspNetCore.Identity;` line (not needed).

- [ ] **Step 6.3: Delete the god file**

```bash
rm src/Core/Entities/AuthEntities.cs
```

- [ ] **Step 6.4: Verify build**

```bash
dotnet build src/Core/Core.csproj
```

Expected: `Build succeeded`.

---

### Task 7: Split `Core/Entities/InventoryEntities.cs`

**Files:**
- Create 8 files in `src/Core/Entities/Inventory/`
- Delete: `src/Core/Entities/InventoryEntities.cs`

- [ ] **Step 7.1: Create `Inventory/` subfolder**

```bash
mkdir -p src/Core/Entities/Inventory
```

- [ ] **Step 7.2: Create each file**

| File | Class | Source lines in `InventoryEntities.cs` |
|---|---|---|
| `Supplier.cs` | `Supplier` | Lines 8–33 |
| `ImportReceipt.cs` | `ImportReceipt` | Lines 35–58 |
| `ImportReceiptDetail.cs` | `ImportReceiptDetail` | Lines 60–76 |
| `ProductSerial.cs` | `ProductSerial` | Lines 78–103 |
| `InventoryCheck.cs` | `InventoryCheck` | Lines 105–147 |
| `InventoryCheckDetail.cs` | `InventoryCheckDetail` | Lines 149–176 |
| `InventoryCheckDetailSerial.cs` | `InventoryCheckDetailSerial` | Lines 178–232 |
| `InventoryAdjustmentLog.cs` | `InventoryAdjustmentLog` | Lines 234–268 |

Each file template:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // [paste exact class body]
}
```

- [ ] **Step 7.3: Delete the god file**

```bash
rm src/Core/Entities/InventoryEntities.cs
```

- [ ] **Step 7.4: Verify build**

```bash
dotnet build src/Core/Core.csproj
```

Expected: `Build succeeded`.

---

### Task 8: Split `Core/Entities/ServiceEntities.cs`

**Files:**
- Create 8 files in `src/Core/Entities/ServiceTickets/`
- Delete: `src/Core/Entities/ServiceEntities.cs`

- [ ] **Step 8.1: Create `ServiceTickets/` subfolder**

```bash
mkdir -p src/Core/Entities/ServiceTickets
```

- [ ] **Step 8.2: Create each file**

| File | Class | Source lines in `ServiceEntities.cs` |
|---|---|---|
| `ServiceTicket.cs` | `ServiceTicket` | Lines 7–84 |
| `ServiceTicketStatusHistory.cs` | `ServiceTicketStatusHistory` | Lines 85–103 |
| `Quotation.cs` | `Quotation` | Lines 104–130 |
| `QuotationItem.cs` | `QuotationItem` | Lines 131–154 |
| `RmaShipment.cs` | `RmaShipment` | Lines 155–185 |
| `ServiceInvoice.cs` | `ServiceInvoice` | Lines 186–222 |
| `ServiceInvoiceItem.cs` | `ServiceInvoiceItem` | Lines 223–246 |
| `SerialRepairLog.cs` | `SerialRepairLog` | Lines 247–end |

Each file template:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // [paste exact class body]
}
```

- [ ] **Step 8.3: Delete the god file**

```bash
rm src/Core/Entities/ServiceEntities.cs
```

- [ ] **Step 8.4: Verify full solution build**

```bash
dotnet build PBL3.sln
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 8.5: Commit Phase 2 entity work**

```bash
git add -A
git commit -m "refactor: split Core/Entities god files into one-class-per-file structure"
```

---

### Task 9: Split `Core/Interfaces/IRepositories.cs`

**Files:**
- Create 16 interface files across `src/Core/Interfaces/Repositories/{Products,Inventory,Sales,Customers,Misc}/`
- Delete: `src/Core/Interfaces/IRepositories.cs`

- [ ] **Step 9.1: Create subfolder tree**

```bash
mkdir -p src/Core/Interfaces/Repositories/Products
mkdir -p src/Core/Interfaces/Repositories/Inventory
mkdir -p src/Core/Interfaces/Repositories/Sales
mkdir -p src/Core/Interfaces/Repositories/Customers
mkdir -p src/Core/Interfaces/Repositories/Misc
```

- [ ] **Step 9.2: Create Products group interfaces**

Extract from `src/Core/Interfaces/IRepositories.cs`. Each file uses namespace `PBL3.Core.Interfaces` and starts with `using PBL3.Core.Entities;`.

| File | Interface | Source lines |
|---|---|---|
| `Repositories/Products/IManufacturerRepository.cs` | `IManufacturerRepository` | Lines 8–42 |
| `Repositories/Products/ICategoryRepository.cs` | `ICategoryRepository` | Lines 47–59 |
| `Repositories/Products/IProductRepository.cs` | `IProductRepository` | Lines 64–119 |

Template:

```csharp
using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    // [paste exact interface body]
}
```

- [ ] **Step 9.3: Create Inventory group interfaces**

| File | Interface | Source lines |
|---|---|---|
| `Repositories/Inventory/ISupplierRepository.cs` | `ISupplierRepository` | Lines 124–148 |
| `Repositories/Inventory/IImportReceiptRepository.cs` | `IImportReceiptRepository` | Lines 153–185 |
| `Repositories/Inventory/IProductSerialRepository.cs` | `IProductSerialRepository` | Lines 186–262 |
| `Repositories/Inventory/IInventoryCheckRepository.cs` | `IInventoryCheckRepository` | Lines 547–642 |

Same template as above.

- [ ] **Step 9.4: Create Sales group interfaces**

| File | Interface | Source lines |
|---|---|---|
| `Repositories/Sales/IVoucherRepository.cs` | `IVoucherRepository` | Lines 263–342 |
| `Repositories/Sales/IOrderRepository.cs` | `IOrderRepository` | Lines 343–377 |
| `Repositories/Sales/IWarrantyRepository.cs` | `IWarrantyRepository` | Lines 378–397 |
| `Repositories/Sales/ICartRepository.cs` | `ICartRepository` | Lines 449–472 |

Same template as above.

- [ ] **Step 9.5: Create Customers group interfaces**

| File | Interface | Source lines |
|---|---|---|
| `Repositories/Customers/ICustomerRepository.cs` | `ICustomerRepository` | Lines 398–431 |
| `Repositories/Customers/IEmployeeRepository.cs` | `IEmployeeRepository` | Lines 432–448 |
| `Repositories/Customers/IUserAddressRepository.cs` | `IUserAddressRepository` | Lines 473–484 |

Same template as above.

- [ ] **Step 9.6: Create Misc group interfaces**

| File | Interface | Source lines |
|---|---|---|
| `Repositories/Misc/IBannerRepository.cs` | `IBannerRepository` | Lines 485–519 |
| `Repositories/Misc/IProductReviewRepository.cs` | `IProductReviewRepository` | Lines 520–546 |

Same template as above.

- [ ] **Step 9.7: Delete the god file**

```bash
rm src/Core/Interfaces/IRepositories.cs
```

- [ ] **Step 9.8: Verify build**

```bash
dotnet build PBL3.sln
```

Expected: `Build succeeded` with 0 errors.

---

### Task 10: Split `Core/Interfaces/IServiceRepositories.cs`

**Files:**
- Create 5 interface files in `src/Core/Interfaces/Repositories/ServiceTickets/`
- Delete: `src/Core/Interfaces/IServiceRepositories.cs`

- [ ] **Step 10.1: Create subfolder**

```bash
mkdir -p src/Core/Interfaces/Repositories/ServiceTickets
```

- [ ] **Step 10.2: Create each interface file**

Extract from `src/Core/Interfaces/IServiceRepositories.cs`. Each file: namespace `PBL3.Core.Interfaces`, using `PBL3.Core.Entities`.

| File | Interface | Source lines |
|---|---|---|
| `Repositories/ServiceTickets/IServiceTicketRepository.cs` | `IServiceTicketRepository` | Lines 8–72 |
| `Repositories/ServiceTickets/IQuotationRepository.cs` | `IQuotationRepository` | Lines 73–101 |
| `Repositories/ServiceTickets/IRmaShipmentRepository.cs` | `IRmaShipmentRepository` | Lines 102–120 |
| `Repositories/ServiceTickets/IServiceInvoiceRepository.cs` | `IServiceInvoiceRepository` | Lines 121–167 |
| `Repositories/ServiceTickets/ISerialRepairLogRepository.cs` | `ISerialRepairLogRepository` | Lines 168–end |

- [ ] **Step 10.3: Delete the god file**

```bash
rm src/Core/Interfaces/IServiceRepositories.cs
```

- [ ] **Step 10.4: Verify build**

```bash
dotnet build PBL3.sln
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 10.5: Commit**

```bash
git add -A
git commit -m "refactor: split Core/Interfaces god files into one-interface-per-file structure"
```

---

### Task 11: Group `Infrastructure/Repositories` flat files into domain subfolders

**Files:** Move 21 `.cs` files into 5 subfolders. No code changes — only file locations change. Namespace (`PBL3.Infrastructure.Repositories` or similar) stays unchanged inside each file.

- [ ] **Step 11.1: Create subfolder tree**

```bash
mkdir -p src/Infrastructure/Repositories/Products
mkdir -p src/Infrastructure/Repositories/Inventory
mkdir -p src/Infrastructure/Repositories/Sales
mkdir -p src/Infrastructure/Repositories/Customers
mkdir -p src/Infrastructure/Repositories/ServiceTickets
mkdir -p src/Infrastructure/Repositories/Misc
```

- [ ] **Step 11.2: Move Products group**

```bash
mv src/Infrastructure/Repositories/ProductRepository.cs src/Infrastructure/Repositories/Products/
mv src/Infrastructure/Repositories/CategoryRepository.cs src/Infrastructure/Repositories/Products/
mv src/Infrastructure/Repositories/ManufacturerRepository.cs src/Infrastructure/Repositories/Products/
```

- [ ] **Step 11.3: Move Inventory group**

```bash
mv src/Infrastructure/Repositories/ImportReceiptRepository.cs src/Infrastructure/Repositories/Inventory/
mv src/Infrastructure/Repositories/InventoryCheckRepository.cs src/Infrastructure/Repositories/Inventory/
mv src/Infrastructure/Repositories/ProductSerialRepository.cs src/Infrastructure/Repositories/Inventory/
mv src/Infrastructure/Repositories/SupplierRepository.cs src/Infrastructure/Repositories/Inventory/
```

- [ ] **Step 11.4: Move Sales group**

```bash
mv src/Infrastructure/Repositories/OrderRepository.cs src/Infrastructure/Repositories/Sales/
mv src/Infrastructure/Repositories/CartRepository.cs src/Infrastructure/Repositories/Sales/
mv src/Infrastructure/Repositories/VoucherRepository.cs src/Infrastructure/Repositories/Sales/
mv src/Infrastructure/Repositories/WarrantyRepository.cs src/Infrastructure/Repositories/Sales/
```

- [ ] **Step 11.5: Move Customers group**

```bash
mv src/Infrastructure/Repositories/CustomerRepository.cs src/Infrastructure/Repositories/Customers/
mv src/Infrastructure/Repositories/EmployeeRepository.cs src/Infrastructure/Repositories/Customers/
mv src/Infrastructure/Repositories/UserAddressRepository.cs src/Infrastructure/Repositories/Customers/
```

- [ ] **Step 11.6: Move ServiceTickets group**

```bash
mv src/Infrastructure/Repositories/ServiceTicketRepository.cs src/Infrastructure/Repositories/ServiceTickets/
mv src/Infrastructure/Repositories/QuotationRepository.cs src/Infrastructure/Repositories/ServiceTickets/
mv src/Infrastructure/Repositories/RmaShipmentRepository.cs src/Infrastructure/Repositories/ServiceTickets/
mv src/Infrastructure/Repositories/ServiceInvoiceRepository.cs src/Infrastructure/Repositories/ServiceTickets/
mv src/Infrastructure/Repositories/SerialRepairLogRepository.cs src/Infrastructure/Repositories/ServiceTickets/
```

- [ ] **Step 11.7: Move Misc group**

```bash
mv src/Infrastructure/Repositories/BannerRepository.cs src/Infrastructure/Repositories/Misc/
mv src/Infrastructure/Repositories/ProductReviewRepository.cs src/Infrastructure/Repositories/Misc/
```

- [ ] **Step 11.8: Verify build**

```bash
dotnet build PBL3.sln
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 11.9: Commit**

```bash
git add -A
git commit -m "refactor: group Infrastructure/Repositories flat files into domain subfolders"
```

---

## Phase 3 — Rename Service → Application

### Task 12: Physical rename and solution/project file updates

**Files:**
- Move: `src/Service/` → `src/Application/`
- Rename: `src/Application/Service.csproj` → `src/Application/Application.csproj`
- Modify: `PBL3.sln`
- Modify: `src/API/API.csproj`

- [ ] **Step 12.1: Rename the folder**

```bash
mv src/Service src/Application
```

- [ ] **Step 12.2: Rename the .csproj file**

```bash
mv src/Application/Service.csproj src/Application/Application.csproj
```

- [ ] **Step 12.3: Update `PBL3.sln`**

Open `PBL3.sln`. Find the Service project entry (it looks like this):

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Service", "src\Service\Service.csproj", "{F6923871-3DAC-40B6-8C8B-6C92FF42BE91}"
```

Replace it with:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Application", "src\Application\Application.csproj", "{F6923871-3DAC-40B6-8C8B-6C92FF42BE91}"
```

(The GUID `{F6923871-3DAC-40B6-8C8B-6C92FF42BE91}` stays the same.)

- [ ] **Step 12.4: Update `src/API/API.csproj`**

In `src/API/API.csproj`, find:

```xml
<ProjectReference Include="..\Service\Service.csproj" />
```

Replace with:

```xml
<ProjectReference Include="..\Application\Application.csproj" />
```

- [ ] **Step 12.5: Verify the solution loads**

```bash
dotnet build PBL3.sln
```

Expected: build errors only about `namespace PBL3.Service` — those are fixed in the next task.

---

### Task 13: Replace all `PBL3.Service` namespace references

**Files:** All `*.cs` files in `src/Application/` and `src/API/` that contain `PBL3.Service`.

- [ ] **Step 13.1: Find all affected files**

```bash
grep -rl "PBL3\.Service" src/
```

Note the file list — it will be ~20-30 files in `src/Application/` and a few in `src/API/`.

- [ ] **Step 13.2: Bulk replace namespace declarations in Application/**

```bash
find src/Application -name "*.cs" -exec sed -i '' 's/namespace PBL3\.Service/namespace PBL3.Application/g' {} +
```

- [ ] **Step 13.3: Bulk replace using statements in API/**

```bash
find src/API -name "*.cs" -exec sed -i '' 's/using PBL3\.Service/using PBL3.Application/g' {} +
```

- [ ] **Step 13.4: Bulk replace any remaining references across entire src/**

```bash
find src/ -name "*.cs" -exec sed -i '' 's/PBL3\.Service/PBL3.Application/g' {} +
```

- [ ] **Step 13.5: Verify no leftover references**

```bash
grep -r "PBL3\.Service" src/
```

Expected: no output (zero matches).

- [ ] **Step 13.6: Verify full build**

```bash
dotnet build PBL3.sln
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 13.7: Smoke test — start the API**

```bash
dotnet run --project src/API/API.csproj &
sleep 5
curl -s -o /dev/null -w "%{http_code}" https://localhost:7010/health 2>/dev/null || echo "API started (check manually at https://localhost:7010/swagger)"
kill %1 2>/dev/null
```

Expected: API process starts without crash (exit code not 1).

- [ ] **Step 13.8: Commit**

```bash
git add -A
git commit -m "refactor: rename PBL3.Service project to PBL3.Application (folder, csproj, namespace)"
```

---

## Self-Review Checklist

**Spec coverage:**
- ✅ Phase 1.1 devops consolidation → Task 1
- ✅ Phase 1.2 empty folder deletion → Task 2
- ✅ Phase 1.3 tests skeleton → Task 3
- ✅ Phase 2.1 ProductEntities split → Task 4
- ✅ Phase 2.1 SaleEntities split → Task 5
- ✅ Phase 2.1 AuthEntities split → Task 6
- ✅ Phase 2.1 InventoryEntities split → Task 7
- ✅ Phase 2.1 ServiceEntities split → Task 8
- ✅ Phase 2.2 IRepositories split → Task 9
- ✅ Phase 2.2 IServiceRepositories split → Task 10
- ✅ Phase 2.3 Infrastructure/Repositories grouping → Task 11
- ✅ Phase 3 rename Service → Application → Tasks 12–13
- ✅ Banner.cs — no change (already one class, one file — no task needed)
- ✅ IInventorySyncService.cs, IUnitOfWork.cs — no change (already single files — no task needed)
- ✅ Shared/DTOs — no change (already organized — no task needed)
