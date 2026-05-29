using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Banner (Banner hiển thị trên trang chủ).
    /// </summary>
    public interface IBannerRepository
    {
        /// <summary>
        /// Lấy danh sách banner phân trang + tìm kiếm theo Title.
        /// </summary>
        Task<(List<Banner> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết banner theo Id (no tracking).
        /// </summary>
        Task<Banner?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy chi tiết banner theo Id (with tracking) để cập nhật/soft-delete.
        /// </summary>
        Task<Banner?> GetByIdWithTrackingAsync(int id);

        /// <summary>
        /// Lấy danh sách banner đang hiệu lực (IsActive + trong khoảng StartDate/EndDate),
        /// sắp xếp theo SortOrder ASC, Id ASC. Dùng cho trang chủ public.
        /// </summary>
        Task<List<Banner>> GetActiveAsync(DateTime nowUtc);

        Task AddAsync(Banner banner);
        Task SaveChangesAsync();
    }
}
