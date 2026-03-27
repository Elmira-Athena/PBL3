# IMPLEMENTATION PLAN: TÁI CẤU TRÚC MODULE VOUCHER & ORDER (BACKEND API)

**Project:** HushStore
**Module:** Sale — Voucher & Order Restructuring
**Architecture:** N-Layer Clean Architecture + Repository Pattern + Unit of Work

---

## 0. BỐI CẢNH VẤN ĐỀ (PROBLEM STATEMENT)

### Thiết kế hiện tại (Sai)
Bảng `Orders` đang có cột `VoucherId` (FK trực tiếp sang bảng `Vouchers`).

**Hậu quả:**
* 1 đơn hàng chỉ áp dụng được **1 voucher duy nhất** → Giới hạn nghiệp vụ.
* Không track được **lịch sử**: User nào đã dùng voucher nào, khi nào?
* Không thể kiểm tra **"1 user chỉ dùng 1 mã đúng 1 lần"** — phải scan toàn bộ bảng Orders.

### Giải pháp: Bảng trung gian `VoucherUsages`
* Xóa cột `VoucherId` khỏi `Orders`.
* Tạo bảng `VoucherUsages` với Unique Index `(UserId, VoucherId)` → Đảm bảo constraint ở tầng Database.
* Quan hệ: `Order (1) ← (N) VoucherUsages (N) → (1) Voucher`, `User (1) ← (N) VoucherUsages`.

> [!IMPORTANT]
> Đây là **Breaking Change** đối với Database Schema. Cần tạo EF Core Migration mới sau khi sửa Entity.

---

## PHẦN 1: CORE LAYER — Entities & EF Core Configuration

### 1.1. Tạo Entity mới: `VoucherUsage`

**File:** `src/Core/Entities/SaleEntities.cs` — thêm class mới bên dưới class `Cart`.

```csharp
// 6. VoucherUsages (Bảng trung gian: User đã dùng Voucher nào, trong Order nào)
[Table("VoucherUsages")]
public class VoucherUsage
{
    [Key]
    public int Id { get; set; }

    public int VoucherId { get; set; }
    public Guid UserId { get; set; }
    public int OrderId { get; set; }

    /// <summary>
    /// Số tiền thực tế được giảm bởi voucher này trong đơn hàng.
    /// VD: Voucher giảm 20%, MaxDiscount = 50k, đơn 300k → DiscountApplied = 50k.
    /// </summary>
    public decimal DiscountApplied { get; set; }

    public DateTime UsedDate { get; set; } = DateTime.UtcNow;

    [ForeignKey("VoucherId")]
    public virtual Voucher Voucher { get; set; } = null!;
    [ForeignKey("UserId")]
    public virtual AppUser User { get; set; } = null!;
    [ForeignKey("OrderId")]
    public virtual Order Order { get; set; } = null!;
}
```

> [!TIP]
> **// FIX: Thêm cột `DiscountApplied`** — Yêu cầu gốc không có trường này, nhưng nếu 1 đơn áp dụng nhiều voucher, ta **bắt buộc** phải lưu số tiền giảm của **từng** voucher để đối soát tài chính và xử lý hoàn tiền (Refund). Không có cột này thì khi cần hoàn tiền 1 voucher, không biết hoàn bao nhiêu.

### 1.2. Sửa Entity `Order` — Xóa quan hệ cũ

**File:** `src/Core/Entities/SaleEntities.cs` — class `Order`

**Xóa hoàn toàn:**
```diff
-    public int? VoucherId { get; set; }
     // ...
-    [ForeignKey("VoucherId")]
-    public virtual Voucher? Voucher { get; set; }
```

**Thêm Navigation Property:**
```diff
+    /// <summary>
+    /// Danh sách voucher đã áp dụng cho đơn hàng này.
+    /// </summary>
+    public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
```

### 1.3. Sửa Entity `Voucher` — Đổi Navigation Property

**File:** `src/Core/Entities/SaleEntities.cs` — class `Voucher`

```diff
-    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
+    /// <summary>
+    /// Lịch sử sử dụng voucher (gồm thông tin user, order, số tiền giảm).
+    /// </summary>
+    public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
```

### 1.4. Cấu hình Fluent API — `HushStoreDbContext`

**File:** `src/Infrastructure/Data/HushStoreDbContext.cs`

**Bước A — Thêm DbSet:**
```csharp
// Sale
public DbSet<VoucherUsage> VoucherUsages { get; set; }
```

**Bước B — Xóa cấu hình FK cũ** (nếu có bất kỳ config liên quan `Order.VoucherId` trong `OnModelCreating`).
Hiện tại DbContext chưa cấu hình FK `Order → Voucher` bằng Fluent API (đang dùng Data Annotation `[ForeignKey]`), nên chỉ cần xóa property ở Entity là đủ.

**Bước C — Thêm cấu hình cho `VoucherUsage`:**
```csharp
modelBuilder.Entity<VoucherUsage>(entity =>
{
    // UNIQUE INDEX: 1 user chỉ được dùng 1 mã voucher đúng 1 lần
    entity.HasIndex(vu => new { vu.UserId, vu.VoucherId })
          .IsUnique()
          .HasDatabaseName("IX_VoucherUsages_UserId_VoucherId");

    // Index cho truy vấn theo OrderId
    entity.HasIndex(vu => vu.OrderId);

    entity.Property(e => e.DiscountApplied).HasColumnType("decimal(18,2)");

    // FK -> Voucher
    entity.HasOne(vu => vu.Voucher)
          .WithMany(v => v.VoucherUsages)
          .HasForeignKey(vu => vu.VoucherId)
          .OnDelete(DeleteBehavior.NoAction); // Không xóa voucher khi có usage

    // FK -> User (AppUser)
    entity.HasOne(vu => vu.User)
          .WithMany()
          .HasForeignKey(vu => vu.UserId)
          .OnDelete(DeleteBehavior.NoAction); // Tránh multiple cascade paths

    // FK -> Order
    entity.HasOne(vu => vu.Order)
          .WithMany(o => o.VoucherUsages)
          .HasForeignKey(vu => vu.OrderId)
          .OnDelete(DeleteBehavior.Cascade); // Xóa đơn → xóa usage records
});
```

> [!WARNING]
> **Tất cả FK đều dùng `NoAction`** trừ `OrderId` dùng `Cascade` — vì khi hủy/xóa đơn hàng, các usage records không còn ý nghĩa. Còn `UserId` và `VoucherId` phải `NoAction` để tránh SQL Server lỗi "Multiple Cascade Paths" (tương tự pattern đã áp dụng cho `OrderSerial`).

### 1.5. Tạo Migration

```bash
dotnet ef migrations add AddVoucherUsages_RemoveOrderVoucherId \
  --project src/Infrastructure \
  --startup-project src/API
```

> [!CAUTION]
> Nếu Database đang có dữ liệu `Orders` có `VoucherId != NULL`, cần viết logic migrate dữ liệu sang bảng `VoucherUsages` **trước** khi xóa cột. Thêm SQL thủ công vào file Migration nếu cần.

---

## PHẦN 2: SHARED LAYER — DTOs

### 2.1. Cập nhật `CreateOrderRequest`

**File mới:** `src/Shared/DTOs/Sale/CreateOrderRequest.cs`

```csharp
public class CreateOrderRequest
{
    // --- Shipping Info ---
    [Required] public string ShipName { get; set; } = string.Empty;
    [Required] public string ShipPhone { get; set; } = string.Empty;
    [Required] public string ShipAddress { get; set; } = string.Empty;
    [Required] public string ShipCity { get; set; } = string.Empty;

    // --- Payment ---
    public byte PaymentMethod { get; set; } // 0: COD, 1: Banking, 2: VNPay
    public string? Note { get; set; }

    // --- Vouchers (MỚI: Nhận DANH SÁCH mã giảm giá) ---
    /// <summary>
    /// Danh sách mã voucher (Code, không phải Id).
    /// Dùng Code vì Client không nên biết Id nội bộ.
    /// Có thể rỗng nếu không áp dụng voucher.
    /// </summary>
    public List<string>? VoucherCodes { get; set; }

    // --- Cart Items ---
    public List<CreateOrderDetailRequest> Items { get; set; } = new();
}

public class CreateOrderDetailRequest
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}
```

> [!TIP]
> **// FIX: Dùng `List<string> VoucherCodes` thay vì `List<int> VoucherIds`.**
> Lý do: Client (Frontend) chỉ nên thấy mã voucher dạng text (VD: `"SALE50K"`), không nên expose Internal Id. Điều này cũng an toàn hơn — tránh việc client brute-force Id để thử mã.

### 2.2. Response DTOs

**File mới:** `src/Shared/DTOs/Sale/OrderDto.cs`

```csharp
/// <summary>
/// DTO hiển thị chi tiết đơn hàng (kèm danh sách voucher đã dùng).
/// </summary>
public class OrderDetailDto
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public byte Status { get; set; }

    // Shipping
    public string ShipName { get; set; } = string.Empty;
    public string ShipPhone { get; set; } = string.Empty;
    public string ShipAddress { get; set; } = string.Empty;

    // Money
    public decimal SubTotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }

    // Nested
    public List<OrderDetailLineDto> Items { get; set; } = new();
    public List<VoucherUsageDto> AppliedVouchers { get; set; } = new();
}

/// <summary>
/// Thông tin voucher đã áp dụng (hiển thị trong chi tiết đơn).
/// </summary>
public class VoucherUsageDto
{
    public string VoucherCode { get; set; } = string.Empty;
    public string VoucherName { get; set; } = string.Empty;
    public byte DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountApplied { get; set; } // Số tiền thực tế được giảm
}

public class OrderDetailLineDto
{
    public int VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalLine { get; set; }
}
```

### 2.3. Validation (FluentValidation)

**File mới:** `src/Shared/Validators/CreateOrderRequestValidator.cs`

```csharp
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.ShipName).NotEmpty().WithMessage("Tên người nhận không được để trống.");
        RuleFor(x => x.ShipPhone).NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.ShipAddress).NotEmpty().WithMessage("Địa chỉ giao hàng không được để trống.");
        RuleFor(x => x.ShipCity).NotEmpty().WithMessage("Thành phố không được để trống.");

        RuleFor(x => x.Items).NotEmpty().WithMessage("Đơn hàng phải có ít nhất một sản phẩm.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).GreaterThan(0).WithMessage("Mã sản phẩm không hợp lệ.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
        });

        // Voucher codes: Nếu có thì không được trùng lặp nội bộ
        RuleFor(x => x.VoucherCodes)
            .Must(codes => codes == null || codes.Distinct().Count() == codes.Count)
            .WithMessage("Danh sách mã giảm giá không được chứa mã trùng lặp.");
    }
}
```

---

## PHẦN 3: SERVICE LAYER — Business Logic (★ Cực kỳ quan trọng)

### 3.1. Repository Interface

**File:** `src/Core/Interfaces/IRepositories.cs` — Thêm:

```csharp
/// <summary>
/// Repository interface cho Voucher & VoucherUsage.
/// </summary>
public interface IVoucherRepository
{
    /// <summary>
    /// Lấy danh sách Voucher theo danh sách mã Code.
    /// Dùng 1 query duy nhất bằng WHERE IN để tránh N+1.
    /// </summary>
    Task<List<Voucher>> GetByCodesAsync(List<string> codes);

    /// <summary>
    /// Kiểm tra danh sách cặp (UserId, VoucherId) đã tồn tại trong VoucherUsages chưa.
    /// Trả về danh sách VoucherId mà User này đã dùng.
    /// Batch query — 1 lần duy nhất, không loop.
    /// </summary>
    Task<List<int>> GetUsedVoucherIdsByUserAsync(Guid userId, List<int> voucherIds);

    /// <summary>
    /// Thêm danh sách VoucherUsage vào context.
    /// </summary>
    Task AddUsagesAsync(IEnumerable<VoucherUsage> usages);

    Task SaveChangesAsync();
}

/// <summary>
/// Repository interface cho Order.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdWithDetailsAsync(int id);
    Task<string?> GetLastOrderCodeByDateAsync(string datePrefix);
    Task AddAsync(Order order);
    Task SaveChangesAsync();
}
```

### 3.2. Hàm `ApplyVouchers` — Walkthrough Logic

**File:** `src/Service/Orders/OrderService.cs`
**Method Signature:**

```csharp
/// <summary>
/// Validate và tính toán discount cho danh sách voucher codes.
/// Trả về danh sách VoucherUsage đã tính DiscountApplied.
/// KHÔNG insert DB — chỉ tính toán. Insert sẽ nằm trong luồng PlaceOrder.
/// </summary>
private async Task<(List<VoucherUsage> Usages, decimal TotalDiscount)> ApplyVouchersAsync(
    decimal subTotal,
    List<string> voucherCodes,
    Guid userId)
```

**Walkthrough chi tiết từng bước:**

#### Bước 1: Lấy tất cả Voucher trong 1 query
```csharp
var vouchers = await _voucherRepo.GetByCodesAsync(voucherCodes);
```
Kiểm tra: Có mã nào **không tồn tại** không?
```csharp
var foundCodes = vouchers.Select(v => v.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
var invalidCodes = voucherCodes.Where(c => !foundCodes.Contains(c)).ToList();
if (invalidCodes.Any())
    throw new BusinessException($"Mã giảm giá không tồn tại: {string.Join(", ", invalidCodes)}");
```

#### Bước 2: Validate từng Voucher (Loop — nhưng KHÔNG query DB trong loop)
```csharp
var now = DateTime.UtcNow;
foreach (var voucher in vouchers)
{
    // 2a. Kiểm tra active
    if (!voucher.IsActive)
        throw new BusinessException($"Mã '{voucher.Code}' đã bị vô hiệu hóa.");

    // 2b. Kiểm tra thời gian hiệu lực
    if (now < voucher.StartDate || now > voucher.EndDate)
        throw new BusinessException($"Mã '{voucher.Code}' đã hết hạn hoặc chưa đến thời gian sử dụng.");

    // 2c. Kiểm tra số lượng (UsedCount vs Quantity)
    if (voucher.UsedCount >= voucher.Quantity)
        throw new BusinessException($"Mã '{voucher.Code}' đã hết lượt sử dụng.");

    // 2d. Kiểm tra MinOrderValue
    if (subTotal < voucher.MinOrderValue)
        throw new BusinessException(
            $"Mã '{voucher.Code}' yêu cầu đơn hàng tối thiểu {voucher.MinOrderValue:#,0}đ " +
            $"(đơn hiện tại: {subTotal:#,0}đ).");
}
```

#### Bước 3: Kiểm tra "User đã dùng mã này chưa?" — 1 query batch duy nhất
```csharp
var voucherIds = vouchers.Select(v => v.Id).ToList();
var alreadyUsedIds = await _voucherRepo.GetUsedVoucherIdsByUserAsync(userId, voucherIds);
if (alreadyUsedIds.Any())
{
    var usedCodes = vouchers.Where(v => alreadyUsedIds.Contains(v.Id))
                            .Select(v => v.Code);
    throw new BusinessException(
        $"Bạn đã sử dụng mã giảm giá: {string.Join(", ", usedCodes)}. " +
        "Mỗi mã chỉ được sử dụng 1 lần.");
}
```

#### Bước 4: Tính toán DiscountApplied cho từng voucher
```csharp
var usages = new List<VoucherUsage>();
decimal totalDiscount = 0;

foreach (var voucher in vouchers)
{
    decimal discountApplied;

    if (voucher.DiscountType == 0) // Amount (Giảm cố định)
    {
        discountApplied = voucher.DiscountValue;
    }
    else // Percentage (Giảm theo %)
    {
        discountApplied = subTotal * voucher.DiscountValue / 100;

        // Áp dụng trần MaxDiscountAmount nếu có
        if (voucher.MaxDiscountAmount.HasValue
            && discountApplied > voucher.MaxDiscountAmount.Value)
        {
            discountApplied = voucher.MaxDiscountAmount.Value;
        }
    }

    totalDiscount += discountApplied;

    usages.Add(new VoucherUsage
    {
        VoucherId = voucher.Id,
        UserId = userId,
        // OrderId sẽ được gán SAU khi insert Order (lấy được Id)
        DiscountApplied = discountApplied,
        UsedDate = DateTime.UtcNow
    });
}

// Đảm bảo tổng discount không vượt quá SubTotal
if (totalDiscount > subTotal)
    totalDiscount = subTotal;

return (usages, totalDiscount);
```

> [!IMPORTANT]
> **// FIX: Tổng Discount không được > SubTotal.** Tình huống: User nhập 3 mã, mỗi mã giảm 100k, đơn chỉ 200k → Tổng discount = 300k > 200k. Nếu không cap lại, TotalAmount sẽ **âm**. Cách xử lý: `totalDiscount = Math.Min(totalDiscount, subTotal)`.

### 3.3. Luồng `PlaceOrderAsync` — Transaction Flow

**Method Signature:**
```csharp
public async Task<ApiResult<OrderDetailDto>> PlaceOrderAsync(
    CreateOrderRequest request, Guid userId)
```

**Walkthrough:**

```
┌──────────────────────────────────────────────────────────┐
│  Bước 1: Validate cơ bản (FluentValidation đã check)    │
│          + Kiểm tra Variant tồn tại + đủ tồn kho        │
├──────────────────────────────────────────────────────────┤
│  Bước 2: Tính SubTotal                                   │
│          = SUM(UnitPrice × Quantity) cho tất cả items    │
├──────────────────────────────────────────────────────────┤
│  Bước 3: ApplyVouchersAsync(subTotal, codes, userId)     │
│          → Trả về (usages, totalDiscount)                │
├──────────────────────────────────────────────────────────┤
│  Bước 4: ★ BEGIN TRANSACTION ★                           │
│  ├── 4a. Insert Order (Status = Pending)                 │
│  │        TotalAmount = SubTotal + ShippingFee - Discount│
│  │        SaveChanges → Lấy Order.Id                     │
│  │                                                       │
│  ├── 4b. Insert OrderDetails (loop items)                │
│  │                                                       │
│  ├── 4c. Gán OrderId vào từng VoucherUsage               │
│  │        → AddUsagesAsync(usages)                       │
│  │                                                       │
│  ├── 4d. Tăng UsedCount của mỗi Voucher đã áp dụng      │
│  │        voucher.UsedCount += 1;                        │
│  │                                                       │
│  ├── 4e. SaveChanges (flush tất cả)                      │
│  │                                                       │
│  └── 4f. COMMIT Transaction                              │
├──────────────────────────────────────────────────────────┤
│  Bước 5: Trả về OrderDetailDto (AutoMapper)              │
└──────────────────────────────────────────────────────────┘
```

**Code mẫu Transaction:**
```csharp
await _unitOfWork.BeginTransactionAsync();
try
{
    // 4a. Insert Order
    var order = new Order
    {
        OrderCode = await GenerateOrderCodeAsync(),
        UserId = userId,
        OrderDate = DateTime.UtcNow,
        Status = 0, // Pending
        SubTotal = subTotal,
        ShippingFee = shippingFee,
        DiscountAmount = totalDiscount,
        TotalAmount = subTotal + shippingFee - totalDiscount,
        ShipName = request.ShipName,
        ShipPhone = request.ShipPhone,
        ShipAddress = request.ShipAddress,
        ShipCity = request.ShipCity,
        PaymentMethod = request.PaymentMethod,
        PaymentStatus = 0, // Unpaid
        Note = request.Note
    };
    await _orderRepo.AddAsync(order);
    await _unitOfWork.SaveChangesAsync(); // Flush để lấy Order.Id

    // 4b. Insert OrderDetails
    foreach (var item in request.Items)
    {
        order.OrderDetails.Add(new OrderDetail
        {
            OrderId = order.Id,
            VariantId = item.VariantId,
            Quantity = item.Quantity,
            UnitPrice = variantPrices[item.VariantId] // Lấy từ bước 2
        });
    }

    // 4c. Gán OrderId cho VoucherUsages
    foreach (var usage in usages)
        usage.OrderId = order.Id;
    await _voucherRepo.AddUsagesAsync(usages);

    // 4d. Tăng UsedCount
    foreach (var voucher in vouchers)
        voucher.UsedCount += 1;

    // 4e. Flush all
    await _unitOfWork.SaveChangesAsync();

    // 4f. Commit
    await _unitOfWork.CommitAsync();

    return ApiResult<OrderDetailDto>.Success(
        _mapper.Map<OrderDetailDto>(order),
        "Đặt hàng thành công!");
}
catch (Exception)
{
    await _unitOfWork.RollbackAsync();
    throw;
}
```

> [!WARNING]
> **Race Condition — Concurrent Voucher Usage:**
> Khi 2 user cùng dùng 1 mã voucher gần như đồng thời, cả hai đều pass check `UsedCount < Quantity`, rồi cả hai đều `UsedCount += 1`. Unique Index `(UserId, VoucherId)` chỉ bảo vệ **cùng 1 user** dùng 2 lần, không bảo vệ **2 user khác nhau** dùng hết lượt.
>
> **Giải pháp đề xuất** (nếu cần, phạm vi nâng cao):
> - Dùng `SET TRANSACTION ISOLATION LEVEL SERIALIZABLE` cho vùng voucher.
> - Hoặc dùng **Optimistic Concurrency** bằng `[ConcurrencyCheck]` trên cột `UsedCount`.
> - Hoặc dùng raw SQL: `UPDATE Vouchers SET UsedCount = UsedCount + 1 WHERE Id = @id AND UsedCount < Quantity` rồi check `rowsAffected > 0`.

---

## PHẦN 4: LƯU Ý TỐI ƯU HÓA (Performance)

### 4.1. Tránh N+1 Problem

**❌ SAI — Query N+1 (Loop query trong foreach):**
```csharp
// ĐỪNG LÀM THẾ NÀY
foreach (var code in voucherCodes)
{
    var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Code == code); // N queries!
    var used = await _db.VoucherUsages.AnyAsync(vu => vu.UserId == userId
                                                    && vu.VoucherId == voucher.Id); // N queries!
}
```

**✅ ĐÚNG — Batch query (WHERE IN):**
```csharp
// 1 query lấy tất cả voucher (WHERE Code IN (...))
var vouchers = await _db.Vouchers
    .Where(v => voucherCodes.Contains(v.Code))
    .ToListAsync();

// 1 query kiểm tra tất cả usage (WHERE UserId = X AND VoucherId IN (...))
var voucherIds = vouchers.Select(v => v.Id).ToList();
var alreadyUsed = await _db.VoucherUsages
    .Where(vu => vu.UserId == userId && voucherIds.Contains(vu.VoucherId))
    .Select(vu => vu.VoucherId)
    .ToListAsync();
```

**Tổng kết:** Toàn bộ logic `ApplyVouchers` chỉ cần **đúng 2 queries** bất kể user nhập bao nhiêu mã.

### 4.2. Tính lại TotalAmount — Dùng `.Sum()` phía DB

Khi cần hiển thị chi tiết đơn hàng (GET), tính `DiscountAmount` trực tiếp bằng SQL:

```csharp
var order = await _db.Orders
    .AsNoTracking()
    .Include(o => o.OrderDetails)
        .ThenInclude(od => od.Variant) // Eager load Variant
    .Include(o => o.VoucherUsages)
        .ThenInclude(vu => vu.Voucher) // Eager load Voucher info
    .FirstOrDefaultAsync(o => o.Id == orderId);
```

Hoặc dùng `ProjectTo<OrderDetailDto>()` (AutoMapper) để chỉ SELECT đúng các cột cần thiết — nhẹ hơn `Include`:

```csharp
var dto = await _db.Orders
    .AsNoTracking()
    .Where(o => o.Id == orderId)
    .ProjectTo<OrderDetailDto>(_mapper.ConfigurationProvider)
    .FirstOrDefaultAsync();
```

### 4.3. Index Strategy tóm tắt

| Bảng             | Index                              | Mục đích                                      |
|-------------------|------------------------------------|-----------------------------------------------|
| `VoucherUsages`   | `UNIQUE (UserId, VoucherId)`       | Đảm bảo 1 user / 1 voucher / 1 lần           |
| `VoucherUsages`   | `IX_OrderId`                       | JOIN nhanh khi load chi tiết đơn hàng          |
| `Vouchers`        | `UNIQUE (Code)`                    | Lookup voucher bằng mã Code (đã có sẵn)       |
| `Orders`          | `IX_UserId`                        | Lọc đơn hàng theo user (đã có sẵn)            |

---

## PHẦN 5: TỔNG KẾT FILE CHANGES

| Layer          | File                                     | Hành động             |
|----------------|------------------------------------------|-----------------------|
| **Core**       | `Entities/SaleEntities.cs`               | MODIFY — Thêm `VoucherUsage`, sửa `Order`, sửa `Voucher` |
| **Core**       | `Interfaces/IRepositories.cs`            | MODIFY — Thêm `IVoucherRepository`, `IOrderRepository`    |
| **Infrastructure** | `Data/HushStoreDbContext.cs`         | MODIFY — Thêm `DbSet`, Fluent API config                  |
| **Infrastructure** | `Repositories/VoucherRepository.cs`  | NEW — Implement `IVoucherRepository`                       |
| **Infrastructure** | `Repositories/OrderRepository.cs`    | NEW — Implement `IOrderRepository`                         |
| **Shared**     | `DTOs/Sale/CreateOrderRequest.cs`        | NEW                                                        |
| **Shared**     | `DTOs/Sale/OrderDto.cs`                  | NEW                                                        |
| **Shared**     | `Validators/CreateOrderRequestValidator.cs` | NEW                                                     |
| **Service**    | `Orders/IOrderService.cs`               | NEW — Interface                                            |
| **Service**    | `Orders/OrderService.cs`                | NEW — Implement logic PlaceOrder + ApplyVouchers           |
| **API**        | `Controllers/OrdersController.cs`        | NEW — POST/GET endpoints                                   |

---

## PHẦN 6: VERIFICATION PLAN

### Automated (EF Core Migration)
```bash
# Tạo migration
dotnet ef migrations add AddVoucherUsages_RemoveOrderVoucherId --project src/Infrastructure --startup-project src/API

# Kiểm tra migration SQL preview
dotnet ef migrations script --project src/Infrastructure --startup-project src/API

# Áp dụng migration
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

### Manual Testing (Postman / Swagger)
1. **Happy path:** Tạo đơn hàng với 2 mã voucher hợp lệ → Verify `VoucherUsages` có 2 rows, `UsedCount` tăng đúng, `DiscountAmount` tính đúng.
2. **Mã không tồn tại:** Gửi `VoucherCodes: ["FAKE123"]` → Expect lỗi 400 `"Mã giảm giá không tồn tại"`.
3. **Mã hết hạn:** Dùng voucher đã qua `EndDate` → Expect lỗi 400.
4. **Mã hết lượt:** Dùng voucher có `UsedCount = Quantity` → Expect lỗi 400.
5. **Đơn hàng không đạt MinOrderValue:** → Expect lỗi 400 có thông báo rõ ràng.
6. **User dùng lại mã cũ:** Tạo đơn thứ 2 với cùng voucher code → Expect lỗi 400 hoặc SQL Unique Constraint violation.
7. **Transaction Rollback:** Force lỗi giữa chừng (VD: Variant không tồn tại) → Verify không có dữ liệu rác trong `VoucherUsages` và `UsedCount` không bị tăng.
