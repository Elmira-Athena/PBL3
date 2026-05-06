using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Vouchers;

namespace PBL3.Service.Vouchers
{
    public class VoucherService : IVoucherService
    {
        private readonly IVoucherRepository _voucherRepo;
        private readonly ILogger<VoucherService> _logger;

        public VoucherService(
            IVoucherRepository voucherRepo,
            ILogger<VoucherService> logger)
        {
            _voucherRepo = voucherRepo;
            _logger = logger;
        }

        // ========================================================
        // GET LIST
        // ========================================================
        public async Task<ApiResult<PagedResult<VoucherDto>>> GetPagedListAsync(VoucherFilterRequest filter)
        {
            var (items, totalCount) = await _voucherRepo.GetPagedListAsync(
                filter.Keyword,
                filter.IsActive,
                filter.FromDate,
                filter.ToDate,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending);

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
        public async Task<ApiResult<VoucherDto>> GetByIdAsync(int id)
        {
            var voucher = await _voucherRepo.GetByIdWithCategoriesAsync(id);

            if (voucher == null)
                return ApiResult<VoucherDto>.Fail("Không tìm thấy voucher yêu cầu.");

            return ApiResult<VoucherDto>.Ok(MapToDto(voucher));
        }

        // ========================================================
        // CREATE
        // ========================================================
        public async Task<ApiResult<VoucherDto>> CreateAsync(CreateVoucherRequest request)
        {
            var normalizedCode = request.Code.Trim().ToUpper();

            if (await _voucherRepo.IsDuplicateCodeAsync(normalizedCode))
                return ApiResult<VoucherDto>.Fail($"Mã voucher \"{normalizedCode}\" đã tồn tại trong hệ thống.");

            var voucher = new Voucher
            {
                Code             = normalizedCode,
                Name             = request.Name.Trim(),
                DiscountType     = request.DiscountType,
                DiscountValue    = request.DiscountValue,
                MinOrderValue    = request.MinOrderValue,
                MaxDiscountAmount = request.MaxDiscountAmount,
                StartDate        = request.StartDate,
                EndDate          = request.EndDate,
                Quantity         = request.Quantity,
                MaxUsesPerUser   = request.MaxUsesPerUser,
                ApplyFor         = request.ApplyFor,
                IsStackable      = request.IsStackable,
                IsActive         = request.IsActive,
                Description      = request.Description?.Trim(),
                CreatedDate      = DateTime.UtcNow,
                IsDeleted        = false,
                UsedCount        = 0
            };

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
        public async Task<ApiResult<VoucherDto>> UpdateAsync(int id, UpdateVoucherRequest request)
        {
            var voucher = await _voucherRepo.GetByIdWithCategoriesAsync(id);

            if (voucher == null)
                return ApiResult<VoucherDto>.Fail("Không tìm thấy voucher yêu cầu.");

            // Validate Quantity không được nhỏ hơn UsedCount
            if (request.Quantity.HasValue && request.Quantity.Value < voucher.UsedCount)
                return ApiResult<VoucherDto>.Fail(
                    $"Số lượng phát hành ({request.Quantity}) không được nhỏ hơn số lần đã sử dụng ({voucher.UsedCount}).");

            // Cập nhật fields
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

            // Cập nhật VoucherCategories: xóa hết, thêm lại
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
        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            var voucher = await _voucherRepo.GetByIdWithCategoriesAsync(id);

            if (voucher == null)
                return ApiResult<bool>.Fail("Không tìm thấy voucher yêu cầu.");

            voucher.IsDeleted   = true;
            voucher.DeletedDate = DateTime.UtcNow;
            voucher.IsActive    = false;

            await _voucherRepo.SaveChangesAsync();

            _logger.LogInformation("Xóa voucher: {Code} (Id: {Id})", voucher.Code, voucher.Id);

            return ApiResult<bool>.Ok(true, "Xóa voucher thành công.");
        }

        // ========================================================
        // TOGGLE STATUS
        // ========================================================
        public async Task<ApiResult<VoucherDto>> ToggleStatusAsync(int id)
        {
            var voucher = await _voucherRepo.GetByIdWithCategoriesAsync(id);

            if (voucher == null)
                return ApiResult<VoucherDto>.Fail("Không tìm thấy voucher yêu cầu.");

            voucher.IsActive = !voucher.IsActive;

            await _voucherRepo.SaveChangesAsync();

            var status = voucher.IsActive ? "kích hoạt" : "vô hiệu hóa";
            _logger.LogInformation("{Status} voucher: {Code} (Id: {Id})", status, voucher.Code, voucher.Id);

            return ApiResult<VoucherDto>.Ok(MapToDto(voucher), $"Đã {status} voucher thành công.");
        }

        // ========================================================
        // VALIDATE VOUCHER CODE (preview trước checkout)
        // ========================================================
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

            if (!voucher.IsActive)
                return OkInvalid(request.Code, "Mã voucher đã bị vô hiệu hóa.");

            if (now < voucher.StartDate)
                return OkInvalid(request.Code, $"Mã voucher chưa đến thời gian sử dụng (từ {voucher.StartDate:dd/MM/yyyy}).");

            if (now > voucher.EndDate)
                return OkInvalid(request.Code, $"Mã voucher đã hết hạn vào ngày {voucher.EndDate:dd/MM/yyyy}.");

            if (voucher.Quantity.HasValue && voucher.UsedCount >= voucher.Quantity.Value)
                return OkInvalid(request.Code, "Mã voucher đã hết lượt sử dụng.");

            if (request.SubTotal < voucher.MinOrderValue)
                return OkInvalid(request.Code,
                    $"Đơn hàng chưa đạt giá trị tối thiểu {voucher.MinOrderValue:#,0}đ để sử dụng mã này.");

            if (request.IsOnlineOrder && voucher.ApplyFor == 2)
                return OkInvalid(request.Code, "Mã voucher này chỉ áp dụng tại quầy.");

            if (!request.IsOnlineOrder && voucher.ApplyFor == 1)
                return OkInvalid(request.Code, "Mã voucher này chỉ áp dụng cho đơn hàng online.");

            // Kiểm tra danh mục sản phẩm
            if (voucher.VoucherCategories.Any() && request.OrderItemCategoryIds != null && request.OrderItemCategoryIds.Any())
            {
                var voucherCategoryIds = voucher.VoucherCategories.Select(vc => vc.CategoryId).ToHashSet();
                if (!request.OrderItemCategoryIds.Any(catId => voucherCategoryIds.Contains(catId)))
                    return OkInvalid(request.Code, "Mã voucher không áp dụng cho các sản phẩm trong đơn hàng.");
            }

            // Kiểm tra MaxUsesPerUser nếu có userId
            if (userId.HasValue && voucher.MaxUsesPerUser.HasValue)
            {
                var usageCounts = await _voucherRepo.GetUserVoucherUsageCountsAsync(
                    userId.Value, new List<int> { voucher.Id });
                var currentCount = usageCounts.GetValueOrDefault(voucher.Id, 0);
                if (currentCount >= voucher.MaxUsesPerUser.Value)
                    return OkInvalid(request.Code,
                        $"Bạn đã sử dụng mã này {currentCount} lần (tối đa {voucher.MaxUsesPerUser} lần/khách).");
            }

            // Tính discount
            decimal discountAmount;
            if (voucher.DiscountType == 0)
            {
                discountAmount = voucher.DiscountValue;
            }
            else
            {
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
        public async Task<ApiResult<List<VoucherAvailabilityDto>>> GetAvailableForOrderAsync(
            GetAvailableVouchersRequest request, Guid? userId)
        {
            var vouchers = await _voucherRepo.GetActiveVouchersForCustomerAsync();

            Dictionary<int, int>? usageCounts = null;
            if (userId.HasValue && vouchers.Any())
            {
                var ids = vouchers.Select(v => v.Id).ToList();
                usageCounts = await _voucherRepo.GetUserVoucherUsageCountsAsync(userId.Value, ids);
            }

            var result = vouchers.Select(v =>
            {
                string? reason = null;

                if (request.SubTotal < v.MinOrderValue)
                    reason = $"Đơn từ {v.MinOrderValue:#,0}đ (bạn: {request.SubTotal:#,0}đ)";
                else if (request.IsOnlineOrder && v.ApplyFor == 2)
                    reason = "Chỉ áp dụng tại quầy";
                else if (!request.IsOnlineOrder && v.ApplyFor == 1)
                    reason = "Chỉ áp dụng online";
                else if (userId.HasValue && v.MaxUsesPerUser.HasValue)
                {
                    var used = usageCounts?.GetValueOrDefault(v.Id, 0) ?? 0;
                    if (used >= v.MaxUsesPerUser.Value)
                        reason = $"Bạn đã dùng {used}/{v.MaxUsesPerUser} lần";
                }

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
