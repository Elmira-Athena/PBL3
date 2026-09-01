namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Cấp số thứ tự cho mã chứng từ bằng SQL SEQUENCE (đợt 3).
    /// </summary>
    /// <remarks>
    /// VÌ SAO CẦN MỘT LỚP TRỪU TƯỢNG RIÊNG thay vì để <see cref="IDocumentCodeGenerator"/>
    /// tự chạm <c>DbContext</c>: <c>DocumentCodeGenerator</c> nằm ở tầng Service, và repo
    /// giữ luật "truy cập dữ liệu đi qua interface ở Core, cài đặt ở Infrastructure".
    /// Service <em>có</em> tham chiếu Infrastructure nên vẫn biên dịch được nếu chạm thẳng,
    /// nhưng làm vậy là mở một mô hình tư duy thứ hai cho cùng một việc.
    ///
    /// 🔴 <b>`NEXT VALUE FOR` KHÔNG mang tính giao dịch — và đó là ĐIỀU MONG MUỐN.</b>
    /// Giá trị bị tiêu thụ ngay cả khi transaction rollback, nên dãy mã <b>sẽ có lỗ</b>.
    /// Đừng "sửa" điều này: chính việc không giữ khoá mới là thứ làm nó không đua nhau.
    /// Hệ quả phải nói với người quyết định nghiệp vụ: <b>số trong mã không còn là
    /// "chứng từ thứ N"</b>. Muốn đếm thì dùng <c>COUNT(*)</c>.
    /// (Suy ra số lượng từ mã <em>vốn đã sai từ trước</em> — rollback đã tạo lỗ hổng rồi.)
    ///
    /// Hệ quả thứ hai, có lợi: khi execution strategy <b>retry</b> một transaction, lần thử
    /// sau lấy một giá trị MỚI. Đó đúng là điều cần — lần thử lại phải có mã chưa ai dùng,
    /// không phải mã vừa đụng unique index.
    /// </remarks>
    public interface IDocumentSequence
    {
        /// <summary>
        /// Lấy giá trị kế tiếp của một sequence. Không bao giờ trả về hai lần cùng một
        /// giá trị, kể cả khi nhiều tiến trình gọi đồng thời.
        /// </summary>
        /// <param name="sequenceName">
        /// Tên sequence — bắt buộc là một trong các hằng của <see cref="DocumentSequences"/>.
        /// </param>
        Task<long> NextValueAsync(string sequenceName);
    }

    /// <summary>
    /// Tên các SEQUENCE trong DB. Đây là <b>chỗ duy nhất</b> viết ra các tên này;
    /// <c>HushStoreDbContext</c> khai sequence từ chính các hằng số này, nên không thể
    /// lệch giữa "tên code dùng" và "tên DB có".
    /// </summary>
    public static class DocumentSequences
    {
        /// <summary>
        /// Dùng CHUNG cho <c>ORD-</c> và <c>POS-</c>.
        /// </summary>
        /// <remarks>
        /// 🔴 KHÔNG tách thành hai sequence. Cả hai loại ghi vào cùng cột
        /// <c>Orders.OrderCode</c> có unique index. Hai sequence độc lập vẫn sinh mã
        /// không trùng <em>trong phạm vi từng loại</em>, nhưng vì tiền tố khác nhau nên
        /// chuỗi cuối cùng cũng không trùng — tách ra <em>vẫn đúng</em>. Lý do dùng chung
        /// là để một mã đơn hàng xác định được nguồn gốc mà không cần đọc tiền tố, và để
        /// chỉ có một con số phải theo dõi cho một cột unique.
        /// </remarks>
        public const string Order = "SeqOrderCode";

        public const string ImportReceipt = "SeqImportReceiptCode";
        public const string InventoryCheck = "SeqInventoryCheckCode";
        public const string ServiceTicket = "SeqServiceTicketCode";
        public const string ServiceInvoice = "SeqServiceInvoiceCode";

        /// <summary>Tất cả sequence — dùng để khai trong DbContext, đừng liệt kê lại tay.</summary>
        public static readonly string[] All =
        [
            Order, ImportReceipt, InventoryCheck, ServiceTicket, ServiceInvoice
        ];
    }
}
