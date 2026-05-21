namespace PBL3.Shared.Enums
{
    /// <summary>
    /// Trạng thái của một mã Serial trong phiếu kiểm kê.
    /// </summary>
    public enum InventoryScanStatus : byte
    {
        /// <summary>Nằm trong snapshot nhưng chưa được quét (trạng thái ban đầu sau chốt).</summary>
        Pending = 0,

        /// <summary>Quét thấy, trùng khớp với snapshot Available.</summary>
        Matched = 1,

        /// <summary>Không quét thấy thực tế (phát sinh khi submit phiếu).</summary>
        Missing = 2,

        /// <summary>Quét thấy nhưng Serial tồn tại trong DB với trạng thái không phải Available (Sold/Reserved/...).</summary>
        Surplus = 3,

        /// <summary>Quét thấy nhưng Serial hoàn toàn không tồn tại trong DB.</summary>
        UnknownSurplus = 4,

        /// <summary>Được nhân viên đánh dấu là hàng lỗi vật lý.</summary>
        Defective = 5
    }
}
