using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Manufacturers;

namespace PBL3.Application.Manufacturers
{
    public interface IManufacturerService
    {
        /// <summary>
        /// Lấy danh sách hãng sản xuất có phân trang và tìm kiếm.
        /// </summary>
        Task<ApiResult<PagedResult<ManufacturerDto>>> GetPagedListAsync(ManufacturerFilterRequest filter);

        /// <summary>
        /// Lấy danh sách tất cả hãng (rút gọn) dùng cho Dropdown.
        /// </summary>
        Task<ApiResult<List<ManufacturerSummaryDto>>> GetAllForDropdownAsync();

        /// <summary>
        /// Lấy chi tiết hãng sản xuất theo Id.
        /// </summary>
        Task<ApiResult<ManufacturerDto>> GetByIdAsync(int id);

        /// <summary>
        /// Tạo mới hãng sản xuất.
        /// </summary>
        Task<ApiResult<ManufacturerDto>> CreateAsync(CreateManufacturerRequest request);

        /// <summary>
        /// Cập nhật hãng sản xuất.
        /// </summary>
        Task<ApiResult<ManufacturerDto>> UpdateAsync(int id, UpdateManufacturerRequest request);

        /// <summary>
        /// Xóa mềm hãng sản xuất (Soft Delete).
        /// Quy tắc: Không được xóa nếu còn sản phẩm đang dùng hãng này.
        /// </summary>
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
