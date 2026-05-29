namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceTicketBranchDto
    {
        public byte ResolutionType { get; set; } // 1=InternalRepair, 2=Rma, 3=Swap, 4=PaidRepair
    }
}
