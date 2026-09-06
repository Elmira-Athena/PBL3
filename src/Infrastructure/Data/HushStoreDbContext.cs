using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PBL3.Core.Constants;
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

        /// <summary>
        /// Bộ đếm rate limit dùng chung giữa các task API — xem <see cref="RateLimitCounter"/>.
        /// Đây là điều kiện mà <c>infra/tf/modules/ecs/variables.tf</c> đòi trước khi cho
        /// <c>max_instance_count &gt; 1</c>.
        /// </summary>
        public DbSet<RateLimitCounter> RateLimitCounters { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Identity mappings

            // ── citext: khôi phục ngữ nghĩa case-insensitive của SQL Server (đợt 7) ──
            //
            // 🔴 SQL Server dùng collation CI mặc định, nên 25 chỗ so sánh chuỗi trong repo đang
            // đúng NHỜ CẤU HÌNH DB — không có HasCollation nào trong toàn bộ src/. PostgreSQL
            // phân biệt hoa/thường ⇒ 25 chỗ đó đổi hành vi mà KHÔNG ném lỗi, KHÔNG ghi log.
            // Nặng nhất là hai chỗ trả HTTP 200 trong khi làm sai: PosService gắn voucher, và
            // VoucherRepository.TryConsumeByCodesAsync báo "tiêu thụ thành công" mà không trừ lượt.
            //
            // Vì sao citext chứ không phải chuẩn hoá bằng code: BA đường GHI của chính các cột này
            // không chuẩn hoá (ProductVariantService, ProductService, ImportReceiptService chỉ
            // .Trim()). Phương án code đòi mọi write path hiện tại VÀ tương lai nhớ gọi .ToUpper(),
            // và không gì bắt lỗi khi quên — đúng loại lỗi âm thầm mà đợt này đang diệt.
            // citext sửa ở TẦNG LƯU TRỮ nên đúng cho cả call-site chưa ai viết.
            //
            // Vì sao KHÔNG dùng collation non-deterministic: đã đo trên PG 17.11 —
            //   ERROR: nondeterministic collations are not supported for LIKE
            // Mà 8 chỗ .Contains() dịch thẳng thành LIKE. Nó biến bug im lặng thành exception
            // lúc chạy ở ô tìm kiếm: đổi LOẠI lỗi chứ không sửa lỗi.
            // Bằng chứng: docs/evidence/2026-09-03-baseline-hoa-thuong-truoc-postgresql.md §6
            //
            // ⚠️ Khai bằng HasPostgresExtension (cấu hình model) chứ KHÔNG bằng migrationBuilder.Sql:
            // SQL thô thì snapshot không nhớ, và lần sinh migration sau sẽ mất extension.
            modelBuilder.HasPostgresExtension("citext");

            // ── Concurrency token: xmin (đợt 7, thay `rowversion` của SQL Server) ──
            //
            // 🔴 VÌ SAO CẦN — lý do này KHÔNG đổi theo provider. LoadProbe S06 đo được sổ tổn thất
            // nhân 5: năm lần phê duyệt song song cùng một phiếu kiểm kê, cả năm đều 200, và một
            // serial khách vừa mua bị ghi đè thẳng từ Sold sang Lost. Nguyên nhân: EF sinh
            // `UPDATE "ProductSerials" SET "Status"=5 WHERE "Id"=@p` — KHÔNG có mệnh đề trạng thái
            // nào. Có token thì thành `… WHERE "Id"=@p AND xmin=@v` → 0 dòng bị ảnh hưởng →
            // DbUpdateConcurrencyException → transaction rollback → all-or-nothing cho cả lần
            // phê duyệt, đúng điều mong muốn.
            //
            // VÌ SAO SHADOW PROPERTY chứ không phải thuộc tính `uint` trên entity: giữ thuộc tính
            // chỉ có giá trị khi client phải GỬI LẠI token. Repo không làm thế — mọi chốt
            // concurrency là read-modify-write TRONG CÙNG một request, nên token chỉ cần sống
            // trong đời DbContext. Đã kiểm: `RowVersion` xuất hiện 0 lần ở src/Shared và
            // src/Client, và 0 chỗ nào đọc hay gán nó. Shadow property là dạng ít xâm lấn nhất.
            // Muốn đọc token thì `context.Entry(e).Property("xmin")`.
            //
            // ⚠️ GIỚI HẠN 1 — xung đột GIẢ. xmin đổi khi BẤT KỲ cột nào của hàng đổi, không riêng
            // cột ta quan tâm. Hai thao tác sửa hai cột KHÁC NHAU vẫn đụng nhau. Đó là cái giá của
            // token cấp-hàng; đừng "sửa" bằng cách bỏ token. Giống hệt `rowversion` cũ.
            //
            // 🚨 GIỚI HẠN 2 — ExecuteUpdateAsync BỎ QUA HOÀN TOÀN token này. Nó đi thẳng xuống SQL,
            // không qua Change Tracker. Ai đó chuyển một đường ghi từ tracked-write sang
            // ExecuteUpdate sẽ ÂM THẦM GỠ MẤT lớp bảo vệ, và không gì báo lỗi — comment này là thứ
            // duy nhất chặn điều đó. Nếu chuyển, phải tự đưa vị từ trạng thái vào Where (mẫu:
            // InventoryCheckService.ApproveAsync). Repo hiện có 19 chỗ ExecuteUpdateAsync và
            // KHÔNG chỗ nào trong số đó được token bảo vệ.
            //
            // ⚠️ GIỚI HẠN 3 — RIÊNG của xmin, không có ở rowversion: xmin là 32-bit và QUAY VÒNG.
            // Về lý thuyết một hàng không đổi suốt một chu kỳ wraparound rồi bị sửa đúng lúc trùng
            // giá trị cũ sẽ bỏ lọt xung đột. Xác suất ở quy mô này bằng 0 — ghi ra để người phát
            // hiện lại sau này không phải hoảng.
            // ⚠️ KHÔNG dùng `UseXminAsConcurrencyToken()` — hàm đó ĐÃ BỊ GỠ khỏi Npgsql 10.
            // Đã kiểm bằng cách soi assembly 10.0.3: không có chuỗi "xmin" nào trong đó. Tài liệu
            // và câu trả lời trên mạng vẫn nhắc tên hàm này rất nhiều, nên đây là chỗ dễ chép nhầm.
            // Khai tay dưới đây CHÍNH LÀ thứ hàm cũ làm bên trong — xem UseXmin().
            UseXmin<ProductSerial>(modelBuilder);
            UseXmin<InventoryCheck>(modelBuilder);
            UseXmin<ServiceTicket>(modelBuilder);
            UseXmin<Quotation>(modelBuilder);
            UseXmin<RmaShipment>(modelBuilder);
            UseXmin<Order>(modelBuilder);

            // ── SEQUENCE cấp số cho mã chứng từ (đợt 3, mục 🅶) ──
            //
            // 🔴 VÌ SAO SEQUENCE, VÀ VÌ SAO KHÔNG RESET THEO NGÀY.
            // Thuật toán cũ là check-then-act: đọc mọi mã trong ngày, lấy max, +1.
            // N request đồng thời ĐỀU tính ra cùng một giá trị, nên unique index chặn
            // và người thua nhận lỗi. LoadProbe S01 đo được: 32–41/50 đơn hỏng.
            //
            // Thứ GÂY RA race chính là yêu cầu "reset mỗi ngày" — nó bắt buộc phải có
            // câu SELECT MAX để biết hôm nay đã tới đâu. Bỏ việc reset là bỏ NGUYÊN NHÂN,
            // không phải vá triệu chứng. Vì vậy sequence ở đây là TOÀN CỤC, không reset;
            // phần ngày trong mã (`PREFIX-yyyyMMdd-NNNNNN`) giữ lại chỉ để mã còn đọc
            // được và còn sắp đúng thời gian.
            //
            // ⚠️ VÌ SAO KHÔNG chọn "bắt unique-violation rồi retry" làm phương án chính:
            // với bộ cấp phát SELECT MAX, mỗi vòng retry chỉ cho ĐÚNG MỘT người qua
            // => cần O(N) vòng, mỗi vòng một round-trip. Ở 50 request đồng thời đó là
            // hàng nghìn round-trip. Retry là công cụ cho va chạm HIẾM, không phải va
            // chạm CHẮC CHẮN.
            //
            // ⚠️ TRẦN ĐỘ RỘNG CỘT — kiểm trước khi đổi định dạng. Cột mã là nvarchar(20).
            // Vì sequence không reset, con số lớn dần mãi:
            //     "ORD-yyyyMMdd-" (13) + 6 chữ số = 19  ✓
            //     tiền tố 3 ký tự (ORD/POS/SRV) chịu được tối đa 7 chữ số = 20  ✓
            //                                            8 chữ số = 21  ✗ TRÀN
            // => trần thực tế là 9.999.999 chứng từ cho mỗi sequence có tiền tố 3 ký tự
            // (99.999.999 cho PN/KK/ST). Việc so sánh CHUỖI đã bị bỏ hẳn nên số rộng thêm
            // không gây sai như quả bom {n:D3} cũ — chỉ độ rộng cột mới là giới hạn thật.
            foreach (var seq in PBL3.Core.Interfaces.DocumentSequences.All)
            {
                // StartAt 1000: chừa khoảng cho dữ liệu đã có mã sinh bằng thuật toán cũ,
                // để mã mới không đụng unique index với mã cũ trong CÙNG một ngày triển khai.
                modelBuilder.HasSequence<long>(seq).StartsAt(1000).IncrementsBy(1);
            }

            // --- AUTH: Rename Identity tables theo convention ---
            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("AppUsers");
                entity.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
                // IsUnicode(false) đã gỡ: trên SQL Server nó cho varchar thay nvarchar; PostgreSQL
                // mọi text đều UTF-8 nên Npgsql bỏ qua — giữ lại là code chết gây hiểu nhầm.
                entity.Property(u => u.PhoneNumber).HasMaxLength(20);
            });
            modelBuilder.Entity<RateLimitCounter>(entity =>
            {
                entity.ToTable("RateLimitCounters");

                // Khoá chính GHÉP (PartitionKey, WindowStart) — và nó phải là KHOÁ CHÍNH, không
                // phải chỉ một index. `ON CONFLICT ("PartitionKey", "WindowStart")` trong
                // PostgresRateLimitStore chỉ hợp lệ khi có một ràng buộc unique đúng trên cặp
                // cột đó; thiếu nó thì câu lệnh KHÔNG chạy được — hỏng lúc chạy, không lúc
                // biên dịch, và hỏng ở đúng đường đăng nhập.
                entity.HasKey(c => new { c.PartitionKey, c.WindowStart });

                // 128 ký tự đủ cho "PolicyName:IPv6". Không để mặc định text vô hạn: khoá chính
                // là nơi PostgreSQL phải so sánh mọi lần upsert.
                entity.Property(c => c.PartitionKey).HasMaxLength(128).IsRequired();

                entity.Property(c => c.WindowStart).HasColumnType("timestamp with time zone");
                entity.Property(c => c.Count).IsRequired();

                // Index để RateLimitCounterCleanupService quét theo thời gian. Không có nó thì
                // câu DELETE dọn rác phải seq-scan cả bảng.
                entity.HasIndex(c => c.WindowStart)
                      .HasDatabaseName("IX_RateLimitCounters_WindowStart");

                // ⚠️ KHÔNG có global query filter IsDeleted ở đây, và cũng KHÔNG có cột đó.
                // Đây là dữ liệu vận hành sống ngắn; soft-delete chỉ làm bảng phình.
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
                entity.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
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
                // citext (Products.Slug): unique index này đổi nghĩa ngầm nếu để `text`.
                entity.Property(p => p.Slug).HasColumnType("citext");
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Slug).IsUnique();
                // citext (Categories.Slug): unique index này đổi nghĩa ngầm nếu để `text`.
                entity.Property(c => c.Slug).HasColumnType("citext");
                // Recursive Relationship (Adjacency List)
                entity.HasOne(c => c.Parent)
                    .WithMany(p => p.Children)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ProductVariant>(entity =>
            {
                entity.HasIndex(v => v.SKU).IsUnique();
                // citext (ProductVariants.SKU): unique index này đổi nghĩa ngầm nếu để `text`.
                entity.Property(v => v.SKU).HasColumnType("citext");
                entity.HasIndex(v => v.ProductId);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OriginalPrice).HasColumnType("decimal(18,2)");

                // StockQuantity - cột vật lý, default = 0
                entity.Property(e => e.StockQuantity)
                      .HasDefaultValue(0);

                // Specifications - JSON column
                var specComparer = new ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => c1 == c2 || (c1 != null && c2 != null &&
                                c1.Count == c2.Count && !c1.Except(c2).Any()),
                    c => c == null ? 0 : c.Aggregate(0, (a, p) =>
                                HashCode.Combine(a, p.Key.GetHashCode(),
                                    p.Value == null ? 0 : p.Value.GetHashCode())),
                    c => new Dictionary<string, string>(c ?? new())
                );
                entity.Property(e => e.Specifications)
                      // jsonb, không phải text: ProductRepository.FilterBySpecificationAsync
                      // cần truy vấn VÀO TRONG JSON (trước đây bằng JSON_VALUE của SQL Server).
                      .HasColumnType("jsonb")
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
                // citext (ProductSerials.SerialNumber): unique index này đổi nghĩa ngầm nếu để `text`.
                entity.Property(s => s.SerialNumber).HasColumnType("citext");
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
                      .HasComputedColumnSql("(\"ActualQuantity\" - \"SystemQuantity\")", stored: true);
            });

            modelBuilder.Entity<InventoryCheckDetailSerial>(entity =>
            {
                entity.HasQueryFilter(s => !s.Check.IsDeleted);
                entity.HasIndex(s => s.CheckId);
                entity.HasIndex(s => new { s.CheckId, s.ScanStatus });

                // Chống quét trùng trong cùng 1 phiếu
                // citext (InventoryCheckDetailSerials.SerialNumberRaw): cột thứ SÁU của nhóm,
                // và là cột dễ sót nhất vì nó nằm trong index TỔ HỢP chứ không phải
                // `HasIndex(x).IsUnique()` đơn lẻ như năm cột kia. Bất biến "một serial chỉ
                // xuất hiện một lần trong một phiếu kiểm kê" đổi nghĩa ngầm nếu để `text`:
                // nhân viên quét 'abc123' rồi gõ tay 'ABC123' sẽ tạo được HAI dòng.
                entity.Property(s => s.SerialNumberRaw).HasColumnType("citext");
                entity.HasIndex(s => new { s.CheckId, s.SerialNumberRaw })
                      .IsUnique()
                      .HasDatabaseName(DbConstraints.InventoryCheckDetailSerial);

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

                // 🔴 Một serial chỉ được ghi tổn thất ĐÚNG MỘT LẦN trong một phiếu kiểm kê.
                // Trước đây chỉ có 3 index ĐƠN ở trên, không có unique tổ hợp — LoadProbe S06
                // đo được sổ tổn thất nhân 5 (5 lần approve song song, cả 5 đều 200), và
                // pre_migration_checks.sql CHECK 1b định lượng được thiệt hại: CostImpact bị
                // tính THỪA 3.200.000₫ trên đúng một cặp (phiếu, serial).
                //
                // Đây là phòng tuyến THỨ HAI. Phòng tuyến thứ nhất là conditional update ở
                // InventoryCheckService.ApproveAsync. Giữ cả hai: cái thứ nhất cho thông báo
                // tử tế, cái thứ hai để dữ liệu không hỏng được kể cả khi ai đó viết đường ghi mới.
                entity.HasIndex(l => new { l.AuditCheckId, l.SerialId })
                      .IsUnique()
                      .HasDatabaseName(DbConstraints.InventoryAdjustmentLogAuditSerial);

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
                    t.HasCheckConstraint("CK_Vouchers_Date", "\"EndDate\" >= \"StartDate\"");
                    // Quantity nullable: null = unlimited
                    t.HasCheckConstraint("CK_Vouchers_Quantity", "\"Quantity\" IS NULL OR \"UsedCount\" <= \"Quantity\"");
                });
                entity.HasIndex(v => v.Code).IsUnique();
                // citext (Vouchers.Code): unique index này đổi nghĩa ngầm nếu để `text`.
                entity.Property(v => v.Code).HasColumnType("citext");
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

                // 🔴 UNIQUE, và cột thứ ba là thứ làm nó ĐÚNG với mọi MaxUsesPerUser.
                // Bản cũ là index THƯỜNG với comment "check bằng count trong service" — chính
                // cái "check bằng count" đó là check-then-act mà LoadProbe S03 đã bắt: 1 khách
                // dùng 10 lần một mã MaxUsesPerUser=1, cả 10 request đều 200.
                //
                // ⚠️ Vì sao KHÔNG phải unique (UserId, VoucherId): xem VoucherUsage.SeqPerUser.
                // Tóm lại — validator cho phép MaxUsesPerUser = 3, nên index 2 cột sẽ chặn oan
                // lần dùng thứ hai của một voucher hoàn toàn hợp lệ.
                //
                // 🚨 HasQueryFilter ở trên KHÔNG áp cho index: unique index sống ở tầng DB và
                // không biết gì về voucher đã soft-delete. Đó là hành vi ĐÚNG ở đây (một lần
                // dùng đã xảy ra thì vẫn đã xảy ra), nhưng đừng suy rộng sang index khác.
                entity.HasIndex(vu => new { vu.UserId, vu.VoucherId, vu.SeqPerUser })
                      .IsUnique()
                      .HasDatabaseName(DbConstraints.VoucherUsagePerUserSeq);

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
                    .HasComputedColumnSql("(\"Quantity\" * \"UnitPrice\")", stored: true);
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
                      .HasDatabaseName(DbConstraints.ProductReviewProductUser);

                entity.HasIndex(r => r.ProductId)
                      .HasDatabaseName("IX_ProductReviews_ProductId");

                entity.ToTable(t =>
                    t.HasCheckConstraint("CK_ProductReviews_Rating", "\"Rating\" BETWEEN 1 AND 5"));

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

                // 🔴 Mỗi serial tối đa MỘT phiếu chưa đóng. LoadProbe S04 đo được 2 phiếu chưa
                // đóng trên cùng một serial — và nó chỉ vỡ khi có HAI instance, nên đây đúng là
                // loại lỗi mà hạ tầng 2 replica sinh ra để bắt. Cửa sổ check-then-act của
                // HasOpenTicketForSerialAsync đủ hẹp để một tiến trình che được.
                //
                // 🚨 VỊ TỪ NÀY PHẢI LUÔN KHỚP VỚI ServiceTicketRepository.HasOpenTicketForSerialAsync
                // (terminalStates = 3, 8, 9, 10 + query filter !IsDeleted). Thêm một trạng thái
                // terminal ở đó mà quên ở đây thì bất biến ÂM THẦM NỚI RA — không có gì báo lỗi.
                // Hai chỗ này comment chéo nhau; đọc một chỗ thì sang chỗ kia.
                //
                // ⚠️ Giữ nguyên dạng BUNG `<>` thay vì gộp lại `NOT IN`. Giới hạn "filtered index
                // không cho phép NOT IN" là của SQL SERVER — PostgreSQL cho phép. Nhưng vị từ này
                // phải khớp NGUYÊN VĂN với HasOpenTicketForSerialAsync, nên đổi cách viết chỉ để
                // gọn hơn là tự tạo rủi ro lệch, không đổi lấy gì.
                //
                // 🚨 `IsDeleted` là `boolean` trên PostgreSQL, không phải `bit`.
                // `"IsDeleted" = 0` sẽ LỖI KIỂU, phải là `= false`.
                entity.HasIndex(t => t.SerialId)
                      .IsUnique()
                      .HasFilter("\"Status\" <> 3 AND \"Status\" <> 8 AND \"Status\" <> 9 AND \"Status\" <> 10 AND \"IsDeleted\" = false")
                      .HasDatabaseName(DbConstraints.ServiceTicketSerialOpen);
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
                    .HasComputedColumnSql("(\"Quantity\" * \"UnitPrice\")", stored: true);
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
                    .HasComputedColumnSql("(\"Quantity\" * \"UnitPrice\")", stored: true);
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

        /// <summary>
        /// Khai cột hệ thống <c>xmin</c> của PostgreSQL làm concurrency token cho <typeparamref name="TEntity"/>.
        /// </summary>
        /// <remarks>
        /// Đây là bản viết tay thay cho <c>UseXminAsConcurrencyToken()</c> — hàm tiện ích đó
        /// <b>không còn</b> trong Npgsql 10 (đã soi assembly 10.0.3 để xác nhận, không phải suy đoán).
        ///
        /// Bốn mảnh, thiếu mảnh nào cũng hỏng một kiểu khác nhau:
        /// <list type="bullet">
        ///   <item><c>Property&lt;uint&gt;("xmin")</c> — <b>shadow property</b>, không có thuộc tính
        ///     tương ứng trên entity. Đó là chủ ý: repo không bao giờ gửi token về client, mọi chốt
        ///     concurrency là read-modify-write trong cùng một request.</item>
        ///   <item><c>HasColumnType("xid")</c> — kiểu thật của <c>xmin</c> trong PostgreSQL.</item>
        ///   <item><c>ValueGeneratedOnAddOrUpdate()</c> — DB tự sinh, EF không được ghi.
        ///     Thiếu nó thì EF cố INSERT vào một cột hệ thống chỉ-đọc.</item>
        ///   <item><c>IsConcurrencyToken()</c> — mảnh làm nên tác dụng: đưa cột vào mệnh đề
        ///     <c>WHERE</c> của <c>UPDATE</c>. <b>Thiếu riêng mảnh này thì mọi thứ vẫn chạy, vẫn
        ///     build, và bảo vệ biến mất hoàn toàn mà không có gì báo.</b></item>
        /// </list>
        /// </remarks>
        private static void UseXmin<TEntity>(ModelBuilder modelBuilder) where TEntity : class
            => modelBuilder.Entity<TEntity>()
                           .Property<uint>("xmin")
                           .HasColumnName("xmin")
                           .HasColumnType("xid")
                           .ValueGeneratedOnAddOrUpdate()
                           .IsConcurrencyToken();
    }
}
