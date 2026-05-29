namespace PBL3.Shared.DTOs.ServiceTickets
{
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
}
