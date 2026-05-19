using System;
using System.Collections.Generic;
using PBL3.Shared.DTOs.Products; // For PagedResult if needed, or if we need to reference Cart DTOs later
using PBL3.Shared.DTOs.Sale; // OrderDto if it exists

namespace PBL3.Shared.DTOs.Customers
{
    // ========================================================
    // READ DTOs
    // ========================================================

    public class CustomerDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public byte Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public bool IsActive { get; set; }
        public string? LockReason { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CustomerDetailDto : CustomerDto
    {
        // Simple representation of orders to avoid circular dependency
        // In reality, this would use an OrderLiteDto or similar depending on existing Sales DTOs
        public List<CustomerOrderHistoryDto> RecentOrders { get; set; } = new();
        public List<CustomerCartItemDto> CartItems { get; set; } = new();
    }

    public class CustomerOrderHistoryDto
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public byte Status { get; set; } // 0: Pending, 1: Confirmed, 2: Shipping, 3: Success, 4: Cancelled, 5: Returned
    }

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

    // ========================================================
    // WRITE DTOs (CUD)
    // ========================================================

    public class CreateCustomerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public byte Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
    }

    public class UpdateCustomerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public byte Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
    }

    // ========================================================
    // AUTHENTICATION DTOs
    // ========================================================

    public class RegisterCustomerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RegisterCustomerResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    // ========================================================
    // QUERY PARAMETERS
    // ========================================================

    public class CustomerFilterRequest
    {
        public string? Keyword { get; set; }
        public bool? IsActive { get; set; }
        public byte? Gender { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
