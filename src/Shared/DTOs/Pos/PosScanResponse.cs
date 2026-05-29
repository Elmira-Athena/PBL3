using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PBL3.Shared.DTOs.Pos
{
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
}
