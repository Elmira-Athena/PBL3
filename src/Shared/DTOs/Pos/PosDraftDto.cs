using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PBL3.Shared.DTOs.Pos
{
    public class PosDraftDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
