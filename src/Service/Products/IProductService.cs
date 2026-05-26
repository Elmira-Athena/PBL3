using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Service.Products
{
    public interface IProductService
    {
        /// <summary>
        /// Lấy danh sách sản phẩm có phân trang và bộ lọc.
        /// </summary>
        Task<ApiResult<PagedResult<ProductListDto>>> GetListAsync(ProductFilterRequest request);

        /// <summary>
        /// Lấy chi tiết sản phẩm theo Id (bao gồm Variants, Images, Attributes).
        /// </summary>
        Task<ApiResult<ProductDetailDto>> GetByIdAsync(int id);

        /// <summary>
        /// Tạo mới sản phẩm (bao gồm Variants lồng nhau, dùng Transaction).
        /// </summary>
        Task<ApiResult<ProductDetailDto>> CreateAsync(CreateProductRequest request);

        /// <summary>
        /// Cập nhật thông tin chung của sản phẩm.
        /// </summary>
        Task<ApiResult<ProductDetailDto>> UpdateAsync(int id, UpdateProductRequest request);

        /// <summary>
        /// Soft Delete sản phẩm.
        /// </summary>
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
