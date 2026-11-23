using Microsoft.EntityFrameworkCore;

namespace PBL3.Infrastructure.Data
{
    /// <summary>
    /// DbContext chỉ-đọc định tuyến tới RDS Read Replica (khi <c>ConnectionStrings:ReadOnlyConnection</c>
    /// được cấu hình; ngược lại fallback về primary).
    /// </summary>
    /// <remarks>
    /// KHÔNG dùng cho đường ghi, Identity/phân quyền, kiểm tra <c>IsActive</c>, hay các luồng
    /// ghi-rồi-đọc-lại (vì replication bất đồng bộ có độ trễ).
    /// </remarks>
    public sealed class ReadOnlyHushStoreDbContext : HushStoreDbContext
    {
        public ReadOnlyHushStoreDbContext(DbContextOptions<ReadOnlyHushStoreDbContext> options) : base(options)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public override int SaveChanges()
            => throw new InvalidOperationException(
                "ReadOnlyHushStoreDbContext chỉ dùng cho truy vấn đọc, không cho phép ghi dữ liệu.");

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
            => throw new InvalidOperationException(
                "ReadOnlyHushStoreDbContext chỉ dùng cho truy vấn đọc, không cho phép ghi dữ liệu.");

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "ReadOnlyHushStoreDbContext chỉ dùng cho truy vấn đọc, không cho phép ghi dữ liệu.");

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "ReadOnlyHushStoreDbContext chỉ dùng cho truy vấn đọc, không cho phép ghi dữ liệu.");
    }
}
