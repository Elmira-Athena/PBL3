namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Các loại chứng từ có mã tự sinh theo ngày.
    /// </summary>
    /// <remarks>
    /// Order và PosOrder khác tiền tố (ORD- / POS-) nhưng cùng ghi vào một cột
    /// <c>Orders.OrderCode</c> có unique index — điểm này quan trọng khi đợt 3
    /// thay ruột bằng SEQUENCE: hai loại phải dùng CHUNG một sequence.
    /// </remarks>
    public enum DocumentCodeKind
    {
        Order,
        PosOrder,
        ImportReceipt,
        InventoryCheck,
        ServiceTicket,
        ServiceInvoice
    }

    /// <summary>
    /// Sinh mã chứng từ dạng <c>PREFIX-yyyyMMdd-NNNNNN</c>.
    /// </summary>
    /// <remarks>
    /// GOM VỀ MỘT CHỖ (đợt 1): trước đây 7 khối code trùng lặp nằm rải rác ở 6 file,
    /// mỗi khối tự đọc mã cuối trong ngày rồi +1. Gom lại để đợt 3 chỉ phải thay ruột
    /// MỘT file khi chuyển sang SEQUENCE.
    ///
    /// ⚠️ THUẬT TOÁN HIỆN TẠI VẪN CÒN RACE (check-then-act): N request đồng thời cùng
    /// đọc ra một giá trị "mã cuối" rồi cùng +1 => cùng sinh ra một mã => unique index
    /// chặn lại và người thua nhận HTTP 500. Bản vá thật là SEQUENCE ở đợt 3;
    /// ở đợt 1 chỉ gom code và bịt quả bom {n:D3} (xem NumberWidth).
    /// </remarks>
    public interface IDocumentCodeGenerator
    {
        Task<string> NextAsync(DocumentCodeKind kind);
    }
}
