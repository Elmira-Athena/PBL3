using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PBL3.Shared.DTOs.Pos
{
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
}
