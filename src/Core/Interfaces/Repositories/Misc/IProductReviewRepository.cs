using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho ProductReview (Đánh giá sản phẩm).
    /// </summary>
    public interface IProductReviewRepository
    {
        /// <summary>
        /// Lấy danh sách đánh giá theo productId có phân trang, sắp xếp mới nhất trước.
        /// </summary>
        Task<(List<ProductReview> Items, int TotalCount)> GetPagedByProductIdAsync(
            int productId, int pageNumber, int pageSize);

        /// <summary>
        /// Kiểm tra khách hàng đã đánh giá sản phẩm này chưa.
        /// </summary>
        Task<bool> ExistsAsync(int productId, Guid userId);

        Task<ProductReview?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy đánh giá theo Id (WITH TRACKING để soft-delete).
        /// </summary>
        Task<ProductReview?> GetByIdWithTrackingAsync(int id);

        Task AddAsync(ProductReview review);
        Task SaveChangesAsync();
    }
}
