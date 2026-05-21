namespace PBL3.Shared.Enums
{
    /// <summary>
    /// Phạm vi kiểm kê kho hàng.
    /// </summary>
    public enum InventoryCheckScopeType : byte
    {
        /// <summary>Toàn bộ kho.</summary>
        AllStore = 0,

        /// <summary>Theo danh mục sản phẩm (bao gồm cây con).</summary>
        Category = 1
    }
}
