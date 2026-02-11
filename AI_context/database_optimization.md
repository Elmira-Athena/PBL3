
# DATABASE & QUERY OPTIMIZATION GUIDELINES
## Scope: EF Core, LINQ, PLINQ

---

## 1. CORE PHILOSOPHY (TRIẾT LÝ CỐT LÕI)

- **Server-side Evaluation First**: Luôn ưu tiên đẩy Filter, Sort, Project xuống SQL Server.
- **Client-side Evaluation (In-Memory)**: Chỉ dùng khi logic quá phức tạp mà SQL không hỗ trợ, hoặc cần tính toán CPU-bound trên tập dữ liệu đã giới hạn.
- **No "Select *"**: Tuyệt đối không dùng `SELECT *`. Chỉ lấy các cột cần thiết thông qua DTO Projection.

---

## 2. LINQ TO ENTITIES (DATABASE QUERYING)

Áp dụng cho mọi thao tác truy vấn vào `DbContext`.

### 2.1. Read-Only Optimization

**Quy tắc**: Với query chỉ để hiển thị (GET), **bắt buộc** dùng `.AsNoTracking()`.

**Lý do**: EF Core không cần theo dõi Change Tracker → tăng tốc 2-5 lần.

```csharp
// BAD
var products = await _context.Products.ToListAsync();

// GOOD
var products = await _context.Products
    .AsNoTracking()
    .Where(p => p.Status == ProductStatus.Active)
    .ToListAsync();
```

### 2.2. Projection (DTO Mapping)

**Quy tắc**: Không bao giờ trả về Entity. Hãy `Select` thẳng ra DTO ngay trong LINQ.

```csharp
// GOOD
var query = _context.Products
    .AsNoTracking()
    .Select(p => new ProductDto
    {
        Id    = p.Id,
        Name  = p.Name,
        Price = p.Price
        // Không lấy Description nặng nề
    });
```

### 2.3. N+1 Problem Prevention

**Quy tắc**: Khi cần dữ liệu bảng con, phải dùng `.Include()` hoặc Projection. Tuyệt đối không dùng vòng lặp foreach gọi DB.

```csharp
// BAD - gây ra 101 query nếu có 100 sản phẩm
foreach (var p in products)
{
    p.Category = _context.Categories.Find(p.CategoryId);
}

// GOOD - chỉ 1 query (JOIN)
var products = await _context.Products
    .Include(p => p.Category)
    .AsNoTracking()
    .ToListAsync();
```

### 2.4. Pagination

**Quy tắc**: Mọi API danh sách **phải** có `Skip()` + `Take()`. Không bao giờ trả về toàn bộ bảng.

---

## 3. PLINQ (PARALLEL LINQ) & IN-MEMORY PROCESSING

Chỉ sử dụng khi đã lấy dữ liệu về RAM và cần tính toán nặng (CPU-bound).

### 3.1. Khi nào dùng `.AsParallel()`?

- **Scenario 1**: Build PC Compatibility – kiểm tra chéo hàng nghìn luật tương thích.
- **Scenario 2**: Report Aggregation – tổng hợp báo cáo phức tạp từ dữ liệu đã có trong Cache.
- **Scenario 3**: Batch Processing – xử lý ảnh, file Excel sau khi upload.

### 3.2. Quy trình chuyển đổi (The Bridge)

```text
Database (IQueryable) → ToList/ToArray (RAM) → AsParallel (PLINQ) → Processing
```

```csharp
// Bước 1: Lấy dữ liệu thô từ DB (nhẹ nhất có thể)
var components = await _context.Products
    .AsNoTracking()
    .Where(p => ids.Contains(p.Id))
    .ToListAsync();

// Bước 2: PLINQ cho tính toán nặng
var compatibilityResult = components
    .AsParallel()
    .WithDegreeOfParallelism(Environment.ProcessorCount)
    .Select(c => ComplexCompatibilityCheckAlgorithm(c))
    .ToList();
```

### 3.3. Anti-Patterns (Cảnh báo quan trọng)

- **KHÔNG** dùng `.AsParallel()` trực tiếp trên `DbSet` hoặc `IQueryable`.  
  → Sẽ tải toàn bộ bảng về RAM trước khi lọc → dễ gây **OutOfMemoryException**.

---

## 4. ASYNC/AWAIT BEST PRACTICES

- Database I/O: Luôn dùng `ToListAsync()`, `FirstOrDefaultAsync()`, `AnyAsync()`, …
- Tránh Deadlock: Không dùng `.Result` hoặc `.Wait()`. Luôn `await` từ Controller → Repository.
- Cancellation: Repository method nên nhận `CancellationToken`.

```csharp
public async Task<List<Product>> GetAllAsync(CancellationToken ct = default)
{
    return await _context.Products
        .AsNoTracking()
        .ToListAsync(ct);
}
```

---

### Cách sử dụng file này

1. Lưu file thành tên: `database_optimization.md` trong thư mục `02_AI_Context`.
2. Khi yêu cầu code, dùng prompt sau:

> *"Code cho tôi chức năng [Tên chức năng]. Lưu ý tuân thủ chặt chẽ các quy tắc tối ưu hóa trong @database_optimization.md. Nếu logic cần tính toán phức tạp trên RAM thì dùng PLINQ, còn nếu chỉ truy xuất dữ liệu thì tối ưu LINQ to Entities."*

