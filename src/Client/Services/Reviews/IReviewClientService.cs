using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Reviews;

namespace Client.Services.Reviews
{
    public interface IReviewClientService
    {
        Task<ApiResult<PagedResult<ReviewDto>>?> GetReviewsAsync(int productId, int page, int pageSize);
        Task<ApiResult<ReviewDto>?> CreateAsync(CreateReviewRequest request);
        Task<ApiResult<bool>?> DeleteAsync(int reviewId);
    }
}
