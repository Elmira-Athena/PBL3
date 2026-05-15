# Controllers & Validators Reorganization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move 21 flat controllers into `Admin/` and `Storefront/` subfolders, and move 11 flat validator files into domain subfolders matching the existing `DTOs/` structure, splitting `ProductValidators.cs` into two focused files.

**Architecture:** Pure file-move + namespace-string refactor. No business logic changes. ASP.NET Core discovers controllers via assembly scan so routing is unaffected; FluentValidation discovers validators via assembly scan so DI registration is unaffected. Only `API/Program.cs` needs explicit `using` updates (3 explicit `IValidator<T>` registrations reference concrete class names).

**Tech Stack:** C# / ASP.NET Core 10, FluentValidation, `git mv` (preserve history), macOS `sed -i ''`

---

## Task 1: Move admin controllers to `Admin/` subfolder

**Files:**
- Create dir: `src/API/Controllers/Admin/`
- Modify: all 20 flat controllers (namespace line only)

- [ ] **Step 1: Create the Admin subfolder and git-move all 20 controllers**

```bash
cd /Users/ml/Athena/Code/PBL3
mkdir -p src/API/Controllers/Admin
git mv src/API/Controllers/AnalyticsController.cs    src/API/Controllers/Admin/
git mv src/API/Controllers/AuthController.cs         src/API/Controllers/Admin/
git mv src/API/Controllers/BannersController.cs      src/API/Controllers/Admin/
git mv src/API/Controllers/BuildPcController.cs      src/API/Controllers/Admin/
git mv src/API/Controllers/CategoriesController.cs   src/API/Controllers/Admin/
git mv src/API/Controllers/CustomersController.cs    src/API/Controllers/Admin/
git mv src/API/Controllers/EmployeesController.cs    src/API/Controllers/Admin/
git mv src/API/Controllers/ImageController.cs        src/API/Controllers/Admin/
git mv src/API/Controllers/ImportReceiptsController.cs src/API/Controllers/Admin/
git mv src/API/Controllers/InventoryController.cs    src/API/Controllers/Admin/
git mv src/API/Controllers/ManufacturersController.cs src/API/Controllers/Admin/
git mv src/API/Controllers/OrdersController.cs       src/API/Controllers/Admin/
git mv src/API/Controllers/PosController.cs          src/API/Controllers/Admin/
git mv src/API/Controllers/ProductsController.cs     src/API/Controllers/Admin/
git mv src/API/Controllers/ProductSerialsController.cs src/API/Controllers/Admin/
git mv src/API/Controllers/ReviewsController.cs      src/API/Controllers/Admin/
git mv src/API/Controllers/ServiceInvoicesController.cs src/API/Controllers/Admin/
git mv src/API/Controllers/ServiceTicketsController.cs src/API/Controllers/Admin/
git mv src/API/Controllers/SuppliersController.cs    src/API/Controllers/Admin/
git mv src/API/Controllers/VouchersController.cs     src/API/Controllers/Admin/
```

- [ ] **Step 2: Update namespace in the 19 block-scoped controllers**

```bash
cd /Users/ml/Athena/Code/PBL3
for f in \
  src/API/Controllers/Admin/AnalyticsController.cs \
  src/API/Controllers/Admin/AuthController.cs \
  src/API/Controllers/Admin/BannersController.cs \
  src/API/Controllers/Admin/BuildPcController.cs \
  src/API/Controllers/Admin/CategoriesController.cs \
  src/API/Controllers/Admin/CustomersController.cs \
  src/API/Controllers/Admin/EmployeesController.cs \
  src/API/Controllers/Admin/ImportReceiptsController.cs \
  src/API/Controllers/Admin/InventoryController.cs \
  src/API/Controllers/Admin/ManufacturersController.cs \
  src/API/Controllers/Admin/OrdersController.cs \
  src/API/Controllers/Admin/PosController.cs \
  src/API/Controllers/Admin/ProductsController.cs \
  src/API/Controllers/Admin/ProductSerialsController.cs \
  src/API/Controllers/Admin/ReviewsController.cs \
  src/API/Controllers/Admin/ServiceInvoicesController.cs \
  src/API/Controllers/Admin/ServiceTicketsController.cs \
  src/API/Controllers/Admin/SuppliersController.cs \
  src/API/Controllers/Admin/VouchersController.cs; do
  sed -i '' 's/^namespace PBL3\.API\.Controllers$/namespace PBL3.API.Controllers.Admin/' "$f"
done
```

- [ ] **Step 3: Update namespace in `ImageController.cs` (file-scoped namespace)**

`ImageController.cs` uses file-scoped namespace syntax (`namespace X;`), so it needs a separate sed pattern:

```bash
sed -i '' 's/^namespace PBL3\.API\.Controllers;$/namespace PBL3.API.Controllers.Admin;/' \
  src/API/Controllers/Admin/ImageController.cs
```

Verify it changed:

```bash
grep "^namespace" src/API/Controllers/Admin/ImageController.cs
```

Expected output: `namespace PBL3.API.Controllers.Admin;`

- [ ] **Step 4: Commit**

```bash
git add src/API/Controllers/Admin/
git commit -m "refactor: move admin controllers to Admin/ subfolder"
```

---

## Task 2: Move `CartController.cs` to `Storefront/` subfolder

**Files:**
- Modify: `src/API/Controllers/CartController.cs` (namespace line)

- [ ] **Step 1: git-move CartController**

```bash
cd /Users/ml/Athena/Code/PBL3
git mv src/API/Controllers/CartController.cs src/API/Controllers/Storefront/
```

- [ ] **Step 2: Update namespace**

```bash
sed -i '' 's/^namespace PBL3\.API\.Controllers$/namespace PBL3.API.Controllers.Storefront/' \
  src/API/Controllers/Storefront/CartController.cs
```

Verify:

```bash
grep "^namespace" src/API/Controllers/Storefront/CartController.cs
```

Expected: `namespace PBL3.API.Controllers.Storefront`

- [ ] **Step 3: Commit**

```bash
git add src/API/Controllers/Storefront/CartController.cs
git commit -m "refactor: move CartController to Storefront/ subfolder"
```

---

## Task 3: Move flat validators into domain subfolders

**Files:**
- Create dirs: `Banners/`, `Categories/`, `Customers/`, `Employees/`, `Inventory/`, `Reviews/`, `ServiceTickets/`, `Suppliers/`, `Vouchers/`
- Modify: namespace line in each moved file

- [ ] **Step 1: Create domain subdirectories**

```bash
cd /Users/ml/Athena/Code/PBL3/src/Shared/Validators
mkdir -p Banners Categories Customers Employees Inventory Reviews ServiceTickets Suppliers Vouchers
```

- [ ] **Step 2: git-move validator files**

```bash
cd /Users/ml/Athena/Code/PBL3
git mv src/Shared/Validators/BannerValidators.cs       src/Shared/Validators/Banners/
git mv src/Shared/Validators/CategoryValidators.cs     src/Shared/Validators/Categories/
git mv src/Shared/Validators/CustomerValidators.cs     src/Shared/Validators/Customers/
git mv src/Shared/Validators/EmployeeValidators.cs     src/Shared/Validators/Employees/
git mv src/Shared/Validators/ImportReceiptValidators.cs src/Shared/Validators/Inventory/
git mv src/Shared/Validators/ExportOrderValidators.cs  src/Shared/Validators/Inventory/
git mv src/Shared/Validators/ReviewValidators.cs       src/Shared/Validators/Reviews/
git mv src/Shared/Validators/ServiceTicketValidators.cs src/Shared/Validators/ServiceTickets/
git mv src/Shared/Validators/SupplierValidators.cs     src/Shared/Validators/Suppliers/
git mv src/Shared/Validators/VoucherValidators.cs      src/Shared/Validators/Vouchers/
```

- [ ] **Step 3: Update namespace in each moved file**

```bash
cd /Users/ml/Athena/Code/PBL3

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.Banners/' \
  src/Shared/Validators/Banners/BannerValidators.cs

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.Categories/' \
  src/Shared/Validators/Categories/CategoryValidators.cs

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.Customers/' \
  src/Shared/Validators/Customers/CustomerValidators.cs

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.Employees/' \
  src/Shared/Validators/Employees/EmployeeValidators.cs

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.Inventory/' \
  src/Shared/Validators/Inventory/ImportReceiptValidators.cs

# ExportOrderValidators.cs already has namespace PBL3.Shared.Validators.Inventory — no change needed

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.Reviews/' \
  src/Shared/Validators/Reviews/ReviewValidators.cs

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.ServiceTickets/' \
  src/Shared/Validators/ServiceTickets/ServiceTicketValidators.cs

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.Suppliers/' \
  src/Shared/Validators/Suppliers/SupplierValidators.cs

sed -i '' 's/^namespace PBL3\.Shared\.Validators$/namespace PBL3.Shared.Validators.Vouchers/' \
  src/Shared/Validators/Vouchers/VoucherValidators.cs
```

- [ ] **Step 4: Verify all namespace lines are correct**

```bash
grep "^namespace" \
  src/Shared/Validators/Banners/BannerValidators.cs \
  src/Shared/Validators/Categories/CategoryValidators.cs \
  src/Shared/Validators/Customers/CustomerValidators.cs \
  src/Shared/Validators/Employees/EmployeeValidators.cs \
  src/Shared/Validators/Inventory/ImportReceiptValidators.cs \
  src/Shared/Validators/Inventory/ExportOrderValidators.cs \
  src/Shared/Validators/Reviews/ReviewValidators.cs \
  src/Shared/Validators/ServiceTickets/ServiceTicketValidators.cs \
  src/Shared/Validators/Suppliers/SupplierValidators.cs \
  src/Shared/Validators/Vouchers/VoucherValidators.cs
```

Expected output:
```
src/Shared/Validators/Banners/BannerValidators.cs:namespace PBL3.Shared.Validators.Banners
src/Shared/Validators/Categories/CategoryValidators.cs:namespace PBL3.Shared.Validators.Categories
src/Shared/Validators/Customers/CustomerValidators.cs:namespace PBL3.Shared.Validators.Customers
src/Shared/Validators/Employees/EmployeeValidators.cs:namespace PBL3.Shared.Validators.Employees
src/Shared/Validators/Inventory/ImportReceiptValidators.cs:namespace PBL3.Shared.Validators.Inventory
src/Shared/Validators/Inventory/ExportOrderValidators.cs:namespace PBL3.Shared.Validators.Inventory
src/Shared/Validators/Reviews/ReviewValidators.cs:namespace PBL3.Shared.Validators.Reviews
src/Shared/Validators/ServiceTickets/ServiceTicketValidators.cs:namespace PBL3.Shared.Validators.ServiceTickets
src/Shared/Validators/Suppliers/SupplierValidators.cs:namespace PBL3.Shared.Validators.Suppliers
src/Shared/Validators/Vouchers/VoucherValidators.cs:namespace PBL3.Shared.Validators.Vouchers
```

- [ ] **Step 5: Commit**

```bash
git add src/Shared/Validators/
git commit -m "refactor: move validators into domain subfolders"
```

---

## Task 4: Split `ProductValidators.cs` and move to `Products/`

**Files:**
- Create dir: `src/Shared/Validators/Products/`
- Modify: `src/Shared/Validators/ProductValidators.cs` → becomes `Products/ProductValidators.cs`
- Create: `src/Shared/Validators/Products/VariantValidators.cs`

- [ ] **Step 1: Create the Products subfolder and git-move ProductValidators.cs**

```bash
cd /Users/ml/Athena/Code/PBL3
mkdir -p src/Shared/Validators/Products
git mv src/Shared/Validators/ProductValidators.cs src/Shared/Validators/Products/
```

- [ ] **Step 2: Rewrite `Products/ProductValidators.cs`** (keep only Product-level validators, update namespace)

Replace the full file content:

```csharp
using FluentValidation;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Shared.Validators.Products
{
    public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
    {
        public CreateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên sản phẩm không được để trống.")
                .MaximumLength(255).WithMessage("Tên sản phẩm không được vượt quá 255 ký tự.");

            RuleFor(x => x.ManufacturerId)
                .GreaterThan(0).WithMessage("Vui lòng chọn nhà sản xuất.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Vui lòng chọn danh mục.");

            RuleFor(x => x.Variants)
                .NotEmpty().WithMessage("Sản phẩm phải có ít nhất một phiên bản.")
                .Must(v => v != null && v.Count > 0).WithMessage("Sản phẩm phải có ít nhất một phiên bản.");

            RuleForEach(x => x.Variants)
                .SetValidator(new CreateVariantRequestValidator());
        }
    }

    public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
    {
        public UpdateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên sản phẩm không được để trống.")
                .MaximumLength(255).WithMessage("Tên sản phẩm không được vượt quá 255 ký tự.");

            RuleFor(x => x.ManufacturerId)
                .GreaterThan(0).WithMessage("Vui lòng chọn nhà sản xuất.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Vui lòng chọn danh mục.");

            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Trạng thái sản phẩm không hợp lệ.");
        }
    }
}
```

- [ ] **Step 3: Create `Products/VariantValidators.cs`**

```csharp
using FluentValidation;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Shared.Validators.Products
{
    public class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
    {
        public CreateVariantRequestValidator()
        {
            RuleFor(x => x.SKU)
                .NotEmpty().WithMessage("Mã SKU không được để trống.")
                .MaximumLength(50).WithMessage("Mã SKU không được vượt quá 50 ký tự.")
                .Matches(@"^\S+$").WithMessage("Mã SKU không được chứa khoảng trắng.");

            RuleFor(x => x.VariantName)
                .NotEmpty().WithMessage("Tên phiên bản không được để trống.")
                .MaximumLength(200).WithMessage("Tên phiên bản không được vượt quá 200 ký tự.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá bán phải lớn hơn 0.");

            RuleFor(x => x.OriginalPrice)
                .GreaterThan(0).When(x => x.OriginalPrice.HasValue)
                .WithMessage("Giá gốc phải lớn hơn 0.");

            RuleFor(x => x.WarrantyMonth)
                .GreaterThanOrEqualTo(0).WithMessage("Thời gian bảo hành phải >= 0 tháng.");
        }
    }

    public class SaveVariantRequestValidator : AbstractValidator<SaveVariantRequest>
    {
        public SaveVariantRequestValidator()
        {
            RuleFor(x => x.SKU)
                .NotEmpty().WithMessage("Mã SKU không được để trống.")
                .MaximumLength(50).WithMessage("Mã SKU không được vượt quá 50 ký tự.")
                .Matches(@"^\S+$").WithMessage("Mã SKU không được chứa khoảng trắng.");

            RuleFor(x => x.VariantName)
                .NotEmpty().WithMessage("Tên phiên bản không được để trống.")
                .MaximumLength(200).WithMessage("Tên phiên bản không được vượt quá 200 ký tự.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá bán phải lớn hơn 0.");

            RuleFor(x => x.OriginalPrice)
                .GreaterThan(0).When(x => x.OriginalPrice.HasValue)
                .WithMessage("Giá gốc phải lớn hơn 0.");

            RuleFor(x => x.WarrantyMonth)
                .GreaterThanOrEqualTo(0).WithMessage("Thời gian bảo hành phải >= 0 tháng.");
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add src/Shared/Validators/Products/
git commit -m "refactor: split ProductValidators into ProductValidators + VariantValidators under Products/"
```

---

## Task 5: Update `API/Program.cs` using statements

**Files:**
- Modify: `src/API/Program.cs` line 41

The line `using PBL3.Shared.Validators;` covers `CreateReviewRequestValidator` (now in `.Reviews`) and `CreateBannerRequestValidator` / `UpdateBannerRequestValidator` (now in `.Banners`).

- [ ] **Step 1: Replace the using statement**

In `src/API/Program.cs`, find and replace:

```csharp
using PBL3.Shared.Validators;
```

with:

```csharp
using PBL3.Shared.Validators.Banners;
using PBL3.Shared.Validators.Reviews;
```

- [ ] **Step 2: Commit**

```bash
git add src/API/Program.cs
git commit -m "refactor: update Validators using statements in Program.cs"
```

---

## Task 6: Build verification

- [ ] **Step 1: Build the full solution**

```bash
cd /Users/ml/Athena/Code/PBL3
dotnet build PBL3.sln
```

Expected: `Build succeeded.` with 0 errors and 0 warnings related to namespaces.

- [ ] **Step 2: If build fails, diagnose**

Common failure patterns:
- `CS0246: The type or namespace name 'XyzValidator' could not be found` → a `using` statement in some file still references the old `PBL3.Shared.Validators` namespace. Run `grep -rn "PBL3.Shared.Validators\"" src/ --include="*.cs"` to find any remaining direct references and update them.
- `CS0234: The type or namespace name 'Admin' does not exist in the namespace 'PBL3.API.Controllers'` → a controller file's namespace wasn't updated. Run `grep -rn "namespace PBL3.API.Controllers$" src/API/Controllers/Admin/` to find the culprit.

- [ ] **Step 3: Final commit (only if Step 2 required fixes)**

```bash
git add -A
git commit -m "fix: resolve build errors after Controllers/Validators reorganization"
```
