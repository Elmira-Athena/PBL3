using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;
using PBL3.Shared.DTOs.Suppliers;

namespace Client.Services.Supplier
{
    public interface ISupplierClientService
    {
        Task<ApiResult<PagedResult<SupplierDto>>> GetListAsync(SupplierFilterRequest request);
        Task<ApiResult<SupplierDto>> GetByIdAsync(int id);
        Task<ApiResult<SupplierDto>> CreateAsync(CreateSupplierRequest request);
        Task<ApiResult<SupplierDto>> UpdateAsync(int id, UpdateSupplierRequest request);
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
