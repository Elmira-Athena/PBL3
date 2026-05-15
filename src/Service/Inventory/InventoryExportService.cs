using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.Enums;

namespace PBL3.Service.Inventory
{
    public class InventoryExportService : IInventoryExportService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderRepository _orderRepo;
        private readonly IProductSerialRepository _serialRepo;
        private readonly IInventorySyncService _inventorySyncService;
        private readonly ILogger<InventoryExportService> _logger;

        public InventoryExportService(
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepo,
            IProductSerialRepository serialRepo,
            IInventorySyncService inventorySyncService,
            ILogger<InventoryExportService> logger)
        {
            _unitOfWork = unitOfWork;
            _orderRepo = orderRepo;
            _serialRepo = serialRepo;
            _inventorySyncService = inventorySyncService;
            _logger = logger;
        }

        public async Task<ApiResult<bool>> ExportOrderAsync(ExportOrderRequest request)
        {
            // 1. Xác thực Đơn hàng (Order Validation)
            var order = await _orderRepo.GetByIdWithDetailsTrackedAsync(request.OrderId);
            if (order == null)
            {
                return ApiResult<bool>.Fail("Đơn hàng không tồn tại.");
            }

            if (order.Status != (byte)OrderStatus.Confirmed)
            {
                return ApiResult<bool>.Fail("Đơn hàng không ở trạng thái Đã xác nhận (Confirmed). Không thể xuất kho.");
            }

            // Gộp tất cả mã Serial từ request để lấy lên một lần (batch loading)
            var allSerialNumbers = request.Details.SelectMany(d => d.SerialNumbers).Distinct().ToList();
            if (!allSerialNumbers.Any())
            {
                return ApiResult<bool>.Fail("Không có mã Serial nào được quét.");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Load Serials WITH TRACKING so updates are tracked by DbContext
                var dbSerials = await _serialRepo.GetSerialsWithTrackingAsync(allSerialNumbers);
                var dbSerialsMap = dbSerials.ToDictionary(s => s.SerialNumber, s => s, StringComparer.OrdinalIgnoreCase);
                var variantIdsToSync = new HashSet<int>();

                foreach (var detailReq in request.Details)
                {
                    // Find corresponding OrderDetail in DB
                    var orderDetail = order.OrderDetails.FirstOrDefault(od => od.Id == detailReq.OrderDetailId);
                    if (orderDetail == null)
                    {
                        throw new Exception($"Chi tiết đơn hàng {detailReq.OrderDetailId} không thuộc về đơn hàng này.");
                    }

                    // 2. Kiểm tra Số lượng (Quantity Match)
                    if (detailReq.SerialNumbers.Count != orderDetail.Quantity)
                    {
                        throw new Exception($"Chưa quét đúng số lượng cho sản phẩm {orderDetail.Variant?.VariantName ?? orderDetail.VariantId.ToString()}. " +
                                            $"Yêu cầu: {orderDetail.Quantity}, Đã quét: {detailReq.SerialNumbers.Count}");
                    }

                    // 3. Bảo vệ chéo Mismatched Variant & Trạng thái Available
                    foreach (var serialNo in detailReq.SerialNumbers)
                    {
                        if (!dbSerialsMap.TryGetValue(serialNo, out var productSerial))
                        {
                            throw new Exception($"Mã Serial '{serialNo}' không tồn tại trong hệ thống.");
                        }

                        if (productSerial.Status != (byte)SerialStatus.Available)
                        {
                            throw new Exception($"Mã Serial '{serialNo}' không ở trạng thái trong kho (Available). Trạng thái hiện tại: {productSerial.Status}.");
                        }

                        if (productSerial.VariantId != orderDetail.VariantId)
                        {
                            throw new Exception($"Mã Serial '{serialNo}' (thuộc sản phẩm {productSerial.Variant?.VariantName ?? productSerial.VariantId.ToString()}) " +
                                                $"KHÔNG KHỚP với sản phẩm yêu cầu trong đơn hàng ({orderDetail.Variant?.VariantName ?? orderDetail.VariantId.ToString()}).");
                        }

                        // 4 & 5. Cập nhật Dữ liệu (Ghi nhận Xuất kho)
                        // Update ProductSerial status
                        productSerial.Status = (byte)SerialStatus.Sold;
                        productSerial.SoldDate = DateTime.UtcNow;
                        productSerial.OrderId = order.Id;

                        // Insert OrderSerial link
                        orderDetail.OrderSerials.Add(new OrderSerial
                        {
                            OrderDetailId = orderDetail.Id,
                            SerialId = productSerial.Id
                        });

                        variantIdsToSync.Add(productSerial.VariantId);
                    }
                }

                // 5. Cập nhật Trạng thái Đơn hàng
                order.Status = (byte)OrderStatus.Exported;

                // 6. Commit Transaction
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // 7. Đồng bộ StockQuantity (Out of transaction, but necessary)
                if (variantIdsToSync.Any())
                {
                    await _inventorySyncService.SyncStockBatchAsync(variantIdsToSync);
                }

                _logger.LogInformation("Xuất kho thành công cho đơn hàng {OrderId} ({OrderCode})", order.Id, order.OrderCode);

                return ApiResult<bool>.Ok(true, "Xuất kho thành công. Đơn hàng chuyển sang trạng thái Đã xuất kho.");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xuất kho cho đơn hàng {OrderId}", order.Id);
                return ApiResult<bool>.Fail($"Lỗi khi xuất kho: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId)
        {
            var serial = await _serialRepo.GetBySerialNumberAsync(serialNo);

            if (serial == null)
                return ApiResult<bool>.Fail($"Mã Serial '{serialNo}' không tồn tại trong hệ thống.");

            if (serial.VariantId != variantId)
                return ApiResult<bool>.Fail($"Mã Serial '{serialNo}' không thuộc sản phẩm yêu cầu.");

            if (serial.Status != (byte)SerialStatus.Available)
                return ApiResult<bool>.Fail($"Mã Serial '{serialNo}' không ở trạng thái Available (có thể đã bán hoặc hỏng).");

            return ApiResult<bool>.Ok(true);
        }
    }
}
