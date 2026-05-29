using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PBL3.Shared.DTOs.Pos
{
    public class PosCheckoutRequest
    {
        public string? CustomerPhone { get; set; }
        public string? VoucherCode { get; set; }
        public byte PaymentMethod { get; set; } // 0: Cash, 1: Banking, 2: Card
        public string? EmployeeNote { get; set; }
        public string? ShipAddress { get; set; }
        public string? ShipCity { get; set; }
        public List<PosCheckoutItemRequest> Items { get; set; } = new();
    }
}
