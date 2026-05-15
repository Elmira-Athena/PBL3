using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Banner
{
    public interface IBannerClientService
    {
        Task<ApiResult<PagedResult<BannerDto>>> GetListAsync(BannerFilterRequest request);
        Task<ApiResult<BannerDto>> GetByIdAsync(int id);
        Task<ApiResult<BannerDto>> CreateAsync(CreateBannerRequest request);
        Task<ApiResult<BannerDto>> UpdateAsync(int id, UpdateBannerRequest request);
        Task<ApiResult<bool>> DeleteAsync(int id);

        /// <summary>
        /// Lấy danh sách banner đang hiệu lực cho trang chủ (không cần auth).
        /// </summary>
        Task<ApiResult<List<BannerPublicDto>>> GetActiveAsync();
    }
}
