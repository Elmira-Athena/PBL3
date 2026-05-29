using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;
using PBL3.Shared.DTOs.Suppliers;

namespace PBL3.Application.Suppliers
{
    public class SupplierService : ISupplierService
    {
        private readonly ISupplierRepository _supplierRepo;
        private readonly ILogger<SupplierService> _logger;

        public SupplierService(ISupplierRepository supplierRepo, ILogger<SupplierService> logger)
        {
            _supplierRepo = supplierRepo;
            _logger = logger;
        }

        // ========================================================
        // GET LIST — Phân trang + tìm kiếm
        // ========================================================
        public async Task<ApiResult<PagedResult<SupplierDto>>> GetPagedListAsync(SupplierFilterRequest filter)
        {
            var (items, totalCount) = await _supplierRepo.GetPagedListAsync(
                filter.Keyword,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending);

            var dtos = items.Select(MapToDto).ToList();

            var result = new PagedResult<SupplierDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };

            return ApiResult<PagedResult<SupplierDto>>.Ok(result);
        }

        // ========================================================
        // GET BY ID — Chi tiết nhà cung cấp
        // ========================================================
        public async Task<ApiResult<SupplierDto>> GetByIdAsync(int id)
        {
            var supplier = await _supplierRepo.GetByIdAsync(id);

            if (supplier == null)
                return ApiResult<SupplierDto>.Fail("Không tìm thấy nhà cung cấp yêu cầu.", ApiErrorCode.NotFound);

            return ApiResult<SupplierDto>.Ok(MapToDto(supplier));
        }

        // ========================================================
        // CREATE — Tạo mới nhà cung cấp
        // ========================================================
        public async Task<ApiResult<SupplierDto>> CreateAsync(CreateSupplierRequest request)
        {
            var supplier = new Supplier
            {
                Name = request.Name.Trim(),
                ContactPerson = request.ContactPerson?.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                Email = request.Email?.Trim(),
                Address = request.Address?.Trim(),
                TaxCode = request.TaxCode?.Trim(),
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            };

            await _supplierRepo.AddAsync(supplier);
            await _supplierRepo.SaveChangesAsync();

            _logger.LogInformation("Tạo nhà cung cấp mới: {SupplierName} (Id: {SupplierId})", supplier.Name, supplier.Id);

            return ApiResult<SupplierDto>.Ok(MapToDto(supplier), "Tạo nhà cung cấp thành công.");
        }

        // ========================================================
        // UPDATE — Cập nhật nhà cung cấp
        // ========================================================
        public async Task<ApiResult<SupplierDto>> UpdateAsync(int id, UpdateSupplierRequest request)
        {
            var supplier = await _supplierRepo.GetByIdAsync(id);

            if (supplier == null)
                return ApiResult<SupplierDto>.Fail("Không tìm thấy nhà cung cấp yêu cầu.", ApiErrorCode.NotFound);

            // Cập nhật entity
            supplier.Name = request.Name.Trim();
            supplier.ContactPerson = request.ContactPerson?.Trim();
            supplier.PhoneNumber = request.PhoneNumber.Trim();
            supplier.Email = request.Email?.Trim();
            supplier.Address = request.Address?.Trim();
            supplier.TaxCode = request.TaxCode?.Trim();

            await _supplierRepo.SaveChangesAsync();

            _logger.LogInformation("Cập nhật nhà cung cấp: {SupplierName} (Id: {SupplierId})", supplier.Name, supplier.Id);

            return ApiResult<SupplierDto>.Ok(MapToDto(supplier), "Cập nhật nhà cung cấp thành công.");
        }

        // ========================================================
        // DELETE — Xóa mềm (Soft Delete)
        // Quy tắc: KHÔNG xóa cứng vì Supplier có thể đã gắn với
        // các Phiếu nhập kho cũ → tránh lỗi Foreign Key.
        // ========================================================
        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            var supplier = await _supplierRepo.GetByIdAsync(id);

            if (supplier == null)
                return ApiResult<bool>.Fail("Không tìm thấy nhà cung cấp yêu cầu.", ApiErrorCode.NotFound);

            // Soft Delete
            supplier.IsDeleted = true;

            await _supplierRepo.SaveChangesAsync();

            _logger.LogInformation("Xóa mềm nhà cung cấp: {SupplierName} (Id: {SupplierId})", supplier.Name, supplier.Id);

            return ApiResult<bool>.Ok(true, "Xóa nhà cung cấp thành công.");
        }

        // ========================================================
        // PRIVATE HELPERS
        // ========================================================
        private static SupplierDto MapToDto(Supplier entity)
        {
            return new SupplierDto
            {
                Id = entity.Id,
                Name = entity.Name,
                ContactPerson = entity.ContactPerson,
                PhoneNumber = entity.PhoneNumber,
                Email = entity.Email,
                Address = entity.Address,
                TaxCode = entity.TaxCode,
                CreatedDate = entity.CreatedDate
            };
        }
    }
}
