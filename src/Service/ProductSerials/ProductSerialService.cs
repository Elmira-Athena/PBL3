using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.Interfaces;
using PBL3.Service.Inventory;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.Enums;

namespace PBL3.Service.ProductSerials
{
    public class ProductSerialService : IProductSerialService
    {
        private readonly IProductSerialRepository _productSerialRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IInventorySyncService _inventorySyncService;
        private readonly ILogger<ProductSerialService> _logger;

        public ProductSerialService(
            IProductSerialRepository productSerialRepository,
            IOrderRepository orderRepository,
            IInventorySyncService inventorySyncService,
            ILogger<ProductSerialService> logger)
        {
            _productSerialRepository = productSerialRepository;
            _orderRepository = orderRepository;
            _inventorySyncService = inventorySyncService;
            _logger = logger;
        }

        public async Task<ApiResult<bool>> CheckExistAsync(string serialNumber, int variantId)
        {
            var exists = await _productSerialRepository.ExistsAsync(serialNumber, variantId);
            return new ApiResult<bool>
            {
                Success = true,
                Message = exists
                    ? "Mã Serial đã tồn tại trong hệ thống."
                    : "Mã Serial hợp lệ, chưa có trong hệ thống.",
                Data = exists
            };
        }

        public async Task<ApiResult<PagedResult<ProductSerialListDto>>> GetPagedListAsync(ProductSerialFilterRequest filter)
        {
            var (items, totalCount) = await _productSerialRepository.GetPagedListAsync(
                filter.Keyword, filter.ProductId, filter.VariantId,
                filter.Status, filter.FromDate, filter.ToDate,
                filter.PageNumber, filter.PageSize, filter.SortBy, filter.SortDescending);

            var dtos = items.Select(s => new ProductSerialListDto
            {
                Id = s.Id,
                SerialNumber = s.SerialNumber,
                VariantId = s.VariantId,
                VariantName = s.Variant.VariantName,
                SKU = s.Variant.SKU,
                ProductId = s.Variant.ProductId,
                ProductName = s.Variant.Product.Name,
                ImportReceiptId = s.ImportReceiptId,
                ReceiptCode = s.ImportReceipt.ReceiptCode,
                Status = s.Status,
                StatusLabel = GetStatusLabel(s.Status),
                OrderId = s.OrderId,
                CreatedDate = s.CreatedDate,
                SoldDate = s.SoldDate
            }).ToList();

            var result = new PagedResult<ProductSerialListDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };

            return ApiResult<PagedResult<ProductSerialListDto>>.Ok(result);
        }

        public async Task<ApiResult<ProductSerialDetailDto>> GetByIdAsync(int id)
        {
            var serial = await _productSerialRepository.GetByIdWithDetailsAsync(id);
            if (serial == null)
                return ApiResult<ProductSerialDetailDto>.Fail("Không tìm thấy Serial yêu cầu.");

            var dto = new ProductSerialDetailDto
            {
                Id = serial.Id,
                SerialNumber = serial.SerialNumber,
                Status = serial.Status,
                StatusLabel = GetStatusLabel(serial.Status),
                CreatedDate = serial.CreatedDate,
                SoldDate = serial.SoldDate,
                VariantId = serial.VariantId,
                VariantName = serial.Variant.VariantName,
                SKU = serial.Variant.SKU,
                WarrantyMonth = serial.Variant.WarrantyMonth,
                Price = serial.Variant.Price,
                ProductId = serial.Variant.ProductId,
                ProductName = serial.Variant.Product.Name,
                ImportReceiptId = serial.ImportReceiptId,
                ReceiptCode = serial.ImportReceipt.ReceiptCode,
                ImportDate = serial.ImportReceipt.ImportDate,
                SupplierName = serial.ImportReceipt.Supplier.Name,
                OrderId = serial.OrderId
            };

            if (serial.OrderId.HasValue)
            {
                var order = await _orderRepository.GetByIdAsync(serial.OrderId.Value);
                if (order != null)
                {
                    dto.OrderCode = order.OrderCode;
                    dto.OrderDate = order.OrderDate;
                    dto.OrderStatus = order.Status;
                }
            }

            return ApiResult<ProductSerialDetailDto>.Ok(dto);
        }

        public async Task<ApiResult<ProductSerialStatisticsDto>> GetStatisticsAsync(int? productId, int? variantId)
        {
            var counts = await _productSerialRepository.GetStatusCountsAsync(productId, variantId);

            var dto = new ProductSerialStatisticsDto
            {
                AvailableCount = counts.GetValueOrDefault((byte)SerialStatus.Available, 0),
                ReservedCount = counts.GetValueOrDefault((byte)SerialStatus.Reserved, 0),
                SoldCount = counts.GetValueOrDefault((byte)SerialStatus.Sold, 0),
                DefectiveCount = counts.GetValueOrDefault((byte)SerialStatus.Defective, 0),
                ReturnedCount = counts.GetValueOrDefault((byte)SerialStatus.Returned, 0),
                LostCount = counts.GetValueOrDefault((byte)SerialStatus.Lost, 0),
                ProductId = productId,
                VariantId = variantId
            };
            dto.TotalCount = counts.Values.Sum();

            return ApiResult<ProductSerialStatisticsDto>.Ok(dto);
        }

        public async Task<ApiResult<bool>> UpdateStatusAsync(int id, UpdateSerialStatusRequest request)
        {
            var serial = await _productSerialRepository.GetByIdWithTrackingAsync(id);
            if (serial == null)
                return ApiResult<bool>.Fail("Không tìm thấy Serial yêu cầu.");

            var currentStatus = (SerialStatus)serial.Status;
            var newStatus = (SerialStatus)request.NewStatus;

            var validationError = ValidateTransition(currentStatus, newStatus);
            if (validationError != null)
                return ApiResult<bool>.Fail(validationError);

            var variantId = serial.VariantId;

            try
            {
                serial.Status = request.NewStatus;
                await _productSerialRepository.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(request.Note))
                    _logger.LogInformation("Serial {SerialNumber} chuyển trạng thái {From} → {To}. Lý do: {Note}",
                        serial.SerialNumber, currentStatus, newStatus, request.Note);

                await _inventorySyncService.SyncStockAsync(variantId);

                return ApiResult<bool>.Ok(true, "Cập nhật trạng thái Serial thành công.");
            }
            // PHẢI đứng trước catch (Exception), nếu không nó nuốt xung đột đồng thời thành
            // một câu chung. throw; để ConflictExceptionHandler ánh xạ sang 409.
            // Giải thích đầy đủ: InventoryCheckService.ApproveAsync.
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái Serial {Id}", id);
                return ApiResult<bool>.Fail("Đã xảy ra lỗi khi cập nhật trạng thái Serial.");
            }
        }

        private static string? ValidateTransition(SerialStatus current, SerialStatus next)
        {
            if (current == SerialStatus.Sold)
                return "Không thể thay đổi trạng thái Serial đã bán.";

            // NOTE: The 1-for-1 swap path in ServiceTicketService.Perform1For1SwapAsync
            // bypasses this validator entirely, mutating ProductSerial.Status directly within
            // a transaction. This is intentional to enforce atomicity across multiple entities
            // (old serial → Returned, new serial → Sold, Warranty rows, OrderSerial.SerialId, etc.).

            var allowed = (current, next) switch
            {
                (SerialStatus.Available, SerialStatus.Defective) => true,
                (SerialStatus.Reserved,  SerialStatus.Defective) => true,
                (SerialStatus.Defective, SerialStatus.Returned)  => true,
                (SerialStatus.Defective, SerialStatus.Available) => true,
                (SerialStatus.Returned,  SerialStatus.Available) => true,
                (SerialStatus.Lost,      SerialStatus.Returned)  => true,
                _ => false
            };

            return allowed
                ? null
                : $"Không thể chuyển trạng thái từ '{GetStatusLabel((byte)current)}' sang '{GetStatusLabel((byte)next)}'.";
        }

        private static string GetStatusLabel(byte status) => status switch
        {
            0 => "Trong kho",
            1 => "Đã đặt hàng",
            2 => "Đã bán",
            3 => "Hàng lỗi",
            4 => "Đã trả lại",
            5 => "Thất thoát",
            _ => "Không xác định"
        };
    }
}
