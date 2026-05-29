using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Service.Products
{
    public interface IProductVariantService
    {
        /// <summary>
        /// Lấy chi tiết 1 variant (kèm ảnh + specs).
        /// </summary>
        Task<ApiResult<ProductVariantDto>> GetByIdAsync(int variantId);

        /// <summary>
        /// Thêm variant mới cho 1 product đã tồn tại (kèm ảnh + specs nếu có).
        /// </summary>
        Task<ApiResult<ProductVariantDto>> CreateAsync(int productId, SaveVariantRequest request);

        /// <summary>
        /// Cập nhật metadata (SKU, tên, giá, bảo hành) của 1 variant.
        /// </summary>
        Task<ApiResult<ProductVariantDto>> UpdateAsync(int variantId, UpdateVariantRequest request);

        /// <summary>
        /// Thay toàn bộ ảnh của 1 variant.
        /// </summary>
        Task<ApiResult<bool>> ReplaceImagesAsync(int variantId, SaveVariantImagesRequest request);

        /// <summary>
        /// Thay toàn bộ thông số kỹ thuật của 1 variant.
        /// </summary>
        Task<ApiResult<bool>> ReplaceSpecificationsAsync(int variantId, SaveVariantSpecificationsRequest request);

        /// <summary>
        /// Soft delete 1 variant. Bị chặn nếu đó là variant duy nhất còn lại của product.
        /// </summary>
        Task<ApiResult<bool>> DeleteAsync(int variantId);
    }
}
