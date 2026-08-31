using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Exceptions;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Pos;
using PBL3.Shared.Enums;

namespace PBL3.Service.Pos
{
    public class PosService : IPosService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderRepository _orderRepo;
        private readonly IProductSerialRepository _serialRepo;
        private readonly IVoucherRepository _voucherRepo;
        private readonly IWarrantyRepository _warrantyRepo;
        private readonly IProductRepository _productRepo;
        private readonly IInventorySyncService _inventorySyncService;
        private readonly HushStoreDbContext _dbContext; // For user lookup and quick queries
        private readonly UserManager<AppUser> _userManager;

        private readonly IDocumentCodeGenerator _codeGenerator;
        private readonly ILogger<PosService> _logger;


        public PosService(
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepo,
            IProductSerialRepository serialRepo,
            IVoucherRepository voucherRepo,
            IWarrantyRepository warrantyRepo,
            IProductRepository productRepo,
            IInventorySyncService inventorySyncService,
            HushStoreDbContext dbContext,
            UserManager<AppUser> userManager,
            IDocumentCodeGenerator codeGenerator,
            ILogger<PosService> logger)
        {
            _unitOfWork = unitOfWork;
            _orderRepo = orderRepo;
            _serialRepo = serialRepo;
            _voucherRepo = voucherRepo;
            _warrantyRepo = warrantyRepo;
            _productRepo = productRepo;
            _inventorySyncService = inventorySyncService;
            _dbContext = dbContext;
            _userManager = userManager;
            _codeGenerator = codeGenerator;
            _logger = logger;
        }

        // ========================================================
        // SCAN SERIAL — Quét mã vạch (Barcode/Serial) kiểm tra hàng bán trực tiếp tại quầy POS
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Quét mã Serial (Barcode) vật lý của sản phẩm tại quầy thu ngân.
        /// Đảm bảo tính hợp lệ tuyệt đối của thiết bị trước khi đưa vào hóa đơn:
        /// 1. Truy vấn mã serial trong kho dữ liệu xem có tồn tại hay không.
        /// 2. Bắt buộc kiểm tra trạng thái vật lý (Serial.Status == Available).
        ///    - Ngăn chặn triệt để lỗi bán nhầm sản phẩm lỗi, sản phẩm đã xuất kho, hoặc hàng đang giữ chỗ cho đơn hàng trực tuyến khác.
        /// 3. Trả về thông tin biến thể sản phẩm, giá bán hiện hành và cấu hình thời hạn bảo hành.
        /// </summary>
        public async Task<ApiResult<PosScanResponse>> ScanSerialAsync(string serialNumber)
        {
            var serial = await _serialRepo.GetBySerialNumberAsync(serialNumber);
            
            // NGHIỆP VỤ: Serial quét được bắt buộc phải tồn tại trong cơ sở dữ liệu
            if (serial == null)
            {
                return ApiResult<PosScanResponse>.Fail("Mã Serial không hợp lệ hoặc không có trong kho.");
            }

            // NGHIỆP VỤ: Mã Serial vật lý bắt buộc phải có trạng thái Available (Trong kho và sẵn sàng bán)
            // Ngăn chặn bán nhầm sản phẩm đã thuộc về đơn hàng khác hoặc sản phẩm lỗi/báo mất.
            if (serial.Status != (byte)SerialStatus.Available)
            {
                return ApiResult<PosScanResponse>.Fail("Sản phẩm không ở trạng thái sẵn sàng bán (đã bán hoặc lỗi).");
            }

            var variant = serial.Variant;
            var product = variant?.Product;

            if (variant == null || product == null)
                return ApiResult<PosScanResponse>.Fail("Không thể lấy thông tin sản phẩm. Biến thể hoặc sản phẩm không còn tồn tại trong hệ thống.");

            var thumbnail = variant.Images?
                .OrderByDescending(i => i.IsMain)
                .ThenBy(i => i.SortOrder)
                .FirstOrDefault()?.ImageUrl;

            return ApiResult<PosScanResponse>.Ok(new PosScanResponse
            {
                SerialId = serial.Id,
                SerialNumber = serial.SerialNumber,
                VariantId = variant.Id,
                SKU = variant.SKU,
                VariantName = variant.VariantName,
                ProductName = product.Name,
                Price = variant.Price,
                WarrantyMonth = variant.WarrantyMonth,
                ThumbnailUrl = thumbnail
            });
        }

        /// <summary>
        /// NGHIỆP VỤ: Tra cứu khách hàng thành viên tại quầy bằng Số điện thoại.
        /// Phục vụ cho việc:
        /// - Áp dụng chính sách ưu đãi thành viên hoặc tích điểm.
        /// - Liên kết hóa đơn POS với tài khoản người dùng để phục vụ tra cứu lịch sử mua hàng và yêu cầu bảo hành sau này.
        /// </summary>
        public async Task<ApiResult<PosCustomerDto>> LookupCustomerAsync(string phone)
        {
            var user = await _dbContext.Users
                .Include(u => u.Profile)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PhoneNumber == phone);

            if (user == null)
            {
                return ApiResult<PosCustomerDto>.Fail("Không tìm thấy khách hàng.");
            }

            return ApiResult<PosCustomerDto>.Ok(new PosCustomerDto
            {
                UserId = user.Id,
                FullName = user.Profile?.FullName ?? user.UserName ?? "Khách hàng",
                PhoneNumber = user.PhoneNumber ?? phone,
                Email = user.Email
            });
        }

        /// <summary>
        /// NGHIỆP VỤ: Kiểm tra nhanh tính hợp lệ của mã giảm giá (Voucher) ngay tại quầy thu ngân.
        /// Thực thi các bộ quy tắc validation nhanh:
        /// 1. Tồn tại và trạng thái kích hoạt (IsActive).
        /// 2. Hạn hiệu lực thời gian của mã.
        /// 3. Tổng số lượng lượt sử dụng còn lại toàn hệ thống.
        /// 4. Giá trị đơn hàng tối thiểu (MinOrderValue).
        /// 5. Tính toán chính xác số tiền giảm dựa trên loại giảm giá (tiền mặt trực tiếp hoặc tỷ lệ % có khống chế trần tối đa).
        /// </summary>
        public async Task<ApiResult<VoucherValidationDto>> ValidateVoucherAsync(string code, decimal subTotal)
        {
            var vouchers = await _voucherRepo.GetByCodesAsync(new List<string> { code });
            var voucher = vouchers.FirstOrDefault();

            if (voucher == null || !voucher.IsActive)
            {
                return ApiResult<VoucherValidationDto>.Fail("Mã Voucher không tồn tại hoặc đã bị khóa.");
            }

            var now = DateTime.UtcNow;
            if (now < voucher.StartDate || now > voucher.EndDate)
            {
                return ApiResult<VoucherValidationDto>.Fail("Mã Voucher đã hết hạn hoặc chưa đến thời gian sử dụng.");
            }

            if (voucher.UsedCount >= voucher.Quantity)
            {
                return ApiResult<VoucherValidationDto>.Fail("Mã Voucher đã hết lượt sử dụng.");
            }

            if (subTotal < voucher.MinOrderValue)
            {
                return ApiResult<VoucherValidationDto>.Fail($"Đơn hàng chưa đạt giá trị tối thiểu {voucher.MinOrderValue:#,0}đ.");
            }

            decimal discountApplied = 0;
            if (voucher.DiscountType == 0) // Amount (Giảm tiền mặt cố định)
            {
                discountApplied = voucher.DiscountValue;
            }
            else // Percentage (Giảm theo phần trăm)
            {
                discountApplied = subTotal * voucher.DiscountValue / 100;
                if (voucher.MaxDiscountAmount.HasValue && discountApplied > voucher.MaxDiscountAmount.Value)
                {
                    discountApplied = voucher.MaxDiscountAmount.Value;
                }
            }

            return ApiResult<VoucherValidationDto>.Ok(new VoucherValidationDto
            {
                IsValid = true,
                Code = voucher.Code,
                DiscountAmount = discountApplied,
                Message = "Áp dụng Voucher thành công."
            });
        }

        /// <summary>
        /// NGHIỆP VỤ: Hoàn tất thanh toán và xuất hóa đơn bán hàng trực tiếp tại quầy POS.
        /// Quy trình nghiệp vụ cốt lõi có độ phức tạp cao, chạy dưới một Transaction cơ sở dữ liệu:
        /// 1. Xác định thông tin khách hàng mua: Tra cứu qua SĐT để tích điểm thành viên, nếu không tìm thấy mặc định ghi nhận là "Khách vãng lai" (hàng vãng lai).
        /// 2. Gộp nhóm Serial vật lý (Variant Grouping):
        ///    - Khi nhân viên quét nhiều serial của cùng một biến thể sản phẩm (Ví dụ: 3 máy iPhone 15 Pro Max 256GB), hệ thống gom nhóm chúng lại thành 1 dòng chi tiết đơn hàng (OrderDetail) duy nhất để in hóa đơn gọn gàng, nhưng vẫn lưu vết liên kết đầy đủ 3 serial vào bảng ánh xạ.
        ///    - Tái xác thực trạng thái 'Available' của từng mã Serial tại thời điểm chốt đơn để chống xung đột dữ liệu.
        /// 3. Xác thực và áp dụng Voucher chiết khấu trực tiếp tại quầy (phải hỗ trợ kênh POS).
        /// 4. Tự động sinh mã hóa đơn đặc thù POS định dạng POS-yyyyMMdd-NNN theo ngày để phân biệt với đơn hàng trực tuyến.
        /// 5. Khởi tạo đơn hàng ở trạng thái Success (Hoàn tất) và PaymentStatus = 1 (Đã thanh toán) ngay lập tức do giao dịch trực tiếp bằng tiền mặt hoặc chuyển khoản tại quầy.
        /// 6. Cập nhật vật lý các thực thể liên quan:
        ///    - Đổi trạng thái Serial sang 'Sold' (Đã bán), lưu ngày bán và Id hóa đơn.
        ///    - Tự động kích hoạt phiếu Bảo hành vật lý (Active Warranty): Tính toán chính xác thời hạn bảo hành kế thừa từ cấu hình biến thể (WarrantyMonth) kể từ ngày mua.
        ///    - Tăng số lượt sử dụng của Voucher và ghi nhận lịch sử sử dụng nếu là khách thành viên.
        /// 7. Thực hiện đồng bộ tồn kho vật lý (Physical Inventory Sync) tức thời cho các biến thể vừa bán thông qua IInventorySyncService sau khi Transaction commit thành công.
        /// </summary>
        public async Task<ApiResult<PosOrderDto>> CheckoutAsync(PosCheckoutRequest request, Guid employeeId)
        {
            if (request.Items == null || !request.Items.Any())
            {
                return ApiResult<PosOrderDto>.Fail("Giỏ hàng trống.");
            }

            decimal subTotal = 0;
            var variantIdsToSync = new HashSet<int>();

            // KẾ HOẠCH DỮ LIỆU THUẦN (không phải entity) cho phần ghi.
            // Mọi entity — Order, OrderDetail, OrderSerial, Warranty, ProductSerial — đều được
            // dựng/nạp LẠI bên trong delegate ở mỗi lần thử. Danh sách entity dựng sẵn ở ngoài
            // (kiểu `serialsToUpdate` / `newWarranties` cũ) là khuôn hỏng khi chạy lại: nó vừa
            // giữ instance đã detached sau ChangeTracker.Clear(), vừa cộng dồn bản ghi qua các
            // lần thử.
            var itemPlan = new List<(int SerialId, int VariantId, decimal Price)>();

            // ── BƯỚC 1: XÁC ĐỊNH KHÁCH HÀNG (Resolve Customer) ──
            // Nghiệp vụ: POS hỗ trợ tra cứu SĐT thành viên để cộng điểm/áp dụng chiết khấu. Nếu không có SĐT, mặc định là "Khách vãng lai"
            Guid? customerId = null;
            string? customerName = null;
            if (!string.IsNullOrEmpty(request.CustomerPhone))
            {
                var userLookup = await LookupCustomerAsync(request.CustomerPhone);
                if (userLookup.Success && userLookup.Data != null)
                {
                    customerId = userLookup.Data.UserId;
                    customerName = userLookup.Data.FullName;
                }
            }

            // ── BƯỚC 2: XÁC THỰC CÁC SERIAL & TÍNH TỔNG TIỀN (SubTotal) ──
            // Gom nhóm các mã Serial quét được theo biến thể VariantId để đưa vào chi tiết đơn hàng (OrderDetail).
            // Ví dụ: Nhân viên quét 3 serial riêng biệt của cùng biến thể 'iPhone 15 Pro Max' -> 
            // Hệ thống chỉ tạo 1 dòng OrderDetail (iPhone 15 Pro Max, Quantity = 3) để hóa đơn gọn gàng, nhưng vẫn liên kết đầy đủ 3 serial đó.
            // Kiểm tra NGOÀI transaction: đọc AsNoTracking để trả lỗi đẹp và tính SubTotal.
            // Chốt THẬT nằm trong delegate (nạp lại có tracking + kiểm lại Available).
            foreach (var item in request.Items)
            {
                var dbSerial = await _dbContext.ProductSerials
                    .AsNoTracking()
                    .Where(x => x.Id == item.SerialId)
                    .Select(x => new { x.Id, x.Status, x.VariantId, Price = x.Variant.Price })
                    .FirstOrDefaultAsync();

                // Kiểm tra lại lần nữa: Đảm bảo Serial vật lý vẫn ở trạng thái Available trước khi chốt hóa đơn
                if (dbSerial == null || dbSerial.Status != (byte)SerialStatus.Available)
                {
                    return ApiResult<PosOrderDto>.Fail($"Sản phẩm có mã SerialId {item.SerialId} không tồn tại hoặc đã bán.");
                }

                subTotal += dbSerial.Price;
                variantIdsToSync.Add(dbSerial.VariantId);
                itemPlan.Add((dbSerial.Id, dbSerial.VariantId, dbSerial.Price));
            }

            // ── BƯỚC 3: ÁP DỤNG MÃ GIẢM GIÁ (Voucher) ──
            decimal discountAmount = 0;
            // Chỉ giữ Id + Code (dữ liệu thuần), KHÔNG giữ entity Voucher tracked: sau
            // ChangeTracker.Clear() ở lần thử lại nó đã detached, và ta cũng không ghi thẳng
            // vào nó — việc trừ lượt do TryConsumeAsync (một câu UPDATE nguyên tử) đảm nhiệm.
            int? appliedVoucherId = null;
            string? appliedVoucherCode = null;
            if (!string.IsNullOrEmpty(request.VoucherCode))
            {
                var validateRes = await ValidateVoucherAsync(request.VoucherCode, subTotal);
                if (!validateRes.Success)
                    return ApiResult<PosOrderDto>.Fail(validateRes.Message);

                discountAmount = validateRes.Data!.DiscountAmount;

                var voucherInfo = await _dbContext.Vouchers
                    .AsNoTracking()
                    .Where(v => v.Code == request.VoucherCode)
                    .Select(v => new { v.Id, v.Code })
                    .FirstOrDefaultAsync();

                appliedVoucherId = voucherInfo?.Id;
                appliedVoucherCode = voucherInfo?.Code;
            }

            discountAmount = Math.Min(discountAmount, subTotal);
            decimal totalAmount = subTotal - discountAmount;

            // ── BƯỚC 4+5: SINH MÃ, TẠO HÓA ĐƠN & THANH TOÁN (Transaction bảo vệ dữ liệu) ──
            // Mã hoá đơn CỐ Ý sinh bên trong delegate (điều kiện 2 của hợp đồng retry) — sinh ở
            // ngoài thì lần thử lại dùng lại đúng mã cũ.
            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork. Đây là call-site
                // nặng nhất của nhóm; trước khi rà nó vi phạm cả ba, mỗi cái theo một kiểu khác:
                //
                //   (a) `order` DỰNG ở ngoài rồi AddAsync bên trong — lần thử 2 gọi AddAsync trên
                //       entity đã tracked ở trạng thái Unchanged là NO-OP, nên hoá đơn KHÔNG BAO
                //       GIỜ được chèn và od.OrderId trỏ vào một Order không tồn tại;
                //   (b) `serialsToUpdate` / `orderDetailsMap` / `newWarranties` dựng ở ngoài —
                //       vừa giữ instance đã detached sau Clear(), vừa cộng dồn qua các lần thử;
                //   (c) mã hoá đơn POS sinh ở ngoài nên lần thử 2 dùng lại đúng mã cũ.
                //
                // Cả ba nay đã nằm BÊN TRONG delegate, dựng mới ở mỗi lần thử từ `itemPlan`
                // (dữ liệu thuần). TryConsumeAsync vốn đã an toàn: nó nằm trong transaction nên
                // rollback hoàn tác luôn phép +1.
                var order = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var now = DateTime.UtcNow;

                    // Mã hoá đơn sinh BÊN TRONG: mỗi lần thử lấy một mã mới.
                    string newOrderCode = await _codeGenerator.NextAsync(DocumentCodeKind.PosOrder);

                    var order = new Order
                    {
                        OrderCode = newOrderCode,
                        UserId = customerId,
                        EmployeeId = employeeId,
                        OrderDate = now,
                        Status = (byte)OrderStatus.Success, // Nghiệp vụ POS: Đơn hoàn tất ngay tại quầy
                        OrderType = (byte)OrderType.POS,     // Đơn bán tại quầy POS
                        ShipName = customerName ?? "Khách vãng lai",
                        ShipPhone = request.CustomerPhone ?? "",
                        ShipAddress = !string.IsNullOrWhiteSpace(request.ShipAddress) ? request.ShipAddress : "Tại quầy",
                        ShipCity = !string.IsNullOrWhiteSpace(request.ShipCity) ? request.ShipCity : "Tại quầy",
                        SubTotal = subTotal,
                        ShippingFee = 0, // Bán trực tiếp không tính phí vận chuyển
                        DiscountAmount = discountAmount,
                        TotalAmount = totalAmount,
                        PaymentMethod = request.PaymentMethod,
                        PaymentStatus = 1, // Đã thanh toán (Paid)
                        Note = request.EmployeeNote
                    };

                    // Lưu hóa đơn POS
                    await _orderRepo.AddAsync(order);
                    await _unitOfWork.SaveChangesAsync();

                    // Gộp Serial theo Variant thành các dòng OrderDetail — dựng MỚI mỗi lần thử.
                    var orderDetailsMap = new Dictionary<int, OrderDetail>();
                    foreach (var planned in itemPlan)
                    {
                        if (!orderDetailsMap.TryGetValue(planned.VariantId, out var od))
                        {
                            od = new OrderDetail
                            {
                                OrderId = order.Id,
                                VariantId = planned.VariantId,
                                Quantity = 0,
                                UnitPrice = planned.Price
                            };
                            orderDetailsMap[planned.VariantId] = od;
                            await _dbContext.OrderDetails.AddAsync(od);
                        }
                        od.Quantity++;
                    }
                    await _unitOfWork.SaveChangesAsync(); // Lưu để lấy Id chi tiết đơn hàng

                    // ── BƯỚC 5.1: CẬP NHẬT TRẠNG THÁI SERIAL, ORDER_SERIAL & TẠO PHIẾU BẢO HÀNH (WARRANTY) ──
                    var newWarranties = new List<Warranty>();
                    foreach (var planned in itemPlan)
                    {
                        // Nạp LẠI có tracking bên trong delegate — mỗi lần thử lấy instance mới.
                        var serial = await _serialRepo.GetByIdWithTrackingAsync(planned.SerialId);

                        // Chốt chống race THẬT (kiểm ở BƯỚC 2 chỉ là chốt sớm cho UX).
                        // NÉM chứ không return, để transaction rollback.
                        if (serial is null || serial.Status != (byte)SerialStatus.Available)
                            throw new ConcurrentModificationException(
                                $"Mã Serial có SerialId {planned.SerialId} vừa được bán hoặc đổi trạng thái. " +
                                "Vui lòng quét lại giỏ hàng.");

                        // Chuyển trạng thái Serial sang "Sold" (Đã bán)
                        serial.Status = (byte)SerialStatus.Sold;
                        serial.SoldDate = now;
                        serial.OrderId = order.Id;

                        var od = orderDetailsMap[serial.VariantId];
                        // Liên kết Serial với dòng chi tiết hóa đơn (OrderSerial)
                        await _dbContext.OrderSerials.AddAsync(new OrderSerial
                        {
                            OrderDetailId = od.Id,
                            SerialId = serial.Id
                        });

                        // NGHIỆP VỤ PHÁT SINH BẢO HÀNH TỰ ĐỘNG:
                        // Nếu sản phẩm đó có cấu hình thời hạn bảo hành (WarrantyMonth > 0), hệ thống tự sinh bản ghi Warranty ở trạng thái Active.
                        if (serial.Variant.WarrantyMonth > 0)
                        {
                            newWarranties.Add(new Warranty
                            {
                                SerialId = serial.Id,
                                CustomerId = customerId,
                                OrderId = order.Id,
                                StartDate = now,
                                EndDate = now.AddMonths(serial.Variant.WarrantyMonth),
                                Status = (byte)WarrantyStatus.Active
                            });
                        }
                    }

                    if (newWarranties.Any())
                    {
                        await _warrantyRepo.AddRangeAsync(newWarranties);
                    }

                    // Ghi nhận Voucher đã dùng
                    if (appliedVoucherId.HasValue)
                    {
                        // TIÊU THỤ NGUYÊN TỬ thay cho appliedVoucher.UsedCount++ (lost update).
                        // Kiểm ở ValidateVoucherAsync chỉ là chốt sớm cho UX; chốt THẬT là câu UPDATE này.
                        if (!await _voucherRepo.TryConsumeAsync(appliedVoucherId.Value))
                            // BusinessRuleException, KHÔNG phải InvalidOperationException: khối catch
                            // bên dưới phải phân biệt được "luật nghiệp vụ" với "lỗi hạ tầng", mà
                            // EF Core cũng ném InvalidOperationException cho chuyện hoàn toàn khác.
                            throw new BusinessRuleException(
                                $"Mã '{appliedVoucherCode}' đã hết lượt sử dụng. Vui lòng bỏ mã và thử lại.");

                        if (customerId.HasValue)
                        {
                            await _dbContext.VoucherUsages.AddAsync(new VoucherUsage
                            {
                                VoucherId = appliedVoucherId.Value,
                                UserId = customerId.Value,
                                OrderId = order.Id,
                                DiscountApplied = discountAmount,
                                UsedDate = now
                            });
                        }
                    }

                    await _unitOfWork.SaveChangesAsync();

                    return order;
                }, retrySafe: true);

                // ── BƯỚC 6: ĐỒNG BỘ TỒN KHO THỰC TẾ (StockQuantity) ──
                // Cập nhật tồn kho (Stock Qty) cho các Variant sau khi đã xuất bán thành công các serial vật lý
                await _inventorySyncService.SyncStockBatchAsync(variantIdsToSync);

                return ApiResult<PosOrderDto>.Ok(new PosOrderDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    OrderDate = order.OrderDate,
                    SubTotal = order.SubTotal,
                    DiscountAmount = order.DiscountAmount,
                    TotalAmount = order.TotalAmount,
                    CustomerName = order.ShipName,
                    CustomerPhone = order.ShipPhone
                }, "Thanh toán thành công.");
            }
            catch (ConcurrentModificationException ex)
            {
                _logger.LogWarning(ex, "Xung đột đồng thời khi thanh toán POS. Thu ngân: {EmployeeId}.", employeeId);
                return ApiResult<PosOrderDto>.Fail(ex.Message);
            }
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN. Nuốt nó thành
                // câu chung là hồi quy UX: thu ngân mất đúng thông tin cần để xử ("bỏ mã ra").
                _logger.LogInformation("Chặn thanh toán POS vì luật nghiệp vụ: {Reason}", ex.Message);
                return ApiResult<PosOrderDto>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                // KHÔNG nối ex.Message: ở đây ex thường của EF Core / SQL Server và nội dung là
                // tiếng Anh, vừa vi phạm quy tắc user-facing message của CLAUDE.md vừa lộ nội
                // tạng ORM. Cùng lỗi đã sửa ở mục 🅴 cho OrderService.CheckoutAsync.
                _logger.LogError(ex, "Lỗi hệ thống khi thanh toán POS. Thu ngân: {EmployeeId}.", employeeId);
                return ApiResult<PosOrderDto>.Fail(
                    "Không thể hoàn tất thanh toán do lỗi hệ thống. Vui lòng thử lại; "
                    + "nếu vẫn không được, xin liên hệ bộ phận kỹ thuật.");
            }
        }

        /// <summary>
        /// NGHIỆP VỤ: Lưu tạm đơn hàng POS (Draft Order) để xử lý sau.
        /// Cho phép thu ngân lưu nháp trạng thái giỏ hàng khi khách hàng cần lấy thêm đồ hoặc có sự cố thanh toán tạm thời.
        /// Trạng thái đơn hàng sẽ là PosDraft, không cập nhật tồn kho vật lý hay tạo phiếu bảo hành cho đến khi chốt checkout thực tế.
        /// </summary>
        public async Task<ApiResult<PosDraftDto>> SaveDraftAsync(PosCheckoutRequest request, Guid employeeId)
        {
            // Similar to checkout but Status = Draft, no Serial modification, no Warranty.
            // But how do we save selected serials? OrderDetail does not save serials until checkout.
            // For a Draft, we can just save it as Order with PosDraft status, and add generic OrderDetails.
            // When resuming, we can either re-scan or not.
            // PosDraft logic can be simplified: just save order and orderdetails without updating inventory.
            return ApiResult<PosDraftDto>.Fail("Tính năng lưu tạm đang được xây dựng (cần thống nhất cách lưu trữ SerialDraft).");
            // I'll leave SaveDraft implemented quickly:
        }

        /// <summary>
        /// NGHIỆP VỤ: Truy vấn danh sách các đơn hàng lưu tạm (Draft Orders) của một nhân viên POS cụ thể.
        /// Phục vụ phục hồi lại phiên làm việc dở dang tại quầy thu ngân.
        /// </summary>
        public async Task<ApiResult<List<PosDraftDto>>> GetDraftsAsync(Guid employeeId)
        {
            var drafts = await _orderRepo.GetDraftsByEmployeeAsync(employeeId);
            var dtos = drafts.Select(d => new PosDraftDto
            {
                OrderId = d.Id,
                OrderCode = d.OrderCode,
                OrderDate = d.OrderDate,
                TotalAmount = d.TotalAmount
            }).ToList();

            return ApiResult<List<PosDraftDto>>.Ok(dtos);
        }

        /// <summary>
        /// NGHIỆP VỤ: Truy vấn thông tin chi tiết của một đơn hàng nháp theo mã đơn và nhân viên.
        /// </summary>
        public async Task<ApiResult<PosDraftDto>> GetDraftByIdAsync(int orderId, Guid employeeId)
        {
             var draft = await _dbContext.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && o.EmployeeId == employeeId && o.Status == (byte)OrderStatus.PosDraft);
             if (draft == null) return ApiResult<PosDraftDto>.Fail("Không tìm thấy đơn chờ.");
             return ApiResult<PosDraftDto>.Ok(new PosDraftDto {
                OrderId = draft.Id,
                OrderCode = draft.OrderCode,
                OrderDate = draft.OrderDate,
                TotalAmount = draft.TotalAmount
             });
        }

        /// <summary>
        /// NGHIỆP VỤ: Xóa vĩnh viễn một đơn hàng lưu tạm khi khách hàng hủy mua hoặc không quay lại quầy.
        /// </summary>
        public async Task<ApiResult<bool>> DeleteDraftAsync(int orderId, Guid employeeId)
        {
             var draft = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.EmployeeId == employeeId && o.Status == (byte)OrderStatus.PosDraft);
             if (draft == null) return ApiResult<bool>.Fail("Không tìm thấy đơn chờ.");
             
             _dbContext.Orders.Remove(draft);
             await _dbContext.SaveChangesAsync();
             return ApiResult<bool>.Ok(true, "Xoá thành công.");
        }
    }
}
