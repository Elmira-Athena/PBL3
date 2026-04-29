using System;
using System.Collections.Generic;

namespace PBL3.Shared.DTOs.Common
{
    /// <summary>
    /// Kết quả phân trang dùng chung cho toàn hệ thống.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 1));
    }
}
