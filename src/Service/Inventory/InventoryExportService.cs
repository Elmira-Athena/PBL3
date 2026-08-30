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

        /// <summary>
        /// NGHIỆP VỤ: Thực thi quy trình xuất kho vật lý cho Đơn hàng.
        /// Chuyển giao các sản phẩm dạng số Serial từ trạng thái lưu kho (Available) sang trạng thái đã bán (Sold) và gắn kết với đơn hàng.
        /// Toàn bộ tiến trình thực thi dưới một Transaction dữ liệu chặt chẽ:
        /// 1. Xác thực trạng thái đơn hàng (phải là Confirmed).
        /// 2. Đối chiếu số lượng Serial quét thực tế vs số lượng mua trong hóa đơn.
        /// 3. Bảo vệ chéo: Kiểm tra Serial tồn tại, ở trạng thái trong kho (Available), và trùng khớp VariantId (tránh giao nhầm sản phẩm/phiên bản).
        /// 4. Ghi nhận xuất kho: chuyển trạng thái Serial sang Sold, cập nhật ngày bán, tạo liên kết OrderSerial.
        /// 5. Cập nhật trạng thái đơn hàng sang Exported.
        /// 6. Đồng bộ số lượng tồn kho thực tế của các sản phẩm liên quan sau khi kết thúc transaction thành công.
        /// </summary>
        public async Task<ApiResult<bool>> ExportOrderAsync(ExportOrderRequest request)
        {
            // 1. Xác thực Đơn hàng (Order Validation)
            // LƯU Ý NGHIỆP VỤ: Chỉ xuất kho đối với đơn hàng ở trạng thái Confirmed (Đã xác nhận thanh toán/chốt đơn)
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

            // BẮT ĐẦU TRANSACTION: Bảo toàn tính toàn vẹn dữ liệu xuất kho hàng loạt
            try
            {
                // ⚠️ CHƯA RÀ RETRY (đợt 1 mục 4.1) — mặc định retrySafe = false, nên lỗi transient
                // ở đây KHÔNG được chạy lại mà ném lỗi rõ ràng. Hành vi người dùng thấy giống hệt
                // trước khi bật EnableRetryOnFailure. Lý do chưa bật được:
                // `order` nạp TRACKED ở ngoài (GetByIdWithDetailsTrackedAsync) rồi đặt
                // order.Status = Exported bên trong. Thêm nữa: orderDetail.OrderSerials.Add(...)
                // chạy lại sẽ sinh bản ghi OrderSerial TRÙNG.
                var variantIdsToSync = await _unitOfWork.ExecuteInTransactionAsync(async () =>
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
                        // LƯU Ý NGHIỆP VỤ: Đảm bảo số lượng Serial quét thực tế khớp chính xác tuyệt đối với số lượng đặt hàng
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

                            // Đảm bảo thiết bị chưa bị xuất bán hoặc bị hỏng hóc từ trước
                            if (productSerial.Status != (byte)SerialStatus.Available)
                            {
                                throw new Exception($"Mã Serial '{serialNo}' không ở trạng thái trong kho (Available). Trạng thái hiện tại: {productSerial.Status}.");
                            }

                            // LƯU Ý NGHIỆP VỤ (Mismatched Variant Protection):
                            // Ràng buộc cực kỳ quan trọng ngăn chặn nhân viên đóng nhầm mã hàng/phiên bản màu sắc/dung lượng khác đơn đặt
                            if (productSerial.VariantId != orderDetail.VariantId)
                            {
                                throw new Exception($"Mã Serial '{serialNo}' (thuộc sản phẩm {productSerial.Variant?.VariantName ?? productSerial.VariantId.ToString()}) " +
                                                    $"KHÔNG KHỚP với sản phẩm yêu cầu trong đơn hàng ({orderDetail.Variant?.VariantName ?? orderDetail.VariantId.ToString()}).");
                            }

                            // 4. Cập nhật Dữ liệu (Ghi nhận Xuất kho)
                            // Chuyển trạng thái vật lý của Serial sang Đã bán (Sold) và ghi nhận ngày bán thực tế
                            productSerial.Status = (byte)SerialStatus.Sold;
                            productSerial.SoldDate = DateTime.UtcNow;
                            productSerial.OrderId = order.Id;

                            // Insert OrderSerial link: Tạo bản ghi liên kết lịch sử bán của Serial phục vụ chẩn đoán bảo hành và truy vết giá vốn
                            orderDetail.OrderSerials.Add(new OrderSerial
                            {
                                OrderDetailId = orderDetail.Id,
                                SerialId = productSerial.Id
                            });

                            variantIdsToSync.Add(productSerial.VariantId);
                        }
                    }

                    // 5. Cập nhật Trạng thái Đơn hàng sang Đã xuất kho (Exported)
                    order.Status = (byte)OrderStatus.Exported;

                    // 6. Commit Transaction: Xác nhận mọi thay đổi vật lý thành công
                    await _unitOfWork.SaveChangesAsync();

                    return variantIdsToSync;
                });

                // 7. Đồng bộ số lượng tồn kho ảo thực tế (StockQuantity) bên ngoài transaction
                if (variantIdsToSync.Any())
                {
                    await _inventorySyncService.SyncStockBatchAsync(variantIdsToSync);
                }

                _logger.LogInformation("Xuất kho thành công cho đơn hàng {OrderId} ({OrderCode})", order.Id, order.OrderCode);

                return ApiResult<bool>.Ok(true, "Xuất kho thành công. Đơn hàng chuyển sang trạng thái Đã xuất kho.");
            }
            catch (Exception ex)
            {
                // ROLLBACK TRANSACTION: Reset lại toàn bộ trạng thái nếu xảy ra bất kỳ lỗi quét mã nào
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
