using PBL3.Shared.DTOs.Common;

namespace Client.Services.Address
{
    public interface IProvinceClientService
    {
        Task<ApiResult<List<ProvinceItem>>> GetProvincesAsync();
        Task<ApiResult<List<WardItem>>> GetWardsAsync(int provinceCode);
    }
}
