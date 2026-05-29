using System;
using System.Collections.Generic;
using PBL3.Shared.DTOs.Products; // For PagedResult if needed, or if we need to reference Cart DTOs later
using PBL3.Shared.DTOs.Sale; // OrderDto if it exists

namespace PBL3.Shared.DTOs.Customers
{
    public class CustomerDetailDto : CustomerDto
    {
        // Simple representation of orders to avoid circular dependency
        // In reality, this would use an OrderLiteDto or similar depending on existing Sales DTOs
        public List<CustomerOrderHistoryDto> RecentOrders { get; set; } = new();
        public List<CustomerCartItemDto> CartItems { get; set; } = new();
    }
}
