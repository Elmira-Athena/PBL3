using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Cart.
    /// </summary>
    public interface ICartRepository
    {
        Task<List<Cart>> GetCartItemsByUserAsync(Guid userId);
        Task<List<Cart>> GetCartItemsWithTrackingAsync(Guid userId);

        /// <summary>
        /// Lấy 1 item trong giỏ theo Id (WITH TRACKING để update/delete).
        /// </summary>
        Task<Cart?> GetCartItemAsync(int cartItemId, Guid userId);

        /// <summary>
        /// Tìm item trong giỏ theo UserId + VariantId (WITH TRACKING để cộng dồn Quantity).
        /// </summary>
        Task<Cart?> FindByUserAndVariantAsync(Guid userId, int variantId);

        Task AddAsync(Cart cart);
        void Remove(Cart cart);
        void RemoveRange(IEnumerable<Cart> carts);
        Task SaveChangesAsync();
    }
}
