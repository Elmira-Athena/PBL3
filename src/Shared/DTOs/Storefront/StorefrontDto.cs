using PBL3.Shared.DTOs.Common;

namespace PBL3.Shared.DTOs.Storefront
{
    public class CategoryMenuResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
        public int Level { get; set; }
        public int? ParentId { get; set; }
    }

    /// <summary>
    /// Thông tin chi tiết danh mục dùng cho breadcrumb và tiêu đề trang.
    /// </summary>
    public class CategoryDetailResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
    }

    /// <summary>
    /// Query parameters cho API lấy danh sách sản phẩm theo danh mục.
    /// </summary>
    public class CategoryProductsRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class ProductCardResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string ManufacturerName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        public decimal OldPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public int DiscountPercent { get; set; }

        public bool IsAvailable { get; set; }

        public double Rating { get; set; } = 5.0; // Mock
        public int ReviewCount { get; set; } = 0; // Mock
    }

    /// <summary>
    /// Thông tin phân loại sản phẩm cho trang chi tiết.
    /// Bao gồm ảnh + specs riêng cho mỗi variant.
    /// </summary>
    public class StorefrontVariantResponse
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int WarrantyMonth { get; set; }
        public bool IsAvailable { get; set; }
        public int StockQuantity { get; set; }
        public Dictionary<string, string> Specifications { get; set; } = new();

        /// <summary>Danh sách URL ảnh của variant này, đã sắp xếp (Main trước, rồi theo SortOrder).</summary>
        public List<string> Images { get; set; } = new();

        /// <summary>Ảnh chính của variant (ảnh đầu trong danh sách Images, có thể null nếu variant chưa có ảnh).</summary>
        public string? ThumbnailUrl { get; set; }
    }

    /// <summary>
    /// Toàn bộ dữ liệu chi tiết sản phẩm cho Storefront.
    /// </summary>
    public class ProductDetailResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

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
