namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Unit of Work Pattern — Quản lý Transaction xuyên suốt nhiều Repository.
    /// Service inject interface này thay vì inject trực tiếp DbContext.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Chạy <paramref name="operation"/> trong một transaction do execution strategy
        /// của EF Core điều phối, rồi commit. Ném ra ngoài thì rollback.
        /// </summary>
        /// <remarks>
        /// ĐÂY LÀ CÁCH DUY NHẤT ĐƯỢC DÙNG ĐỂ MỞ TRANSACTION.
        ///
        /// Lý do: EF Core CẤM gọi <c>BeginTransactionAsync()</c> thủ công khi connection
        /// đã bật <c>EnableRetryOnFailure</c> — strategy không biết phải chạy lại từ đâu,
        /// nên nó ném <c>InvalidOperationException</c> LÚC CHẠY, không lúc biên dịch.
        /// Bọc qua <c>CreateExecutionStrategy()</c> là cách hợp lệ duy nhất để có cả hai.
        ///
        /// ⚠️ KHI BẬT EnableRetryOnFailure: delegate này chạy lại TOÀN BỘ mỗi lần thử.
        /// Mọi thứ tính toán TRƯỚC khi gọi phải hoặc là idempotent, hoặc phải chuyển
        /// vào bên trong delegate. Change Tracker KHÔNG được clear giữa các lần thử
        /// (clear sẽ tháo mất các entity đã nạp trước đó ở nhiều call-site hiện tại),
        /// nên phải rà từng chỗ trước khi bật retry, không bật hàng loạt.
        /// </remarks>
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation);

        /// <inheritdoc cref="ExecuteInTransactionAsync{T}(Func{Task{T}})"/>
        Task ExecuteInTransactionAsync(Func<Task> operation);

        /// <summary>
        /// Lưu tất cả thay đổi pending trong DbContext xuống Database.
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
