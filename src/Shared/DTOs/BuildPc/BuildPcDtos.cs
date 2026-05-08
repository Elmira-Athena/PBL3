namespace PBL3.Shared.DTOs.BuildPc
{
    public class BuildPcItemDto
    {
        public int SlotIndex { get; set; }
        public string SlotName { get; set; } = string.Empty;
        public int VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Sku { get; set; } = string.Empty;
        public int WarrantyMonth { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class ExportBuildPcRequest
    {
        public List<BuildPcItemDto> Items { get; set; } = new();
    }
}
