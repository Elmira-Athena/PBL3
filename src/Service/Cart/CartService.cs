using System;
using System.Linq;
using System.Threading.Tasks;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Cart;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Service.Cart
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepo;
        private readonly IProductRepository _productRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProductSerialRepository _serialRepo;
        private readonly IOrderRepository _orderRepo;

        public CartService(
            ICartRepository cartRepo,
            IProductRepository productRepo,
            IUnitOfWork unitOfWork,
            IProductSerialRepository serialRepo,
            IOrderRepository orderRepo)
        {
            _cartRepo = cartRepo;
            _productRepo = productRepo;
            _unitOfWork = unitOfWork;
            _serialRepo = serialRepo;
            _orderRepo = orderRepo;
        }

        /// <summary>
        /// Tính tồn kho ảo (Virtual Stock) cho một Variant.
        /// Tồn kho ảo = Số serial Available - Số lượng đang giữ trong đơn Pending/Confirmed.
        /// </summary>
        private async Task<int> GetVirtualStockAsync(int variantId)
        {
            var available = await _serialRepo.CountAvailableByVariantIdsAsync(new List<int> { variantId });
            var reserved  = await _orderRepo.GetActiveOrderQuantitiesByVariantIdsAsync(new List<int> { variantId });
            return Math.Max(0, available.GetValueOrDefault(variantId) - reserved.GetValueOrDefault(variantId));
        }

        public async Task<ApiResult<CartResponse>> GetMyCartAsync(Guid userId)
        {
            var carts = await _cartRepo.GetCartItemsByUserAsync(userId);

            // Batch tính virtual stock cho tất cả variant trong giỏ hàng
            var allVariantIds = carts.Select(c => c.VariantId).Distinct().ToList();
            Dictionary<int, int> virtualStockMap = new();
            if (allVariantIds.Any())
            {
                var available = await _serialRepo.CountAvailableByVariantIdsAsync(allVariantIds);
                var reserved  = await _orderRepo.GetActiveOrderQuantitiesByVariantIdsAsync(allVariantIds);
                virtualStockMap = allVariantIds.ToDictionary(
                    id => id,
                    id => Math.Max(0, available.GetValueOrDefault(id) - reserved.GetValueOrDefault(id)));
            }

            var response = new CartResponse();
            foreach (var cart in carts)
            {
                var image = cart.Variant.Images.FirstOrDefault(i => i.IsMain)
                            ?? cart.Variant.Images.OrderBy(i => i.SortOrder).FirstOrDefault();

                var virtualStock = virtualStockMap.GetValueOrDefault(cart.VariantId, 0);

                response.Items.Add(new CartItemResponse
                {
                    Id = cart.Id,
                    VariantId = cart.VariantId,
                    ProductName = cart.Variant.Product.Name,
                    VariantName = cart.Variant.VariantName,
                    ImageUrl = image?.ImageUrl,
                    UnitPrice = cart.Variant.Price,
                    Quantity = cart.Quantity,
                    SubTotal = cart.Variant.Price * cart.Quantity,
                    StockQuantity = virtualStock,
                    ProductSlug = cart.Variant.Product.Slug
                });
            }

            response.TotalAmount = response.Items.Sum(i => i.SubTotal);

            return ApiResult<CartResponse>.Ok(response);
        }

        public async Task<ApiResult<CartResponse>> AddToCartAsync(Guid userId, AddToCartRequest request)
        {
            // 1. Check if Variant exists and valid
            var variant = await _productRepo.GetVariantByIdAsync(request.VariantId);
            if (variant == null)
            {
                return ApiResult<CartResponse>.Fail("Sản phẩm không tồn tại hoặc đã ngừng kinh doanh.");
            }

            var product = await _productRepo.GetByIdAsync(variant.ProductId);
            if (product == null || product.Status != 1 || product.IsDeleted)
            {
                return ApiResult<CartResponse>.Fail("Sản phẩm không tồn tại hoặc đã ngừng kinh doanh.");
            }

            if (request.Quantity <= 0)
            {
                return ApiResult<CartResponse>.Fail("Số lượng không hợp lệ.");
            }

            // 2. Check if already in cart
            var existingCart = await _cartRepo.FindByUserAndVariantAsync(userId, request.VariantId);

            var currentQty = existingCart?.Quantity ?? 0;
            var newTotal = currentQty + request.Quantity;

            // 3. Kiểm tra virtual stock (thực tế khả dụng = Available serials - đang giữ trong đơn Pending/Confirmed)
            var virtualStock = await GetVirtualStockAsync(request.VariantId);
            if (newTotal > virtualStock)
            {
                var remaining = virtualStock - currentQty;
                return ApiResult<CartResponse>.Fail(remaining <= 0
                    ? "Sản phẩm đã đạt giới hạn tồn kho trong giỏ hàng."
                    : $"Chỉ có thể thêm tối đa {remaining} sản phẩm nữa.");
            }

            if (existingCart != null)
            {
                // Accumulate quantity
                existingCart.Quantity = newTotal;
            }
            else
            {
                // Create new
                var newCart = new PBL3.Core.Entities.Cart
                {
                    UserId = userId,
                    VariantId = request.VariantId,
                    Quantity = request.Quantity,
                    CreatedDate = DateTime.UtcNow
                };
                await _cartRepo.AddAsync(newCart);
            }

            await _unitOfWork.SaveChangesAsync();

            // Return updated cart
            return await GetMyCartAsync(userId);
        }

        public async Task<ApiResult<CartResponse>> UpdateQuantityAsync(Guid userId, int cartItemId, UpdateCartItemRequest request)
        {
            var cart = await _cartRepo.GetCartItemAsync(cartItemId, userId);
            if (cart == null)
            {
                return ApiResult<CartResponse>.Fail("Không tìm thấy sản phẩm trong giỏ hàng.");
            }

            if (request.Quantity <= 0)
            {
                // Auto remove if quantity <= 0
                _cartRepo.Remove(cart);
            }
            else
            {
                // Kiểm tra virtual stock
                var virtualStock = await GetVirtualStockAsync(cart.VariantId);

                if (request.Quantity > virtualStock)
                {
                    return ApiResult<CartResponse>.Fail($"Số lượng vượt tồn kho ảo. Chỉ còn {virtualStock} sản phẩm khả dụng.");
                }

                cart.Quantity = request.Quantity;
            }

            await _unitOfWork.SaveChangesAsync();

            return await GetMyCartAsync(userId);
        }

        public async Task<ApiResult<CartResponse>> RemoveItemAsync(Guid userId, int cartItemId)
        {
            var cart = await _cartRepo.GetCartItemAsync(cartItemId, userId);
            if (cart == null)
            {
                return ApiResult<CartResponse>.Fail("Không tìm thấy sản phẩm trong giỏ hàng.");
            }

            _cartRepo.Remove(cart);
            await _unitOfWork.SaveChangesAsync();

            return await GetMyCartAsync(userId);
        }

        public async Task<ApiResult<bool>> ClearCartAsync(Guid userId)
        {
            var carts = await _cartRepo.GetCartItemsWithTrackingAsync(userId);
            if (carts.Any())
            {
                _cartRepo.RemoveRange(carts);
                await _unitOfWork.SaveChangesAsync();
            }

            return ApiResult<bool>.Ok(true, "Đã xóa toàn bộ giỏ hàng.");
        }
    }
}
