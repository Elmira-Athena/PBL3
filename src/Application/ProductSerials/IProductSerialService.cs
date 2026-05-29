using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace PBL3.Application.ProductSerials
{
    public interface IProductSerialService
    {
        /// <summary>
        /// Kiểm tra Serial Number đã tồn tại trong DB chưa (theo VariantId).
        /// Trả về ApiResult chứa bool: true = đã tồn tại, false = mã mới.
        /// </summary>
        Task<ApiResult<bool>> CheckExistAsync(string serialNumber, int variantId);

        /// <summary>
        /// Danh sách phân trang Serial với bộ lọc đa điều kiện.
        /// </summary>
        Task<ApiResult<PagedResult<ProductSerialListDto>>> GetPagedListAsync(ProductSerialFilterRequest filter);

        /// <summary>
        /// Lấy chi tiết một Serial kèm thông tin đơn hàng (nếu đã bán).
        /// </summary>
        Task<ApiResult<ProductSerialDetailDto>> GetByIdAsync(int id);

        /// <summary>
        /// Thống kê số lượng Serial theo trạng thái. Có thể lọc theo productId hoặc variantId.
        /// </summary>
        Task<ApiResult<ProductSerialStatisticsDto>> GetStatisticsAsync(int? productId, int? variantId);

        /// <summary>
        /// Thay đổi trạng thái Serial (đánh dấu lỗi, hoàn trả, khôi phục về kho).
        /// </summary>
        Task<ApiResult<bool>> UpdateStatusAsync(int id, UpdateSerialStatusRequest request);
    }
}
