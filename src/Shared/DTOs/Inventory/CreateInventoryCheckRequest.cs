namespace PBL3.Shared.DTOs.Inventory
{
    public class CreateInventoryCheckRequest
    {
        /// <summary>0 = AllStore, 1 = Category</summary>
        public byte ScopeType { get; set; }

        /// <summary>Bắt buộc khi ScopeType = 1.</summary>
        public int? ScopeCategoryId { get; set; }

        public string? Note { get; set; }
    }
}
