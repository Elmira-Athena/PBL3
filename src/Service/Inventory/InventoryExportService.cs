using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Exceptions;
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
            // Kiểm tra NGOÀI transaction bằng projection AsNoTracking: trả lỗi sớm mà không phải
            // mở transaction, và lấy OrderCode cho log. Cố ý KHÔNG nạp entity tracked ở đây —
            // xem giải thích tại khối ExecuteInTransactionAsync bên dưới.
            var precheck = await _orderRepo.GetQueryable()
                .AsNoTracking()
                .Where(o => o.Id == request.OrderId)
                .Select(o => new { o.Status, o.OrderCode })
                .FirstOrDefaultAsync();

            if (precheck == null)
            {
                return ApiResult<bool>.Fail("Đơn hàng không tồn tại.");
            }

            if (precheck.Status != (byte)OrderStatus.Confirmed)
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
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `order` (kèm OrderDetails/OrderSerials) và `dbSerials` đều nạp LẠI bên
                //     trong delegate; (2) `SoldDate = DateTime.UtcNow` tính bên trong;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate.
                //
                // ⚠️ NẠP LẠI `order` BÊN TRONG LÀ BẮT BUỘC, VÌ HAI LÝ DO ĐỘC LẬP:
                //
                //   a) `order.Status = Exported` — nếu giữ entity của lần thử trước thì EF đã
                //      đánh dấu nó Unchanged với snapshot = Exported, nên lần thử này gán lại
                //      đúng giá trị đó sẽ KHÔNG sinh UPDATE nào, trong khi hàng dữ liệu vừa bị
                //      rollback về Confirmed. Đơn hàng "xuất kho thành công" mà vẫn Confirmed.
                //
                //   b) `orderDetail.OrderSerials.Add(...)` — GetByIdWithDetailsTrackedAsync CÓ
                //      Include(OrderSerials), nên collection giữ luôn các bản ghi Add của lần
                //      thử trước. Chạy lại sẽ Add chồng lên và sinh OrderSerial TRÙNG.
                //
                // ChangeTracker.Clear() (chạy khi retrySafe: true) + nạp lại xử lý được cả hai.
                var variantIdsToSync = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Nạp LẠI bên trong delegate — mỗi lần thử đọc bản mới từ DB.
                    var order = await _orderRepo.GetByIdWithDetailsTrackedAsync(request.OrderId);

                    // Chốt chống race. NÉM chứ không return, để transaction rollback.
                    if (order is null || order.Status != (byte)OrderStatus.Confirmed)
                        throw new ConcurrentModificationException(
                            "Đơn hàng vừa được thay đổi bởi thao tác khác. Vui lòng tải lại trang.");

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
                            throw new BusinessRuleException($"Chi tiết đơn hàng {detailReq.OrderDetailId} không thuộc về đơn hàng này.");
                        }

                        // 2. Kiểm tra Số lượng (Quantity Match)
                        // LƯU Ý NGHIỆP VỤ: Đảm bảo số lượng Serial quét thực tế khớp chính xác tuyệt đối với số lượng đặt hàng
                        if (detailReq.SerialNumbers.Count != orderDetail.Quantity)
                        {
                            throw new BusinessRuleException($"Chưa quét đúng số lượng cho sản phẩm {orderDetail.Variant?.VariantName ?? orderDetail.VariantId.ToString()}. " +
                                                $"Yêu cầu: {orderDetail.Quantity}, Đã quét: {detailReq.SerialNumbers.Count}");
                        }

                        // 3. Bảo vệ chéo Mismatched Variant & Trạng thái Available
                        foreach (var serialNo in detailReq.SerialNumbers)
                        {
                            if (!dbSerialsMap.TryGetValue(serialNo, out var productSerial))
                            {
                                throw new BusinessRuleException($"Mã Serial '{serialNo}' không tồn tại trong hệ thống.");
                            }

                            // Đảm bảo thiết bị chưa bị xuất bán hoặc bị hỏng hóc từ trước
                            if (productSerial.Status != (byte)SerialStatus.Available)
                            {
                                throw new BusinessRuleException($"Mã Serial '{serialNo}' không ở trạng thái trong kho (Available). Trạng thái hiện tại: {productSerial.Status}.");
                            }

                            // LƯU Ý NGHIỆP VỤ (Mismatched Variant Protection):
                            // Ràng buộc cực kỳ quan trọng ngăn chặn nhân viên đóng nhầm mã hàng/phiên bản màu sắc/dung lượng khác đơn đặt
                            if (productSerial.VariantId != orderDetail.VariantId)
                            {
                                throw new BusinessRuleException($"Mã Serial '{serialNo}' (thuộc sản phẩm {productSerial.Variant?.VariantName ?? productSerial.VariantId.ToString()}) " +
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
                }, retrySafe: true);

                // 7. Đồng bộ số lượng tồn kho ảo thực tế (StockQuantity) bên ngoài transaction
                if (variantIdsToSync.Any())
                {
                    await _inventorySyncService.SyncStockBatchAsync(variantIdsToSync);
                }

                _logger.LogInformation("Xuất kho thành công cho đơn hàng {OrderId} ({OrderCode})", request.OrderId, precheck.OrderCode);

                return ApiResult<bool>.Ok(true, "Xuất kho thành công. Đơn hàng chuyển sang trạng thái Đã xuất kho.");
            }
            catch (ConcurrentModificationException ex)
            {
                _logger.LogWarning(ex, "Xung đột đồng thời khi xuất kho đơn hàng {OrderId}", request.OrderId);
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (BusinessRuleException ex)
            {
                // 5 chốt nghiệp vụ trong delegate (serial không tồn tại / không ở trạng thái
                // Available / không thuộc đơn / chưa quét đủ số lượng) ném kiểu này. Thông báo
                // của chúng ĐÃ soạn cho người dùng và phải đi ra NGUYÊN VĂN — nhân viên kho cần
                // biết CHÍNH XÁC serial nào sai để quét lại. Nuốt thành câu chung là hồi quy UX
                // nặng hơn lỗi ban đầu.
                _logger.LogInformation(
                    "Chặn xuất kho đơn {OrderId} vì luật nghiệp vụ: {Reason}", request.OrderId, ex.Message);
                return ApiResult<bool>.Fail(ex.Message);
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
                // ROLLBACK TRANSACTION: Reset lại toàn bộ trạng thái nếu xảy ra bất kỳ lỗi quét mã nào.
                // KHÔNG nối ex.Message vào thông báo: tới đây ex là lỗi HẠ TẦNG (EF Core / SQL
                // Server), nội dung tiếng Anh và lộ nội tạng ORM. Cùng lỗi đã sửa ở mục 🅴.
                _logger.LogError(ex, "Lỗi hệ thống khi xuất kho cho đơn hàng {OrderId}", request.OrderId);
                return ApiResult<bool>.Fail(
                    "Không thể hoàn tất xuất kho do lỗi hệ thống. Vui lòng thử lại; "
                    + "nếu vẫn không được, xin liên hệ bộ phận kỹ thuật.");
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
