using System;
using System.Collections.Generic;

namespace PBL3.Shared.DTOs.Sale
{
    public class OrderDetailLineDto
    {
        public int Id { get; set; }
        public int VariantId { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalLine { get; set; }
        public string? MainImageUrl { get; set; }
        public List<string> Serials { get; set; } = new();
    }
}
