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
            // `nextval()` của PostgreSQL nhận tên sequence dưới dạng GIÁ TRỊ (kiểu `regclass`),
            // nên về lý thuyết tham số hoá được. Chốt danh sách trắng vẫn GIỮ NGUYÊN — nó là
            // phòng thủ có chủ đích, không phải hệ quả của giới hạn cú pháp; bỏ đi vì "giờ đã
            // tham số hoá được" là gỡ một lớp bảo vệ để đổi lấy không gì cả.
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
            // Nháy KÉP lồng trong nháy ĐƠN, và đó là bắt buộc: PostgreSQL hạ mọi định danh
            // không nháy về chữ thường, nên `nextval('SeqOrderCode')` sẽ đi tìm `seqordercode`
            // và ném `relation does not exist`. Tên sequence do EF tạo giữ nguyên hoa/thường.
            command.CommandText = $"SELECT nextval('\"{sequenceName}\"')";

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
