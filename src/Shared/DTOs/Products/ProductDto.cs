using PBL3.Shared.DTOs.Common;
namespace PBL3.Shared.DTOs.Products
{
    // ========================================================
    // ENUMS
    // ========================================================

    /// <summary>
    /// Trạng thái sản phẩm.
    /// </summary>
    public enum ProductStatus
    {
        Draft = 0,
        Active = 1,
        StopBusiness = 2
    }

    // ========================================================
    // READ DTOs
    // ========================================================

    /// <summary>
    /// DTO chi tiết một Image.
    /// </summary>
    public class ProductImageDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
    }


    /// <summary>
    /// DTO chi tiết một Variant (dùng trong ProductDetailDto).
    /// </summary>
    public class ProductVariantDto
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int StockQuantity { get; set; }
        public int WarrantyMonth { get; set; }
        public Dictionary<string, string>? Specifications { get; set; }
        public List<ProductImageDto> Images { get; set; } = new();
    }

    /// <summary>
    /// DTO chi tiết sản phẩm (dùng cho trang detail).
    /// </summary>
    public class ProductDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public int ManufacturerId { get; set; }
        public string ManufacturerName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public ProductStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ProductVariantDto> Variants { get; set; } = new();
    }

    /// <summary>
    /// DTO dùng cho trang danh sách sản phẩm (nhẹ hơn DetailDto).
    /// </summary>
    public class ProductListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string ManufacturerName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public ProductStatus Status { get; set; }

        /// <summary>
        /// Giá thấp nhất trong tất cả Variants.
        /// </summary>
        public decimal MinPrice { get; set; }

        /// <summary>
        /// Giá cao nhất trong tất cả Variants.
        /// </summary>
        public decimal MaxPrice { get; set; }

        /// <summary>
        /// Chuỗi khoảng giá hiển thị (VD: "20.000.000đ - 25.000.000đ").
        /// </summary>
        public string PriceRange { get; set; } = string.Empty;

        /// <summary>
        /// Ảnh đại diện (ảnh chính của Variant đầu tiên).
        /// </summary>
        public string? ThumbnailUrl { get; set; }

        /// <summary>
        /// Tổng tồn kho (tạm = 0, chờ module Inventory).
        /// </summary>
        public int TotalStock { get; set; }

        /// <summary>
        /// Số lượng biến thể.
        /// </summary>
        public int VariantCount { get; set; }

        public DateTime CreatedDate { get; set; }
    }

    // ========================================================
    // WRITE DTOs (CUD)
    // ========================================================

    /// <summary>
    /// Request tạo ảnh cho Variant.
    /// </summary>
    public class SaveImageRequest
    {
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
    }


    /// <summary>
    /// Request tạo Variant khi tạo Product mới.
    /// </summary>
    public class CreateVariantRequest
    {
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int WarrantyMonth { get; set; } = 12;
        public Dictionary<string, string>? Specifications { get; set; }
        public List<SaveImageRequest> Images { get; set; } = new();
    }

    /// <summary>
    /// Request tạo sản phẩm mới (bao gồm Variants lồng nhau).
    /// </summary>
    public class CreateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public int ManufacturerId { get; set; }
        public int CategoryId { get; set; }
        public List<CreateVariantRequest> Variants { get; set; } = new();
    }

    /// <summary>
    /// Request cập nhật thông tin chung sản phẩm.
    /// </summary>
    public class UpdateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public int ManufacturerId { get; set; }
        public int CategoryId { get; set; }
        public ProductStatus Status { get; set; }
    }

    /// <summary>
    /// Request thêm/sửa Variant trong Product đã tồn tại.
    /// Nếu Id == null → Thêm mới. Nếu Id != null → Cập nhật.
    /// </summary>
    public class SaveVariantRequest
    {
        public int? Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int WarrantyMonth { get; set; } = 12;
        public Dictionary<string, string>? Specifications { get; set; }
        public List<SaveImageRequest> Images { get; set; } = new();
    }

    /// <summary>
    /// Request cập nhật metadata của 1 variant (KHÔNG bao gồm ảnh + specs — dùng endpoint riêng).
    /// </summary>
    public class UpdateVariantRequest
    {
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int WarrantyMonth { get; set; } = 12;
    }

    /// <summary>
    /// Request thay toàn bộ ảnh của 1 variant.
    /// </summary>
    public class SaveVariantImagesRequest
    {
        public List<SaveImageRequest> Images { get; set; } = new();
    }

    /// <summary>
    /// Request thay toàn bộ specs của 1 variant.
    /// </summary>
    public class SaveVariantSpecificationsRequest
    {
        public Dictionary<string, string> Specifications { get; set; } = new();
    }

    // ========================================================
    // QUERY PARAMETERS
    // ========================================================

    /// <summary>
    /// Bộ lọc cho danh sách sản phẩm.
    /// </summary>
    public class ProductFilterRequest : PagedRequest
    {
        public string? Keyword { get; set; }
        public int? CategoryId { get; set; }
        public int? ManufacturerId { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }
        public ProductStatus? Status { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }

}

