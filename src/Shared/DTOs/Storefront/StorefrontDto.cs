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

    /// <summary>
    /// Thông tin phân loại sản phẩm cho trang chi tiết.
    /// Bảo mật: Không trả về StockQuantity, chỉ trả về cờ IsAvailable.
    /// </summary>
    public class StorefrontVariantResponse
    {
        public int Id { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        /// <summary>
        /// true nếu còn hàng (StockQuantity > 0), false nếu hết hàng.
        /// </summary>
        public bool IsAvailable { get; set; }
        public Dictionary<string, string> Specifications { get; set; } = new();
    }

    /// <summary>
    /// Toàn bộ dữ liệu chi tiết sản phẩm cho Storefront.
    /// </summary>
    public class ProductDetailResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        /// <summary>Thương hiệu (VD: "MSI")</summary>
        public string ManufacturerName { get; set; } = string.Empty;

        /// <summary>Bài viết HTML mô tả chi tiết sản phẩm.</summary>
        public string? Description { get; set; }

        /// <summary>Chuỗi JSON thông số kỹ thuật.</summary>
        public string? Specifications { get; set; }

        /// <summary>Danh sách URL ảnh để làm Gallery.</summary>
        public List<string> Images { get; set; } = new();

        /// <summary>Danh sách phân loại (BẮT BUỘC CÓ).</summary>
        public List<StorefrontVariantResponse> Variants { get; set; } = new();

        /// <summary>Các gạch đầu dòng mô tả ngắn, parse từ Specifications.</summary>
        public List<string> ShortFeatures { get; set; } = new();

        public double Rating { get; set; } = 5.0; // Mock
        public int ReviewCount { get; set; } = 13; // Mock
    }
}
