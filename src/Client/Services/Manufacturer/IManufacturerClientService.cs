using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Manufacturers;

namespace Client.Services.Manufacturer
{
    public interface IManufacturerClientService
    {
        Task<ApiResult<PagedResult<ManufacturerDto>>> GetListAsync(ManufacturerFilterRequest request);
        Task<ApiResult<List<ManufacturerSummaryDto>>> GetDropdownAsync();
        Task<ApiResult<ManufacturerDto>> GetByIdAsync(int id);
        Task<ApiResult<ManufacturerDto>> CreateAsync(CreateManufacturerRequest request);
        Task<ApiResult<ManufacturerDto>> UpdateAsync(int id, UpdateManufacturerRequest request);
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
