using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PBL3.Shared.DTOs.Pos
{
    public class PosCustomerDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        // Thêm các trường khác như hạng, điểm nếu có sau này
    }
}
