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

        public CartService(ICartRepository cartRepo, IProductRepository productRepo, IUnitOfWork unitOfWork)
        {
            _cartRepo = cartRepo;
            _productRepo = productRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResult<CartResponse>> GetMyCartAsync(Guid userId)
        {
            var carts = await _cartRepo.GetCartItemsByUserAsync(userId);

            var response = new CartResponse();
            foreach (var cart in carts)
            {
                var image = cart.Variant.Images.FirstOrDefault(i => i.IsMain) 
                            ?? cart.Variant.Images.OrderBy(i => i.SortOrder).FirstOrDefault();

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
                    StockQuantity = cart.Variant.StockQuantity
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

            // 3. Check stock limit
            if (newTotal > variant.StockQuantity)
            {
                var remaining = variant.StockQuantity - currentQty;
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
                // Check stock limit
                var variant = await _productRepo.GetVariantByIdAsync(cart.VariantId);
                var stockLimit = variant?.StockQuantity ?? 99;

                if (request.Quantity > stockLimit)
                {
                    return ApiResult<CartResponse>.Fail($"Số lượng vượt tồn kho. Chỉ còn {stockLimit} sản phẩm.");
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
