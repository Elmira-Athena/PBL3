using Microsoft.EntityFrameworkCore.Storage;
using PBL3.Core.Interfaces;

namespace PBL3.Infrastructure.Data
{
    /// <summary>
    /// Unit of Work implementation — bọc HushStoreDbContext.
    /// Quản lý IDbContextTransaction cho các nghiệp vụ phức tạp (nhập kho, đặt hàng...).
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly HushStoreDbContext _context;
        private IDbContextTransaction? _transaction;

        public UnitOfWork(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitAsync()
        {
            if (_transaction == null)
                throw new InvalidOperationException("Chưa có Transaction nào được mở.");

            await _transaction.CommitAsync();
        }

        public async Task RollbackAsync()
        {
            if (_transaction == null)
                throw new InvalidOperationException("Chưa có Transaction nào được mở.");

            await _transaction.RollbackAsync();
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
