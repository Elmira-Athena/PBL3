namespace PBL3.Shared.DTOs.Inventory
{
    /// <summary>
    /// DTO chi tiết 1 dòng sản phẩm trong phiếu nhập.
    /// </summary>
    public class ImportReceiptDetailDto
    {
        public int Id { get; set; }
        public int VariantId { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal ImportPrice { get; set; }
        public decimal SubTotal { get; set; }

        /// <summary>Danh sách Serial đã nhập cho dòng này.</summary>
        public List<string> SerialNumbers { get; set; } = new();
    }
}
