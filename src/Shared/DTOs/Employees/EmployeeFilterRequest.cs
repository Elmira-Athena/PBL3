using System;

namespace PBL3.Shared.DTOs.Employees
{
    public class EmployeeFilterRequest
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
