namespace PBL3.Shared.Enums
{
    /// <summary>
    /// Trạng thái của phiếu kiểm kê kho hàng.
    /// </summary>
    public enum InventoryCheckStatus : byte
    {
        /// <summary>Đang soạn thảo / kiểm đếm.</summary>
        Draft = 0,

        /// <summary>Đã gửi, đang chờ Admin phê duyệt.</summary>
        AwaitingApproval = 1,

        /// <summary>Đã phê duyệt và cân bằng kho hoàn tất.</summary>
        Completed = 2,

        /// <summary>Đã hủy.</summary>
        Cancelled = 3
    }
}
