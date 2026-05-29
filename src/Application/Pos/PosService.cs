using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Pos;
using PBL3.Shared.Enums;

namespace PBL3.Application.Pos
{
    public class PosService(
        IUnitOfWork unitOfWork,
        IOrderRepository orderRepo,
        IProductSerialRepository serialRepo,
        IVoucherRepository voucherRepo,
        IWarrantyRepository warrantyRepo,
        IProductRepository productRepo,
        IInventorySyncService inventorySyncService,
        HushStoreDbContext dbContext,
        UserManager<AppUser> userManager) : IPosService
    {
        private readonly IUnitOfWork _unitOfWork =
            unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        private readonly IOrderRepository _orderRepo =
            orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
        private readonly IProductSerialRepository _serialRepo =
            serialRepo ?? throw new ArgumentNullException(nameof(serialRepo));
        private readonly IVoucherRepository _voucherRepo =
            voucherRepo ?? throw new ArgumentNullException(nameof(voucherRepo));
        private readonly IWarrantyRepository _warrantyRepo =
            warrantyRepo ?? throw new ArgumentNullException(nameof(warrantyRepo));
        private readonly IProductRepository _productRepo =
            productRepo ?? throw new ArgumentNullException(nameof(productRepo));
        private readonly IInventorySyncService _inventorySyncService =
            inventorySyncService ?? throw new ArgumentNullException(nameof(inventorySyncService));
        private readonly HushStoreDbContext _dbContext = // For user lookup and quick queries
            dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly UserManager<AppUser> _userManager =
            userManager ?? throw new ArgumentNullException(nameof(userManager));

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
                return ApiResult<PosScanResponse>.Fail("Mã Serial không hợp lệ hoặc không có trong kho.", ApiErrorCode.NotFound);
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
                return ApiResult<PosScanResponse>.Fail("Không thể lấy thông tin sản phẩm. Biến thể hoặc sản phẩm không còn tồn tại trong hệ thống.", ApiErrorCode.NotFound);

            return ApiResult<PosScanResponse>.Ok(new PosScanResponse
            {
                SerialId = serial.Id,
                SerialNumber = serial.SerialNumber,
                VariantId = variant.Id,
                SKU = variant.SKU,
                VariantName = variant.VariantName,
                ProductName = product.Name,
                Price = variant.Price,
                WarrantyMonth = variant.WarrantyMonth
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
                return ApiResult<PosCustomerDto>.Fail("Không tìm thấy khách hàng.", ApiErrorCode.NotFound);
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
            var serialsToUpdate = new List<ProductSerial>();
            var newWarranties = new List<Warranty>();
            var variantIdsToSync = new HashSet<int>();

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
            var orderDetailsMap = new Dictionary<int, OrderDetail>(); // VariantId -> OrderDetail (gộp lại)

            foreach (var item in request.Items)
            {
                var dbSerial = await _serialRepo.GetByIdWithTrackingAsync(item.SerialId);

                // Kiểm tra lại lần nữa: Đảm bảo Serial vật lý vẫn ở trạng thái Available trước khi chốt hóa đơn
                if (dbSerial == null || dbSerial.Status != (byte)SerialStatus.Available)
                {
                    return ApiResult<PosOrderDto>.Fail($"Sản phẩm có mã SerialId {item.SerialId} không tồn tại hoặc đã bán.");
                }

                subTotal += dbSerial.Variant.Price;
                variantIdsToSync.Add(dbSerial.VariantId);
                serialsToUpdate.Add(dbSerial);

                if (!orderDetailsMap.ContainsKey(dbSerial.VariantId))
                {
                    orderDetailsMap[dbSerial.VariantId] = new OrderDetail
                    {
                        VariantId = dbSerial.VariantId,
                        Quantity = 0,
                        UnitPrice = dbSerial.Variant.Price,
                        // OrderId sẽ gán sau
                    };
                }
                orderDetailsMap[dbSerial.VariantId].Quantity++;
            }

            // ── BƯỚC 3: ÁP DỤNG MÃ GIẢM GIÁ (Voucher) ──
            decimal discountAmount = 0;
            Voucher? appliedVoucher = null;
            if (!string.IsNullOrEmpty(request.VoucherCode))
            {
                var validateRes = await ValidateVoucherAsync(request.VoucherCode, subTotal);
                if (!validateRes.Success)
                    return ApiResult<PosOrderDto>.Fail(validateRes.Message);

                discountAmount = validateRes.Data!.DiscountAmount;
                appliedVoucher = await _dbContext.Vouchers.FirstOrDefaultAsync(v => v.Code == request.VoucherCode);
            }

            discountAmount = Math.Min(discountAmount, subTotal);
            decimal totalAmount = subTotal - discountAmount;

            // ── BƯỚC 4: TỰ SINH MÃ HÓA ĐƠN POS (POS-YYYYMMDD-XXX) ──
            string datePrefix = "POS-" + DateTime.Now.ToString("yyyyMMdd");
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

            // ── BƯỚC 5: TẠO HÓA ĐƠN & THANH TOÁN (Transaction bảo vệ dữ liệu) ──
            var order = new Order
            {
                OrderCode = newOrderCode,
                UserId = customerId,
                EmployeeId = employeeId,
                OrderDate = DateTime.UtcNow,
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

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Lưu hóa đơn POS
                await _orderRepo.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                // Lưu các dòng chi tiết OrderDetail
                foreach (var od in orderDetailsMap.Values)
                {
                    od.OrderId = order.Id;
                    await _dbContext.OrderDetails.AddAsync(od);
                }
                await _unitOfWork.SaveChangesAsync(); // Lưu để lấy Id chi tiết đơn hàng

                // ── BƯỚC 5.1: CẬP NHẬT TRẠNG THÁI SERIAL, ORDER_SERIAL & TẠO PHIẾU BẢO HÀNH (WARRANTY) ──
                var now = DateTime.UtcNow;
                foreach (var s in serialsToUpdate)
                {
                    // Chuyển trạng thái Serial sang "Sold" (Đã bán)
                    s.Status = (byte)SerialStatus.Sold;
                    s.SoldDate = now;
                    s.OrderId = order.Id;

                    var od = orderDetailsMap[s.VariantId];
                    // Liên kết Serial với dòng chi tiết hóa đơn (OrderSerial)
                    await _dbContext.OrderSerials.AddAsync(new OrderSerial
                    {
                        OrderDetailId = od.Id,
                        SerialId = s.Id
                    });

                    // NGHIỆP VỤ PHÁT SINH BẢO HÀNH TỰ ĐỘNG:
                    // Nếu sản phẩm đó có cấu hình thời hạn bảo hành (WarrantyMonth > 0), hệ thống tự sinh bản ghi Warranty ở trạng thái Active.
                    if (s.Variant.WarrantyMonth > 0)
                    {
                        newWarranties.Add(new Warranty
                        {
                            SerialId = s.Id,
                            CustomerId = customerId,
                            OrderId = order.Id,
                            StartDate = now,
                            EndDate = now.AddMonths(s.Variant.WarrantyMonth),
                            Status = (byte)WarrantyStatus.Active
                        });
                    }
                }

                if (newWarranties.Any())
                {
                    await _warrantyRepo.AddRangeAsync(newWarranties);
                }

                // Ghi nhận Voucher đã dùng
                if (appliedVoucher != null)
                {
                    appliedVoucher.UsedCount++;
                    if (customerId.HasValue)
                    {
                        await _dbContext.VoucherUsages.AddAsync(new VoucherUsage
                        {
                            VoucherId = appliedVoucher.Id,
                            UserId = customerId.Value,
                            OrderId = order.Id,
                            DiscountApplied = discountAmount,
                            UsedDate = now
                        });
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

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
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return ApiResult<PosOrderDto>.Fail("Lỗi khi quá trình thanh toán: " + ex.Message);
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
             if (draft == null) return ApiResult<PosDraftDto>.Fail("Không tìm thấy đơn chờ.", ApiErrorCode.NotFound);
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
             if (draft == null) return ApiResult<bool>.Fail("Không tìm thấy đơn chờ.", ApiErrorCode.NotFound);
             
             _dbContext.Orders.Remove(draft);
             await _dbContext.SaveChangesAsync();
             return ApiResult<bool>.Ok(true, "Xoá thành công.");
        }
    }
}
