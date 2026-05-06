using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace Client.Services.Inventory
{
    public interface IProductSerialClientService
    {
        /// <summary>
        /// Kiểm tra mã Serial đã tồn tại trong DB chưa.
        /// </summary>
        /// <param name="serialNumber">Mã Serial cần kiểm tra.</param>
        /// <param name="variantId">Id của ProductVariant.</param>
        /// <returns>ApiResult chứa true nếu đã tồn tại, false nếu mã mới.</returns>
        Task<ApiResult<bool>> CheckExistAsync(string serialNumber, int variantId);

        /// <summary>
        /// Lấy danh sách phân trang ProductSerial với bộ lọc đa điều kiện.
        /// </summary>
        Task<ApiResult<PagedResult<ProductSerialListDto>>> GetPagedListAsync(ProductSerialFilterRequest filter);

        /// <summary>
        /// Lấy thống kê số Serial theo trạng thái.
        /// </summary>
        Task<ApiResult<ProductSerialStatisticsDto>> GetStatisticsAsync(int? productId = null, int? variantId = null);

        /// <summary>
        /// Lấy chi tiết một Serial kèm thông tin variant, phiếu nhập và đơn hàng (nếu có).
        /// </summary>
        Task<ApiResult<ProductSerialDetailDto>> GetByIdAsync(int id);

        /// <summary>
        /// Cập nhật trạng thái Serial.
        /// </summary>
        Task<ApiResult<bool>> UpdateStatusAsync(int id, UpdateSerialStatusRequest request);
    }
}
