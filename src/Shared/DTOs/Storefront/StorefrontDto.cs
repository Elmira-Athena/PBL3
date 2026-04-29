namespace PBL3.Shared.DTOs.Storefront
{
    public class CategoryMenuResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? IconUrl { get; set; } // Map from ImageUrl
    }

    public class ProductCardResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        
        public decimal OldPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public int DiscountPercent { get; set; }
        
        public double Rating { get; set; } = 5.0; // Mock
        public int ReviewCount { get; set; } = 0; // Mock
    }
}
