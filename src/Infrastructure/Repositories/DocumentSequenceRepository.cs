using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    /// <inheritdoc cref="IDocumentSequence"/>
    public class DocumentSequenceRepository : IDocumentSequence
    {
        private readonly HushStoreDbContext _dbContext;

        public DocumentSequenceRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<long> NextValueAsync(string sequenceName)
        {
            // 🔴 KHÔNG nội suy chuỗi trực tiếp vào SQL mà không kiểm.
            // `NEXT VALUE FOR` không nhận tên sequence dưới dạng THAM SỐ — nó là một
            // định danh, không phải giá trị — nên câu SQL buộc phải ghép chuỗi. Điều đó
            // biến `sequenceName` thành một đường SQL injection nếu nó tới từ bên ngoài.
            //
            // Chốt: chỉ nhận tên nằm trong danh sách trắng của DocumentSequences.All.
            // Hiện mọi lời gọi đều truyền hằng số, nên vòng kiểm này không bao giờ đỏ —
            // nó ở đây để lần sau ai đó nối `sequenceName` với dữ liệu người dùng thì
            // hỏng NGAY và ồn ào, thay vì mở một lỗ hổng im lặng.
            if (!DocumentSequences.All.Contains(sequenceName))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sequenceName), sequenceName,
                    "Tên sequence không nằm trong danh sách cho phép.");
            }

            // Dùng chính connection/transaction đang mở của DbContext, nên khi lời gọi này
            // nằm trong `IUnitOfWork.ExecuteInTransactionAsync` thì nó chạy trong đúng
            // transaction đó. (Giá trị vẫn bị tiêu thụ dù transaction rollback — xem
            // phần ghi chú về tính không-giao-dịch ở IDocumentSequence.)
            await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT NEXT VALUE FOR [{sequenceName}]";

            var transaction = _dbContext.Database.CurrentTransaction;
            if (transaction is not null)
            {
                command.Transaction = transaction.GetDbTransaction();
            }

            // `OpenConnectionAsync` là idempotent với EF Core: nếu connection đã mở (trường
            // hợp đang trong transaction) thì nó chỉ tăng bộ đếm tham chiếu. Cặp
            // open/close phải khớp nhau, nên dùng try/finally.
            await _dbContext.Database.OpenConnectionAsync();
            try
            {
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt64(result);
            }
            finally
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
