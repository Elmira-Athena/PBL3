namespace PBL3.Shared.Enums
{
    public enum OrderStatus : byte
    {
        Pending = 0,
        Confirmed = 1,
        Shipping = 2,
        Success = 3,
        Cancelled = 4,
        Returned = 5,
        PosDraft = 6 // Đơn chờ POS
    }
}
