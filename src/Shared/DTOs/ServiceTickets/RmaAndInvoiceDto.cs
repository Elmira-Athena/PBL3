namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class RmaShipmentCreateDto
    {
        public string CarrierName { get; set; } = string.Empty;
        public string TrackingCode { get; set; } = string.Empty;
    }

    public class RmaShipmentDetailDto
    {
        public int Id { get; set; }
        public int TicketId { get; set; }

        public string CarrierName { get; set; } = string.Empty;
        public string TrackingCode { get; set; } = string.Empty;

        public DateTime? ShippedDate { get; set; }

        public DateTime? ReceivedBackDate { get; set; }
        public byte ManufacturerResolution { get; set; } // 0=Pending, 1=Repaired, 2=Replaced, 3=Refused
        public string ManufacturerResolutionLabel { get; set; } = string.Empty;

        public string? ManufacturerNotes { get; set; }
    }

    public class RmaResolutionUpdateDto
    {
        public byte ManufacturerResolution { get; set; } // 1=Repaired, 2=Replaced, 3=Refused
        public string? ManufacturerNotes { get; set; }

        // If Replaced: which serial to swap in
        public int? ReplacementSerialId { get; set; }
    }

    public class Perform1For1SwapDto
    {
        public int ReplacementSerialId { get; set; }
    }

    public class ServiceInvoiceCreateDto
    {
        public byte PaymentMethod { get; set; } // 0=Cash, 1=Banking, 2=VNPay
        public string? Note { get; set; }
    }

    public class ServiceInvoiceDetailDto
    {
        public int Id { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public int TicketId { get; set; }
        public int? QuotationId { get; set; }
        public Guid? IssuedByEmployeeId { get; set; }

        public DateTime IssuedDate { get; set; }

        public decimal LaborCost { get; set; }
        public decimal PartsTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public byte PaymentMethod { get; set; } // 0=Cash, 1=Banking, 2=VNPay
        public string PaymentMethodLabel { get; set; } = string.Empty;

        public byte PaymentStatus { get; set; } // 0=Unpaid, 1=Paid
        public string PaymentStatusLabel { get; set; } = string.Empty;

        public string? Note { get; set; }

        public List<ServiceInvoiceItemDto> Items { get; set; } = new();
    }

    public class ServiceInvoiceItemDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class ServiceInvoiceListDto
    {
        public int Id { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public int TicketId { get; set; }
        public DateTime IssuedDate { get; set; }
        public decimal GrandTotal { get; set; }
        public byte PaymentStatus { get; set; }
        public string PaymentStatusLabel { get; set; } = string.Empty;
    }

    public class SerialRepairHistoryDto
    {
        public int Id { get; set; }
        public int SerialId { get; set; }
        public int? TicketId { get; set; }
        public string? TicketCode { get; set; }

        public byte ResolutionType { get; set; }
        public string ResolutionTypeLabel { get; set; } = string.Empty;

        public DateTime LoggedAt { get; set; }
        public string Summary { get; set; } = string.Empty;

        public int? ReplacedBySerialId { get; set; }
        public string? ReplacedBySerialNumber { get; set; }
    }
}
