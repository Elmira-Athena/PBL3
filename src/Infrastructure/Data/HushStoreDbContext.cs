using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

        // Auth
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

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
                entity.Property(e => e.Specifications)
                      .HasColumnType("nvarchar(max)")
                      .HasConversion(
                          v => System.Text.Json.JsonSerializer.Serialize(v, System.Text.Json.JsonSerializerOptions.Default),
                          v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, System.Text.Json.JsonSerializerOptions.Default)
                               ?? new Dictionary<string, string>()
                      );
            });

            // --- PRODUCT ---
            // Global Query Filter: Tự động bỏ qua Manufacturer đã bị xoá mềm
            modelBuilder.Entity<Manufacturer>(entity =>
            {
                entity.HasQueryFilter(m => !m.IsDeleted);
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
                entity.HasIndex(r => r.ReceiptCode).IsUnique();
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<ImportReceiptDetail>(entity =>
            {
                entity.HasIndex(d => d.ReceiptId);
                entity.HasIndex(d => d.VariantId);
                entity.Property(e => e.ImportPrice).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<InventoryCheckDetail>(entity =>
            {
                entity.Property(e => e.Difference)
                      .HasComputedColumnSql("([ActualQuantity] - [SystemQuantity])");
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
        }
    }
}
