using System;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Shared.DTOs.Sale
{
    public class OrderFilterRequest
    {
        public string? Keyword { get; set; }
        public int? Status { get; set; }
        public byte? MinStatus { get; set; }
        public byte? MaxStatus { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
