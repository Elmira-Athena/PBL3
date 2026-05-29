using System;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Shared.DTOs.Sale
{
    public class CancelOrderRequest
    {
        public string CancelReason { get; set; } = string.Empty;
    }
}
