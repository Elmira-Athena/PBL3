using System;
using System.Collections.Generic;
using PBL3.Shared.DTOs.Products; // For PagedResult if needed, or if we need to reference Cart DTOs later
using PBL3.Shared.DTOs.Sale; // OrderDto if it exists

namespace PBL3.Shared.DTOs.Customers
{
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
}
