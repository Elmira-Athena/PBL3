using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Sale;

namespace PBL3.Service.Orders
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderRepository _orderRepo;
        private readonly IVoucherRepository _voucherRepo;
        private readonly IProductRepository _productRepo; // Assuming we need to get prices
        private readonly IMapper _mapper;

        public OrderService(
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepo,
            IVoucherRepository voucherRepo,
            IProductRepository productRepo,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _orderRepo = orderRepo;
            _voucherRepo = voucherRepo;
            _productRepo = productRepo;
            _mapper = mapper;
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
                        // we can update it this way because EF Core tracks it from GetByCodesAsync! (Actually, GetByCodesAsync might need to be tracked or we get it fresh here)
                    }
                }

                // 4e-4f. Flush and Commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // 5. Mapping
                // Note: to get the full mapper correctly we'd map from order after loading details, 
                // but since we just saved it we can construct the DTO directly or use a Db mapping.
                
                // For simplicity, we just return success without the full DTO tree immediately, or re-query.
                // Re-query:
                var savedOrderInfo = await _orderRepo.GetByIdWithDetailsAsync(order.Id);
                var dto = _mapper.Map<OrderDetailDto>(savedOrderInfo);

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

            var dto = _mapper.Map<OrderDetailDto>(order);
            return ApiResult<OrderDetailDto>.Ok(dto);
        }
    }
}
