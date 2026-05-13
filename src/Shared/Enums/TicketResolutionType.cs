namespace PBL3.Shared.Enums;

public enum TicketResolutionType : byte
{
    Pending = 0,
    InternalRepair = 1,
    Rma = 2,
    Swap = 3,
    PaidRepair = 4,
    Rejected = 5,
    Cancelled = 6
}
