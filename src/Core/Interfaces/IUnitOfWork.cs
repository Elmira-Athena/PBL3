namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Unit of Work Pattern — Quản lý Transaction xuyên suốt nhiều Repository.
    /// Service inject interface này thay vì inject trực tiếp DbContext.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Bắt đầu một Database Transaction mới.
        /// </summary>
        Task BeginTransactionAsync();

        /// <summary>
        /// Commit Transaction hiện tại.
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Rollback Transaction hiện tại.
        /// </summary>
        Task RollbackAsync();

        /// <summary>
        /// Lưu tất cả thay đổi pending trong DbContext xuống Database.
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
