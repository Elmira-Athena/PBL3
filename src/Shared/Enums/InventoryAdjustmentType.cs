namespace PBL3.Shared.Enums
{
    /// <summary>
    /// Loại điều chỉnh kho trong bút toán kiểm kê.
    /// </summary>
    public enum InventoryAdjustmentType : byte
    {
        /// <summary>Serial thất thoát (Missing → Lost).</summary>
        Lost = 1,

        /// <summary>Serial lỗi vật lý (Matched/Defective → Defective).</summary>
        Defective = 2
    }
}
