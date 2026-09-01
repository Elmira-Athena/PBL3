using PBL3.Core.Interfaces;

namespace PBL3.Service.Common
{
    /// <inheritdoc cref="IDocumentCodeGenerator"/>
    public class DocumentCodeGenerator : IDocumentCodeGenerator
    {
        /// <summary>
        /// Độ rộng phần số thứ tự.
        /// </summary>
        /// <remarks>
        /// LỊCH SỬ, giữ lại vì nó giải thích vì sao con số là 6 chứ không phải 3:
        /// bản đầu dùng 3, và mã cuối trong ngày được tìm bằng <c>ORDER BY Code DESC</c>,
        /// tức SO SÁNH CHUỖI. Quá 999 chứng từ/ngày thì "...-1000" sắp TRƯỚC "...-999",
        /// nên truy vấn luôn trả về "-999" và hệ thống sinh mã trùng VĨNH VIỄN.
        ///
        /// ✅ Đợt 3 đã bỏ hẳn việc so sánh chuỗi VÀ bỏ hẳn việc đọc mã cũ. Số thứ tự nay
        /// do SQL SEQUENCE cấp, nên độ rộng chỉ còn là chuyện ĐỊNH DẠNG — không còn là
        /// chuyện đúng/sai. Giới hạn thật bây giờ là ĐỘ RỘNG CỘT (nvarchar(20)):
        /// tiền tố 3 ký tự chịu được tối đa 7 chữ số. Xem comment ở
        /// <c>HushStoreDbContext.OnModelCreating</c>.
        ///
        /// <c>ToString("D6")</c> KHÔNG cắt số: khi sequence vượt 999.999 nó tự in 7 chữ số.
        /// Đó là hành vi đúng — thà mã dài hơn một ký tự còn hơn sinh mã trùng.
        /// </remarks>
        private const int NumberWidth = 6;

        private readonly IDocumentSequence _sequence;

        public DocumentCodeGenerator(IDocumentSequence sequence)
        {
            _sequence = sequence;
        }

        public async Task<string> NextAsync(DocumentCodeKind kind)
        {
            // UtcNow cho MỌI loại chứng từ. Trước đây Order/POS dùng DateTime.Now còn các
            // loại khác dùng UtcNow — hai chứng từ tạo cùng lúc có thể rơi vào hai NGÀY
            // khác nhau trong mã, và ngày trong mã lệch với cột ngày (vốn luôn lưu UTC).
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");

            // Một round-trip, không khoá, không đọc dữ liệu cũ. Đây là toàn bộ bản vá:
            // không còn "đọc rồi +1" nên không còn khe hở giữa đọc và ghi.
            var next = await _sequence.NextValueAsync(SequenceOf(kind));

            return $"{PrefixOf(kind)}-{datePart}-{next.ToString(new string('0', NumberWidth))}";
        }

        private static string PrefixOf(DocumentCodeKind kind) => kind switch
        {
            DocumentCodeKind.Order          => "ORD",
            DocumentCodeKind.PosOrder       => "POS",
            DocumentCodeKind.ImportReceipt  => "PN",
            DocumentCodeKind.InventoryCheck => "KK",
            DocumentCodeKind.ServiceTicket  => "ST",
            DocumentCodeKind.ServiceInvoice => "SRV",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Loại chứng từ không được hỗ trợ.")
        };

        /// <remarks>
        /// 🔴 <c>Order</c> và <c>PosOrder</c> dùng CHUNG một sequence — cả hai ghi vào cùng
        /// cột <c>Orders.OrderCode</c> có unique index. Đừng tách ra "cho gọn".
        /// </remarks>
        private static string SequenceOf(DocumentCodeKind kind) => kind switch
        {
            DocumentCodeKind.Order or DocumentCodeKind.PosOrder
                                            => DocumentSequences.Order,
            DocumentCodeKind.ImportReceipt  => DocumentSequences.ImportReceipt,
            DocumentCodeKind.InventoryCheck => DocumentSequences.InventoryCheck,
            DocumentCodeKind.ServiceTicket  => DocumentSequences.ServiceTicket,
            DocumentCodeKind.ServiceInvoice => DocumentSequences.ServiceInvoice,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Loại chứng từ không được hỗ trợ.")
        };
    }
}
