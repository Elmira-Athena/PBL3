using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Reviews;

namespace PBL3.Service.Reviews
{
    public interface IProductReviewService
    {
        Task<ApiResult<PagedResult<ReviewDto>>> GetReviewsAsync(int productId, int page, int pageSize);
        Task<ApiResult<ReviewDto>> CreateReviewAsync(CreateReviewRequest request, Guid userId);
        Task<ApiResult<bool>> DeleteReviewAsync(int reviewId, Guid userId);
    }
}
