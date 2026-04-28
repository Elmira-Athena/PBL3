using System.Collections.Generic;

namespace PBL3.Shared.DTOs.Inventory
{
    public class ExportOrderRequest
    {
        public int OrderId { get; set; }
        public List<ExportOrderDetailRequest> Details { get; set; } = new();
    }

    public class ExportOrderDetailRequest
    {
        public int OrderDetailId { get; set; }
        public List<string> SerialNumbers { get; set; } = new();
    }
}
