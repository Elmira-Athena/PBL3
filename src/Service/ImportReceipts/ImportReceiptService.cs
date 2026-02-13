using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Service.ImportReceipts
{
    public class ImportReceiptService : IImportReceiptService
    {
        private readonly IImportReceiptRepository _receiptRepo;
        private readonly IProductSerialRepository _serialRepo;
        private readonly ISupplierRepository _supplierRepo;
        private readonly IProductRepository _productRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ImportReceiptService> _logger;

        public ImportReceiptService(
            IImportReceiptRepository receiptRepo,
            IProductSerialRepository serialRepo,
            ISupplierRepository supplierRepo,
            IProductRepository productRepo,
            IUnitOfWork unitOfWork,
            ILogger<ImportReceiptService> logger)
        {
            _receiptRepo = receiptRepo;
            _serialRepo = serialRepo;
            _supplierRepo = supplierRepo;
            _productRepo = productRepo;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ========================================================
        // CREATE — Tạo phiếu nhập kho (Transaction Required)
        // ========================================================
        public async Task<ApiResult<ImportReceiptDto>> CreateAsync(CreateImportReceiptRequest request)
        {
            // -------------------------------------------------------
            // Bước 1: Pre-checks (Validate nghiệp vụ)
            // -------------------------------------------------------

            // 1a. Kiểm tra Supplier tồn tại
            var supplier = await _supplierRepo.GetByIdAsync(request.SupplierId);
            if (supplier == null)
                return ApiResult<ImportReceiptDto>.Fail("Nhà cung cấp không tồn tại hoặc đã bị xoá.");

            // 1b. Thu thập tất cả Serial trong request
            var allSerials = request.Details
                .SelectMany(d => d.SerialNumbers)
                .Select(s => s.Trim())
                .ToList();

            // 1c. Kiểm tra Serial trùng nội bộ (đã validate ở FluentValidation, nhưng double-check)
            var duplicateInternal = allSerials
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateInternal.Any())
                return ApiResult<ImportReceiptDto>.Fail(
                    $"Mã Serial bị trùng lặp trong phiếu nhập: {string.Join(", ", duplicateInternal)}");

            // 1d. Kiểm tra Serial đã tồn tại trong DB
            var existingSerials = await _serialRepo.GetExistingSerialsAsync(allSerials);
            if (existingSerials.Any())
                return ApiResult<ImportReceiptDto>.Fail(
                    $"Mã Serial đã tồn tại trong hệ thống: {string.Join(", ", existingSerials)}");

            // 1e. Kiểm tra các VariantId có tồn tại không (qua IProductRepository)
            var variantIds = request.Details.Select(d => d.VariantId).Distinct().ToList();
            var existingVariants = await _productRepo.GetExistingVariantIdsAsync(variantIds);

            var missingVariants = variantIds.Except(existingVariants).ToList();
            if (missingVariants.Any())
                return ApiResult<ImportReceiptDto>.Fail(
                    $"Biến thể sản phẩm không tồn tại: {string.Join(", ", missingVariants)}");

            // -------------------------------------------------------
            // Bước 2: Mở Transaction (qua IUnitOfWork)
            // -------------------------------------------------------
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // ---------------------------------------------------
                // Bước 3: Tạo Header (ImportReceipt)
                // ---------------------------------------------------
                var receiptCode = await GenerateReceiptCodeAsync();

                var totalAmount = request.Details
                    .Sum(d => d.Quantity * d.ImportPrice);

                var receipt = new ImportReceipt
                {
                    ReceiptCode = receiptCode,
                    SupplierId = request.SupplierId,
                    EmployeeId = Guid.Empty, // Tạm hardcode — chưa có Auth module
                    ImportDate = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    Note = request.Note?.Trim(),
                    IsDeleted = false
                };

                await _receiptRepo.AddAsync(receipt);
                await _unitOfWork.SaveChangesAsync(); // Lưu để lấy ReceiptId

                // ---------------------------------------------------
                // Bước 4: Tạo Details & Serials
                // ---------------------------------------------------
                var allNewSerials = new List<ProductSerial>();

                foreach (var detailReq in request.Details)
                {
                    var detail = new ImportReceiptDetail
                    {
                        ReceiptId = receipt.Id,
                        VariantId = detailReq.VariantId,
                        Quantity = detailReq.Quantity,
                        ImportPrice = detailReq.ImportPrice
                    };

                    await _receiptRepo.AddDetailAsync(detail);

                    // Tạo ProductSerials cho dòng này
                    foreach (var serialNumber in detailReq.SerialNumbers)
                    {
                        allNewSerials.Add(new ProductSerial
                        {
                            SerialNumber = serialNumber.Trim(),
                            VariantId = detailReq.VariantId,
                            ImportReceiptId = receipt.Id,
                            Status = 0, // Available
                            CreatedDate = DateTime.UtcNow
                        });
                    }
                }

                // Bulk insert Serials
                await _serialRepo.AddRangeAsync(allNewSerials);

                // ---------------------------------------------------
                // Bước 5: Đồng bộ tồn kho (Stock Sync) — qua IProductRepository
                // ---------------------------------------------------
                foreach (var detailReq in request.Details)
                {
                    var variant = await _productRepo.GetVariantByIdAsync(detailReq.VariantId);
                    if (variant != null)
                    {
                        variant.StockQuantity += detailReq.Quantity;
                    }
                }

                // Lưu tất cả (Details + Serials + StockQuantity)
                await _unitOfWork.SaveChangesAsync();

                // ---------------------------------------------------
                // Bước 6: Commit Transaction
                // ---------------------------------------------------
                await _unitOfWork.CommitAsync();

                _logger.LogInformation(
                    "Tạo phiếu nhập kho thành công: {ReceiptCode}, NCC: {SupplierName}, Tổng: {TotalAmount}",
                    receiptCode, supplier.Name, totalAmount);

                // Map kết quả trả về
                var resultDto = new ImportReceiptDto
                {
                    Id = receipt.Id,
                    ReceiptCode = receipt.ReceiptCode,
                    SupplierId = receipt.SupplierId,
                    SupplierName = supplier.Name,
                    EmployeeName = "Hệ thống", // Tạm hardcode
                    ImportDate = receipt.ImportDate,
                    TotalAmount = receipt.TotalAmount,
                    Note = receipt.Note
                };

                return ApiResult<ImportReceiptDto>.Ok(resultDto, "Tạo phiếu nhập kho thành công.");
            }
            catch (Exception ex)
            {
                // Rollback toàn bộ nếu có lỗi
                await _unitOfWork.RollbackAsync();

                _logger.LogError(ex, "Lỗi khi tạo phiếu nhập kho.");

                return ApiResult<ImportReceiptDto>.Fail("Đã xảy ra lỗi khi tạo phiếu nhập kho. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // GET LIST — Danh sách phiếu nhập (Lịch sử nhập kho)
        // ========================================================
        public async Task<ApiResult<PagedResult<ImportReceiptDto>>> GetPagedListAsync(ImportReceiptFilterRequest filter)
        {
            var (items, totalCount) = await _receiptRepo.GetPagedListAsync(
                filter.Keyword,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending);

            var dtos = items.Select(r => new ImportReceiptDto
            {
                Id = r.Id,
                ReceiptCode = r.ReceiptCode,
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier?.Name ?? string.Empty,
                EmployeeName = "Hệ thống", // Tạm hardcode
                ImportDate = r.ImportDate,
                TotalAmount = r.TotalAmount,
                Note = r.Note
            }).ToList();

            var result = new PagedResult<ImportReceiptDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };

            return ApiResult<PagedResult<ImportReceiptDto>>.Ok(result);
        }

        // ========================================================
        // GET BY ID — Chi tiết phiếu nhập (kèm danh sách Serial)
        // ========================================================
        public async Task<ApiResult<ImportReceiptDto>> GetByIdAsync(int id)
        {
            var receipt = await _receiptRepo.GetByIdWithDetailsAsync(id);

            if (receipt == null)
                return ApiResult<ImportReceiptDto>.Fail("Không tìm thấy phiếu nhập kho yêu cầu.");

            // Query Serial cho từng detail line (qua IProductSerialRepository)
            var detailDtos = new List<ImportReceiptDetailDto>();

            foreach (var detail in receipt.Details)
            {
                var serials = await _serialRepo.GetSerialsByReceiptAndVariantAsync(receipt.Id, detail.VariantId);

                detailDtos.Add(new ImportReceiptDetailDto
                {
                    Id = detail.Id,
                    VariantId = detail.VariantId,
                    VariantName = detail.Variant?.VariantName ?? string.Empty,
                    SKU = detail.Variant?.SKU ?? string.Empty,
                    Quantity = detail.Quantity,
                    ImportPrice = detail.ImportPrice,
                    SubTotal = detail.Quantity * detail.ImportPrice,
                    SerialNumbers = serials
                });
            }

            var dto = new ImportReceiptDto
            {
                Id = receipt.Id,
                ReceiptCode = receipt.ReceiptCode,
                SupplierId = receipt.SupplierId,
                SupplierName = receipt.Supplier?.Name ?? string.Empty,
                EmployeeName = "Hệ thống", // Tạm hardcode
                ImportDate = receipt.ImportDate,
                TotalAmount = receipt.TotalAmount,
                Note = receipt.Note,
                Details = detailDtos
            };

            return ApiResult<ImportReceiptDto>.Ok(dto);
        }

        // ========================================================
        // PRIVATE HELPERS
        // ========================================================

        /// <summary>
        /// Sinh mã phiếu nhập tự động: PN-yyyyMMdd-NNN
        /// VD: PN-20260214-001, PN-20260214-002
        /// </summary>
        private async Task<string> GenerateReceiptCodeAsync()
        {
            var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"PN-{dateStr}-";

            var lastCode = await _receiptRepo.GetLastReceiptCodeByDateAsync(prefix);

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                // Lấy phần số cuối: "PN-20260214-003" -> "003" -> 3 -> +1 = 4
                var lastPart = lastCode.Substring(prefix.Length);
                if (int.TryParse(lastPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D3}";
        }
    }
}
