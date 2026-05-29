using FluentValidation;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Application.Banners
{
    public class BannerService : IBannerService
    {
        private readonly IBannerRepository _bannerRepo;
        private readonly IValidator<CreateBannerRequest> _createValidator;
        private readonly IValidator<UpdateBannerRequest> _updateValidator;
        private readonly ILogger<BannerService> _logger;

        public BannerService(
            IBannerRepository bannerRepo,
            IValidator<CreateBannerRequest> createValidator,
            IValidator<UpdateBannerRequest> updateValidator,
            ILogger<BannerService> logger)
        {
            _bannerRepo = bannerRepo;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _logger = logger;
        }

        public async Task<ApiResult<PagedResult<BannerDto>>> GetPagedListAsync(BannerFilterRequest filter)
        {
            var (items, totalCount) = await _bannerRepo.GetPagedListAsync(
                filter.Keyword, filter.PageNumber, filter.PageSize, filter.SortBy, filter.SortDescending);

            var dtos = items.Select(MapToDto).ToList();

            var result = new PagedResult<BannerDto>
            {
                Items      = dtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize   = filter.PageSize
            };

            return ApiResult<PagedResult<BannerDto>>.Ok(result);
        }

        public async Task<ApiResult<BannerDto>> GetByIdAsync(int id)
        {
            var banner = await _bannerRepo.GetByIdAsync(id);
            if (banner == null)
                return ApiResult<BannerDto>.Fail("Không tìm thấy banner yêu cầu.", ApiErrorCode.NotFound);

            return ApiResult<BannerDto>.Ok(MapToDto(banner));
        }

        public async Task<ApiResult<List<BannerPublicDto>>> GetActiveAsync()
        {
            var banners = await _bannerRepo.GetActiveAsync(DateTime.UtcNow);

            var dtos = banners.Select(b => new BannerPublicDto
            {
                Id       = b.Id,
                Title    = b.Title,
                ImageUrl = b.ImageUrl,
                LinkUrl  = b.LinkUrl
            }).ToList();

            return ApiResult<List<BannerPublicDto>>.Ok(dtos);
        }

        public async Task<ApiResult<BannerDto>> CreateAsync(CreateBannerRequest request)
        {
            var validation = await _createValidator.ValidateAsync(request);
            if (!validation.IsValid)
                return ApiResult<BannerDto>.Fail(validation.Errors.First().ErrorMessage, ApiErrorCode.Validation);

            var banner = new Banner
            {
                Title       = request.Title.Trim(),
                ImageUrl    = request.ImageUrl.Trim(),
                LinkUrl     = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl.Trim(),
                SortOrder   = request.SortOrder,
                IsActive    = request.IsActive,
                StartDate   = request.StartDate,
                EndDate     = request.EndDate,
                CreatedDate = DateTime.UtcNow,
                IsDeleted   = false
            };

            await _bannerRepo.AddAsync(banner);
            await _bannerRepo.SaveChangesAsync();

            _logger.LogInformation("Tạo banner mới: {Title} (Id: {Id})", banner.Title, banner.Id);

            return ApiResult<BannerDto>.Ok(MapToDto(banner), "Tạo banner thành công.");
        }

        public async Task<ApiResult<BannerDto>> UpdateAsync(int id, UpdateBannerRequest request)
        {
            var validation = await _updateValidator.ValidateAsync(request);
            if (!validation.IsValid)
                return ApiResult<BannerDto>.Fail(validation.Errors.First().ErrorMessage, ApiErrorCode.Validation);

            var banner = await _bannerRepo.GetByIdWithTrackingAsync(id);
            if (banner == null)
                return ApiResult<BannerDto>.Fail("Không tìm thấy banner yêu cầu.", ApiErrorCode.NotFound);

            banner.Title        = request.Title.Trim();
            banner.ImageUrl     = request.ImageUrl.Trim();
            banner.LinkUrl      = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl.Trim();
            banner.SortOrder    = request.SortOrder;
            banner.IsActive     = request.IsActive;
            banner.StartDate    = request.StartDate;
            banner.EndDate      = request.EndDate;
            banner.ModifiedDate = DateTime.UtcNow;

            await _bannerRepo.SaveChangesAsync();

            _logger.LogInformation("Cập nhật banner: {Title} (Id: {Id})", banner.Title, banner.Id);

            return ApiResult<BannerDto>.Ok(MapToDto(banner), "Cập nhật banner thành công.");
        }

        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            var banner = await _bannerRepo.GetByIdWithTrackingAsync(id);
            if (banner == null)
                return ApiResult<bool>.Fail("Không tìm thấy banner yêu cầu.", ApiErrorCode.NotFound);

            banner.IsDeleted   = true;
            banner.DeletedDate = DateTime.UtcNow;

            await _bannerRepo.SaveChangesAsync();

            _logger.LogInformation("Xóa mềm banner: {Title} (Id: {Id})", banner.Title, banner.Id);

            return ApiResult<bool>.Ok(true, "Xóa banner thành công.");
        }

        private static BannerDto MapToDto(Banner entity) => new()
        {
            Id          = entity.Id,
            Title       = entity.Title,
            ImageUrl    = entity.ImageUrl,
            LinkUrl     = entity.LinkUrl,
            SortOrder   = entity.SortOrder,
            IsActive    = entity.IsActive,
            StartDate   = entity.StartDate,
            EndDate     = entity.EndDate,
            CreatedDate = entity.CreatedDate
        };
    }
}
