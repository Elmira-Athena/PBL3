using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;
using PBL3.Shared.DTOs.Suppliers;

namespace PBL3.Application.Suppliers
{
    public interface ISupplierService
    {
        /// <summary>
        /// Lấy danh sách nhà cung cấp có phân trang và tìm kiếm.
        /// </summary>
        Task<ApiResult<PagedResult<SupplierDto>>> GetPagedListAsync(SupplierFilterRequest filter);

        /// <summary>
        /// Lấy chi tiết nhà cung cấp theo Id.
        /// </summary>
        Task<ApiResult<SupplierDto>> GetByIdAsync(int id);

        /// <summary>
        /// Tạo mới nhà cung cấp.
        /// </summary>
        Task<ApiResult<SupplierDto>> CreateAsync(CreateSupplierRequest request);

        /// <summary>
        /// Cập nhật nhà cung cấp.
        /// </summary>
        Task<ApiResult<SupplierDto>> UpdateAsync(int id, UpdateSupplierRequest request);

        /// <summary>
        /// Xóa mềm nhà cung cấp (Soft Delete).
        /// </summary>
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
