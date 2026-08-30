using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<UnitOfWork> _logger;

        public UnitOfWork(HushStoreDbContext context, ILogger<UnitOfWork> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation,
            bool retrySafe = false,
            [CallerMemberName] string? caller = null,
            [CallerFilePath] string? callerFile = null,
            [CallerLineNumber] int callerLine = 0)
        {
            ArgumentNullException.ThrowIfNull(operation);

            // EnableRetryOnFailure đã bật, nên strategy này CÓ THỂ chạy delegate nhiều lần.
            // Xem hợp đồng retry đầy đủ ở IUnitOfWork.
            var strategy = _context.Database.CreateExecutionStrategy();

            var site = $"{caller} ({System.IO.Path.GetFileName(callerFile)}:{callerLine})";
            var attempt = 0;

            return await strategy.ExecuteAsync(async () =>
            {
                attempt++;

                if (attempt > 1)
                {
                    if (!retrySafe)
                    {
                        // CHỐT CHẶN CÓ CHỦ ĐÍCH — thà lỗi ồn ào còn hơn hỏng dữ liệu im lặng.
                        //
                        // Call-site này chưa được rà theo hợp đồng retry, mà chạy lại nó thì
                        // có thể MẤT DỮ LIỆU ÂM THẦM: nếu lỗi transient rơi đúng lúc commit
                        // thì các SaveChanges bên trong đã thành công, entity đã thành
                        // Unchanged với snapshot = giá trị mới, nên lần thử này gán lại đúng
                        // giá trị đó sẽ KHÔNG sinh câu UPDATE nào — trong khi hàng dữ liệu
                        // vừa bị rollback về giá trị cũ.
                        //
                        // Ném ở đây cho ra đúng hành vi cũ (trước khi bật retry): request lỗi.
                        // Người dùng bấm lại. Không mất dữ liệu, và log chỉ thẳng chỗ cần rà.
                        //
                        // Lưu ý khi đọc log: exception gốc (SqlException transient) KHÔNG đi
                        // kèm cái ném ở đây — execution strategy đã nuốt nó để quyết định thử
                        // lại. Nhưng EF Core tự ghi nó ở mức Warning qua sự kiện
                        // CoreEventId.ExecutionStrategyRetrying, nên vẫn tra được: tìm dòng
                        // ngay TRƯỚC dòng lỗi này trong log.
                        _logger.LogError(
                            "Transaction tại {Site} gặp lỗi transient và ĐÃ BỊ CHẶN retry vì " +
                            "call-site chưa được rà theo hợp đồng retry (retrySafe = false). " +
                            "Xem IUnitOfWork.ExecuteInTransactionAsync để biết ba điều kiện.",
                            site);

                        throw new InvalidOperationException(
                            $"Transaction tại {site} gặp lỗi tạm thời của cơ sở dữ liệu. " +
                            "Hệ thống KHÔNG tự chạy lại giao dịch này vì call-site chưa được " +
                            "rà theo hợp đồng retry (IUnitOfWork.ExecuteInTransactionAsync) — " +
                            "chạy lại khi chưa rà có thể làm mất dữ liệu âm thầm. " +
                            "Vui lòng thực hiện lại thao tác.");
                    }

                    // BẮT BUỘC với call-site retry-safe: entity của lần thử trước vẫn nằm
                    // trong identity map với Id đã bị rollback. Không clear thì lần thử này
                    // nhận đúng Id đó từ DB và EF ném "another instance with the same key
                    // value is already being tracked".
                    _context.ChangeTracker.Clear();

                    _logger.LogWarning(
                        "Chạy lại transaction tại {Site} (lần thử {Attempt}) sau lỗi transient. " +
                        "Change Tracker đã được clear.",
                        site, attempt);
                }

                // using: transaction được Dispose kể cả khi commit/rollback ném.
                // Bản cũ giữ transaction trong field và không bao giờ Dispose —
                // connection bị giữ lại cho tới khi scope của DbContext kết thúc.
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var result = await operation();

                await transaction.CommitAsync();
                return result;
            });
        }

        public async Task ExecuteInTransactionAsync(
            Func<Task> operation,
            bool retrySafe = false,
            [CallerMemberName] string? caller = null,
            [CallerFilePath] string? callerFile = null,
            [CallerLineNumber] int callerLine = 0)
        {
            ArgumentNullException.ThrowIfNull(operation);

            // Truyền caller info xuống TƯỜNG MINH. Để mặc định thì compiler điền tên của
            // chính phương thức này và log sẽ chỉ vào UnitOfWork thay vì vào call-site thật.
            await ExecuteInTransactionAsync(async () =>
            {
                await operation();
                return true;
            }, retrySafe, caller, callerFile, callerLine);
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
