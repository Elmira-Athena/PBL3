namespace PBL3.Shared.DTOs.Inventory
{
    /// <summary>
    /// Chi tiết 1 dòng sản phẩm trong phiếu nhập.
    /// </summary>
    public class ImportReceiptDetailRequest
    {
        /// <summary>Biến thể sản phẩm được chọn.</summary>
        public int VariantId { get; set; }

        /// <summary>Số lượng nhập. Phải > 0.</summary>
        public int Quantity { get; set; }

        /// <summary>Giá nhập (giá vốn). Phải >= 0.</summary>
        public decimal ImportPrice { get; set; }

        /// <summary>
        /// Danh sách mã Serial quét từ vỏ hộp.
        /// Số lượng phần tử BẮT BUỘC phải bằng Quantity.
        /// </summary>
        public List<string> SerialNumbers { get; set; } = new();
    }
}
