using Microsoft.EntityFrameworkCore;
using PBL3.Core.Interfaces;

namespace PBL3.Infrastructure.Data
{
    /// <summary>
    /// Unit of Work implementation — bọc HushStoreDbContext.
    /// Quản lý transaction cho các nghiệp vụ phức tạp (nhập kho, đặt hàng...).
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly HushStoreDbContext _context;

        public UnitOfWork(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            // Execution strategy hiện tại là bản không retry, nên delegate chạy đúng
            // một lần và hành vi giống hệt cách viết BeginTransaction/Commit cũ.
            // Bọc sẵn qua đây để lúc bật EnableRetryOnFailure không phải sửa call-site.
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                // using: transaction được Dispose kể cả khi commit/rollback ném.
                // Bản cũ giữ transaction trong field và không bao giờ Dispose —
                // connection bị giữ lại cho tới khi scope của DbContext kết thúc.
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var result = await operation();

                await transaction.CommitAsync();
                return result;
            });
        }

        public async Task ExecuteInTransactionAsync(Func<Task> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            await ExecuteInTransactionAsync(async () =>
            {
                await operation();
                return true;
            });
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
