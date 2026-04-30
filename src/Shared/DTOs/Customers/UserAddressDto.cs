using System;

namespace PBL3.Shared.DTOs.Customers
{
    public class UserAddressDto
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string ReceiverName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AddressLine { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }
}
