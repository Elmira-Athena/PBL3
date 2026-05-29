using System;
using System.Collections.Generic;
using PBL3.Shared.DTOs.Products; // For PagedResult if needed, or if we need to reference Cart DTOs later
using PBL3.Shared.DTOs.Sale; // OrderDto if it exists

namespace PBL3.Shared.DTOs.Customers
{
    public class CustomerCartItemDto
    {
        public int Id { get; set; }
        public int VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? ThumbnailUrl { get; set; }
    }
}
