using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Reviews;

namespace PBL3.Application.Reviews
{
    public class ProductReviewService : IProductReviewService
    {
        private readonly IProductReviewRepository _reviewRepo;
        private readonly HushStoreDbContext _context;

        public ProductReviewService(IProductReviewRepository reviewRepo, HushStoreDbContext context)
        {
            _reviewRepo = reviewRepo;
            _context = context;
        }

        public async Task<ApiResult<PagedResult<ReviewDto>>> GetReviewsAsync(
            int productId, int page, int pageSize)
        {
            var (items, totalCount) = await _reviewRepo.GetPagedByProductIdAsync(productId, page, pageSize);
            return ApiResult<PagedResult<ReviewDto>>.Ok(new PagedResult<ReviewDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            });
        }

        public async Task<ApiResult<ReviewDto>> CreateReviewAsync(
            CreateReviewRequest request, Guid userId)
        {
            var productExists = await _context.Products
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.ProductId && p.Status == 1 && !p.IsDeleted);

            if (!productExists)
                return ApiResult<ReviewDto>.Fail("Không tìm thấy sản phẩm yêu cầu.", ApiErrorCode.NotFound);

            if (await _reviewRepo.ExistsAsync(request.ProductId, userId))
                return ApiResult<ReviewDto>.Fail("Bạn đã đánh giá sản phẩm này rồi.");

            var review = new ProductReview
            {
                ProductId   = request.ProductId,
                UserId      = userId,
                Rating      = request.Rating,
                Title       = request.Title?.Trim(),
                Content     = request.Content?.Trim(),
                CreatedDate = DateTime.UtcNow
            };

            await _reviewRepo.AddAsync(review);
            await _reviewRepo.SaveChangesAsync();

            var profile = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            return ApiResult<ReviewDto>.Ok(new ReviewDto
            {
                Id            = review.Id,
                UserId        = userId,
                UserFullName  = profile?.FullName ?? "Khách hàng",
                UserAvatarUrl = profile?.AvatarUrl,
                Rating        = review.Rating,
                Title         = review.Title,
                Content       = review.Content,
                CreatedDate   = review.CreatedDate
            }, "Đánh giá của bạn đã được ghi nhận.");
        }

        public async Task<ApiResult<bool>> DeleteReviewAsync(int reviewId, Guid userId)
        {
            var review = await _reviewRepo.GetByIdWithTrackingAsync(reviewId);

            if (review == null)
                return ApiResult<bool>.Fail("Không tìm thấy đánh giá yêu cầu.", ApiErrorCode.NotFound);

            if (review.UserId != userId)
                return ApiResult<bool>.Fail("Bạn không có quyền xóa đánh giá này.", ApiErrorCode.Forbidden);

            review.IsDeleted   = true;
            review.DeletedDate = DateTime.UtcNow;

            await _reviewRepo.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Đánh giá đã được xóa.");
        }

        private static ReviewDto MapToDto(ProductReview r) => new()
        {
            Id            = r.Id,
            UserId        = r.UserId,
            UserFullName  = r.User?.Profile?.FullName ?? r.User?.UserName ?? "Khách hàng",
            UserAvatarUrl = r.User?.Profile?.AvatarUrl,
            Rating        = r.Rating,
            Title         = r.Title,
            Content       = r.Content,
            CreatedDate   = r.CreatedDate
        };
    }
}
