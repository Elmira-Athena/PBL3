namespace PBL3.Shared.Enums
{
    /// <summary>
    /// Trạng thái của Serial sản phẩm trong kho.
    /// </summary>
    public enum SerialStatus : byte
    {
        /// <summary>Trong kho, sẵn sàng bán.</summary>
        Available = 0,

        /// <summary>Có khách đặt Online (đang chờ giao).</summary>
        Reserved = 1,

        /// <summary>Đã bán thành công.</summary>
        Sold = 2,

        /// <summary>Hàng lỗi, chờ trả hãng.</summary>
        Defective = 3,

        /// <summary>Đã trả lại.</summary>
        Returned = 4,

        /// <summary>Thất thoát (phát hiện qua kiểm kê).</summary>
        Lost = 5
    }
}
