using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Application.ImportReceipts
{
    /// <summary>
    /// Dịch vụ quản lý Nhập kho (Import Receipt Service).
    /// Đảm nhận nghiệp vụ mua hàng và nhập kho vật lý từ nhà cung cấp, tự động sinh mã số định danh hàng loạt (Product Serial) 
    /// ở trạng thái khả dụng để sẵn sàng bán, đồng thời thực hiện đồng bộ hóa số lượng tồn kho sổ sách tức thời.
    /// </summary>
    public class ImportReceiptService : IImportReceiptService
    {
        private readonly IImportReceiptRepository _receiptRepo;
        private readonly IProductSerialRepository _serialRepo;
        private readonly ISupplierRepository _supplierRepo;
        private readonly IProductRepository _productRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IInventorySyncService _inventorySyncService;
        private readonly ILogger<ImportReceiptService> _logger;

        public ImportReceiptService(
            IImportReceiptRepository receiptRepo,
            IProductSerialRepository serialRepo,
            ISupplierRepository supplierRepo,
            IProductRepository productRepo,
            IUnitOfWork unitOfWork,
            IInventorySyncService inventorySyncService,
            ILogger<ImportReceiptService> logger)
        {
            _receiptRepo = receiptRepo;
            _serialRepo = serialRepo;
            _supplierRepo = supplierRepo;
            _productRepo = productRepo;
            _unitOfWork = unitOfWork;
            _inventorySyncService = inventorySyncService;
            _logger = logger;
        }

        // ========================================================
        // CREATE — Tạo phiếu nhập kho (Transaction Required)
        // ========================================================
        /// <summary>
        /// Nghiệp vụ Tạo phiếu nhập kho: Ghi nhận thông tin nhập kho từ nhà cung cấp, kiểm tra trùng lặp mã serial,
        /// đăng ký số serial vật lý mới vào kho ở trạng thái Available, và tự động đồng bộ số tồn kho thực tế.
        /// </summary>
        public async Task<ApiResult<ImportReceiptDto>> CreateAsync(CreateImportReceiptRequest request)
        {
            // -------------------------------------------------------
            // Bước 1: Pre-checks (Validate nghiệp vụ trước khi mở Transaction)
            // -------------------------------------------------------

            // 1a. Kiểm tra Nhà cung cấp có thực sự tồn tại và hợp lệ không
            var supplier = await _supplierRepo.GetByIdAsync(request.SupplierId);
            if (supplier == null)
                return ApiResult<ImportReceiptDto>.Fail("Nhà cung cấp không tồn tại hoặc đã bị xoá.");

            // 1b. Thu thập tất cả mã Serial được khai báo nhập kho trong phiếu yêu cầu
            var allSerials = request.Details
                .SelectMany(d => d.SerialNumbers)
                .Select(s => s.Trim())
                .ToList();

            // 1c. NGHIỆP VỤ: Kiểm tra trùng lặp serial nội bộ ngay trong chính phiếu nhập yêu cầu.
            // Tránh trường hợp người dùng vô tình quét/nhập một mã serial hai lần trên cùng một phiếu.
            var duplicateInternal = allSerials
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateInternal.Any())
                return ApiResult<ImportReceiptDto>.Fail(
                    $"Mã Serial bị trùng lặp trong phiếu nhập: {string.Join(", ", duplicateInternal)}");

            // 1d. NGHIỆP VỤ: Kiểm tra mã Serial đã từng tồn tại trên hệ thống trước đây chưa.
            // Vì Serial Number vật lý là duy nhất trên toàn cầu đối với mỗi sản phẩm, việc trùng lặp là không thể chấp nhận.
            var existingSerials = await _serialRepo.GetExistingSerialsAsync(allSerials);
            if (existingSerials.Any())
                return ApiResult<ImportReceiptDto>.Fail(
                    $"Mã Serial đã tồn tại trong hệ thống: {string.Join(", ", existingSerials)}");

            // 1e. Kiểm tra các VariantId (Biến thể sản phẩm) có thực sự tồn tại trong danh mục không
            var variantIds = request.Details.Select(d => d.VariantId).Distinct().ToList();
            var existingVariants = await _productRepo.GetExistingVariantIdsAsync(variantIds);

            var missingVariants = variantIds.Except(existingVariants).ToList();
            if (missingVariants.Any())
                return ApiResult<ImportReceiptDto>.Fail(
                    $"Biến thể sản phẩm không tồn tại: {string.Join(", ", missingVariants)}");

            // -------------------------------------------------------
            // Bước 2: Mở Transaction (qua IUnitOfWork) để bảo vệ tính nhất quán dữ liệu
            // -------------------------------------------------------
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // ---------------------------------------------------
                // Bước 3: Tạo Header phiếu nhập kho (ImportReceipt)
                // ---------------------------------------------------
                var receiptCode = await GenerateReceiptCodeAsync();

                // Tính tổng tiền phiếu nhập = Tổng của (Số lượng nhập * Đơn giá nhập từng biến thể)
                var totalAmount = request.Details
                    .Sum(d => d.Quantity * d.ImportPrice);

                var receipt = new ImportReceipt
                {
                    ReceiptCode = receiptCode,
                    SupplierId = request.SupplierId,
                    EmployeeId = Guid.Empty, // Tạm hardcode — chưa tích hợp hoàn thiện mô-đun Auth phân quyền nhân viên
                    ImportDate = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    Note = request.Note?.Trim(),
                    IsDeleted = false
                };

                await _receiptRepo.AddAsync(receipt);
                await _unitOfWork.SaveChangesAsync(); // Lưu tạm để DB phát sinh ReceiptId phục vụ cho các bản ghi chi tiết bên dưới

                // ---------------------------------------------------
                // Bước 4: Tạo Details (Chi tiết hàng nhập) & Đăng ký danh sách Serials vật lý tương ứng
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

                    // Tạo bản ghi ProductSerial tương ứng với số lượng (Quantity) và danh sách mã quét được
                    foreach (var serialNumber in detailReq.SerialNumbers)
                    {
                        allNewSerials.Add(new ProductSerial
                        {
                            SerialNumber = serialNumber.Trim(),
                            VariantId = detailReq.VariantId,
                            ImportReceiptId = receipt.Id,
                            Status = 0, // Trạng thái ban đầu: 0 = Available (Sẵn sàng bán ra)
                            CreatedDate = DateTime.UtcNow
                        });
                    }
                }

                // Thực hiện chèn hàng loạt (Bulk Insert) danh sách Serial mới để tối ưu hiệu năng cơ sở dữ liệu
                await _serialRepo.AddRangeAsync(allNewSerials);

                // Lưu toàn bộ chi tiết và mã serial mới đăng ký vào database
                await _unitOfWork.SaveChangesAsync();

                // ---------------------------------------------------
                // Bước 5: Cập nhật đồng bộ tồn kho (StockQuantity)
                // ---------------------------------------------------
                // NGHIỆP VỤ: Kích hoạt đồng bộ hóa số lượng tồn kho vật lý khả dụng dựa trên số lượng Serial thực tế vừa nhập.
                var importedVariantIds = request.Details.Select(d => d.VariantId).Distinct().ToList();
                await _inventorySyncService.SyncStockBatchAsync(importedVariantIds);

                // ---------------------------------------------------
                // Bước 6: Commit Transaction để hoàn tất giao dịch nhập kho
                // ---------------------------------------------------
                await _unitOfWork.CommitAsync();

                _logger.LogInformation(
                    "Tạo phiếu nhập kho thành công: {ReceiptCode}, NCC: {SupplierName}, Tổng: {TotalAmount}",
                    receiptCode, supplier.Name, totalAmount);

                // Bản đồ hóa sang DTO trả về cho giao diện
                var resultDto = new ImportReceiptDto
                {
                    Id = receipt.Id,
                    ReceiptCode = receipt.ReceiptCode,
                    SupplierId = receipt.SupplierId,
                    SupplierName = supplier.Name,
                    EmployeeName = "Hệ thống", // Tạm thời hiển thị Hệ thống do chưa có Auth
                    ImportDate = receipt.ImportDate,
                    TotalAmount = receipt.TotalAmount,
                    Note = receipt.Note
                };

                return ApiResult<ImportReceiptDto>.Ok(resultDto, "Tạo phiếu nhập kho thành công.");
            }
            catch (Exception ex)
            {
                // NGHIỆP VỤ: Nếu xảy ra bất kỳ lỗi gì trong quá trình lưu hoặc đồng bộ tồn kho,
                // lập tức Rollback toàn bộ Transaction để tránh tình trạng rác dữ liệu hoặc lệch tồn kho.
                await _unitOfWork.RollbackAsync();

                _logger.LogError(ex, "Lỗi khi tạo phiếu nhập kho.");

                return ApiResult<ImportReceiptDto>.Fail("Đã xảy ra lỗi khi tạo phiếu nhập kho. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // GET LIST — Danh sách phiếu nhập (Lịch sử nhập kho)
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Truy vấn lịch sử danh sách phiếu nhập kho có phân trang và bộ lọc linh hoạt.
        /// Hỗ trợ ban giám đốc và thủ kho tìm kiếm phiếu theo từ khóa (mã phiếu, ghi chú), lọc theo khoảng thời gian
        /// (Từ ngày - Đến ngày) để kiểm soát lượng hàng về theo tuần/tháng/quý, và lọc theo Nhà cung cấp cụ thể.
        /// Kết quả trả về được sắp xếp động để dễ dàng theo dõi các lô hàng mới nhất hoặc có giá trị cao nhất.
        /// </summary>
        public async Task<ApiResult<PagedResult<ImportReceiptDto>>> GetPagedListAsync(ImportReceiptFilterRequest filter)
        {
            // Thực hiện truy vấn cơ sở dữ liệu có phân trang và áp dụng các tiêu chí lọc
            var (items, totalCount) = await _receiptRepo.GetPagedListAsync(
                filter.Keyword,
                filter.FromDate,
                filter.ToDate,
                filter.SupplierId,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending);

            // Bản đồ hóa danh sách thực thể ImportReceipt sang cấu trúc DTO hiển thị trên giao diện quản trị
            var dtos = items.Select(r => new ImportReceiptDto
            {
                Id = r.Id,
                ReceiptCode = r.ReceiptCode,
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier?.Name ?? string.Empty,
                EmployeeName = "Hệ thống", // Hiển thị tạm do mô-đun Auth phân quyền nhân viên chưa tích hợp hoàn chỉnh
                ImportDate = r.ImportDate,
                TotalAmount = r.TotalAmount,
                Note = r.Note
            }).ToList();

            // Đóng gói kết quả phân trang để cung cấp cho Grid UI ở Front-end điều khiển thanh phân trang
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
        /// <summary>
        /// NGHIỆP VỤ: Truy vấn thông tin chi tiết của một phiếu nhập kho cụ thể bằng ID.
        /// Không chỉ lấy thông tin Header (nhà cung cấp, ngày nhập, tổng tiền), phương thức này còn đi sâu
        /// truy vấn danh sách toàn bộ các mã số định danh vật lý (Product Serial Numbers) tương ứng với từng
        /// dòng chi tiết mặt hàng (Variant) đã thực tế nhập vào trong lô hàng đó.
        /// Đây là cơ sở pháp lý và đối chiếu kiểm kê cực kỳ quan trọng khi có tranh chấp về hàng lỗi với nhà cung cấp.
        /// </summary>
        public async Task<ApiResult<ImportReceiptDto>> GetByIdAsync(int id)
        {
            // Lấy thông tin Header phiếu nhập cùng các dòng chi tiết liên quan
            var receipt = await _receiptRepo.GetByIdWithDetailsAsync(id);

            if (receipt == null)
                return ApiResult<ImportReceiptDto>.Fail("Không tìm thấy phiếu nhập kho yêu cầu.");

            var detailDtos = new List<ImportReceiptDetailDto>();

            // Duyệt qua từng dòng mặt hàng trong phiếu để lấy danh sách số serial đã đăng ký
            foreach (var detail in receipt.Details)
            {
                // Truy vấn từ bảng ProductSerials các mã serial được liên kết với phiếu nhập và biến thể sản phẩm này
                var serials = await _serialRepo.GetSerialsByReceiptAndVariantAsync(receipt.Id, detail.VariantId);

                detailDtos.Add(new ImportReceiptDetailDto
                {
                    Id = detail.Id,
                    VariantId = detail.VariantId,
                    VariantName = detail.Variant?.VariantName ?? string.Empty,
                    SKU = detail.Variant?.SKU ?? string.Empty,
                    Quantity = detail.Quantity,
                    ImportPrice = detail.ImportPrice,
                    SubTotal = detail.Quantity * detail.ImportPrice, // Tổng giá trị nhập của dòng mặt hàng
                    SerialNumbers = serials // Danh sách serial vật lý phục vụ đối chiếu chi tiết
                });
            }

            // Đóng gói DTO hoàn chỉnh chứa thông tin Header và danh sách dòng chi tiết (kèm serial)
            var dto = new ImportReceiptDto
            {
                Id = receipt.Id,
                ReceiptCode = receipt.ReceiptCode,
                SupplierId = receipt.SupplierId,
                SupplierName = receipt.Supplier?.Name ?? string.Empty,
                EmployeeName = "Hệ thống",
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
        /// NGHIỆP VỤ: Sinh mã phiếu nhập tự động theo định dạng chuẩn: PN-yyyyMMdd-NNN (PN = Phiếu Nhập).
        /// Đảm bảo tính liên tục của số thứ tự NNN (001, 002,...) trong cùng một ngày.
        /// </summary>
        private async Task<string> GenerateReceiptCodeAsync()
        {
            var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"PN-{dateStr}-";

            // Tìm mã phiếu nhập cuối cùng được tạo ra trong ngày hôm nay
            var lastCode = await _receiptRepo.GetLastReceiptCodeByDateAsync(prefix);

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                // Tách lấy phần số thứ tự cuối cùng: ví dụ "PN-20260521-003" -> tách lấy "003" -> chuyển thành số 3 -> cộng thêm 1 = 4
                var lastPart = lastCode.Substring(prefix.Length);
                if (int.TryParse(lastPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            // Trả về mã định danh chuẩn hóa, đảm bảo tối thiểu có 3 chữ số cho số thứ tự (ví dụ: PN-20260521-004)
            return $"{prefix}{nextNumber:D3}";
        }
    }
}
