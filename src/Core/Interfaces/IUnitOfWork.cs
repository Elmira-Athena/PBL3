using System.Runtime.CompilerServices;

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
        /// <param name="operation">Phần việc chạy bên trong transaction.</param>
        /// <param name="retrySafe">
        /// Chỉ đặt <c>true</c> khi call-site ĐÃ ĐƯỢC RÀ và chứng minh là chạy lại được.
        /// Mặc định <c>false</c> — xem phần "HỢP ĐỒNG RETRY" bên dưới.
        /// </param>
        /// <remarks>
        /// ĐÂY LÀ CÁCH DUY NHẤT ĐƯỢC DÙNG ĐỂ MỞ TRANSACTION.
        ///
        /// Lý do: EF Core CẤM gọi <c>BeginTransactionAsync()</c> thủ công khi connection
        /// đã bật <c>EnableRetryOnFailure</c> — strategy không biết phải chạy lại từ đâu,
        /// nên nó ném <c>InvalidOperationException</c> LÚC CHẠY, không lúc biên dịch.
        /// Bọc qua <c>CreateExecutionStrategy()</c> là cách hợp lệ duy nhất để có cả hai.
        ///
        /// ═══ HỢP ĐỒNG RETRY ═══
        ///
        /// <c>EnableRetryOnFailure</c> ĐÃ BẬT (đợt 1, mục 4.1). Nhưng bật retry KHÔNG có
        /// nghĩa là mọi transaction tự động chạy lại được. Khi strategy thử lại, delegate
        /// chạy lại TOÀN BỘ trên CÙNG MỘT DbContext, và Change Tracker KHÔNG tự sạch.
        ///
        /// Chế độ hỏng nguy hiểm nhất — MẤT DỮ LIỆU ÂM THẦM:
        ///   Nếu lỗi transient rơi đúng lúc <c>CommitAsync</c>, thì mọi
        ///   <c>SaveChangesAsync</c> BÊN TRONG đã thành công rồi. EF đã đánh dấu các
        ///   entity là Unchanged và cập nhật snapshot giá trị gốc thành giá trị MỚI.
        ///   Lần thử thứ hai gán lại đúng giá trị đó (<c>ticket.Status = 2</c>) thì EF
        ///   thấy KHÔNG CÓ THAY ĐỔI => KHÔNG sinh câu UPDATE nào => hàng dữ liệu (đã bị
        ///   rollback về giá trị cũ) GIỮ NGUYÊN GIÁ TRỊ CŨ. Không exception, không log.
        ///
        /// Vì vậy <paramref name="retrySafe"/> mặc định là <c>false</c>, và khi đó lần
        /// thử thứ hai sẽ NÉM RA LỖI RÕ RÀNG kèm tên call-site thay vì làm hỏng dữ liệu.
        /// Hành vi mà người dùng thấy giống hệt trước khi bật retry: request lỗi.
        ///
        /// Điều kiện để được đặt <c>retrySafe: true</c> — phải thoả CẢ BA:
        ///   1. Mọi entity bị GHI đều được NẠP BÊN TRONG delegate (không nạp sẵn ở ngoài
        ///      rồi sửa ở trong — entity nạp ngoài sẽ dính đúng bẫy mất dữ liệu trên).
        ///   2. Mọi giá trị sinh một lần (mã chứng từ, DateTime.UtcNow dùng để ghi) đều
        ///      tính BÊN TRONG delegate.
        ///   3. Không có tác dụng phụ không-idempotent nào chạy TRƯỚC delegate mà lại
        ///      phụ thuộc vào transaction (ví dụ tăng <c>Voucher.UsedCount</c>).
        ///
        /// Khi <c>retrySafe: true</c>, Change Tracker được <c>Clear()</c> ở đầu mỗi lần
        /// thử lại. Bắt buộc: nếu không clear, entity của lần thử 1 còn nằm trong identity
        /// map với Id đã bị rollback, và lần thử 2 nhận đúng Id đó từ DB sẽ ném
        /// "another instance with the same key value is already being tracked".
        /// </remarks>
        Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation,
            bool retrySafe = false,
            [CallerMemberName] string? caller = null,
            [CallerFilePath] string? callerFile = null,
            [CallerLineNumber] int callerLine = 0);

        /// <inheritdoc cref="ExecuteInTransactionAsync{T}(Func{Task{T}}, bool, string, string, int)"/>
        Task ExecuteInTransactionAsync(
            Func<Task> operation,
            bool retrySafe = false,
            [CallerMemberName] string? caller = null,
            [CallerFilePath] string? callerFile = null,
            [CallerLineNumber] int callerLine = 0);

        /// <summary>
        /// Lưu tất cả thay đổi pending trong DbContext xuống Database.
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
