using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Vouchers;

namespace PBL3.Application.Vouchers
{
    public class VoucherService(
        IVoucherRepository voucherRepo,
        ILogger<VoucherService> logger) : IVoucherService
    {
        private readonly IVoucherRepository _voucherRepo =
            voucherRepo ?? throw new ArgumentNullException(nameof(voucherRepo));
        private readonly ILogger<VoucherService> _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));

        // ========================================================
        // GET LIST
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Truy vấn danh sách Voucher có phân trang và bộ lọc linh hoạt.
        /// Cho phép quản trị viên tìm kiếm theo từ khóa (Mã voucher, tên voucher), lọc theo trạng thái hoạt động
        /// và khoảng thời gian hiệu lực để quản lý các chiến dịch khuyến mãi đang và sắp diễn ra.
        /// </summary>
        public async Task<ApiResult<PagedResult<VoucherDto>>> GetPagedListAsync(VoucherFilterRequest filter)
        {
            // Thực hiện truy vấn cơ sở dữ liệu có phân trang và áp dụng các tiêu chí lọc
            var (items, totalCount) = await _voucherRepo.GetPagedListAsync(
                filter.Keyword,
                filter.StatusFilter,
                filter.FromDate,
                filter.ToDate,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending);

            // Chuyển đổi danh sách thực thể Voucher sang DTO và phản hồi
            var result = new PagedResult<VoucherDto>
            {
                Items      = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize   = filter.PageSize
            };

            return ApiResult<PagedResult<VoucherDto>>.Ok(result);
        }

        // ========================================================
        // GET BY ID
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Truy vấn chi tiết một mã Voucher theo ID, đi kèm danh sách các danh mục sản phẩm được áp dụng.
        /// Giúp bộ phận vận hành kiểm tra cấu hình chi tiết của từng chương trình khuyến mãi.
        /// </summary>
        public async Task<ApiResult<VoucherDto>> GetByIdAsync(int id)
        {
            var voucher = await _voucherRepo.GetByIdWithCategoriesAsync(id);

            if (voucher == null)
                return ApiResult<VoucherDto>.Fail("Không tìm thấy voucher yêu cầu.", ApiErrorCode.NotFound);

            return ApiResult<VoucherDto>.Ok(MapToDto(voucher));
        }

        // ========================================================
        // CREATE
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Đăng ký một chương trình Voucher khuyến mãi mới trên hệ thống.
        /// Thực hiện các nghiệp vụ:
        /// 1. Chuẩn hóa mã voucher sang dạng chữ in hoa không khoảng trắng (Normalized Code).
        /// 2. Kiểm tra trùng lặp mã trên toàn hệ thống để đảm bảo tính duy nhất khi khách hàng áp dụng mã lúc thanh toán.
        /// 3. Khởi tạo các thông số chiết khấu, giới hạn ngân sách (MinOrderValue, MaxDiscountAmount) và thời hạn hiệu lực.
        /// 4. Đăng ký các danh mục sản phẩm được hưởng ưu đãi (VoucherCategories) nếu chương trình giới hạn phạm vi áp dụng.
        /// </summary>
        public async Task<ApiResult<VoucherDto>> CreateAsync(CreateVoucherRequest request)
        {
            var normalizedCode = request.Code.Trim().ToUpper();

            // NGHIỆP VỤ: Chặn trùng mã voucher
            if (await _voucherRepo.IsDuplicateCodeAsync(normalizedCode))
                return ApiResult<VoucherDto>.Fail($"Mã voucher \"{normalizedCode}\" đã tồn tại trong hệ thống.", ApiErrorCode.Conflict);

            var voucher = new Voucher
            {
                Code             = normalizedCode,
                Name             = request.Name.Trim(),
                DiscountType     = request.DiscountType, // 0 = Giảm tiền cố định, 1 = Giảm theo %
                DiscountValue    = request.DiscountValue,
                MinOrderValue    = request.MinOrderValue, // Giá trị đơn hàng tối thiểu để được dùng
                MaxDiscountAmount = request.MaxDiscountAmount, // Mức giảm tối đa khống chế (cho loại giảm theo %)
                StartDate        = request.StartDate,
                EndDate          = request.EndDate,
                Quantity         = request.Quantity, // Tổng số lượt phát hành tối đa (null nếu không giới hạn)
                MaxUsesPerUser   = request.MaxUsesPerUser, // Giới hạn số lần sử dụng của riêng từng khách hàng
                ApplyFor         = request.ApplyFor, // Kênh áp dụng: 0 = Cả hai, 1 = Chỉ Online, 2 = Chỉ POS tại quầy
                IsStackable      = request.IsStackable, // Có được dùng chung với khuyến mãi khác hay không
                IsActive         = request.IsActive,
                Description      = request.Description?.Trim(),
                CreatedDate      = DateTime.UtcNow,
                IsDeleted        = false,
                UsedCount        = 0
            };

            // Nếu voucher giới hạn theo danh mục sản phẩm, thực hiện ánh xạ VoucherCategory
            if (request.CategoryIds != null && request.CategoryIds.Count > 0)
            {
                foreach (var categoryId in request.CategoryIds.Distinct())
                {
                    voucher.VoucherCategories.Add(new VoucherCategory
                    {
                        CategoryId = categoryId
                    });
                }
            }

            await _voucherRepo.AddAsync(voucher);
            await _voucherRepo.SaveChangesAsync();

            _logger.LogInformation("Tạo voucher mới: {Code} — {Name} (Id: {Id})",
                voucher.Code, voucher.Name, voucher.Id);

            return ApiResult<VoucherDto>.Ok(MapToDto(voucher), "Tạo voucher thành công.");
        }

        // ========================================================
        // UPDATE
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Cập nhật thông tin cấu hình chương trình Voucher đang chạy.
        /// Chốt chặn quan trọng: Số lượng phát hành mới bổ sung (Quantity) tuyệt đối không được phép
        /// nhỏ hơn số lượng thực tế khách hàng đã sử dụng (`UsedCount`) để tránh gây lỗi bất nhất số liệu tài chính.
        /// Thực hiện làm sạch và thiết lập lại các danh mục sản phẩm áp dụng mới.
        /// </summary>
        public async Task<ApiResult<VoucherDto>> UpdateAsync(int id, UpdateVoucherRequest request)
        {
            var voucher = await _voucherRepo.GetByIdWithCategoriesAsync(id);

            if (voucher == null)
                return ApiResult<VoucherDto>.Fail("Không tìm thấy voucher yêu cầu.", ApiErrorCode.NotFound);

            // NGHIỆP VỤ: Khống chế số lượng phát hành tối thiểu không được nhỏ hơn số lượt đã thực tế sử dụng
            if (request.Quantity.HasValue && request.Quantity.Value < voucher.UsedCount)
                return ApiResult<VoucherDto>.Fail(
                    $"Số lượng phát hành ({request.Quantity}) không được nhỏ hơn số lần đã sử dụng ({voucher.UsedCount}).");

            // Cập nhật cấu hình
            voucher.Name             = request.Name.Trim();
            voucher.DiscountType     = request.DiscountType;
            voucher.DiscountValue    = request.DiscountValue;
            voucher.MinOrderValue    = request.MinOrderValue;
            voucher.MaxDiscountAmount = request.MaxDiscountAmount;
            voucher.StartDate        = request.StartDate;
            voucher.EndDate          = request.EndDate;
            voucher.Quantity         = request.Quantity;
            voucher.MaxUsesPerUser   = request.MaxUsesPerUser;
            voucher.ApplyFor         = request.ApplyFor;
            voucher.IsStackable      = request.IsStackable;
            voucher.IsActive         = request.IsActive;
            voucher.Description      = request.Description?.Trim();

            // NGHIỆP VỤ: Làm sạch và cập nhật lại danh sách danh mục sản phẩm được áp dụng Voucher
            voucher.VoucherCategories.Clear();
            if (request.CategoryIds != null && request.CategoryIds.Count > 0)
            {
                foreach (var categoryId in request.CategoryIds.Distinct())
                {
                    voucher.VoucherCategories.Add(new VoucherCategory
                    {
                        VoucherId  = voucher.Id,
                        CategoryId = categoryId
                    });
                }
            }

            await _voucherRepo.SaveChangesAsync();

            _logger.LogInformation("Cập nhật voucher: {Code} (Id: {Id})", voucher.Code, voucher.Id);

            return ApiResult<VoucherDto>.Ok(MapToDto(voucher), "Cập nhật voucher thành công.");
        }

        // ========================================================
        // DELETE (Soft Delete)
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Xóa bỏ một Voucher khuyến mãi khỏi hệ thống.
        /// Áp dụng cơ chế xóa mềm (Soft Delete): Đánh dấu `IsDeleted = true`, ghi nhận ngày xóa và tự động vô hiệu hóa mã.
        /// Việc xóa mềm giúp hệ thống bảo toàn tính toàn vẹn dữ liệu tham chiếu (foreign key constraints)
        /// của các đơn hàng lịch sử đã áp dụng mã giảm giá này, đảm bảo độ chính xác cho báo cáo tài chính/kiểm toán sau này.
        /// </summary>
        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            var voucher = await _voucherRepo.GetByIdWithCategoriesAsync(id);

            if (voucher == null)
                return ApiResult<bool>.Fail("Không tìm thấy voucher yêu cầu.", ApiErrorCode.NotFound);

            voucher.IsDeleted   = true;
            voucher.DeletedDate = DateTime.UtcNow;
            voucher.IsActive    = false; // Vô hiệu hóa ngay lập tức để không thể áp dụng cho đơn mới

            await _voucherRepo.SaveChangesAsync();

            _logger.LogInformation("Xóa voucher: {Code} (Id: {Id})", voucher.Code, voucher.Id);

            return ApiResult<bool>.Ok(true, "Xóa voucher thành công.");
        }

        // ========================================================
        // TOGGLE STATUS
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Đảo trạng thái kích hoạt/vô hiệu hóa tức thì của Voucher.
        /// Giúp bộ phận quản lý tạm dừng chương trình khuyến mãi khẩn cấp khi có sự cố gian lận (exploit)
        /// hoặc kích hoạt lại nhanh chóng khi chiến dịch chạy tiếp mà không cần nhập lại cấu hình.
        /// </summary>
        public async Task<ApiResult<VoucherDto>> ToggleStatusAsync(int id)
        {
            var voucher = await _voucherRepo.GetByIdWithCategoriesAsync(id);

            if (voucher == null)
                return ApiResult<VoucherDto>.Fail("Không tìm thấy voucher yêu cầu.", ApiErrorCode.NotFound);

            voucher.IsActive = !voucher.IsActive; // Đảo trạng thái hoạt động

            await _voucherRepo.SaveChangesAsync();

            var status = voucher.IsActive ? "kích hoạt" : "vô hiệu hóa";
            _logger.LogInformation("{Status} voucher: {Code} (Id: {Id})", status, voucher.Code, voucher.Id);

            return ApiResult<VoucherDto>.Ok(MapToDto(voucher), $"Đã {status} voucher thành công.");
        }

        // ========================================================
        // VALIDATE VOUCHER CODE (preview trước checkout)
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Xác thực điều kiện sử dụng Voucher (bản xem trước - preview trước khi đặt hàng).
        /// Kiểm tra một loạt điều kiện: trạng thái kích hoạt, thời gian hiệu lực, số lượng còn lại,
        /// giá trị đơn tối thiểu, kênh bán hàng (online vs tại quầy POS), tính tương thích danh mục sản phẩm,
        /// và giới hạn số lần sử dụng của riêng từng khách hàng.
        /// </summary>
        public async Task<ApiResult<ValidateVoucherResponse>> ValidateVoucherCodeAsync(
            ValidateVoucherRequest request,
            Guid? userId)
        {
            var vouchers = await _voucherRepo.GetByCodesWithCategoriesAsync(
                new List<string> { request.Code.Trim().ToUpper() });

            var voucher = vouchers.FirstOrDefault();

            if (voucher == null)
            {
                return ApiResult<ValidateVoucherResponse>.Ok(new ValidateVoucherResponse
                {
                    IsValid = false,
                    ErrorMessage = "Mã voucher không tồn tại.",
                    Code = request.Code
                });
            }

            var now = DateTime.UtcNow;

            // 1. NGHIỆP VỤ: Trạng thái hoạt động của Voucher
            if (!voucher.IsActive)
                return OkInvalid(request.Code, "Mã voucher đã bị vô hiệu hóa.");

            // 2. NGHIỆP VỤ: Thời gian hiệu lực của Voucher (Chưa đến ngày áp dụng)
            if (now < voucher.StartDate)
                return OkInvalid(request.Code, $"Mã voucher chưa đến thời gian sử dụng (từ {voucher.StartDate:dd/MM/yyyy}).");

            // 3. NGHIỆP VỤ: Thời gian hết hạn của Voucher
            if (now > voucher.EndDate)
                return OkInvalid(request.Code, $"Mã voucher đã hết hạn vào ngày {voucher.EndDate:dd/MM/yyyy}.");

            // 4. NGHIỆP VỤ: Giới hạn tổng số lượng phát hành của hệ thống
            if (voucher.Quantity.HasValue && voucher.UsedCount >= voucher.Quantity.Value)
                return OkInvalid(request.Code, "Mã voucher đã hết lượt sử dụng.");

            // 5. NGHIỆP VỤ: Giá trị đơn hàng tối thiểu (Min Order Value)
            if (request.SubTotal < voucher.MinOrderValue)
                return OkInvalid(request.Code,
                    $"Đơn hàng chưa đạt giá trị tối thiểu {voucher.MinOrderValue:#,0}đ để sử dụng mã này.");

            // 6. NGHIỆP VỤ: Kiểm tra kênh bán hàng (ApplyFor: 1 = Online Only, 2 = Offline/POS Only, 0 = Cả hai)
            if (request.IsOnlineOrder && voucher.ApplyFor == 2)
                return OkInvalid(request.Code, "Mã voucher này chỉ áp dụng tại quầy.");

            if (!request.IsOnlineOrder && voucher.ApplyFor == 1)
                return OkInvalid(request.Code, "Mã voucher này chỉ áp dụng cho đơn hàng online.");

            // 7. NGHIỆP VỤ: Ràng buộc danh mục sản phẩm (Chỉ áp dụng cho một số danh mục nhất định)
            if (voucher.VoucherCategories.Any() && request.OrderItemCategoryIds != null && request.OrderItemCategoryIds.Any())
            {
                var voucherCategoryIds = voucher.VoucherCategories.Select(vc => vc.CategoryId).ToHashSet();
                if (!request.OrderItemCategoryIds.Any(catId => voucherCategoryIds.Contains(catId)))
                    return OkInvalid(request.Code, "Mã voucher không áp dụng cho các sản phẩm trong đơn hàng.");
            }

            // 8. NGHIỆP VỤ: Giới hạn tần suất sử dụng trên mỗi khách hàng (Max Uses Per User)
            if (userId.HasValue && voucher.MaxUsesPerUser.HasValue)
            {
                var usageCounts = await _voucherRepo.GetUserVoucherUsageCountsAsync(
                    userId.Value, new List<int> { voucher.Id });
                var currentCount = usageCounts.GetValueOrDefault(voucher.Id, 0);
                if (currentCount >= voucher.MaxUsesPerUser.Value)
                    return OkInvalid(request.Code,
                        $"Bạn đã sử dụng mã này {currentCount} lần (tối đa {voucher.MaxUsesPerUser} lần/khách).");
            }

            // 9. NGHIỆP VỤ: Tính toán số tiền chiết khấu dựa trên loại cấu hình (DiscountType: 0 = Cố định, 1 = Phần trăm)
            decimal discountAmount;
            if (voucher.DiscountType == 0)
            {
                // Giảm giá một lượng tiền cố định
                discountAmount = voucher.DiscountValue;
            }
            else
            {
                // Giảm giá theo % và khống chế mức giảm tối đa (MaxDiscountAmount) nếu có
                discountAmount = request.SubTotal * voucher.DiscountValue / 100;
                if (voucher.MaxDiscountAmount.HasValue && discountAmount > voucher.MaxDiscountAmount.Value)
                    discountAmount = voucher.MaxDiscountAmount.Value;
            }

            return ApiResult<ValidateVoucherResponse>.Ok(new ValidateVoucherResponse
            {
                IsValid        = true,
                DiscountAmount = discountAmount,
                VoucherName    = voucher.Name,
                Code           = voucher.Code
            });
        }

        // ========================================================
        // AVAILABLE FOR ORDER
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Liệt kê danh sách tất cả các Voucher khả dụng và không khả dụng đối với một giỏ hàng cụ thể (Customer Self-Service).
        /// Tính toán giá trị chiết khấu dự kiến (Estimated Discount) cho từng voucher khả dụng,
        /// và chỉ ra lý do cụ thể nếu voucher đó không khả dụng (ví dụ: chưa đạt mức chi tiêu tối thiểu, sai kênh áp dụng,...),
        /// giúp khách hàng dễ dàng đưa ra quyết định chọn mã ưu đãi tối ưu nhất.
        /// </summary>
        public async Task<ApiResult<List<VoucherAvailabilityDto>>> GetAvailableForOrderAsync(
            GetAvailableVouchersRequest request, Guid? userId)
        {
            // Lấy danh sách Voucher đang kích hoạt công khai trong thời hạn
            var vouchers = await _voucherRepo.GetActiveVouchersForCustomerAsync();

            // Truy vấn số lần sử dụng của khách hàng đối với từng voucher (nếu đã đăng nhập)
            Dictionary<int, int>? usageCounts = null;
            if (userId.HasValue && vouchers.Any())
            {
                var ids = vouchers.Select(v => v.Id).ToList();
                usageCounts = await _voucherRepo.GetUserVoucherUsageCountsAsync(userId.Value, ids);
            }

            var result = vouchers.Select(v =>
            {
                string? reason = null;

                // 1. Kiểm tra giá trị đơn tối thiểu của voucher
                if (request.SubTotal < v.MinOrderValue)
                    reason = $"Đơn từ {v.MinOrderValue:#,0}đ (bạn: {request.SubTotal:#,0}đ)";
                // 2. Kiểm tra kênh áp dụng (Online vs Offline/POS)
                else if (request.IsOnlineOrder && v.ApplyFor == 2)
                    reason = "Chỉ áp dụng tại quầy";
                else if (!request.IsOnlineOrder && v.ApplyFor == 1)
                    reason = "Chỉ áp dụng online";
                // 3. Kiểm tra số lần sử dụng tối đa của khách hàng này
                else if (userId.HasValue && v.MaxUsesPerUser.HasValue)
                {
                    var used = usageCounts?.GetValueOrDefault(v.Id, 0) ?? 0;
                    if (used >= v.MaxUsesPerUser.Value)
                        reason = $"Bạn đã dùng {used}/{v.MaxUsesPerUser} lần";
                }

                // Tính toán số tiền chiết khấu dự kiến (Estimated Discount)
                decimal discount = 0;
                if (reason == null)
                {
                    discount = v.DiscountType == 0
                        ? v.DiscountValue
                        : Math.Min(
                            request.SubTotal * v.DiscountValue / 100,
                            v.MaxDiscountAmount ?? decimal.MaxValue);
                }

                return new VoucherAvailabilityDto
                {
                    Id = v.Id, Code = v.Code, Name = v.Name, Description = v.Description,
                    DiscountType = v.DiscountType, DiscountValue = v.DiscountValue,
                    MaxDiscountAmount = v.MaxDiscountAmount, MinOrderValue = v.MinOrderValue,
                    StartDate = v.StartDate, EndDate = v.EndDate, IsStackable = v.IsStackable,
                    IsApplicable = reason == null,
                    EstimatedDiscount = discount,
                    NotApplicableReason = reason
                };
            })
            // Ưu tiên hiển thị: Voucher dùng được xếp trước, kế đến là số tiền giảm nhiều nhất
            .OrderByDescending(v => v.IsApplicable)
            .ThenByDescending(v => v.EstimatedDiscount)
            .ToList();

            return ApiResult<List<VoucherAvailabilityDto>>.Ok(result);
        }

        // ========================================================
        // PRIVATE HELPERS
        // ========================================================

        private static ApiResult<ValidateVoucherResponse> OkInvalid(string code, string error) =>
            ApiResult<ValidateVoucherResponse>.Ok(new ValidateVoucherResponse
            {
                IsValid      = false,
                ErrorMessage = error,
                Code         = code
            });

        private static VoucherDto MapToDto(Voucher e) => new()
        {
            Id               = e.Id,
            Code             = e.Code,
            Name             = e.Name,
            DiscountType     = e.DiscountType,
            DiscountValue    = e.DiscountValue,
            MinOrderValue    = e.MinOrderValue,
            MaxDiscountAmount = e.MaxDiscountAmount,
            StartDate        = e.StartDate,
            EndDate          = e.EndDate,
            Quantity         = e.Quantity,
            UsedCount        = e.UsedCount,
            MaxUsesPerUser   = e.MaxUsesPerUser,
            ApplyFor         = e.ApplyFor,
            IsStackable      = e.IsStackable,
            IsActive         = e.IsActive,
            Description      = e.Description,
            CreatedDate      = e.CreatedDate,
            CategoryIds      = e.VoucherCategories.Select(vc => vc.CategoryId).ToList()
        };
    }
}
