namespace PBL3.Shared.DTOs.Products
{
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
}
