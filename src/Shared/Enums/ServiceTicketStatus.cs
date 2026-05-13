namespace PBL3.Shared.Enums;

public enum ServiceTicketStatus : byte
{
    Received = 0,
    Diagnosing = 1,
    QuoteSent = 2,
    QuoteRejected = 3,
    WaitingParts = 4,
    InRepair = 5,
    SentToManufacturer = 6,
    ReceivedFromManufacturer = 7,
    Swapped = 8,
    Completed = 9,
    Cancelled = 10
}
