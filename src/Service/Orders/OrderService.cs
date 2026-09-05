using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Exceptions;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Concurrency;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Sale;
using PBL3.Shared.DTOs.Products;
using PBL3.Shared.Enums;
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

        private readonly IDocumentCodeGenerator _codeGenerator;
        private readonly ILogger<OrderService> _logger;


        public OrderService(
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepo,
            IVoucherRepository voucherRepo,
            IProductRepository productRepo,
            ICartRepository cartRepo,
            IUserAddressRepository userAddressRepo,
            IProductSerialRepository productSerialRepo,
            IDocumentCodeGenerator codeGenerator,
            ILogger<OrderService> logger)
        {
            _unitOfWork = unitOfWork;
            _orderRepo = orderRepo;
            _voucherRepo = voucherRepo;
            _productRepo = productRepo;
            _cartRepo = cartRepo;
            _userAddressRepo = userAddressRepo;
            _productSerialRepo = productSerialRepo;
            _codeGenerator = codeGenerator;
            _logger = logger;
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
                // Checkout từ giỏ hàng hiện tại của khách hàng.
                // Đọc AsNoTracking: ở đây chỉ cần GIÁ và SỐ LƯỢNG để dựng đơn. Giỏ sẽ được
                // nạp LẠI (có tracking) bên trong delegate mới xoá — xem điều kiện 1 của hợp
                // đồng retry. Giữ instance tracked từ ngoài rồi RemoveRange bên trong là khuôn
                // hỏng khi chạy lại: sau ChangeTracker.Clear() chúng đã detached.
                var carts = await _cartRepo.GetCartItemsByUserAsync(userId);
                if (!carts.Any())
                {
                    return ApiResult<CheckoutResponse>.Fail("Giỏ hàng của bạn đang trống.");
                }

                checkoutItems = carts.Select(c => (c.VariantId, c.Quantity, c.Variant.Price)).ToList();
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
            var (usagePlans, totalDiscount) = await ApplyVouchersAsync(
                subTotal, request.VoucherCodes, userId, isOnlineOrder: true, itemCategoryIds);
            
            // Đảm bảo số tiền giảm giá không vượt quá tổng giá trị đơn hàng trước ship
            totalDiscount = Math.Min(totalDiscount, subTotal);

            decimal totalAmount = subTotal + request.ShippingFee - totalDiscount;

            // ── BƯỚC 4: TẠO ĐƠN HÀNG (Chạy trong Transaction đảm bảo tính toàn vẹn) ──
            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `order`, `VoucherUsage` và giỏ hàng cần xoá đều được dựng/nạp BÊN TRONG;
                // (2) mã đơn (IDocumentCodeGenerator), OrderDate và UsedDate đều tính bên trong;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate —
                //     ApplyVouchersAsync ở trên CHỈ ĐỌC (kiểm tra + tính tiền giảm).
                //
                // TryConsumeByCodesAsync vốn đã an toàn: nó nằm trong transaction nên rollback
                // hoàn tác luôn phép +1, lần thử sau tăng lại cho ra đúng net +1.
                var order = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Tự sinh mã đơn hàng định dạng ORD-yyyyMMdd-NNNNNN
                    string newOrderCode = await _codeGenerator.NextAsync(DocumentCodeKind.Order);

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

                    // Ghi nhận lịch sử sử dụng Voucher (VoucherUsages) và tăng số lượt đã dùng của mã.
                    // DỰNG MỚI entity ở mỗi lần thử từ `usagePlans` (dữ liệu thuần) — không tái
                    // dùng instance của lần thử trước, vì chúng đã mang Id bị rollback.
                    if (usagePlans.Any())
                    {
                        // 🔴 SeqPerUser đọc BÊN TRONG delegate — bắt buộc. Lần thử lại phải
                        // lấy số MỚI; mang số từ ngoài vào thì retry đụng lại đúng
                        // UQ_VoucherUsages_UserId_VoucherId_SeqPerUser và không bao giờ qua được.
                        //
                        // Đây là phỏng đoán lạc quan, KHÔNG phải chốt an toàn: hai request đồng
                        // thời cùng đọc ra một số. Chốt là unique index — kẻ thua nhận 2601 →
                        // 409 qua ConflictExceptionHandler. LoadProbe S03 đo được cái giá của
                        // việc KHÔNG có chốt: 1 khách dùng 10 lần một mã MaxUsesPerUser=1.
                        var nextSeq = await _voucherRepo.GetNextSeqPerUserAsync(
                            userId, usagePlans.Select(u => u.VoucherId).ToList());

                        // ════════════════════════════════════════════════════════════════
                        // 🔴 KIỂM LẠI HẠN MỨC Ở ĐÂY. Chốt ở bước 7 của ApplyVouchersAsync
                        // KHÔNG ĐỦ, và lý do đáng đọc vì nó không hiển nhiên.
                        //
                        // Unique index UQ_VoucherUsages_UserId_VoucherId_SeqPerUser chỉ chặn
                        // được HAI INSERT CÙNG MỘT SỐ THỨ TỰ. Nó KHÔNG biết MaxUsesPerUser.
                        // Nên có đúng hai thứ tự xảy ra, và index chỉ đóng được một:
                        //
                        //   (a) Hai request ĐỌC CHỒNG NHAU → cùng ra seq = 1 → index chặn ✅
                        //   (b) Hai request bị TUẦN TỰ HOÁ → kẻ sau đọc MAX = 1 → seq = 2 →
                        //       index CHO QUA, và chốt ngoài transaction đã đọc count = 0 từ
                        //       trước khi kẻ trước commit → khách dùng được 2 lượt 🔴
                        //
                        // Đo được thật, không suy luận: LoadProbe S03 ra ✅ ở lần chạy đầu
                        // (rơi vào (a)) rồi 🔴 `200×2` ở lần chạy sau (rơi vào (b)) mà KHÔNG
                        // một dòng code nào đổi giữa hai lần. Đúng bất đối xứng của runbook:
                        // một lần ✅ không phải bằng chứng an toàn.
                        //
                        // Câu MAX ở trên đọc BÊN TRONG transaction nên nó thấy dữ liệu mới
                        // nhất đã commit; ghép với index, hai thứ phủ kín cả (a) và (b).
                        //
                        // ⚠️ Chốt ở bước 7 vẫn PHẢI giữ: nó cho câu thông báo tử tế trong
                        // trường hợp thường (không có ai đua) và trả lỗi trước khi tạo đơn.
                        // Khối này chỉ là lưới cuối cho đúng khoảnh khắc đua nhau.
                        // ════════════════════════════════════════════════════════════════
                        foreach (var plan in usagePlans)
                        {
                            var seq = nextSeq.GetValueOrDefault(plan.VoucherId, 1);

                            // Khớp CHÍNH XÁC luật ở bước 7: MaxUsesPerUser null + không
                            // stackable ⇒ mặc định 1 lần/khách; null + stackable ⇒ không giới hạn.
                            var limit = plan.MaxUsesPerUser
                                        ?? (plan.IsStackable ? int.MaxValue : 1);

                            if (seq > limit)
                                throw new BusinessRuleException(
                                    $"Bạn đã sử dụng mã '{plan.Code}' đủ số lần cho phép " +
                                    $"(tối đa {limit} lần/khách).");
                        }

                        var usages = usagePlans.Select(u => new VoucherUsage
                        {
                            VoucherId       = u.VoucherId,
                            UserId          = u.UserId,
                            OrderId         = order.Id,
                            SeqPerUser      = nextSeq.GetValueOrDefault(u.VoucherId, 1),
                            DiscountApplied = u.DiscountApplied,
                            UsedDate        = DateTime.UtcNow
                        }).ToList();

                        await _voucherRepo.AddUsagesAsync(usages);
                    
                        if (request.VoucherCodes != null && request.VoucherCodes.Any())
                        {
                            // TIÊU THỤ NGUYÊN TỬ: một câu UPDATE ... SET UsedCount = UsedCount + 1
                            // WHERE UsedCount < Quantity. Cách cũ (đọc entity rồi += 1) là lost update:
                            // hai đơn đồng thời chỉ đếm một lượt, voucher dùng vượt số phát hành.
                            var exhausted = await _voucherRepo.TryConsumeByCodesAsync(request.VoucherCodes);
                            if (exhausted.Any())
                                throw new BusinessRuleException(
                                    $"Mã '{string.Join("', '", exhausted)}' đã hết lượt sử dụng. " +
                                    "Vui lòng bỏ mã này và thử lại.");
                        }
                    }

                    // Dọn dẹp giỏ hàng sau khi đặt hàng thành công.
                    // Nạp LẠI có tracking bên trong delegate — mỗi lần thử lấy instance mới.
                    if (!request.IsBuyNow)
                    {
                        var cartsToRemove = await _cartRepo.GetCartItemsWithTrackingAsync(userId);
                        if (cartsToRemove.Any())
                            _cartRepo.RemoveRange(cartsToRemove);
                    }

                    await _unitOfWork.SaveChangesAsync();

                    return order;
                }, retrySafe: true);

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
            catch (BusinessRuleException)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng (tiếng Việt, an toàn) — cho đi ra
                // NGUYÊN VĂN. Nuốt nó thành câu chung là hồi quy UX: người dùng mất đúng thông
                // tin cần để tự sửa ("bỏ mã hết hạn ra rồi đặt lại").
                throw;
            }
            // PHẢI đứng trước catch (Exception), nếu không nó nuốt xung đột đồng thời thành
            // một câu chung. throw; để ConflictExceptionHandler ánh xạ sang 409.
            // Giải thích đầy đủ: InventoryCheckService.ApproveAsync.
            // 🔴 Vi phạm unique index PHẢI đi qua, không được biến thành "lỗi hệ thống".
            //
            // Đo được: sau khi thêm UQ_VoucherUsages_UserId_VoucherId_SeqPerUser, dữ liệu đã
            // đúng (S03: COUNT = 1) nhưng 9 kẻ thua nhận câu "Không thể hoàn tất đặt hàng do
            // lỗi hệ thống" — vì khối catch (Exception) ngay dưới nuốt DbUpdateException trước
            // khi ConflictExceptionHandler kịp nhìn thấy nó. Đó là ĐÚNG cái bẫy CLAUDE.md ghi:
            // "Nuốt chúng thành câu chung là hồi quy UX nặng hơn lỗi ban đầu" — người dùng
            // tưởng server hỏng, trong khi thật ra họ vừa chạm một hạn mức hợp lệ.
            //
            // throw; → 409 + "Dữ liệu này vừa được người khác tạo hoặc thay đổi. Vui lòng tải
            // lại trang và thử lại." Đó là ĐÚNG lớp thông báo cho tình huống này, và client đã
            // có ánh xạ 409 từ đợt 2.
            //
            // ⚠️ Cố ý KHÔNG cố đoán index nào bị vi phạm — dù PostgreSQL CÓ ConstraintName.
            // constraint, nên cách duy nhất là dò ex.Message — chuỗi tiếng Anh, đổi theo phiên
            // bản SQL Server, đúng bẫy #7. Câu 409 chung ở trên đủ đúng cho MỌI index ở đường
            // này nên không cần phân biệt.
            // Hỏi ConflictClassifier thay vì tự liệt kê số lỗi: nó gom cả 2601/2627,
            // DbUpdateConcurrencyException VÀ 1205 (deadlock) — loại thứ ba mà khối này
            // trước kia bỏ sót, và là loại DUY NHẤT bị EnableRetryOnFailure bọc vào
            // RetryLimitExceededException khi hết lượt thử. Nhờ dùng chung một hàm với
            // ConflictExceptionHandler, cái thoát ra đây luôn khớp cái thành 409.
            catch (Exception ex) when (ConflictClassifier.IsConflict(ex))
            {
                _logger.LogWarning(ex,
                    "Đặt hàng thất bại do xung đột đồng thời. Người dùng {UserId}.", userId);
                throw;
            }
            catch (Exception ex)
            {
                // KHÔNG nối ex.Message vào thông báo người dùng: ở đây ex thường là của
                // EF Core / SQL Server và nội dung là tiếng Anh ("An error occurred while
                // saving the entity changes…"), vừa vi phạm quy tắc tiếng Việt của CLAUDE.md
                // vừa lộ nội tạng ORM. Chi tiết đi vào log; người dùng nhận một câu chung.
                _logger.LogError(ex,
                    "Checkout thất bại cho người dùng {UserId}. IsBuyNow={IsBuyNow}, BuyNowVariantId={BuyNowVariantId}, Vouchers={VoucherCodes}",
                    userId, request.IsBuyNow, request.BuyNowVariantId,
                    request.VoucherCodes == null ? "(không có)" : string.Join(",", request.VoucherCodes));

                throw new Exception(
                    "Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; "
                    + "nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.", ex);
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
            var placeOrderVariantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
            var placeOrderCategoryIds = (request.VoucherCodes != null && request.VoucherCodes.Any())
                ? await _productRepo.GetCategoryIdsByVariantIdsAsync(placeOrderVariantIds)
                : null;

            var (usagePlans, totalDiscount) = await ApplyVouchersAsync(
                subTotal, request.VoucherCodes, userId, isOnlineOrder: true, placeOrderCategoryIds);

            // 3. Ensure discount <= subtotal
            totalDiscount = Math.Min(totalDiscount, subTotal);

            // 4. TRANSACTION
            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `order` và `VoucherUsage` đều được dựng BÊN TRONG delegate;
                // (2) mã đơn (IDocumentCodeGenerator), OrderDate và UsedDate đều tính bên trong;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate —
                //     ApplyVouchersAsync ở trên CHỈ ĐỌC.
                var order = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Generate OrderCode (ORD-yyyyMMdd-NNNNNN)
                    string newOrderCode = await _codeGenerator.NextAsync(DocumentCodeKind.Order);

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

                    // 4c. DỰNG MỚI VoucherUsage ở mỗi lần thử từ `usagePlans` (dữ liệu thuần).
                    // Không tái dùng instance của lần thử trước: chúng đã mang Id bị rollback.
                    if (usagePlans.Any())
                    {
                        // 🔴 SeqPerUser đọc BÊN TRONG delegate — bắt buộc. Lần thử lại phải
                        // lấy số MỚI; mang số từ ngoài vào thì retry đụng lại đúng
                        // UQ_VoucherUsages_UserId_VoucherId_SeqPerUser và không bao giờ qua được.
                        //
                        // Đây là phỏng đoán lạc quan, KHÔNG phải chốt an toàn: hai request đồng
                        // thời cùng đọc ra một số. Chốt là unique index — kẻ thua nhận 2601 →
                        // 409 qua ConflictExceptionHandler. LoadProbe S03 đo được cái giá của
                        // việc KHÔNG có chốt: 1 khách dùng 10 lần một mã MaxUsesPerUser=1.
                        var nextSeq = await _voucherRepo.GetNextSeqPerUserAsync(
                            userId, usagePlans.Select(u => u.VoucherId).ToList());

                        // ════════════════════════════════════════════════════════════════
                        // 🔴 KIỂM LẠI HẠN MỨC Ở ĐÂY. Chốt ở bước 7 của ApplyVouchersAsync
                        // KHÔNG ĐỦ, và lý do đáng đọc vì nó không hiển nhiên.
                        //
                        // Unique index UQ_VoucherUsages_UserId_VoucherId_SeqPerUser chỉ chặn
                        // được HAI INSERT CÙNG MỘT SỐ THỨ TỰ. Nó KHÔNG biết MaxUsesPerUser.
                        // Nên có đúng hai thứ tự xảy ra, và index chỉ đóng được một:
                        //
                        //   (a) Hai request ĐỌC CHỒNG NHAU → cùng ra seq = 1 → index chặn ✅
                        //   (b) Hai request bị TUẦN TỰ HOÁ → kẻ sau đọc MAX = 1 → seq = 2 →
                        //       index CHO QUA, và chốt ngoài transaction đã đọc count = 0 từ
                        //       trước khi kẻ trước commit → khách dùng được 2 lượt 🔴
                        //
                        // Đo được thật, không suy luận: LoadProbe S03 ra ✅ ở lần chạy đầu
                        // (rơi vào (a)) rồi 🔴 `200×2` ở lần chạy sau (rơi vào (b)) mà KHÔNG
                        // một dòng code nào đổi giữa hai lần. Đúng bất đối xứng của runbook:
                        // một lần ✅ không phải bằng chứng an toàn.
                        //
                        // Câu MAX ở trên đọc BÊN TRONG transaction nên nó thấy dữ liệu mới
                        // nhất đã commit; ghép với index, hai thứ phủ kín cả (a) và (b).
                        //
                        // ⚠️ Chốt ở bước 7 vẫn PHẢI giữ: nó cho câu thông báo tử tế trong
                        // trường hợp thường (không có ai đua) và trả lỗi trước khi tạo đơn.
                        // Khối này chỉ là lưới cuối cho đúng khoảnh khắc đua nhau.
                        // ════════════════════════════════════════════════════════════════
                        foreach (var plan in usagePlans)
                        {
                            var seq = nextSeq.GetValueOrDefault(plan.VoucherId, 1);

                            // Khớp CHÍNH XÁC luật ở bước 7: MaxUsesPerUser null + không
                            // stackable ⇒ mặc định 1 lần/khách; null + stackable ⇒ không giới hạn.
                            var limit = plan.MaxUsesPerUser
                                        ?? (plan.IsStackable ? int.MaxValue : 1);

                            if (seq > limit)
                                throw new BusinessRuleException(
                                    $"Bạn đã sử dụng mã '{plan.Code}' đủ số lần cho phép " +
                                    $"(tối đa {limit} lần/khách).");
                        }

                        var usages = usagePlans.Select(u => new VoucherUsage
                        {
                            VoucherId       = u.VoucherId,
                            UserId          = u.UserId,
                            OrderId         = order.Id,
                            SeqPerUser      = nextSeq.GetValueOrDefault(u.VoucherId, 1),
                            DiscountApplied = u.DiscountApplied,
                            UsedDate        = DateTime.UtcNow
                        }).ToList();

                        await _voucherRepo.AddUsagesAsync(usages);
                    }

                    // 4d. Increase UsedCount of vouchers
                    if (request.VoucherCodes != null && request.VoucherCodes.Any())
                    {
                        // TIÊU THỤ NGUYÊN TỬ — xem giải thích ở nhánh checkout phía trên.
                        var exhausted = await _voucherRepo.TryConsumeByCodesAsync(request.VoucherCodes);
                        if (exhausted.Any())
                            throw new BusinessRuleException(
                                $"Mã '{string.Join("', '", exhausted)}' đã hết lượt sử dụng. " +
                                "Vui lòng bỏ mã này và thử lại.");
                    }

                    // 4e-4f. Flush and Commit
                    await _unitOfWork.SaveChangesAsync();

                    return order;
                }, retrySafe: true);

                // 5. Manual Mapping
                var savedOrderInfo = await _orderRepo.GetByIdWithDetailsAsync(order.Id);
                var dto = MapToOrderDetailDto(savedOrderInfo);

                return ApiResult<OrderDetailDto>.Ok(dto, "Đặt hàng thành công!");
            }
            catch (BusinessRuleException)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng (tiếng Việt, an toàn) — cho đi ra
                // NGUYÊN VĂN. Nuốt nó thành câu chung là hồi quy UX: người dùng mất đúng thông
                // tin cần để tự sửa ("bỏ mã hết hạn ra rồi đặt lại").
                throw;
            }
            // PHẢI đứng trước catch (Exception), nếu không nó nuốt xung đột đồng thời thành
            // một câu chung. throw; để ConflictExceptionHandler ánh xạ sang 409.
            // Giải thích đầy đủ: InventoryCheckService.ApproveAsync.
            // Cùng lý do như khối catch của CheckoutAsync ở trên: để 2601/2627 đi qua thành
            // 409, đừng biến hạn mức voucher thành "lỗi hệ thống".
            // Hỏi ConflictClassifier thay vì tự liệt kê số lỗi: nó gom cả 2601/2627,
            // DbUpdateConcurrencyException VÀ 1205 (deadlock) — loại thứ ba mà khối này
            // trước kia bỏ sót, và là loại DUY NHẤT bị EnableRetryOnFailure bọc vào
            // RetryLimitExceededException khi hết lượt thử. Nhờ dùng chung một hàm với
            // ConflictExceptionHandler, cái thoát ra đây luôn khớp cái thành 409.
            catch (Exception ex) when (ConflictClassifier.IsConflict(ex))
            {
                _logger.LogWarning(ex,
                    "Tạo đơn hàng thất bại do xung đột đồng thời. Người dùng {UserId}.", userId);
                throw;
            }
            catch (Exception ex)
            {
                // Cùng lý do như khối catch của CheckoutAsync ở trên.
                _logger.LogError(ex, "Tạo đơn hàng thất bại cho người dùng {UserId}.", userId);

                throw new Exception(
                    "Không thể tạo đơn hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; "
                    + "nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.", ex);
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
        /// <summary>
        /// Dữ liệu thuần để DỰNG <see cref="VoucherUsage"/> — CỐ Ý không phải entity.
        /// </summary>
        /// <remarks>
        /// Trước đây ApplyVouchersAsync trả thẳng entity <c>VoucherUsage</c> đã <c>new</c> sẵn
        /// ở NGOÀI transaction, rồi call-site mới <c>AddUsagesAsync</c> ở TRONG. Khuôn đó
        /// không chạy lại được: sau lần thử 1, các entity ấy đã được EF gán Id và chuyển sang
        /// Unchanged; lần thử 2 (sau <c>ChangeTracker.Clear()</c>) chúng là object detached
        /// nhưng Id KHÁC 0 — Add lại thì hoặc EF coi là đã có khoá, hoặc ghi trùng.
        ///
        /// Trả về dữ liệu thuần buộc call-site phải <c>new</c> entity MỚI bên trong delegate ở
        /// mỗi lần thử, đúng điều kiện 1 của hợp đồng retry.
        /// </remarks>
        /// <summary>
        /// Dữ liệu THUẦN (không phải entity) để dựng <c>VoucherUsage</c> bên trong delegate.
        /// </summary>
        /// <remarks>
        /// 🔴 <c>Code</c>, <c>MaxUsesPerUser</c>, <c>IsStackable</c> có mặt ở đây vì hạn mức
        /// mỗi-khách phải được kiểm LẠI <b>bên trong</b> transaction, và mang entity
        /// <c>Voucher</c> vào trong đó là vi phạm điều 1 của hợp đồng retry.
        /// Xem chỗ dùng để biết vì sao kiểm một lần ở ngoài là KHÔNG đủ.
        /// </remarks>
        private sealed record VoucherUsagePlan(
            int VoucherId,
            Guid UserId,
            decimal DiscountApplied,
            string Code,
            int? MaxUsesPerUser,
            bool IsStackable);

        private async Task<(List<VoucherUsagePlan> Usages, decimal TotalDiscount)> ApplyVouchersAsync(
            decimal subTotal,
            List<string>? voucherCodes,
            Guid userId,
            bool isOnlineOrder,
            List<int>? orderItemCategoryIds = null)
        {
            var usages = new List<VoucherUsagePlan>();
            if (voucherCodes == null || !voucherCodes.Any())
                return (usages, 0);

            var vouchers = await _voucherRepo.GetByCodesWithCategoriesAsync(voucherCodes);

            var foundCodes = vouchers.Select(v => v.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalidCodes = voucherCodes.Where(c => !foundCodes.Contains(c)).ToList();
            if (invalidCodes.Any())
                throw new BusinessRuleException($"Mã giảm giá không tồn tại: {string.Join(", ", invalidCodes)}");

            // NGHIỆP VỤ: Kiểm tra khả năng dùng chung (IsStackable).
            // Nếu khách hàng nhập từ 2 voucher trở lên, nhưng có ít nhất 1 voucher cấu hình "Không cho phép cộng dồn", ta từ chối giao dịch.
            if (vouchers.Count > 1 && vouchers.Any(v => !v.IsStackable))
                throw new BusinessRuleException("Một hoặc nhiều mã giảm giá không thể được sử dụng cùng lúc với mã khác.");

            var now = DateTime.UtcNow;
            foreach (var voucher in vouchers)
            {
                // 1. Kiểm tra trạng thái hoạt động của Voucher
                if (!voucher.IsActive)
                    throw new BusinessRuleException($"Mã '{voucher.Code}' đã bị vô hiệu hóa.");

                // 2. Kiểm tra hiệu lực thời gian
                if (now < voucher.StartDate || now > voucher.EndDate)
                    throw new BusinessRuleException($"Mã '{voucher.Code}' đã hết hạn hoặc chưa đến thời gian sử dụng.");

                // 3. Kiểm tra số lượng phát hành của hệ thống
                if (voucher.Quantity.HasValue && voucher.UsedCount >= voucher.Quantity.Value)
                    throw new BusinessRuleException($"Mã '{voucher.Code}' đã hết lượt sử dụng.");

                // 4. Kiểm tra giá trị đơn hàng tối thiểu để được áp dụng mã
                if (subTotal < voucher.MinOrderValue)
                    throw new BusinessRuleException(
                        $"Mã '{voucher.Code}' yêu cầu đơn hàng tối thiểu {voucher.MinOrderValue:#,0}đ " +
                        $"(đơn hiện tại: {subTotal:#,0}đ).");

                // 5. Kiểm tra kênh áp dụng (Kênh Online vs Kênh Tại quầy POS)
                // ApplyFor = 1: Chỉ áp dụng Online, ApplyFor = 2: Chỉ áp dụng tại quầy POS, ApplyFor = 0: Áp dụng cả hai
                if (isOnlineOrder && voucher.ApplyFor == 2)
                    throw new BusinessRuleException($"Mã '{voucher.Code}' chỉ áp dụng tại quầy, không áp dụng cho đơn online.");
                if (!isOnlineOrder && voucher.ApplyFor == 1)
                    throw new BusinessRuleException($"Mã '{voucher.Code}' chỉ áp dụng cho đơn online, không áp dụng tại quầy.");

                // 6. Kiểm tra danh mục sản phẩm được áp dụng
                // Nếu voucher có cấu hình danh sách Category, thì đơn hàng bắt buộc phải chứa ít nhất một sản phẩm thuộc các danh mục đó.
                if (voucher.VoucherCategories.Any() && orderItemCategoryIds != null && orderItemCategoryIds.Any())
                {
                    var voucherCategoryIds = voucher.VoucherCategories.Select(vc => vc.CategoryId).ToHashSet();
                    if (!orderItemCategoryIds.Any(catId => voucherCategoryIds.Contains(catId)))
                        throw new BusinessRuleException($"Mã '{voucher.Code}' không áp dụng cho danh mục sản phẩm trong đơn hàng này.");
                }
            }

            // 7. Kiểm tra giới hạn số lần sử dụng của mỗi khách hàng (MaxUsesPerUser)
            var voucherIds = vouchers.Select(v => v.Id).ToList();
            var usageCounts = await _voucherRepo.GetUserVoucherUsageCountsAsync(userId, voucherIds);
            foreach (var voucher in vouchers)
            {
                var currentCount = usageCounts.GetValueOrDefault(voucher.Id, 0);
                if (voucher.MaxUsesPerUser.HasValue && currentCount >= voucher.MaxUsesPerUser.Value)
                    throw new BusinessRuleException(
                        $"Bạn đã sử dụng mã '{voucher.Code}' {currentCount} lần " +
                        $"(tối đa {voucher.MaxUsesPerUser} lần/khách).");

                // Tương thích ngược: nếu MaxUsesPerUser null và không stackable, giữ mặc định mỗi khách chỉ dùng tối đa 1 lần
                if (!voucher.MaxUsesPerUser.HasValue && !voucher.IsStackable && currentCount >= 1)
                    throw new BusinessRuleException(
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

                // UsedDate CỐ Ý không tính ở đây: nó là giá trị "sinh một lần để ghi", nên
                // theo điều kiện 2 của hợp đồng retry phải tính BÊN TRONG delegate.
                usages.Add(new VoucherUsagePlan(
                    voucher.Id, userId, discountApplied,
                    voucher.Code, voucher.MaxUsesPerUser, voucher.IsStackable));
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
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.");
            }

            if (order.Status == 2)
            {
                // BusinessRuleException để OrdersController phân biệt được nó với lỗi hạ tầng
                // và trả NGUYÊN VĂN câu này (xem khối catch hai tầng ở controller).
                throw new BusinessRuleException("Đơn hàng đang giao (Shipping). Tuyệt đối cấm hủy.");
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

        /// <summary>
        /// NGHIỆP VỤ: Duyệt đơn hàng trực tuyến (Quyền Admin/Nhân viên).
        /// Chỉ cho phép duyệt những đơn hàng đang ở trạng thái Chờ duyệt (Pending / Status = 0) sang trạng thái Đã xác nhận (Confirmed / Status = 1).
        /// Đây là bước tiền đề để bộ phận kho tiến hành quét serial xuất kho vật lý.
        /// </summary>
        public async Task<ApiResult<bool>> ConfirmOrderAsync(int id)
        {
            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null)
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.");

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
                return ApiResult<OrderDetailDto>.Fail("Không tìm thấy đơn hàng.");

            var dto = MapToOrderDetailDto(order);
            return ApiResult<OrderDetailDto>.Ok(dto);
        }

        public async Task<ApiResult<bool>> CancelMyOrderAsync(int id, Guid userId, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(cancelReason))
                return ApiResult<bool>.Fail("Vui lòng cung cấp lý do hủy đơn.");

            var order = await _orderRepo.GetByIdAsync(id);
            if (order == null || order.UserId != userId)
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.");

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
                return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.");

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
