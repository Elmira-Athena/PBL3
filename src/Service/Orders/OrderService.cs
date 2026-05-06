using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Sale;
using PBL3.Shared.DTOs.Products;
namespace PBL3.Service.Orders
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderRepository _orderRepo;
        private readonly IVoucherRepository _voucherRepo;
        private readonly IProductRepository _productRepo;
        private readonly ICartRepository _cartRepo;
        private readonly IUserAddressRepository _userAddressRepo;
        private readonly IProductSerialRepository _productSerialRepo;

        public OrderService(
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepo,
            IVoucherRepository voucherRepo,
            IProductRepository productRepo,
            ICartRepository cartRepo,
            IUserAddressRepository userAddressRepo,
            IProductSerialRepository productSerialRepo)
        {
            _unitOfWork = unitOfWork;
            _orderRepo = orderRepo;
            _voucherRepo = voucherRepo;
            _productRepo = productRepo;
            _cartRepo = cartRepo;
            _userAddressRepo = userAddressRepo;
            _productSerialRepo = productSerialRepo;
        }

        public async Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request, Guid userId)
        {
            // Step 1: Data Source (Cart vs Buy Now)
            var checkoutItems = new List<(int VariantId, int Quantity, decimal Price)>();
            List<PBL3.Core.Entities.Cart>? cartsToRemove = null;

            if (request.IsBuyNow)
            {
                if (!request.BuyNowVariantId.HasValue || !request.BuyNowQuantity.HasValue || request.BuyNowQuantity.Value <= 0)
                {
                    return ApiResult<CheckoutResponse>.Fail("Thông tin mua ngay không hợp lệ.");
                }

                var variant = await _productRepo.GetVariantByIdAsync(request.BuyNowVariantId.Value);
                if (variant == null)
                {
                    return ApiResult<CheckoutResponse>.Fail("Sản phẩm không tồn tại.");
                }

                checkoutItems.Add((variant.Id, request.BuyNowQuantity.Value, variant.Price));
            }
            else
            {
                var carts = await _cartRepo.GetCartItemsWithTrackingAsync(userId);
                if (!carts.Any())
                {
                    return ApiResult<CheckoutResponse>.Fail("Giỏ hàng của bạn đang trống.");
                }

                checkoutItems = carts.Select(c => (c.VariantId, c.Quantity, c.Variant.Price)).ToList();
                cartsToRemove = carts;
            }

            // Step 2: Address & Inventory Validation
            var address = await _userAddressRepo.GetByIdAsync(request.UserAddressId);
            if (address == null || address.UserId != userId)
            {
                return ApiResult<CheckoutResponse>.Fail("Địa chỉ giao hàng không hợp lệ.");
            }

            var variantIds = checkoutItems.Select(x => x.VariantId).Distinct().ToList();
            
            // Batch Query: Count available serials
            var availableSerialsMap = await _productSerialRepo.CountAvailableByVariantIdsAsync(variantIds);
            
            // Batch Query: Sum quantities in active orders
            var activeOrderQuantitiesMap = await _orderRepo.GetActiveOrderQuantitiesByVariantIdsAsync(variantIds);

            // Check Virtual Inventory
            foreach (var item in checkoutItems)
            {
                var availableSerials = availableSerialsMap.ContainsKey(item.VariantId) ? availableSerialsMap[item.VariantId] : 0;
                var reservedInOrders = activeOrderQuantitiesMap.ContainsKey(item.VariantId) ? activeOrderQuantitiesMap[item.VariantId] : 0;
                var realAvailableStock = availableSerials - reservedInOrders;

                if (realAvailableStock < item.Quantity)
                {
                    return ApiResult<CheckoutResponse>.Fail($"Sản phẩm có mã {item.VariantId} hiện đã hết hàng hoặc không đủ số lượng (Khả dụng: {Math.Max(0, realAvailableStock)}).");
                }
            }

            // Step 3: Voucher & Price Calculation
            decimal subTotal = checkoutItems.Sum(x => x.Price * x.Quantity);
            
            var (usages, totalDiscount) = await ApplyVouchersAsync(subTotal, request.VoucherCodes, userId);
            totalDiscount = Math.Min(totalDiscount, subTotal);

            decimal totalAmount = subTotal + request.ShippingFee - totalDiscount;

            // Step 4: Create Order (within Transaction)
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                string datePrefix = "ORD-" + DateTime.Now.ToString("yyyyMMdd");
                string? lastCode = await _orderRepo.GetLastOrderCodeByDateAsync(datePrefix);
                int nextIndex = 1;
                if (!string.IsNullOrEmpty(lastCode))
                {
                    string suffix = lastCode.Substring(lastCode.LastIndexOf('-') + 1);
                    if (int.TryParse(suffix, out int lastIndex))
                    {
                        nextIndex = lastIndex + 1;
                    }
                }
                string newOrderCode = $"{datePrefix}-{nextIndex:D3}";

                byte orderStatus = (byte)(request.PaymentMethod == 0 ? 1 : 0); // 1: Confirmed (COD), 0: Pending (Online)

                var order = new Order
                {
                    OrderCode = newOrderCode,
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    Status = orderStatus,
                    SubTotal = subTotal,
                    ShippingFee = request.ShippingFee,
                    DiscountAmount = totalDiscount,
                    TotalAmount = totalAmount,
                    ShipName = address.ReceiverName,
                    ShipPhone = address.PhoneNumber,
                    ShipAddress = address.AddressLine,
                    ShipCity = address.City,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = 0,
                    OrderType = 0, // Online
                    Note = request.Note
                };

                await _orderRepo.AddAsync(order);
                await _unitOfWork.SaveChangesAsync(); // To get order.Id

                // Insert OrderDetails
                foreach (var item in checkoutItems)
                {
                    order.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.Id,
                        VariantId = item.VariantId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    });
                }

                // Apply voucher usages
                foreach (var usage in usages)
                {
                    usage.OrderId = order.Id;
                }
                if (usages.Any())
                {
                    await _voucherRepo.AddUsagesAsync(usages);
                    
                    if (request.VoucherCodes != null && request.VoucherCodes.Any())
                    {
                        var vouchersToUpdate = await _voucherRepo.GetByCodesAsync(request.VoucherCodes);
                        foreach (var voucher in vouchersToUpdate)
                        {
                            voucher.UsedCount += 1;
                        }
                    }
                }

                // Step 5: Cleanup & Commit
                if (!request.IsBuyNow && cartsToRemove != null)
                {
                    _cartRepo.RemoveRange(cartsToRemove);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                var response = new CheckoutResponse
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    TotalAmount = order.TotalAmount,
                    Status = order.Status,
                    PaymentMethod = order.PaymentMethod,
                    PaymentUrl = null // TODO: Generate MoMo/VNPay URL if PaymentMethod == 1
                };

                return ApiResult<CheckoutResponse>.Ok(response, "Đặt hàng thành công!");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                throw new Exception("Lỗi hệ thống khi đặt hàng: " + ex.Message, ex);
            }
        }

        public async Task<ApiResult<OrderDetailDto>> PlaceOrderAsync(CreateOrderRequest request, Guid userId)
        {
            // 1. Validate basic and get variant prices
            decimal subTotal = 0;
            var variantPrices = new Dictionary<int, decimal>();

            // Ideally this is optimized to one DB query, but simplifying for demonstration
            foreach (var item in request.Items)
            {
                var variant = await _productRepo.GetVariantByIdAsync(item.VariantId);
                if (variant == null)
                {
                    return ApiResult<OrderDetailDto>.Fail($"Không tìm thấy sản phẩm có mã {item.VariantId}");
                }
                
                // Usually we also check stock here...
                
                variantPrices[item.VariantId] = variant.Price;
                subTotal += variant.Price * item.Quantity;
            }

            decimal shippingFee = 30000; // Hardcoded or calculated

            // 2. Apply Vouchers
            var (usages, totalDiscount) = await ApplyVouchersAsync(subTotal, request.VoucherCodes, userId);

            // 3. Ensure discount <= subtotal
            totalDiscount = Math.Min(totalDiscount, subTotal);

            // 4. TRANSACTION
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Generate OrderCode (e.g. ORD-YYYYMMDD-001)
                string datePrefix = "ORD-" + DateTime.Now.ToString("yyyyMMdd");
                string? lastCode = await _orderRepo.GetLastOrderCodeByDateAsync(datePrefix);
                int nextIndex = 1;
                if (!string.IsNullOrEmpty(lastCode))
                {
                    string suffix = lastCode.Substring(lastCode.LastIndexOf('-') + 1);
                    if (int.TryParse(suffix, out int lastIndex))
                    {
                        nextIndex = lastIndex + 1;
                    }
                }
                string newOrderCode = $"{datePrefix}-{nextIndex:D3}";

                // 4a. Insert Order
                var order = new Order
                {
                    OrderCode = newOrderCode,
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    Status = 0, // Pending
                    SubTotal = subTotal,
                    ShippingFee = shippingFee,
                    DiscountAmount = totalDiscount,
                    TotalAmount = subTotal + shippingFee - totalDiscount,
                    ShipName = request.ShipName,
                    ShipPhone = request.ShipPhone,
                    ShipAddress = request.ShipAddress,
                    ShipCity = request.ShipCity,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = 0, // Unpaid
                    Note = request.Note
                };
                
                await _orderRepo.AddAsync(order);
                await _unitOfWork.SaveChangesAsync(); // Get Order.Id

                // 4b. Insert OrderDetails
                foreach (var item in request.Items)
                {
                    order.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.Id,
                        VariantId = item.VariantId,
                        Quantity = item.Quantity,
                        UnitPrice = variantPrices[item.VariantId]
                    });
                }

                // 4c. Update Usages with OrderId and Add
                foreach (var usage in usages)
                {
                    usage.OrderId = order.Id;
                }
                await _voucherRepo.AddUsagesAsync(usages);

                // 4d. Increase UsedCount of vouchers
                if (request.VoucherCodes != null && request.VoucherCodes.Any())
                {
                    var vouchers = await _voucherRepo.GetByCodesAsync(request.VoucherCodes);
                    foreach (var voucher in vouchers)
                    {
                        voucher.UsedCount += 1;
                    }
                }

                // 4e-4f. Flush and Commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // 5. Manual Mapping
                var savedOrderInfo = await _orderRepo.GetByIdWithDetailsAsync(order.Id);
                var dto = MapToOrderDetailDto(savedOrderInfo);

                return ApiResult<OrderDetailDto>.Ok(dto, "Đặt hàng thành công!");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                throw new Exception("Lỗi khi tạo đơn hàng: " + ex.Message, ex);
            }
        }

        private async Task<(List<VoucherUsage> Usages, decimal TotalDiscount)> ApplyVouchersAsync(
            decimal subTotal,
            List<string>? voucherCodes,
            Guid userId)
        {
            var usages = new List<VoucherUsage>();
            if (voucherCodes == null || !voucherCodes.Any())
            {
                return (usages, 0);
            }

            var vouchers = await _voucherRepo.GetByCodesAsync(voucherCodes);
            
            var foundCodes = vouchers.Select(v => v.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalidCodes = voucherCodes.Where(c => !foundCodes.Contains(c)).ToList();
            if (invalidCodes.Any())
            {
                throw new Exception($"Mã giảm giá không tồn tại: {string.Join(", ", invalidCodes)}");
            }

            var now = DateTime.UtcNow;
            foreach (var voucher in vouchers)
            {
                if (!voucher.IsActive)
                    throw new Exception($"Mã '{voucher.Code}' đã bị vô hiệu hóa.");

                if (now < voucher.StartDate || now > voucher.EndDate)
                    throw new Exception($"Mã '{voucher.Code}' đã hết hạn hoặc chưa đến thời gian sử dụng.");

                if (voucher.UsedCount >= voucher.Quantity)
                    throw new Exception($"Mã '{voucher.Code}' đã hết lượt sử dụng.");

                if (subTotal < voucher.MinOrderValue)
                    throw new Exception(
                        $"Mã '{voucher.Code}' yêu cầu đơn hàng tối thiểu {voucher.MinOrderValue:#,0}đ " +
                        $"(đơn hiện tại: {subTotal:#,0}đ).");
            }

            var voucherIds = vouchers.Select(v => v.Id).ToList();
            var alreadyUsedIds = await _voucherRepo.GetUsedVoucherIdsByUserAsync(userId, voucherIds);
            if (alreadyUsedIds.Any())
            {
                var usedCodes = vouchers.Where(v => alreadyUsedIds.Contains(v.Id))
                                        .Select(v => v.Code);
                throw new Exception(
                    $"Bạn đã sử dụng mã giảm giá: {string.Join(", ", usedCodes)}. " +
                    "Mỗi mã chỉ được sử dụng 1 lần.");
            }

            decimal totalDiscount = 0;

            foreach (var voucher in vouchers)
            {
                decimal discountApplied = 0;

                if (voucher.DiscountType == 0) // Amount
                {
                    discountApplied = voucher.DiscountValue;
                }
                else // Percentage
                {
                    discountApplied = subTotal * voucher.DiscountValue / 100;
                    if (voucher.MaxDiscountAmount.HasValue && discountApplied > voucher.MaxDiscountAmount.Value)
                    {
                        discountApplied = voucher.MaxDiscountAmount.Value;
                    }
                }

                totalDiscount += discountApplied;

                usages.Add(new VoucherUsage
                {
                    VoucherId = voucher.Id,
                    UserId = userId,
                    DiscountApplied = discountApplied,
                    UsedDate = DateTime.UtcNow
                });
            }

            return (usages, totalDiscount);
        }

        public async Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id)
        {
            var order = await _orderRepo.GetByIdWithDetailsAsync(id);
            if (order == null)
                return ApiResult<OrderDetailDto>.Fail("Không tìm thấy đơn hàng.");

            var dto = MapToOrderDetailDto(order);
            return ApiResult<OrderDetailDto>.Ok(dto);
        }

        public async Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetPagedOrdersAsync(OrderFilterRequest request)
        {
            var query = _orderRepo.GetQueryable().AsNoTracking();

            if (!string.IsNullOrEmpty(request.Keyword))
            {
                var lowerKeyword = request.Keyword.ToLower();
                query = query.Where(o => o.OrderCode.ToLower().Contains(lowerKeyword) || 
                                         o.ShipName.ToLower().Contains(lowerKeyword) || 
                                         o.ShipPhone.Contains(request.Keyword));
            }

            if (request.Status.HasValue)
            {
                query = query.Where(o => o.Status == request.Status.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(o => o.OrderDate >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(o => o.OrderDate <= request.ToDate.Value);
            }

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(o => new OrderSummaryResponse
                {
                    Id = o.Id,
                    OrderCode = o.OrderCode,
                    CustomerName = o.ShipName,
                    CustomerPhone = o.ShipPhone,
                    TotalAmount = o.TotalAmount,
                    CreatedDate = o.OrderDate,
                    Status = o.Status,
                    PaymentStatus = o.PaymentStatus
                })
                .ToListAsync();

            var result = new PagedResult<OrderSummaryResponse>
            {
                Items = items,
                TotalCount = totalCount,
                PageSize = request.PageSize,
                PageNumber = request.PageIndex
            };

            return ApiResult<PagedResult<OrderSummaryResponse>>.Ok(result);
        }

        public async Task<ApiResult<bool>> CancelOrderAsync(int id, CancelOrderRequest request)
        {
            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null)
            {
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.");
            }

            if (order.Status == 2)
            {
                throw new Exception("Đơn hàng đang giao (Shipping). Tuyệt đối cấm hủy.");
            }

            if (order.Status != 0 && order.Status != 1)
            {
                return ApiResult<bool>.Fail($"Không thể hủy đơn hàng ở trạng thái hiện tại.");
            }

            order.Status = 4; // Cancelled
            order.CancelReason = request.CancelReason;

            await _unitOfWork.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Hủy đơn hàng thành công.");
        }

        public async Task<ApiResult<bool>> CompleteOrderAsync(int id)
        {
            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null)
            {
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.");
            }

            if (order.Status != 2)
            {
                return ApiResult<bool>.Fail("Chỉ có thể xác nhận giao cho đơn hàng đang trong trạng thái 'Đang giao'.");
            }

            order.Status = 3; // Success
            await _unitOfWork.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Đơn hàng đã được đánh dấu hoàn thành.");
        }

        private OrderDetailDto MapToOrderDetailDto(Order order)
        {
            return new OrderDetailDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                OrderDate = order.OrderDate,
                Status = order.Status,
                ShipName = order.ShipName,
                ShipPhone = order.ShipPhone,
                ShipAddress = order.ShipAddress,
                ShipCity = order.ShipCity,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                OrderType = order.OrderType,
                Note = order.Note,
                CancelReason = order.CancelReason,
                SubTotal = order.SubTotal,
                ShippingFee = order.ShippingFee,
                DiscountAmount = order.DiscountAmount,
                TotalAmount = order.TotalAmount,
                Items = order.OrderDetails.Select(d => new OrderDetailLineDto
                {
                    Id = d.Id,
                    VariantId = d.VariantId,
                    VariantName = d.Variant.VariantName,
                    SKU = d.Variant.SKU,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    TotalLine = d.Quantity * d.UnitPrice,
                    Serials = d.OrderSerials?.Select(os => os.Serial.SerialNumber).ToList() ?? new List<string>()
                }).ToList(),
                AppliedVouchers = order.VoucherUsages?.Select(v => new VoucherUsageDto
                {
                    VoucherCode = v.Voucher.Code,
                    VoucherName = v.Voucher.Name,
                    DiscountType = v.Voucher.DiscountType,
                    DiscountValue = v.Voucher.DiscountValue,
                    DiscountApplied = v.DiscountApplied
                }).ToList() ?? new List<VoucherUsageDto>()
            };
        }
    }
}
