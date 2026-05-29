namespace PBL3.Shared.DTOs.Inventory
{
    /// <summary>
    /// Request tạo phiếu nhập kho.
    /// </summary>
    public class CreateImportReceiptRequest
    {
        /// <summary>Nhà cung cấp (bắt buộc).</summary>
        public int SupplierId { get; set; }

        /// <summary>Ghi chú phiếu nhập.</summary>
        public string? Note { get; set; }

        /// <summary>Danh sách chi tiết các sản phẩm nhập. Bắt buộc có ít nhất 1 dòng.</summary>
        public List<ImportReceiptDetailRequest> Details { get; set; } = new();
    }
}
