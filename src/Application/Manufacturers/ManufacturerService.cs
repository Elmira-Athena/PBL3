using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Manufacturers;

namespace PBL3.Application.Manufacturers
{
    public class ManufacturerService(
        IManufacturerRepository manufacturerRepo,
        ILogger<ManufacturerService> logger) : IManufacturerService
    {
        private readonly IManufacturerRepository _manufacturerRepo =
            manufacturerRepo ?? throw new ArgumentNullException(nameof(manufacturerRepo));
        private readonly ILogger<ManufacturerService> _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));

        // ========================================================
        // GET LIST — Phân trang + tìm kiếm
        // ========================================================
        public async Task<ApiResult<PagedResult<ManufacturerDto>>> GetPagedListAsync(ManufacturerFilterRequest filter)
        {
            var (items, totalCount) = await _manufacturerRepo.GetPagedListAsync(
                filter.Keyword,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending);

            var dtos = items.Select(MapToDto).ToList();

            var result = new PagedResult<ManufacturerDto>
            {
                Items      = dtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize   = filter.PageSize
            };

            return ApiResult<PagedResult<ManufacturerDto>>.Ok(result);
        }

        // ========================================================
        // GET ALL FOR DROPDOWN — Dùng cho form chọn hãng
        // ========================================================
        public async Task<ApiResult<List<ManufacturerSummaryDto>>> GetAllForDropdownAsync()
        {
            var items = await _manufacturerRepo.GetAllActiveAsync();

            var dtos = items.Select(m => new ManufacturerSummaryDto
            {
                Id      = m.Id,
                Name    = m.Name,
                LogoUrl = m.LogoUrl
            }).ToList();

            return ApiResult<List<ManufacturerSummaryDto>>.Ok(dtos);
        }

        // ========================================================
        // GET BY ID — Chi tiết hãng sản xuất
        // ========================================================
        public async Task<ApiResult<ManufacturerDto>> GetByIdAsync(int id)
        {
            var manufacturer = await _manufacturerRepo.GetByIdAsync(id);

            if (manufacturer == null)
                return ApiResult<ManufacturerDto>.Fail("Không tìm thấy hãng sản xuất yêu cầu.", ApiErrorCode.NotFound);

            return ApiResult<ManufacturerDto>.Ok(MapToDto(manufacturer));
        }

        // ========================================================
        // CREATE — Tạo mới hãng sản xuất
        // ========================================================
        public async Task<ApiResult<ManufacturerDto>> CreateAsync(CreateManufacturerRequest request)
        {
            // Kiểm tra tên trùng lặp
            if (await _manufacturerRepo.IsDuplicateNameAsync(request.Name))
                return ApiResult<ManufacturerDto>.Fail($"Hãng sản xuất với tên \"{request.Name}\" đã tồn tại trong hệ thống.", ApiErrorCode.Conflict);

            var manufacturer = new Manufacturer
            {
                Name         = request.Name.Trim(),
                LogoUrl      = request.LogoUrl?.Trim(),
                Website      = request.Website?.Trim(),
                SupportEmail = request.SupportEmail?.Trim(),
                CreatedDate  = DateTime.UtcNow,
                IsDeleted    = false
            };

            await _manufacturerRepo.AddAsync(manufacturer);
            await _manufacturerRepo.SaveChangesAsync();

            _logger.LogInformation("Tạo hãng sản xuất mới: {ManufacturerName} (Id: {ManufacturerId})",
                manufacturer.Name, manufacturer.Id);

            return ApiResult<ManufacturerDto>.Ok(MapToDto(manufacturer), "Tạo hãng sản xuất thành công.");
        }

        // ========================================================
        // UPDATE — Cập nhật hãng sản xuất
        // ========================================================
        public async Task<ApiResult<ManufacturerDto>> UpdateAsync(int id, UpdateManufacturerRequest request)
        {
            var manufacturer = await _manufacturerRepo.GetByIdAsync(id);

            if (manufacturer == null)
                return ApiResult<ManufacturerDto>.Fail("Không tìm thấy hãng sản xuất yêu cầu.", ApiErrorCode.NotFound);

            // Kiểm tra tên trùng (bỏ qua chính nó)
            if (await _manufacturerRepo.IsDuplicateNameAsync(request.Name, excludeId: id))
                return ApiResult<ManufacturerDto>.Fail($"Hãng sản xuất với tên \"{request.Name}\" đã tồn tại trong hệ thống.", ApiErrorCode.Conflict);

            // Cập nhật entity
            manufacturer.Name         = request.Name.Trim();
            manufacturer.LogoUrl      = request.LogoUrl?.Trim();
            manufacturer.Website      = request.Website?.Trim();
            manufacturer.SupportEmail = request.SupportEmail?.Trim();
            manufacturer.ModifiedDate = DateTime.UtcNow;

            await _manufacturerRepo.SaveChangesAsync();

            _logger.LogInformation("Cập nhật hãng sản xuất: {ManufacturerName} (Id: {ManufacturerId})",
                manufacturer.Name, manufacturer.Id);

            return ApiResult<ManufacturerDto>.Ok(MapToDto(manufacturer), "Cập nhật hãng sản xuất thành công.");
        }

        // ========================================================
        // DELETE — Xóa mềm (Soft Delete)
        // Quy tắc: KHÔNG xóa nếu còn Product đang dùng hãng này.
        // Tương tự Supplier → ImportReceipt.
        // ========================================================
        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            var manufacturer = await _manufacturerRepo.GetByIdAsync(id);

            if (manufacturer == null)
                return ApiResult<bool>.Fail("Không tìm thấy hãng sản xuất yêu cầu.", ApiErrorCode.NotFound);

            // Ràng buộc nghiệp vụ: Không xóa nếu còn sản phẩm
            if (await _manufacturerRepo.HasProductsAsync(id))
                return ApiResult<bool>.Fail(
                    "Không thể xóa hãng sản xuất này vì vẫn còn sản phẩm đang thuộc hãng. " +
                    "Vui lòng xóa hoặc chuyển hãng cho tất cả sản phẩm trước.");

            // Soft Delete
            manufacturer.IsDeleted   = true;
            manufacturer.DeletedDate = DateTime.UtcNow;

            await _manufacturerRepo.SaveChangesAsync();

            _logger.LogInformation("Xóa mềm hãng sản xuất: {ManufacturerName} (Id: {ManufacturerId})",
                manufacturer.Name, manufacturer.Id);

            return ApiResult<bool>.Ok(true, "Xóa hãng sản xuất thành công.");
        }

        // ========================================================
        // PRIVATE HELPERS
        // ========================================================
        private static ManufacturerDto MapToDto(Manufacturer entity)
        {
            return new ManufacturerDto
            {
                Id           = entity.Id,
                Name         = entity.Name,
                LogoUrl      = entity.LogoUrl,
                Website      = entity.Website,
                SupportEmail = entity.SupportEmail,
                CreatedDate  = entity.CreatedDate
            };
        }
    }
}
