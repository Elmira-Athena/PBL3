namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class RmaResolutionUpdateDto
    {
        public byte ManufacturerResolution { get; set; } // 1=Repaired, 2=Replaced, 3=Refused
        public string? ManufacturerNotes { get; set; }

        // If Replaced: which serial to swap in
        public int? ReplacementSerialId { get; set; }
    }
}
