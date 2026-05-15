namespace PBL3.Shared.Enums
{
    public enum OrderStatus : byte
    {
        Pending = 0,      // Chờ duyệt
        Confirmed = 1,    // Chờ xuất kho
        Exported = 2,     // Đã xuất kho
        Success = 3,      // Thành công
        Cancelled = 4,    // Đã hủy
        Returned = 5,     // Hoàn trả
        PosDraft = 6      // Đơn chờ POS
    }
}
