using System;
using System.Collections.Generic;
using PBL3.Shared.DTOs.Products; // For PagedResult if needed, or if we need to reference Cart DTOs later
using PBL3.Shared.DTOs.Sale; // OrderDto if it exists

namespace PBL3.Shared.DTOs.Customers
{
    public class CustomerOrderHistoryDto
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public byte Status { get; set; } // 0: Pending, 1: Confirmed, 2: Shipping, 3: Success, 4: Cancelled, 5: Returned
    }
}
