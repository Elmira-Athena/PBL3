using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PBL3.Shared.DTOs.Pos
{
    public class PosScanRequest
    {
        [Required]
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class PosScanResponse
    {
        public int SerialId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public int VariantId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int WarrantyMonth { get; set; }
    }

    public class PosCheckoutRequest
    {
        public string? CustomerPhone { get; set; }
        public string? VoucherCode { get; set; }
        public byte PaymentMethod { get; set; } // 0: Cash, 1: Banking, 2: Card
        public string? EmployeeNote { get; set; }
        public List<PosCheckoutItemRequest> Items { get; set; } = new();
    }

    public class PosCheckoutItemRequest
    {
        public int SerialId { get; set; }
        // For items without serials, we'd pass VariantId and Quantity, but Use Case flow says:
        // "Quét mã vạch sản phẩm hoặc nhập thủ công mã Seri/IMEI/Mã linh kiện"
        // Let's assume everything will be resolved to a SerialId or handled carefully.
        // Actually, if generic items are allowed, we might need:
        public int? VariantId { get; set; }
        public int Quantity { get; set; } = 1; 
    }

    public class PosOrderDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
    }

    public class PosDraftDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class PosCustomerDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        // Thêm các trường khác như hạng, điểm nếu có sau này
    }

    public class VoucherValidationDto
    {
        public bool IsValid { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; } // Số tiền được giảm tương ứng với giỏ hàng
    }
}
