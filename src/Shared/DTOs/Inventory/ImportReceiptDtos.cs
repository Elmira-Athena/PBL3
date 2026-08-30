using PBL3.Shared.DTOs.Common;
namespace PBL3.Shared.DTOs.Inventory
{
    // ========================================================
    // WRITE DTOs (Create)
    // ========================================================

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

    // ========================================================
    // READ DTOs
    // ========================================================

    /// <summary>
    /// DTO hiển thị thông tin phiếu nhập kho (danh sách).
    /// </summary>
    public class ImportReceiptDto
    {
        public int Id { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime ImportDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Note { get; set; }

        /// <summary>Danh sách chi tiết (chỉ hiển thị ở API chi tiết).</summary>
        public List<ImportReceiptDetailDto>? Details { get; set; }
    }

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

    // ========================================================
    // QUERY PARAMETERS
    // ========================================================

    /// <summary>
    /// Bộ lọc cho danh sách phiếu nhập kho.
    /// </summary>
    public class ImportReceiptFilterRequest : PagedRequest
    {
        /// <summary>Tìm kiếm theo Mã phiếu hoặc Tên NCC.</summary>
        public string? Keyword { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? SupplierId { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = true;
    }
}
