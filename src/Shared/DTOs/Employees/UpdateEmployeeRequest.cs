using System;

namespace PBL3.Shared.DTOs.Employees
{
    public class UpdateEmployeeRequest
    {
        public string FullName { get; set; } = string.Empty;
        public byte Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public bool IsTechnician { get; set; }
    }
}
