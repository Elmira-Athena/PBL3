using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PBL3.Core.Entities;

namespace PBL3.Infrastructure.Data
{
    public class HushStoreDbContext : IdentityDbContext<AppUser, AppRole, Guid,
        IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
    {
        public HushStoreDbContext(DbContextOptions<HushStoreDbContext> options) : base(options)
        {
        }

        // Product
        public DbSet<Manufacturer> Manufacturers { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }

        // Inventory
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<ImportReceipt> ImportReceipts { get; set; }
        public DbSet<ImportReceiptDetail> ImportReceiptDetails { get; set; }
        public DbSet<ProductSerial> ProductSerials { get; set; }
        public DbSet<InventoryCheck> InventoryChecks { get; set; }
        public DbSet<InventoryCheckDetail> InventoryCheckDetails { get; set; }
        public DbSet<InventoryCheckDetailSerial> InventoryCheckDetailSerials { get; set; }
        public DbSet<InventoryAdjustmentLog> InventoryAdjustmentLogs { get; set; }

        // Sale
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<VoucherCategory> VoucherCategories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<OrderSerial> OrderSerials { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<VoucherUsage> VoucherUsages { get; set; }
        public DbSet<Warranty> Warranties { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }

        // Service & Warranty
        public DbSet<ServiceTicket> ServiceTickets { get; set; }
        public DbSet<ServiceTicketStatusHistory> ServiceTicketStatusHistories { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<RmaShipment> RmaShipments { get; set; }
        public DbSet<ServiceInvoice> ServiceInvoices { get; set; }
        public DbSet<ServiceInvoiceItem> ServiceInvoiceItems { get; set; }
        public DbSet<SerialRepairLog> SerialRepairLogs { get; set; }

        // Auth
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        // Storefront
        public DbSet<Banner> Banners { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Identity mappings

            // --- AUTH: Rename Identity tables theo convention ---
            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("AppUsers");
                entity.Property(u => u.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
                entity.Property(u => u.PhoneNumber).HasMaxLength(20).IsUnicode(false);
            });
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.ToTable("UserProfiles");
                entity.HasOne(p => p.User)
                      .WithOne(u => u.Profile)
                      .HasForeignKey<UserProfile>(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<AppRole>(entity =>
            {
                entity.ToTable("AppRoles");
                entity.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
                entity.HasIndex(r => r.RoleCode).IsUnique();
            });
            modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("AppUserRoles");
            modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("AppUserClaims");
            modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("AppUserLogins");
            modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("AppRoleClaims");
            modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("AppUserTokens");

            // --- PRODUCT ---
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasIndex(p => p.Slug).IsUnique();
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Slug).IsUnique();
                // Recursive Relationship (Adjacency List)
                entity.HasOne(c => c.Parent)
                    .WithMany(p => p.Children)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ProductVariant>(entity =>
            {
                entity.HasIndex(v => v.SKU).IsUnique();
                entity.HasIndex(v => v.ProductId);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OriginalPrice).HasColumnType("decimal(18,2)");

                // StockQuantity - cột vật lý, default = 0
                entity.Property(e => e.StockQuantity)
                      .HasDefaultValue(0);

                // Specifications - JSON column
                // EF Core không tự so sánh được Dictionary<string,string> theo từng cặp key-value.
                // Nếu không có ValueComparer tuỳ chỉnh, EF Core sẽ luôn đánh dấu cột này là "đã thay đổi"
                // mỗi khi SaveChanges() được gọi — dù dữ liệu thực tế không đổi — gây ra UPDATE thừa.
                // ValueComparer cần 3 hàm:
                var specComparer = new ValueComparer<Dictionary<string, string>>(
                    // 1. equalsExpression — so sánh bằng nhau:
                    //    Hai Dictionary bằng nhau khi cùng null, hoặc có cùng số cặp key-value
                    //    và tất cả các cặp trong c1 đều tồn tại trong c2 (Except trả về rỗng).
                    (c1, c2) => c1 == c2 || (c1 != null && c2 != null &&
                                c1.Count == c2.Count && !c1.Except(c2).Any()),

                    // 2. hashCodeExpression — tính hash code để EF Core lưu "ảnh chụp" trạng thái cũ:
                    //    Nếu null → hash = 0. Nếu có dữ liệu → kết hợp hash của từng cặp key-value
                    //    bằng HashCode.Combine để ra một số duy nhất đại diện cho toàn bộ Dictionary.
                    c => c == null ? 0 : c.Aggregate(0, (a, p) =>
                                HashCode.Combine(a, p.Key.GetHashCode(),
                                    p.Value == null ? 0 : p.Value.GetHashCode())),

                    // 3. snapshotExpression — tạo bản sao độc lập (deep copy):
                    //    EF Core cần lưu bản sao của giá trị gốc để so sánh sau khi entity bị sửa.
                    //    Nếu chỉ gán tham chiếu (=), cả hai sẽ trỏ vào cùng object → mất khả năng phát hiện thay đổi.
                    //    "c ?? new()" đảm bảo không bao giờ snapshot thành null.
                    c => new Dictionary<string, string>(c ?? new())
                );
                entity.Property(e => e.Specifications)
                      .HasColumnType("nvarchar(max)")
                      .HasConversion(
                          v => System.Text.Json.JsonSerializer.Serialize(v, System.Text.Json.JsonSerializerOptions.Default),
                          v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, System.Text.Json.JsonSerializerOptions.Default)
                               ?? new Dictionary<string, string>()
                      )
                      .Metadata.SetValueComparer(specComparer);
            });

            // --- PRODUCT ---
            modelBuilder.Entity<Manufacturer>(entity =>
            {
                entity.HasQueryFilter(m => !m.IsDeleted);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasQueryFilter(p => !p.IsDeleted && !p.Manufacturer.IsDeleted);
            });

            // --- STOREFRONT: BANNER ---
            modelBuilder.Entity<Banner>(entity =>
            {
                entity.HasQueryFilter(b => !b.IsDeleted);
                entity.HasIndex(b => new { b.IsActive, b.SortOrder });
            });

            // --- INVENTORY ---
            // Global Query Filter: Tự động bỏ qua Supplier đã bị xoá mềm
            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.HasQueryFilter(s => !s.IsDeleted);
            });

            modelBuilder.Entity<ProductSerial>(entity =>
            {
                entity.HasIndex(s => s.SerialNumber).IsUnique();
                entity.HasIndex(s => new { s.VariantId, s.Status });
            });

            modelBuilder.Entity<ImportReceipt>(entity =>
            {
                entity.HasQueryFilter(r => !r.IsDeleted && !r.Supplier.IsDeleted);
                entity.HasIndex(r => r.ReceiptCode).IsUnique();
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<ImportReceiptDetail>(entity =>
            {
                entity.HasIndex(d => d.ReceiptId);
                entity.HasIndex(d => d.VariantId);
                entity.Property(e => e.ImportPrice).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<InventoryCheck>(entity =>
            {
                entity.HasQueryFilter(c => !c.IsDeleted);
                entity.HasIndex(c => c.CheckCode).IsUnique();
                entity.HasIndex(c => c.Status);
                entity.HasIndex(c => c.CheckDate);

                entity.HasOne(c => c.ScopeCategory)
                      .WithMany()
                      .HasForeignKey(c => c.ScopeCategoryId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<InventoryCheckDetail>(entity =>
            {
                entity.HasQueryFilter(d => !d.Check.IsDeleted);
                entity.HasIndex(d => d.CheckId);
                entity.HasIndex(d => d.VariantId);

                entity.Property(e => e.Difference)
                      .HasComputedColumnSql("([ActualQuantity] - [SystemQuantity])");
            });

            modelBuilder.Entity<InventoryCheckDetailSerial>(entity =>
            {
                entity.HasQueryFilter(s => !s.Check.IsDeleted);
                entity.HasIndex(s => s.CheckId);
                entity.HasIndex(s => new { s.CheckId, s.ScanStatus });

                // Chống quét trùng trong cùng 1 phiếu
                entity.HasIndex(s => new { s.CheckId, s.SerialNumberRaw })
                      .IsUnique()
                      .HasDatabaseName("UQ_InventoryCheckDetailSerials_CheckId_SerialNumberRaw");

                // FK: CheckId → InventoryChecks (cascade delete: xóa phiếu thì xóa serials)
                entity.HasOne(s => s.Check)
                      .WithMany(c => c.DetailSerials)
                      .HasForeignKey(s => s.CheckId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK: DetailId → InventoryCheckDetails (NoAction: detail serial có thể null khi UnknownSurplus)
                entity.HasOne(s => s.Detail)
                      .WithMany(d => d.DetailSerials)
                      .HasForeignKey(s => s.DetailId)
                      .OnDelete(DeleteBehavior.NoAction);

                // FK: SerialId → ProductSerials (NoAction: tránh multiple cascade qua ProductSerial)
                entity.HasOne(s => s.Serial)
                      .WithMany()
                      .HasForeignKey(s => s.SerialId)
                      .OnDelete(DeleteBehavior.NoAction);

                // FK: VariantId → ProductVariants (NoAction)
                entity.HasOne(s => s.Variant)
                      .WithMany()
                      .HasForeignKey(s => s.VariantId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<InventoryAdjustmentLog>(entity =>
            {
                entity.HasQueryFilter(l => !l.AuditCheck.IsDeleted);
                entity.HasIndex(l => l.AuditCheckId);
                entity.HasIndex(l => l.AdjustedDate);
                entity.HasIndex(l => l.SerialId);

                entity.Property(e => e.CostImpact).HasColumnType("decimal(18,2)");

                // FK: AuditCheckId → InventoryChecks (NoAction: log phải tồn tại độc lập với phiếu)
                entity.HasOne(l => l.AuditCheck)
                      .WithMany()
                      .HasForeignKey(l => l.AuditCheckId)
                      .OnDelete(DeleteBehavior.NoAction);

                // FK: SerialId → ProductSerials (NoAction)
                entity.HasOne(l => l.Serial)
                      .WithMany()
                      .HasForeignKey(l => l.SerialId)
                      .OnDelete(DeleteBehavior.NoAction);

                // FK: VariantId → ProductVariants (NoAction)
                entity.HasOne(l => l.Variant)
                      .WithMany()
                      .HasForeignKey(l => l.VariantId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // --- SALE ---
            modelBuilder.Entity<Voucher>(entity =>
            {
                entity.HasQueryFilter(v => !v.IsDeleted);

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Vouchers_Date", "[EndDate] >= [StartDate]");
                    // Quantity nullable: null = unlimited
                    t.HasCheckConstraint("CK_Vouchers_Quantity", "[Quantity] IS NULL OR [UsedCount] <= [Quantity]");
                });
                entity.HasIndex(v => v.Code).IsUnique();
                entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinOrderValue).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<VoucherCategory>(entity =>
            {
                entity.HasQueryFilter(vc => !vc.Voucher.IsDeleted);
                entity.HasKey(vc => new { vc.VoucherId, vc.CategoryId });

                entity.HasOne(vc => vc.Voucher)
                      .WithMany(v => v.VoucherCategories)
                      .HasForeignKey(vc => vc.VoucherId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(vc => vc.Category)
                      .WithMany()
                      .HasForeignKey(vc => vc.CategoryId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasIndex(o => o.OrderCode).IsUnique();
                entity.HasIndex(o => o.UserId);
                entity.HasIndex(o => o.OrderDate);

                entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ShippingFee).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");

                entity.HasOne(o => o.User)
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(o => o.EmployeeId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<VoucherUsage>(entity =>
            {
                entity.HasQueryFilter(vu => !vu.Voucher.IsDeleted);
                // Non-unique index: MaxUsesPerUser cho phép dùng nhiều lần; check bằng count trong service
                entity.HasIndex(vu => new { vu.UserId, vu.VoucherId })
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
                      .OnDelete(DeleteBehavior.Cascade); // Xóa đơn -> xóa usage records
            });

            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.HasIndex(od => od.OrderId);
                entity.HasIndex(od => od.VariantId);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalLine)
                    .HasColumnType("decimal(18,2)")
                    .HasComputedColumnSql("([Quantity] * [UnitPrice])");
            });

            modelBuilder.Entity<OrderSerial>(entity =>
            {
                entity.HasIndex(os => os.SerialId).IsUnique();

                // FIX: SQL Server Error 1785 — Multiple cascade paths detected.
                // Cascade path 1: ProductVariant → ImportReceiptDetail → ImportReceipt → ProductSerial → OrderSerial (CASCADE)
                // Cascade path 2: ProductVariant → OrderDetail → OrderSerial (CASCADE)
                // Cả 2 đường đều cascade đến OrderSerial → SQL Server từ chối.
                // Giải pháp: Đặt NoAction cho cả 2 FK, xử lý xoá bằng Service logic.
                entity.HasOne(os => os.OrderDetail)
                    .WithMany(od => od.OrderSerials)
                    .HasForeignKey(os => os.OrderDetailId)
                    .OnDelete(DeleteBehavior.NoAction); // FIX: Tránh multiple cascade paths

                entity.HasOne(os => os.Serial)
                    .WithMany()
                    .HasForeignKey(os => os.SerialId)
                    .OnDelete(DeleteBehavior.NoAction); // FIX: Tránh multiple cascade paths
            });

            modelBuilder.Entity<Cart>(entity =>
            {
                entity.HasIndex(c => new { c.UserId, c.VariantId }).IsUnique();
                entity.HasIndex(c => c.VariantId);
            });

            modelBuilder.Entity<Warranty>(entity =>
            {
                entity.HasIndex(w => w.SerialId);
                entity.HasIndex(w => w.CustomerId);
                entity.HasIndex(w => w.OrderId);
            });

            modelBuilder.Entity<ProductReview>(entity =>
            {
                entity.HasQueryFilter(r => !r.IsDeleted);

                entity.HasIndex(r => new { r.ProductId, r.UserId })
                      .IsUnique()
                      .HasDatabaseName("UQ_ProductReviews_ProductId_UserId");

                entity.HasIndex(r => r.ProductId)
                      .HasDatabaseName("IX_ProductReviews_ProductId");

                entity.ToTable(t =>
                    t.HasCheckConstraint("CK_ProductReviews_Rating", "[Rating] BETWEEN 1 AND 5"));

                entity.HasOne(r => r.Product)
                      .WithMany()
                      .HasForeignKey(r => r.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.NoAction); // Tránh multiple cascade paths
            });

            // --- SERVICE TICKETS ---
            modelBuilder.Entity<ServiceTicket>(entity =>
            {
                entity.HasQueryFilter(t => !t.IsDeleted);
                entity.HasIndex(t => t.TicketCode).IsUnique();
                entity.HasIndex(t => t.SerialId);
                entity.HasIndex(t => t.Status);
                entity.HasIndex(t => t.CustomerId);
                entity.HasIndex(t => t.AssignedEmployeeId);
                entity.HasIndex(t => t.IntakeDate);

                // Relationship: ServiceTicket -> ProductSerial
                entity.HasOne(t => t.Serial)
                    .WithMany()
                    .HasForeignKey(t => t.SerialId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Relationship: ServiceTicket -> Order
                entity.HasOne(t => t.OriginalOrder)
                    .WithMany()
                    .HasForeignKey(t => t.OriginalOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Relationship: ServiceTicket -> Customer (AppUser)
                entity.HasOne(t => t.Customer)
                    .WithMany()
                    .HasForeignKey(t => t.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Relationship: ServiceTicket -> ReplacementSerial
                entity.HasOne(t => t.ReplacementSerial)
                    .WithMany()
                    .HasForeignKey(t => t.ReplacementSerialId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Navigation properties
                entity.HasMany(t => t.StatusHistory)
                    .WithOne(h => h.Ticket)
                    .HasForeignKey(h => h.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(t => t.Quotations)
                    .WithOne(q => q.Ticket)
                    .HasForeignKey(q => q.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.RmaShipment)
                    .WithOne(r => r.Ticket)
                    .HasForeignKey<RmaShipment>(r => r.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.Invoice)
                    .WithOne(i => i.Ticket)
                    .HasForeignKey<ServiceInvoice>(i => i.TicketId)
                    .OnDelete(DeleteBehavior.NoAction); // Invoice survives ticket soft-delete
            });

            modelBuilder.Entity<ServiceTicketStatusHistory>(entity =>
            {
                entity.HasQueryFilter(h => !h.Ticket.IsDeleted);
                entity.HasIndex(h => new { h.TicketId, h.ChangedAt });
            });

            modelBuilder.Entity<Quotation>(entity =>
            {
                entity.HasQueryFilter(q => !q.Ticket.IsDeleted);
                entity.HasMany(q => q.Items)
                    .WithOne(i => i.Quotation)
                    .HasForeignKey(i => i.QuotationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.LaborCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PartsTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<QuotationItem>(entity =>
            {
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal)
                    .HasColumnType("decimal(18,2)")
                    .HasComputedColumnSql("([Quantity] * [UnitPrice])");
            });

            modelBuilder.Entity<RmaShipment>(entity =>
            {
                entity.HasQueryFilter(r => !r.Ticket.IsDeleted);
                entity.HasIndex(r => r.TicketId).IsUnique();
            });

            modelBuilder.Entity<ServiceInvoice>(entity =>
            {
                // CRITICAL: ServiceInvoice does NOT use HasQueryFilter.
                // Financial records must survive ticket soft-delete.
                entity.HasIndex(i => i.InvoiceCode).IsUnique();
                entity.HasIndex(i => i.TicketId).IsUnique();

                entity.Property(e => e.LaborCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PartsTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");

                // Relationship: ServiceInvoice -> Quotation (optional)
                entity.HasOne(i => i.Quotation)
                    .WithMany()
                    .HasForeignKey(i => i.QuotationId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Navigation: ServiceInvoice -> Items
                entity.HasMany(i => i.Items)
                    .WithOne(ii => ii.Invoice)
                    .HasForeignKey(ii => ii.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ServiceInvoiceItem>(entity =>
            {
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal)
                    .HasColumnType("decimal(18,2)")
                    .HasComputedColumnSql("([Quantity] * [UnitPrice])");
            });

            modelBuilder.Entity<SerialRepairLog>(entity =>
            {
                entity.HasIndex(l => l.SerialId);

                // Relationship: SerialRepairLog -> ProductSerial (required)
                entity.HasOne(l => l.Serial)
                    .WithMany()
                    .HasForeignKey(l => l.SerialId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Relationship: SerialRepairLog -> ServiceTicket (optional, SetNull on delete)
                entity.HasOne(l => l.Ticket)
                    .WithMany()
                    .HasForeignKey(l => l.TicketId)
                    .OnDelete(DeleteBehavior.SetNull); // Log survives ticket deletion

                // Relationship: SerialRepairLog -> ReplacedBySerial (optional)
                entity.HasOne(l => l.ReplacedBySerial)
                    .WithMany()
                    .HasForeignKey(l => l.ReplacedBySerialId)
                    .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
