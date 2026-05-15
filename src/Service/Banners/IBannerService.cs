using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Service.Banners
{
    public interface IBannerService
    {
        Task<ApiResult<PagedResult<BannerDto>>> GetPagedListAsync(BannerFilterRequest filter);
        Task<ApiResult<BannerDto>> GetByIdAsync(int id);

        /// <summary>
        /// Banner đang hiệu lực cho trang chủ (IsActive + trong khoảng StartDate/EndDate).
        /// </summary>
        Task<ApiResult<List<BannerPublicDto>>> GetActiveAsync();

        Task<ApiResult<BannerDto>> CreateAsync(CreateBannerRequest request);
        Task<ApiResult<BannerDto>> UpdateAsync(int id, UpdateBannerRequest request);
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
