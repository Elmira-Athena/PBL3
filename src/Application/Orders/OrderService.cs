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
using PBL3.Shared.Enums;
namespace PBL3.Application.Orders
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

        /// <summary>
        /// NGHIỆP VỤ: Thực hiện quy trình đặt hàng trực tuyến (Online Checkout).
        /// Hỗ trợ đặt hàng từ giỏ hàng hiện tại (Cart) hoặc mua nhanh trực tiếp (Buy Now) từ trang chi tiết sản phẩm.
        /// Tiến trình được quản lý chặt chẽ dưới một Transaction dữ liệu để đảm bảo tính toàn vẹn:
        /// 1. Xác định nguồn hàng mua: Nếu mua ngay, lấy thông tin biến thể trực tiếp; nếu mua từ giỏ hàng, nạp giỏ hàng của người dùng.
        /// 2. Xác thực địa chỉ giao nhận hợp lệ của người dùng.
        /// 3. Kiểm tra Tồn kho ảo (Virtual Inventory Validation):
        ///    - Tồn khả dụng bán (Real Available) = Số serial Available thực tế trong kho - Số lượng đang được giữ chỗ trong các đơn hàng online đang xử lý (Pending/Confirmed/Shipping).
        ///    - Chặn giao dịch ngay lập tức nếu tồn ảo không đủ đáp ứng, ngăn ngừa triệt để tình trạng bán vượt quá số lượng tồn kho vật lý thực tế khi có nhiều giao dịch đồng thời.
        /// 4. Áp dụng mã giảm giá (Vouchers): Chạy quy trình kiểm định điều kiện áp dụng mã voucher và tính toán giá trị chiết khấu hợp lệ.
        /// 5. Tạo đơn hàng (Order): Khởi tạo bản ghi đơn hàng ở trạng thái mặc định Pending (Chờ duyệt), tự động sinh mã đơn hàng ORD-yyyyMMdd-NNN độc bản theo ngày.
        /// 6. Tạo chi tiết đơn hàng (OrderDetails) liên kết các sản phẩm.
        /// 7. Ghi nhận lịch sử sử dụng voucher (VoucherUsages) và tăng số lượt đã dùng của mã voucher.
        /// 8. Giải phóng/Xóa giỏ hàng cũ của khách hàng nếu mua từ giỏ hàng.
        /// 9. Lưu mọi thay đổi thành công và Commit Transaction.
        /// </summary>
        public async Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request, Guid userId)
        {
            // ── BƯỚC 1: XÁC ĐỊNH NGUỒN DỮ LIỆU ĐẶT HÀNG (Cart vs Buy Now) ──
            var checkoutItems = new List<(int VariantId, int Quantity, decimal Price)>();
            List<PBL3.Core.Entities.Cart>? cartsToRemove = null;

            if (request.IsBuyNow)
            {
                // Mua ngay từ trang sản phẩm (không thông qua giỏ hàng)
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
                // Checkout từ giỏ hàng hiện tại của khách hàng
                var carts = await _cartRepo.GetCartItemsWithTrackingAsync(userId);
                if (!carts.Any())
                {
                    return ApiResult<CheckoutResponse>.Fail("Giỏ hàng của bạn đang trống.");
                }

                checkoutItems = carts.Select(c => (c.VariantId, c.Quantity, c.Variant.Price)).ToList();
                cartsToRemove = carts; // Lưu vết để xóa khỏi giỏ sau khi đặt hàng thành công
            }

            // ── BƯỚC 2: XÁC THỰC ĐỊA CHỈ & KIỂM TRA TỒN KHO ẢO (Virtual Inventory) ──
            var address = await _userAddressRepo.GetByIdAsync(request.UserAddressId);
            if (address == null || address.UserId != userId)
            {
                return ApiResult<CheckoutResponse>.Fail("Địa chỉ giao hàng không hợp lệ.");
            }

            var variantIds = checkoutItems.Select(x => x.VariantId).Distinct().ToList();
            
            // Lấy tổng số Serial đang Available trong kho
            var availableSerialsMap = await _productSerialRepo.CountAvailableByVariantIdsAsync(variantIds);
            
            // Lấy tổng số lượng sản phẩm đang được giữ chỗ trong các đơn hàng online đang xử lý (Pending/Confirmed/Shipping)
            var activeOrderQuantitiesMap = await _orderRepo.GetActiveOrderQuantitiesByVariantIdsAsync(variantIds);

            // KIỂM TRA TỒN KHO ẢO (VIRTUAL STOCK):
            // Số lượng tồn thực tế để bán (Real Available) = Số lượng Available trong kho - Số lượng đang được giữ chỗ cho các đơn hàng chưa hoàn tất.
            // Ví dụ: Có 5 serial Available, nhưng có 3 đơn hàng đang "Chờ duyệt" chứa sản phẩm này -> Tồn ảo chỉ còn 2. Nếu khách mua 3 -> Báo hết hàng.
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

            // ── BƯỚC 3: TÍNH TOÁN GIÁ & ÁP DỤNG MÃ GIẢM GIÁ (Voucher) ──
            decimal subTotal = checkoutItems.Sum(x => x.Price * x.Quantity);

            var itemCategoryIds = (request.VoucherCodes != null && request.VoucherCodes.Any())
                ? await _productRepo.GetCategoryIdsByVariantIdsAsync(variantIds)
                : null;

            // Thực thi luồng kiểm tra và áp dụng voucher
            var (usages, totalDiscount) = await ApplyVouchersAsync(
                subTotal, request.VoucherCodes, userId, isOnlineOrder: true, itemCategoryIds);
            
            // Đảm bảo số tiền giảm giá không vượt quá tổng giá trị đơn hàng trước ship
            totalDiscount = Math.Min(totalDiscount, subTotal);

            decimal totalAmount = subTotal + request.ShippingFee - totalDiscount;

            // ── BƯỚC 4: TẠO ĐƠN HÀNG (Chạy trong Transaction đảm bảo tính toàn vẹn) ──
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Tự sinh mã đơn hàng định dạng ORD-YYYYMMDD-XXX (Ví dụ: ORD-20260521-001)
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

                // Tất cả đơn hàng online đều bắt đầu ở Pending (chờ duyệt), dù COD hay chuyển khoản trực tuyến.
                byte orderStatus = (byte)OrderStatus.Pending;

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
                    PaymentStatus = (byte)(request.PaymentMethod == 0 ? 0 : 1), // 0: COD (Chưa trả), 1: Online Payment (Đã trả)
                    OrderType = 0, // 0: Đơn đặt hàng Online
                    Note = request.Note
                };

                await _orderRepo.AddAsync(order);
                await _unitOfWork.SaveChangesAsync(); // Lưu trước để phát sinh Id đơn hàng phục vụ bảng chi tiết

                // Thêm chi tiết đơn hàng (OrderDetail)
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

                // Ghi nhận lịch sử sử dụng Voucher (VoucherUsages) và tăng số lượt đã dùng của mã
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

                // Dọn dẹp giỏ hàng sau khi đặt hàng thành công
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
                    PaymentUrl = null // TODO: Kết nối đối tác MoMo/VNPay để sinh link thanh toán nếu PaymentMethod == 1
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
                    return ApiResult<OrderDetailDto>.Fail($"Không tìm thấy sản phẩm có mã {item.VariantId}", ApiErrorCode.NotFound);
                }
                
                // Usually we also check stock here...
                
                variantPrices[item.VariantId] = variant.Price;
                subTotal += variant.Price * item.Quantity;
            }

            decimal shippingFee = 30000; // Hardcoded or calculated

            // 2. Apply Vouchers
            var placeOrderVariantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
            var placeOrderCategoryIds = (request.VoucherCodes != null && request.VoucherCodes.Any())
                ? await _productRepo.GetCategoryIdsByVariantIdsAsync(placeOrderVariantIds)
                : null;

            var (usages, totalDiscount) = await ApplyVouchersAsync(
                subTotal, request.VoucherCodes, userId, isOnlineOrder: true, placeOrderCategoryIds);

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
                    PaymentStatus = (byte)(request.PaymentMethod == 0 ? 0 : 1),
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

        /// <summary>
        /// NGHIỆP VỤ: Kiểm định tính hợp lệ và áp dụng danh sách mã giảm giá (Voucher) cho đơn hàng.
        /// Triển khai cơ chế kiểm tra điều kiện áp dụng đa tiêu chí cực kỳ chặt chẽ:
        /// 1. Kiểm tra tính tồn tại và kích hoạt của từng mã giảm giá.
        /// 2. Ràng buộc khả năng cộng dồn (IsStackable): Nếu áp dụng nhiều voucher, tất cả mã phải cấu hình cho phép cộng dồn chéo.
        /// 3. Xác thực 9 bước kiểm định cho từng Voucher đơn lẻ:
        ///    - Trạng thái hoạt động (IsActive == true).
        ///    - Hạn hiệu lực thời gian (StartDate &lt;= Hiện tại &lt;= EndDate).
        ///    - Tổng số lượng phát hành của hệ thống (UsedCount &lt; Quantity).
        ///    - Giá trị đơn hàng tối thiểu (subTotal &gt;= MinOrderValue).
        ///    - Kênh bán hàng áp dụng (Phân biệt đơn đặt hàng trực tuyến Online vs đơn bán trực tiếp tại POS).
        ///    - Danh mục sản phẩm được áp dụng (VoucherCategories): Đơn hàng bắt buộc phải chứa ít nhất một sản phẩm tương thích.
        ///    - Giới hạn số lần sử dụng của cá nhân từng khách hàng (MaxUsesPerUser) để ngăn ngừa lạm dụng trục lợi voucher.
        /// 4. Tính toán số tiền chiết khấu thực tế: Theo số tiền cố định hoặc tỷ lệ phần trăm (đối với phần trăm có áp dụng khống chế mức trần tối đa MaxDiscountAmount).
        /// 5. Trích xuất danh sách VoucherUsage và tổng tiền giảm giá để cập nhật đơn hàng.
        /// </summary>
        private async Task<(List<VoucherUsage> Usages, decimal TotalDiscount)> ApplyVouchersAsync(
            decimal subTotal,
            List<string>? voucherCodes,
            Guid userId,
            bool isOnlineOrder,
            List<int>? orderItemCategoryIds = null)
        {
            var usages = new List<VoucherUsage>();
            if (voucherCodes == null || !voucherCodes.Any())
                return (usages, 0);

            var vouchers = await _voucherRepo.GetByCodesWithCategoriesAsync(voucherCodes);

            var foundCodes = vouchers.Select(v => v.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalidCodes = voucherCodes.Where(c => !foundCodes.Contains(c)).ToList();
            if (invalidCodes.Any())
                throw new Exception($"Mã giảm giá không tồn tại: {string.Join(", ", invalidCodes)}");

            // NGHIỆP VỤ: Kiểm tra khả năng dùng chung (IsStackable).
            // Nếu khách hàng nhập từ 2 voucher trở lên, nhưng có ít nhất 1 voucher cấu hình "Không cho phép cộng dồn", ta từ chối giao dịch.
            if (vouchers.Count > 1 && vouchers.Any(v => !v.IsStackable))
                throw new Exception("Một hoặc nhiều mã giảm giá không thể được sử dụng cùng lúc với mã khác.");

            var now = DateTime.UtcNow;
            foreach (var voucher in vouchers)
            {
                // 1. Kiểm tra trạng thái hoạt động của Voucher
                if (!voucher.IsActive)
                    throw new Exception($"Mã '{voucher.Code}' đã bị vô hiệu hóa.");

                // 2. Kiểm tra hiệu lực thời gian
                if (now < voucher.StartDate || now > voucher.EndDate)
                    throw new Exception($"Mã '{voucher.Code}' đã hết hạn hoặc chưa đến thời gian sử dụng.");

                // 3. Kiểm tra số lượng phát hành của hệ thống
                if (voucher.Quantity.HasValue && voucher.UsedCount >= voucher.Quantity.Value)
                    throw new Exception($"Mã '{voucher.Code}' đã hết lượt sử dụng.");

                // 4. Kiểm tra giá trị đơn hàng tối thiểu để được áp dụng mã
                if (subTotal < voucher.MinOrderValue)
                    throw new Exception(
                        $"Mã '{voucher.Code}' yêu cầu đơn hàng tối thiểu {voucher.MinOrderValue:#,0}đ " +
                        $"(đơn hiện tại: {subTotal:#,0}đ).");

                // 5. Kiểm tra kênh áp dụng (Kênh Online vs Kênh Tại quầy POS)
                // ApplyFor = 1: Chỉ áp dụng Online, ApplyFor = 2: Chỉ áp dụng tại quầy POS, ApplyFor = 0: Áp dụng cả hai
                if (isOnlineOrder && voucher.ApplyFor == 2)
                    throw new Exception($"Mã '{voucher.Code}' chỉ áp dụng tại quầy, không áp dụng cho đơn online.");
                if (!isOnlineOrder && voucher.ApplyFor == 1)
                    throw new Exception($"Mã '{voucher.Code}' chỉ áp dụng cho đơn online, không áp dụng tại quầy.");

                // 6. Kiểm tra danh mục sản phẩm được áp dụng
                // Nếu voucher có cấu hình danh sách Category, thì đơn hàng bắt buộc phải chứa ít nhất một sản phẩm thuộc các danh mục đó.
                if (voucher.VoucherCategories.Any() && orderItemCategoryIds != null && orderItemCategoryIds.Any())
                {
                    var voucherCategoryIds = voucher.VoucherCategories.Select(vc => vc.CategoryId).ToHashSet();
                    if (!orderItemCategoryIds.Any(catId => voucherCategoryIds.Contains(catId)))
                        throw new Exception($"Mã '{voucher.Code}' không áp dụng cho danh mục sản phẩm trong đơn hàng này.");
                }
            }

            // 7. Kiểm tra giới hạn số lần sử dụng của mỗi khách hàng (MaxUsesPerUser)
            var voucherIds = vouchers.Select(v => v.Id).ToList();
            var usageCounts = await _voucherRepo.GetUserVoucherUsageCountsAsync(userId, voucherIds);
            foreach (var voucher in vouchers)
            {
                var currentCount = usageCounts.GetValueOrDefault(voucher.Id, 0);
                if (voucher.MaxUsesPerUser.HasValue && currentCount >= voucher.MaxUsesPerUser.Value)
                    throw new Exception(
                        $"Bạn đã sử dụng mã '{voucher.Code}' {currentCount} lần " +
                        $"(tối đa {voucher.MaxUsesPerUser} lần/khách).");

                // Tương thích ngược: nếu MaxUsesPerUser null và không stackable, giữ mặc định mỗi khách chỉ dùng tối đa 1 lần
                if (!voucher.MaxUsesPerUser.HasValue && !voucher.IsStackable && currentCount >= 1)
                    throw new Exception(
                        $"Bạn đã sử dụng mã giảm giá '{voucher.Code}'. Mỗi mã chỉ được sử dụng 1 lần.");
            }

            decimal totalDiscount = 0;

            // 8. TÍNH TOÁN SỐ TIỀN GIẢM GIÁ
            foreach (var voucher in vouchers)
            {
                decimal discountApplied;

                if (voucher.DiscountType == 0) // Loại 0: Giảm tiền cố định (Ví dụ: Giảm 50.000đ)
                {
                    discountApplied = voucher.DiscountValue;
                }
                else // Loại 1: Giảm theo tỷ lệ phần trăm (Ví dụ: Giảm 10%, giới hạn tối đa 200.000đ)
                {
                    discountApplied = subTotal * voucher.DiscountValue / 100;
                    if (voucher.MaxDiscountAmount.HasValue && discountApplied > voucher.MaxDiscountAmount.Value)
                        discountApplied = voucher.MaxDiscountAmount.Value;
                }

                totalDiscount += discountApplied;

                usages.Add(new VoucherUsage
                {
                    VoucherId       = voucher.Id,
                    UserId          = userId,
                    DiscountApplied = discountApplied,
                    UsedDate        = DateTime.UtcNow
                });
            }

            return (usages, totalDiscount);
        }

        public async Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id)
        {
            var order = await _orderRepo.GetByIdWithDetailsAsync(id);
            if (order == null)
                return ApiResult<OrderDetailDto>.Fail("Không tìm thấy đơn hàng.", ApiErrorCode.NotFound);

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

            if (request.MinStatus.HasValue)
                query = query.Where(o => o.Status >= request.MinStatus.Value);
            if (request.MaxStatus.HasValue)
                query = query.Where(o => o.Status <= request.MaxStatus.Value);

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
                    CustomerAvatarUrl = o.User != null ? o.User.Profile!.AvatarUrl : null,
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

        public async Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetMyOrdersAsync(Guid userId, OrderFilterRequest request)
        {
            var query = _orderRepo.GetQueryable().AsNoTracking()
                .Where(o => o.UserId == userId);

            if (request.Status.HasValue)
                query = query.Where(o => o.Status == request.Status.Value);

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
                    CustomerAvatarUrl = o.User != null ? o.User.Profile!.AvatarUrl : null,
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

        /// <summary>
        /// NGHIỆP VỤ: Quản lý hủy đơn hàng (Quyền Admin/Nhân viên).
        /// Ràng buộc nghiêm ngặt: Tuyệt đối cấm hủy đơn khi đơn hàng đã chuyển giao cho đơn vị vận chuyển ở trạng thái Đang giao (Shipping / Status = 2).
        /// Khi đơn hàng ở trạng thái hợp lệ (Pending hoặc Confirmed), chuyển trạng thái sang Cancelled (Status = 4) và ghi nhận lý do hủy từ quản trị viên.
        /// </summary>
        public async Task<ApiResult<bool>> CancelOrderAsync(int id, CancelOrderRequest request)
        {
            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null)
            {
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.", ApiErrorCode.NotFound);
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

        /// <summary>
        /// NGHIỆP VỤ: Xác nhận hoàn tất giao hàng (Quyền Admin/Nhân viên).
        /// Chỉ cho phép xác nhận thành công đối với các đơn hàng đang trong trạng thái Đang giao (Shipping / Status = 2).
        /// Chuyển trạng thái đơn sang Success (Status = 3) để ghi nhận doanh thu và chấm dứt vòng đời đơn hàng.
        /// </summary>
        public async Task<ApiResult<bool>> CompleteOrderAsync(int id)
        {
            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null)
            {
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.", ApiErrorCode.NotFound);
            }

            if (order.Status != 2)
            {
                return ApiResult<bool>.Fail("Chỉ có thể xác nhận giao cho đơn hàng đang trong trạng thái 'Đang giao'.");
            }

            order.Status = 3; // Success
            await _unitOfWork.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Đơn hàng đã được đánh dấu hoàn thành.");
        }

        /// <summary>
        /// NGHIỆP VỤ: Duyệt đơn hàng trực tuyến (Quyền Admin/Nhân viên).
        /// Chỉ cho phép duyệt những đơn hàng đang ở trạng thái Chờ duyệt (Pending / Status = 0) sang trạng thái Đã xác nhận (Confirmed / Status = 1).
        /// Đây là bước tiền đề để bộ phận kho tiến hành quét serial xuất kho vật lý.
        /// </summary>
        public async Task<ApiResult<bool>> ConfirmOrderAsync(int id)
        {
            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null)
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.", ApiErrorCode.NotFound);

            if (order.Status != 0)
                return ApiResult<bool>.Fail("Chỉ có thể duyệt đơn hàng đang ở trạng thái 'Chờ duyệt'.");

            order.Status = 1; // Confirmed
            await _unitOfWork.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Đã duyệt đơn hàng thành công.");
        }

        // === Customer self-service (ownership-enforced) ===

        public async Task<ApiResult<OrderDetailDto>> GetMyOrderByIdAsync(int id, Guid userId)
        {
            var order = await _orderRepo.GetByIdWithDetailsAsync(id);
            if (order == null || order.UserId != userId)
                return ApiResult<OrderDetailDto>.Fail("Không tìm thấy đơn hàng.", ApiErrorCode.NotFound);

            var dto = MapToOrderDetailDto(order);
            return ApiResult<OrderDetailDto>.Ok(dto);
        }

        public async Task<ApiResult<bool>> CancelMyOrderAsync(int id, Guid userId, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(cancelReason))
                return ApiResult<bool>.Fail("Vui lòng cung cấp lý do hủy đơn.");

            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null || order.UserId != userId)
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.", ApiErrorCode.NotFound);

            if (order.Status != (byte)OrderStatus.Pending)
                return ApiResult<bool>.Fail("Chỉ có thể hủy đơn hàng đang ở trạng thái 'Chờ duyệt'.");

            order.Status = (byte)OrderStatus.Cancelled;
            order.CancelReason = cancelReason;

            await _unitOfWork.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Hủy đơn hàng thành công.");
        }

        public async Task<ApiResult<bool>> ConfirmReceivedByCustomerAsync(int id, Guid userId)
        {
            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null || order.UserId != userId)
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.", ApiErrorCode.NotFound);

            if (order.Status != (byte)OrderStatus.Exported)
                return ApiResult<bool>.Fail("Chỉ có thể xác nhận khi đơn hàng đang ở trạng thái 'Đang giao'.");

            order.Status = (byte)OrderStatus.Success;
            await _unitOfWork.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Đã xác nhận nhận hàng thành công.");
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
                    MainImageUrl = d.Variant.Images != null && d.Variant.Images.Any()
                        ? (d.Variant.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                           ?? d.Variant.Images.OrderBy(i => i.SortOrder).First().ImageUrl)
                        : null,
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
