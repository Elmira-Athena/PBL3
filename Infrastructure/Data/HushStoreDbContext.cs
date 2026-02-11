using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;

namespace PBL3.Infrastructure.Data
{
    public class HushStoreDbContext : DbContext
    {
        public HushStoreDbContext(DbContextOptions<HushStoreDbContext> options) : base(options)
        {
        }

        // Auth
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<AppRole> AppRoles { get; set; }
        public DbSet<AppUserRole> AppUserRoles { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        // Product
        public DbSet<Manufacturer> Manufacturers { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<ProductAttribute> ProductAttributes { get; set; }
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
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<OrderSerial> OrderSerials { get; set; }
        public DbSet<Cart> Carts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- AUTH API ---
            modelBuilder.Entity<AppUserRole>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.RoleId });

                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(u => u.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(r => r.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AppRole>()
                .HasIndex(r => r.RoleCode)
                .IsUnique();
            
            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.NormalizedUserName).HasFilter("[NormalizedUserName] IS NOT NULL");
            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.NormalizedEmail).HasFilter("[NormalizedEmail] IS NOT NULL");

            // --- PRODUCT API ---
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Slug).IsUnique();
                // Recursive Relationship
                entity.HasOne(c => c.Parent)
                    .WithMany(p => p.Children)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.NoAction); // Avoid cycles on delete
            });

            modelBuilder.Entity<ProductVariant>(entity =>
            {
                entity.HasIndex(v => v.SKU).IsUnique();
                // StockQuantity is computed from ProductSerials where Status = Available
                // But in this SQL schema, there is a DEFAULT 0. 
                // The tech doc says it's Computed in Logic or DB? 
                // "StockQuantity ... trong bảng Product là con số Computed". 
                // But ProductVariant has a column StockQuantity in the SQL script "DEFAULT 0".
                // I will leave it as a regular column for now, managed by Service Logic as instructed 
                // in "AI phải viết code trigger hoặc service logic".
            });

            // --- INVENTORY API ---
            modelBuilder.Entity<ProductSerial>(entity =>
            {
                entity.HasIndex(s => s.SerialNumber).IsUnique();
                entity.HasIndex(s => new { s.VariantId, s.Status }); // Inventory count index
            });

            modelBuilder.Entity<ImportReceipt>(entity =>
            {
                entity.HasIndex(r => r.ReceiptCode).IsUnique();
            });

            modelBuilder.Entity<InventoryCheckDetail>(entity =>
            {
                entity.Property(e => e.Difference)
                      .HasComputedColumnSql("([ActualQuantity] - [SystemQuantity])");
            });

            // --- SALE API ---
            modelBuilder.Entity<Voucher>(entity =>
            {
                entity.HasIndex(v => v.Code).IsUnique();
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasIndex(o => o.OrderCode).IsUnique();
                
                // EmployeeId is nullable foreign key to AppUser, but not explicitly defined Navigation 
                // in Entity class if not needed, but good to have constraint
                entity.HasOne(o => o.User)
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                // We can model Emloyee relation if we want
                 entity.HasOne<AppUser>()
                     .WithMany()
                     .HasForeignKey(o => o.EmployeeId)
                     .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.Property(e => e.TotalLine)
                    .HasColumnType("decimal(18,2)")
                    .HasComputedColumnSql("([Quantity] * [UnitPrice])");
            });

            modelBuilder.Entity<OrderSerial>(entity =>
            {
                entity.HasIndex(os => os.SerialId).IsUnique(); // 1 Serial only in 1 order line context
            });

            modelBuilder.Entity<Cart>(entity =>
            {
                entity.HasIndex(c => new { c.UserId, c.VariantId }).IsUnique();
            });

            // --- DECIMAL PRECISION (decimal(18,2) theo SQL schema) ---
            // ProductVariant
            modelBuilder.Entity<ProductVariant>(entity =>
            {
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OriginalPrice).HasColumnType("decimal(18,2)");
            });

            // ImportReceiptDetail
            modelBuilder.Entity<ImportReceiptDetail>(entity =>
            {
                entity.Property(e => e.ImportPrice).HasColumnType("decimal(18,2)");
            });

            // ImportReceipt
            modelBuilder.Entity<ImportReceipt>(entity =>
            {
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            });

            // Voucher
            modelBuilder.Entity<Voucher>(entity =>
            {
                entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinOrderValue).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18,2)");
                entity.HasCheckConstraint("CK_Vouchers_Date", "[EndDate] >= [StartDate]");
            });

            // Order
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ShippingFee).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(o => o.UserId);
                entity.HasIndex(o => o.OrderDate);
            });

            // OrderDetail
            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            });
        }
    }
}
