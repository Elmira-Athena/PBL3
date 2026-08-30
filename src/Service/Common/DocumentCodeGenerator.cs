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
        /// TRƯỚC ĐÂY LÀ 3 — và đó là quả bom hẹn giờ, không phải chuyện thẩm mỹ:
        /// mã cuối trong ngày được tìm bằng <c>ORDER BY Code DESC</c>, tức SO SÁNH CHUỖI.
        /// Quá 999 chứng từ/ngày thì "...-1000" sắp TRƯỚC "...-999" theo thứ tự chuỗi,
        /// nên truy vấn luôn trả về "-999", số kế tiếp luôn ra 1000, và hệ thống sinh
        /// mã trùng VĨNH VIỄN kể từ đó.
        ///
        /// 6 chữ số đẩy trần lên 999.999 chứng từ/ngày. Cột mã rộng 20 ký tự,
        /// mã dài nhất là "ORD-yyyyMMdd-NNNNNN" = 19 ký tự => vừa.
        ///
        /// Riêng việc đổi độ rộng KHÔNG an toàn nếu vẫn giữ cách so sánh chuỗi: mã cũ
        /// "-001" luôn sắp trên mã mới "-000002". Vì vậy repository nay trả về cả danh
        /// sách mã trong ngày và chỗ này tự lấy max theo SỐ — cách so sánh chuỗi bị bỏ hẳn.
        /// </remarks>
        private const int NumberWidth = 6;

        private readonly IOrderRepository _orderRepo;
        private readonly IImportReceiptRepository _receiptRepo;
        private readonly IInventoryCheckRepository _checkRepo;
        private readonly IServiceTicketRepository _ticketRepo;
        private readonly IServiceInvoiceRepository _invoiceRepo;

        public DocumentCodeGenerator(
            IOrderRepository orderRepo,
            IImportReceiptRepository receiptRepo,
            IInventoryCheckRepository checkRepo,
            IServiceTicketRepository ticketRepo,
            IServiceInvoiceRepository invoiceRepo)
        {
            _orderRepo = orderRepo;
            _receiptRepo = receiptRepo;
            _checkRepo = checkRepo;
            _ticketRepo = ticketRepo;
            _invoiceRepo = invoiceRepo;
        }

        public async Task<string> NextAsync(DocumentCodeKind kind)
        {
            // UtcNow cho MỌI loại chứng từ. Trước đây Order/POS dùng DateTime.Now còn các
            // loại khác dùng UtcNow — hai chứng từ tạo cùng lúc có thể rơi vào hai NGÀY
            // khác nhau trong mã, và ngày trong mã lệch với cột ngày (vốn luôn lưu UTC).
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"{PrefixOf(kind)}-{datePart}-";

            var todayCodes = await GetCodesAsync(kind, prefix);

            var max = 0;
            foreach (var code in todayCodes)
            {
                if (code.Length <= prefix.Length) continue;
                if (int.TryParse(code[prefix.Length..], out var n) && n > max)
                    max = n;
            }

            return prefix + (max + 1).ToString(new string('0', NumberWidth));
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

        private Task<List<string>> GetCodesAsync(DocumentCodeKind kind, string prefix) => kind switch
        {
            DocumentCodeKind.Order or DocumentCodeKind.PosOrder
                                            => _orderRepo.GetCodesByDatePrefixAsync(prefix),
            DocumentCodeKind.ImportReceipt  => _receiptRepo.GetCodesByDatePrefixAsync(prefix),
            DocumentCodeKind.InventoryCheck => _checkRepo.GetCodesByDatePrefixAsync(prefix),
            DocumentCodeKind.ServiceTicket  => _ticketRepo.GetCodesByDatePrefixAsync(prefix),
            DocumentCodeKind.ServiceInvoice => _invoiceRepo.GetCodesByDatePrefixAsync(prefix),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Loại chứng từ không được hỗ trợ.")
        };
    }
}
