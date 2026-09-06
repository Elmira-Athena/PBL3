using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Infrastructure.Data;
using PBL3.Shared.Enums;

namespace PBL3.Tools.LoadProbe.Infra;

public sealed record ProbeCustomer(Guid UserId, string Email, int AddressId, string Token);

/// <summary>
/// Dựng và dọn dữ liệu cho các kịch bản.
///
/// MỌI bản ghi do probe tạo ra đều mang tiền tố "LP-" ở cột mã, hoặc thuộc miền
/// email "@loadprobe.local". Dọn dẹp dựa vào đúng hai dấu hiệu đó, nên nó không
/// bao giờ chạm vào dữ liệu thật — và chạy lại được kể cả khi lần chạy trước bị
/// giết giữa chừng.
/// </summary>
public sealed class ProbeFixture
{
    public const string Tag = "LP-";
    public const string EmailDomain = "@loadprobe.local";

    private readonly ProbeEnvironment _env;

    public ProbeFixture(ProbeEnvironment env) => _env = env;

    public Guid AdminUserId { get; private set; }
    public string AdminEmail { get; private set; } = "admin@hushstore.com";
    public string AdminToken { get; private set; } = string.Empty;

    public int VariantId { get; private set; }
    public int CategoryId { get; private set; }
    public int SupplierId { get; private set; }
    public int ImportReceiptId { get; private set; }

    private int _serialCounter;

    // ══════════════════════════════════════════════════════════════════════════
    // Khởi tạo
    // ══════════════════════════════════════════════════════════════════════════

    public async Task InitializeAsync()
    {
        await using var db = _env.NewDbContext();

        var admin = await db.Users
            .AsNoTracking()
            .Where(u => u.Email == AdminEmail)
            .Select(u => new { u.Id, u.Email, u.IsActive })
            .FirstOrDefaultAsync();

        if (admin is null)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy tài khoản {AdminEmail} trong DB. Probe cần một tài khoản " +
                "Admin có thật để đóng vai nhân viên trong các kịch bản kho/dịch vụ. " +
                "Chạy seed_data.sql trước.");
        }

        if (!admin.IsActive)
        {
            throw new InvalidOperationException(
                $"Tài khoản {AdminEmail} đang bị khoá (IsActive = false). Middleware kiểm " +
                "IsActive đọc thẳng DB nên mọi request của probe sẽ trả 403.");
        }

        AdminUserId = admin.Id;
        AdminToken = _env.MintToken(AdminUserId, AdminEmail, "Admin", "Employee");
    }

    /// <summary>
    /// Danh mục + sản phẩm + biến thể + phiếu nhập, và <paramref name="availableSerials"/>
    /// serial ở trạng thái Available. Gọi lại nhiều lần thì tái dùng bản đã dựng.
    /// </summary>
    public async Task EnsureCatalogAsync(int availableSerials)
    {
        await using var db = _env.NewDbContext();

        if (VariantId == 0)
        {
            var manufacturer = await db.Manufacturers.FirstOrDefaultAsync(m => m.Name == Tag + "Manufacturer");
            if (manufacturer is null)
            {
                manufacturer = new Manufacturer { Name = Tag + "Manufacturer" };
                db.Manufacturers.Add(manufacturer);
                await db.SaveChangesAsync();
            }

            var category = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "lp-category");
            if (category is null)
            {
                category = new Category { Name = Tag + "Category", Slug = "lp-category", Level = 0 };
                db.Categories.Add(category);
                await db.SaveChangesAsync();
            }
            CategoryId = category.Id;

            var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == "lp-product");
            if (product is null)
            {
                product = new Product
                {
                    Name = Tag + "Product",
                    Slug = "lp-product",
                    ManufacturerId = manufacturer.Id,
                    CategoryId = category.Id,
                    Status = 1
                };
                db.Products.Add(product);
                await db.SaveChangesAsync();
            }

            var variant = await db.ProductVariants.FirstOrDefaultAsync(v => v.SKU == Tag + "SKU-1");
            if (variant is null)
            {
                variant = new ProductVariant
                {
                    ProductId = product.Id,
                    SKU = Tag + "SKU-1",
                    VariantName = Tag + "Variant",
                    Slug = "lp-variant-1",
                    Price = 1_000_000m,
                    WarrantyMonth = 12
                };
                db.ProductVariants.Add(variant);
                await db.SaveChangesAsync();
            }
            VariantId = variant.Id;

            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Name == Tag + "Supplier");
            if (supplier is null)
            {
                supplier = new Supplier { Name = Tag + "Supplier", PhoneNumber = "0900000000" };
                db.Suppliers.Add(supplier);
                await db.SaveChangesAsync();
            }
            SupplierId = supplier.Id;

            var receipt = await db.ImportReceipts.FirstOrDefaultAsync(r => r.ReceiptCode == Tag + "RECEIPT");
            if (receipt is null)
            {
                receipt = new ImportReceipt
                {
                    ReceiptCode = Tag + "RECEIPT",
                    SupplierId = supplier.Id,
                    EmployeeId = AdminUserId,
                    ImportDate = DateTime.UtcNow,
                    TotalAmount = 0
                };
                db.ImportReceipts.Add(receipt);
                await db.SaveChangesAsync();

                db.Set<ImportReceiptDetail>().Add(new ImportReceiptDetail
                {
                    ReceiptId = receipt.Id,
                    VariantId = VariantId,
                    Quantity = 1,
                    ImportPrice = 800_000m
                });
                await db.SaveChangesAsync();
            }
            ImportReceiptId = receipt.Id;
        }

        var currentAvailable = await db.ProductSerials
            .CountAsync(s => s.VariantId == VariantId && s.Status == (byte)SerialStatus.Available);

        for (var i = currentAvailable; i < availableSerials; i++)
        {
            db.ProductSerials.Add(NewSerial(SerialStatus.Available));
        }

        if (availableSerials > currentAvailable)
        {
            await db.SaveChangesAsync();
            // StockQuantity là cột đồng bộ bởi InventoryService — probe seed thẳng
            // serial nên phải tự kéo cho khớp, nếu không phần đọc catalogue báo hết hàng.
            await SyncStockAsync(db);
        }
    }

    private ProductSerial NewSerial(SerialStatus status) => new()
    {
        SerialNumber = $"{Tag}SN-{Guid.NewGuid():N}"[..24],
        VariantId = VariantId,
        ImportReceiptId = ImportReceiptId,
        Status = (byte)status,
        CreatedDate = DateTime.UtcNow
    };

    private async Task SyncStockAsync(HushStoreDbContext db)
    {
        var available = await db.ProductSerials
            .CountAsync(s => s.VariantId == VariantId && s.Status == (byte)SerialStatus.Available);

        await db.ProductVariants
            .Where(v => v.Id == VariantId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, available));
    }

    /// <summary>Tạo <paramref name="count"/> khách hàng kèm địa chỉ mặc định và token.</summary>
    public async Task<List<ProbeCustomer>> CreateCustomersAsync(int count)
    {
        await using var db = _env.NewDbContext();
        var result = new List<ProbeCustomer>(count);

        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            var email = $"lp-{id:N}"[..16] + EmailDomain;

            db.Users.Add(new AppUser
            {
                Id = id,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                LockoutEnabled = false,
                AccessFailedCount = 0,
                IsActive = true,
                Type = 2, // Customer
                CreatedDate = DateTime.UtcNow
            });

            db.UserAddresses.Add(new UserAddress
            {
                UserId = id,
                ReceiverName = "LoadProbe Customer",
                PhoneNumber = "0900000001",
                AddressLine = Tag + "address",
                City = "Da Nang",
                IsDefault = true
            });

            result.Add(new ProbeCustomer(id, email, 0, _env.MintToken(id, email, "Customer")));
        }

        await db.SaveChangesAsync();

        // Địa chỉ vừa được sinh Id — đọc lại để gắn vào từng khách.
        var addressIds = await db.UserAddresses
            .AsNoTracking()
            .Where(a => result.Select(r => r.UserId).Contains(a.UserId))
            .ToDictionaryAsync(a => a.UserId, a => a.Id);

        return result
            .Select(c => c with { AddressId = addressIds[c.UserId] })
            .ToList();
    }

    /// <summary>
    /// Một serial đã bán kèm đơn hàng gốc — điều kiện cần để tiếp nhận phiếu dịch vụ
    /// (ServiceTicketService yêu cầu Status == Sold và OrderId != null).
    /// </summary>
    public async Task<(int SerialId, string SerialNumber, int OrderId, Guid CustomerId)> CreateSoldSerialAsync()
    {
        var customer = (await CreateCustomersAsync(1)).Single();

        await using var db = _env.NewDbContext();

        var order = new Order
        {
            OrderCode = $"{Tag}ORD-{Interlocked.Increment(ref _serialCounter):D4}-{Guid.NewGuid():N}"[..20],
            UserId = customer.UserId,
            OrderDate = DateTime.UtcNow,
            Status = (byte)OrderStatus.Success,
            ShipName = "LoadProbe",
            ShipPhone = "0900000001",
            ShipAddress = Tag + "address",
            ShipCity = "Da Nang",
            SubTotal = 1_000_000m,
            ShippingFee = 0,
            DiscountAmount = 0,
            TotalAmount = 1_000_000m,
            PaymentMethod = 0,
            PaymentStatus = 1,
            OrderType = 0
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var serial = NewSerial(SerialStatus.Sold);
        serial.OrderId = order.Id;
        serial.SoldDate = DateTime.UtcNow;
        db.ProductSerials.Add(serial);
        await db.SaveChangesAsync();

        return (serial.Id, serial.SerialNumber, order.Id, customer.UserId);
    }

    /// <summary>Một serial Available mới tinh, trả về Id và số serial.</summary>
    public async Task<(int SerialId, string SerialNumber)> CreateAvailableSerialAsync()
    {
        await using var db = _env.NewDbContext();
        var serial = NewSerial(SerialStatus.Available);
        db.ProductSerials.Add(serial);
        await db.SaveChangesAsync();
        await SyncStockAsync(db);
        return (serial.Id, serial.SerialNumber);
    }

    /// <summary>
    /// Một phiếu kiểm kê đang Chờ duyệt, trong đó <paramref name="serialId"/> bị đánh
    /// dấu THẤT THOÁT (ScanStatus = Missing). Phê duyệt phiếu này sẽ chuyển serial
    /// sang Lost và ghi một bản ghi vào InventoryAdjustmentLogs.
    ///
    /// Dựng thẳng ở tầng DB thay vì đi qua API create → scan → submit: đường API
    /// với ScopeType = AllStore sẽ chốt snapshot TOÀN BỘ serial trong kho, tức kéo
    /// cả dữ liệu thật vào phiếu của probe.
    /// </summary>
    public async Task<int> CreateAwaitingApprovalCheckAsync(int serialId, string serialNumber)
    {
        await using var db = _env.NewDbContext();

        var check = new InventoryCheck
        {
            CheckCode = $"{Tag}KK-{Guid.NewGuid():N}"[..14],
            EmployeeId = AdminUserId,
            CheckDate = DateTime.UtcNow,
            SnapshotAt = DateTime.UtcNow,
            Note = "LoadProbe",
            Status = (byte)InventoryCheckStatus.AwaitingApproval,
            ScopeType = 0
        };
        db.InventoryChecks.Add(check);
        await db.SaveChangesAsync();

        var detail = new InventoryCheckDetail
        {
            CheckId = check.Id,
            VariantId = VariantId,
            SystemQuantity = 1,
            ActualQuantity = 0,
            Difference = -1,
            MatchedQuantity = 0,
            MissingQuantity = 1,
            SurplusQuantity = 0,
            DefectiveQuantity = 0,
            Reason = "LoadProbe"
        };
        db.InventoryCheckDetails.Add(detail);
        await db.SaveChangesAsync();

        db.InventoryCheckDetailSerials.Add(new InventoryCheckDetailSerial
        {
            CheckId = check.Id,
            DetailId = detail.Id,
            VariantId = VariantId,
            SerialId = serialId,
            SerialNumberRaw = serialNumber,
            OriginalStatus = (byte)SerialStatus.Available,
            ScanStatus = (byte)InventoryScanStatus.Missing,
            ScannedAt = DateTime.UtcNow,
            ScannedByEmployeeId = AdminUserId
        });
        await db.SaveChangesAsync();

        return check.Id;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Dọn dẹp
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Xoá vật lý mọi bản ghi do probe tạo, theo đúng thứ tự khoá ngoại.
    ///
    /// Dùng SQL thô chứ không dùng EF một cách có chủ đích: global query filter
    /// (`!IsDeleted`) sẽ giấu mất chính những hàng cần xoá, và nhiều bảng ở đây
    /// (Orders, ProductSerials, InventoryChecks, VoucherUsages) KHÔNG CÓ cột
    /// IsDeleted nên xoá mềm không phải lựa chọn.
    /// </summary>
    public async Task CleanupAsync()
    {
        await using var db = _env.NewDbContext();

        // Ba tập con dưới đây là lý do dọn dẹp không thể chỉ dựa vào tiền tố mã.
        // Đơn hàng / phiếu dịch vụ do CHÍNH API tạo ra trong lúc đo mang mã thật
        // (ORD-…, ST-…) chứ không mang "LP-". Nhận diện chúng qua thứ chúng tham
        // chiếu tới: biến thể, serial và tài khoản của probe.
        const string probeUsers = "SELECT \"Id\" FROM \"AppUsers\" WHERE \"Email\" LIKE '%@loadprobe.local'";
        const string probeVariants = "SELECT \"Id\" FROM \"ProductVariants\" WHERE \"SKU\" LIKE 'LP-%'";
        const string probeSerials = "SELECT \"Id\" FROM \"ProductSerials\" WHERE \"SerialNumber\" LIKE 'LP-%'";
        var probeOrders =
            $"SELECT \"Id\" FROM \"Orders\" WHERE \"OrderCode\" LIKE 'LP-%' " +
            $"OR \"UserId\" IN ({probeUsers}) " +
            $"OR \"Id\" IN (SELECT \"OrderId\" FROM \"OrderDetails\" WHERE \"VariantId\" IN ({probeVariants}))";
        var probeTickets =
            $"SELECT \"Id\" FROM \"ServiceTickets\" WHERE \"TicketCode\" LIKE 'LP-%' " +
            $"OR \"SerialId\" IN ({probeSerials})";
        const string probeChecks = "SELECT \"Id\" FROM \"InventoryChecks\" WHERE \"CheckCode\" LIKE 'LP-%'";

        // Thứ tự dưới đây là thứ tự khoá ngoại, đọc từ lá lên gốc của đồ thị phụ
        // thuộc. Đổi chỗ hai dòng bất kỳ là gặp lỗi 547 (REFERENCE constraint).
        //
        // ⚠️ TÊN BẢNG ≠ TÊN DbSet. Bảng lịch sử trạng thái phiếu dịch vụ tên là
        // ServiceTicketStatusHistory (SỐ ÍT) trong khi DbSet là
        // ServiceTicketStatusHistories. Viết theo tên DbSet là lỗi 208
        // "Invalid object name" — đã vấp một lần. Tra bằng:
        //   SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'
        // Mọi bảng còn lại trong danh sách này đã đối chiếu và trùng tên DbSet.
        var statements = new[]
        {
            // Dịch vụ
            $"DELETE FROM \"ServiceTicketStatusHistory\" WHERE \"TicketId\" IN ({probeTickets})",
            $"DELETE FROM \"QuotationItems\" WHERE \"QuotationId\" IN (SELECT \"Id\" FROM \"Quotations\" WHERE \"TicketId\" IN ({probeTickets}))",
            $"DELETE FROM \"ServiceInvoiceItems\" WHERE \"InvoiceId\" IN (SELECT \"Id\" FROM \"ServiceInvoices\" WHERE \"TicketId\" IN ({probeTickets}))",
            $"DELETE FROM \"ServiceInvoices\" WHERE \"TicketId\" IN ({probeTickets})",
            $"DELETE FROM \"RmaShipments\" WHERE \"TicketId\" IN ({probeTickets})",
            $"DELETE FROM \"SerialRepairLogs\" WHERE \"TicketId\" IN ({probeTickets}) OR \"SerialId\" IN ({probeSerials})",
            $"DELETE FROM \"Quotations\" WHERE \"TicketId\" IN ({probeTickets})",
            $"DELETE FROM \"ServiceTickets\" WHERE \"Id\" IN ({probeTickets})",

            // Kiểm kê
            $"DELETE FROM \"InventoryAdjustmentLogs\" WHERE \"AuditCheckId\" IN ({probeChecks}) OR \"SerialId\" IN ({probeSerials})",
            $"DELETE FROM \"InventoryCheckDetailSerials\" WHERE \"CheckId\" IN ({probeChecks}) OR \"SerialId\" IN ({probeSerials})",
            $"DELETE FROM \"InventoryCheckDetails\" WHERE \"CheckId\" IN ({probeChecks}) OR \"VariantId\" IN ({probeVariants})",
            $"DELETE FROM \"InventoryChecks\" WHERE \"Id\" IN ({probeChecks})",

            // Bán hàng
            $"DELETE FROM \"OrderSerials\" WHERE \"SerialId\" IN ({probeSerials}) OR \"OrderDetailId\" IN (SELECT \"Id\" FROM \"OrderDetails\" WHERE \"OrderId\" IN ({probeOrders}))",
            $"DELETE FROM \"Warranties\" WHERE \"OrderId\" IN ({probeOrders}) OR \"SerialId\" IN ({probeSerials})",
            $"DELETE FROM \"OrderDetails\" WHERE \"OrderId\" IN ({probeOrders})",
            $"DELETE FROM \"VoucherUsages\" WHERE \"OrderId\" IN ({probeOrders}) OR \"VoucherId\" IN (SELECT \"Id\" FROM \"Vouchers\" WHERE \"Code\" LIKE 'LP-%')",
            $"UPDATE \"ProductSerials\" SET \"OrderId\" = NULL WHERE \"OrderId\" IN ({probeOrders})",
            $"DELETE FROM \"Orders\" WHERE \"Id\" IN ({probeOrders})",
            "DELETE FROM \"Vouchers\" WHERE \"Code\" LIKE 'LP-%'",

            // Kho
            "DELETE FROM \"ProductSerials\" WHERE \"SerialNumber\" LIKE 'LP-%'",
            "DELETE FROM \"ImportReceiptDetails\" WHERE \"ReceiptId\" IN (SELECT \"Id\" FROM \"ImportReceipts\" WHERE \"ReceiptCode\" LIKE 'LP-%')",
            "DELETE FROM \"ImportReceipts\" WHERE \"ReceiptCode\" LIKE 'LP-%'",
            "DELETE FROM \"Suppliers\" WHERE \"Name\" LIKE 'LP-%'",

            // Danh mục
            "DELETE FROM \"ProductImages\" WHERE \"VariantId\" IN (SELECT \"Id\" FROM \"ProductVariants\" WHERE \"SKU\" LIKE 'LP-%')",
            "DELETE FROM \"ProductVariants\" WHERE \"SKU\" LIKE 'LP-%'",
            "DELETE FROM \"ProductReviews\" WHERE \"ProductId\" IN (SELECT \"Id\" FROM \"Products\" WHERE \"Slug\" = 'lp-product')",
            "DELETE FROM \"Products\" WHERE \"Slug\" = 'lp-product'",
            "DELETE FROM \"VoucherCategories\" WHERE \"CategoryId\" IN (SELECT \"Id\" FROM \"Categories\" WHERE \"Slug\" = 'lp-category')",
            "DELETE FROM \"Categories\" WHERE \"Slug\" = 'lp-category'",
            "DELETE FROM \"Manufacturers\" WHERE \"Name\" LIKE 'LP-%'",

            // Người dùng
            $"DELETE FROM \"Carts\" WHERE \"UserId\" IN ({probeUsers})",
            $"DELETE FROM \"UserAddresses\" WHERE \"UserId\" IN ({probeUsers})",
            $"DELETE FROM \"UserProfiles\" WHERE \"UserId\" IN ({probeUsers})",
            $"DELETE FROM \"RefreshTokens\" WHERE \"UserId\" IN ({probeUsers})",
            $"DELETE FROM \"AppUserRoles\" WHERE \"UserId\" IN ({probeUsers})",
            $"DELETE FROM \"AppUserClaims\" WHERE \"UserId\" IN ({probeUsers})",
            "DELETE FROM \"AppUsers\" WHERE \"Email\" LIKE '%@loadprobe.local'",

            // Bộ đếm rate limit do S11 sinh ra.
            //
            // 🚨 GUARD to_regclass LÀ BẮT BUỘC, KHÔNG PHẢI CẨN THẬN THÁI QUÁ. Bảng
            // RateLimitCounters chưa có trong migration InitialCreatePostgres, nên trên một DB
            // chưa cập nhật thì DELETE trần ném 42P01 — và Program.cs gọi CleanupAsync() NGOÀI
            // try/catch, ngay trước vòng chạy kịch bản. Tức một dòng dọn dẹp hỏng sẽ giết cả
            // lần chạy và KÉO THEO 9 kịch bản không liên quan. Guard biến nó thành no-op.
            //
            // ⚠️ Xoá ô đếm của cửa sổ ĐANG MỞ là hoàn lại hạn mức cho IP đó. Chỉ an toàn vì
            // Program.cs gọi CleanupAsync đúng hai chỗ: trước mọi kịch bản và sau tất cả —
            // TUYỆT ĐỐI không được gọi giữa FireAsync và AssertAsync của S11, làm vậy là tự
            // xoá bằng chứng rồi kết luận "không có hàng đếm nào".
            """
            DO $$ BEGIN
              IF to_regclass('"RateLimitCounters"') IS NOT NULL THEN
                DELETE FROM "RateLimitCounters" WHERE "PartitionKey" LIKE 'LoginRateLimit:%';
                DELETE FROM "RateLimitCounters" WHERE "WindowStart" < NOW() - INTERVAL '1 hour';
              END IF;
            END $$;
            """
        };

        foreach (var sql in statements)
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
