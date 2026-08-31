using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Exceptions;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.Enums;

namespace PBL3.Service.Inventory
{
    /// <summary>
    /// Nghiệp vụ Kiểm kê kho (Inventory Audit/Stocktaking) theo cơ chế quản lý mã vật lý duy nhất (Serial Number).
    /// Quy trình gồm các bước: Tạo phiếu nháp chốt số lượng sổ sách -> Quét mã thực tế (Khớp/Thừa/Thiếu/Hàng lỗi) -> Gửi duyệt -> Phê duyệt cân bằng kho thực tế.
    /// </summary>
    public class InventoryCheckService : IInventoryCheckService
    {
        private readonly IInventoryCheckRepository _checkRepo;
        private readonly IProductSerialRepository _serialRepo;
        private readonly IProductRepository _productRepo;
        private readonly IInventorySyncService _inventorySyncService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HushStoreDbContext _context;
        private readonly ILogger<InventoryCheckService> _logger;

        private readonly IDocumentCodeGenerator _codeGenerator;


        public InventoryCheckService(
            IInventoryCheckRepository checkRepo,
            IProductSerialRepository serialRepo,
            IProductRepository productRepo,
            IInventorySyncService inventorySyncService,
            IUnitOfWork unitOfWork,
            HushStoreDbContext context,
            ILogger<InventoryCheckService> logger,
            IDocumentCodeGenerator codeGenerator)
        {
            _checkRepo = checkRepo;
            _serialRepo = serialRepo;
            _productRepo = productRepo;
            _inventorySyncService = inventorySyncService;
            _unitOfWork = unitOfWork;
            _context = context;
            _logger = logger;
            _codeGenerator = codeGenerator;
        }

        // ========================================================
        // CREATE — Tạo phiếu + chốt snapshot tồn kho sổ sách
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Tạo phiếu kiểm kê kho mới và thực hiện chốt số lượng sổ sách thời gian thực (Book Inventory Snapshot).
        /// Xác định phạm vi kiểm kê (toàn cửa hàng hoặc theo danh mục hàng hóa cụ thể sử dụng thuật toán lấy danh mục con cháu phẳng).
        /// Thực thi trong Transaction để bảo đảm tính nhất quán khi ghi nhận Snapshot chi tiết cho hàng nghìn mã Serial khả dụng (Available).
        /// Khởi tạo trạng thái quét mặc định là Chờ quét (Pending) làm mốc đối chiếu vật lý tại kho.
        /// </summary>
        public async Task<ApiResult<InventoryCheckDto>> CreateAsync(CreateInventoryCheckRequest request, Guid employeeId)
        {
            // NGHIỆP VỤ: Nếu kiểm kê theo danh mục (ScopeType = 1), bắt buộc phải có CategoryId và Category phải tồn tại.
            if (request.ScopeType == (byte)InventoryCheckScopeType.Category)
            {
                if (!request.ScopeCategoryId.HasValue)
                    return ApiResult<InventoryCheckDto>.Fail("Phải chọn danh mục khi phạm vi kiểm kê là theo danh mục.");

                var categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == request.ScopeCategoryId.Value);
                if (!categoryExists)
                    return ApiResult<InventoryCheckDto>.Fail("Danh mục kiểm kê không tồn tại.");
            }

            // NGHIỆP VỤ XÁC ĐỊNH PHẠM VI: Truy vấn toàn bộ các mã biến thể sản phẩm (ProductVariants) thuộc phạm vi kiểm kê.
            // Nếu kiểm kê theo danh mục, tự động dùng thuật toán lấy cả các danh mục con cháu phẳng (flat subcategories tree).
            // Đây là truy vấn CHỈ ĐỌC nên đặt TRƯỚC transaction: trước đây nó nằm trong transaction và
            // nhánh "không có sản phẩm" return thẳng ra ngoài — transaction bị bỏ dở, không commit không rollback.
            var variantIds = await GetVariantIdsInScopeAsync(request.ScopeType, request.ScopeCategoryId);
            if (!variantIds.Any())
            {
                return ApiResult<InventoryCheckDto>.Fail("Không có sản phẩm nào trong phạm vi kiểm kê.");
            }

            // Sử dụng Transaction để đảm bảo tính toàn vẹn dữ liệu khi ghi nhận Snapshot tồn kho số lượng lớn
            //
            // retrySafe: call-site này thoả cả ba điều kiện của hợp đồng retry
            // (xem IUnitOfWork.ExecuteInTransactionAsync):
            //   1. Không sửa entity nào nạp sẵn ở ngoài — `variantIds` chỉ là danh sách int
            //      lấy từ truy vấn chỉ đọc; delegate chỉ TẠO MỚI (InventoryCheck + Detail +
            //      DetailSerial), không cập nhật bản ghi có sẵn nào.
            //   2. Mã phiếu (checkCode) và mốc thời gian sinh BÊN TRONG delegate.
            //   3. Không có tác dụng phụ không-idempotent nào chạy trước delegate.
            try
            {
                var (check, checkCode, availableSerials) = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var now = DateTime.UtcNow;
                    var checkCode = await GenerateCheckCodeAsync();

                    // 1. Tạo bản ghi đầu phiếu kiểm kê ở trạng thái Nháp (Draft)
                    var check = new InventoryCheck
                    {
                        CheckCode = checkCode,
                        EmployeeId = employeeId,
                        CheckDate = now,
                        SnapshotAt = now,
                        Status = (byte)InventoryCheckStatus.Draft,
                        ScopeType = request.ScopeType,
                        ScopeCategoryId = request.ScopeCategoryId,
                        Note = request.Note?.Trim(),
                        IsDeleted = false
                    };

                    await _checkRepo.AddAsync(check);
                    await _unitOfWork.SaveChangesAsync();

                    // 3. CHỐT SNAPSHOT SỔ SÁCH THỜI GIAN THỰC (Book Inventory Snapshot):
                    // Lấy toàn bộ mã Serial đang có trạng thái khả dụng trong kho (Available) thuộc phạm vi trên để làm mốc đối chiếu
                    var availableSerials = await _serialRepo.GetAvailableSerialsBatchAsync(variantIds);

                    // 4. KHỞI TẠO CHI TIẾT PHIẾU (InventoryCheckDetail)
                    // Gom nhóm và đếm số lượng sổ sách của từng Variant dựa trên danh sách Serial chốt được
                    var detailMap = new Dictionary<int, InventoryCheckDetail>();
                    foreach (var variantId in variantIds)
                    {
                        var sysQty = availableSerials.Count(s => s.VariantId == variantId);
                        var detail = new InventoryCheckDetail
                        {
                            CheckId = check.Id,
                            VariantId = variantId,
                            SystemQuantity = sysQty,    // Số lượng sổ sách chốt tại thời điểm kiểm kê (System Qty)
                            ActualQuantity = 0,        // Số lượng thực tế quét được (bắt đầu bằng 0)
                            MatchedQuantity = 0,       // Số lượng khớp thực tế quét (bắt đầu bằng 0)
                            MissingQuantity = 0,       // Số lượng thiếu so với sổ sách (bắt đầu bằng 0)
                            SurplusQuantity = 0,       // Số lượng thừa (bắt đầu bằng 0)
                            DefectiveQuantity = 0      // Số lượng hàng lỗi vật lý quét được (bắt đầu bằng 0)
                        };
                        await _checkRepo.AddDetailAsync(detail);
                        detailMap[variantId] = detail;
                    }

                    await _unitOfWork.SaveChangesAsync(); // Lưu để phát sinh Detail.Id tự động phục vụ khóa ngoại ở bước sau

                    // 5. GHI NHẬN SNAPSHOT SERIAL CHI TIẾT (InventoryCheckDetailSerial)
                    // Mỗi mã serial chốt ở bước 3 sẽ được ánh xạ thành 1 dòng trạng thái "Chờ quét" (Pending)
                    // làm cơ sở đối chiếu khi quét barcode thực tế tại kho
                    var snapshotRows = availableSerials.Select(s => new InventoryCheckDetailSerial
                    {
                        CheckId = check.Id,
                        DetailId = detailMap.TryGetValue(s.VariantId, out var d) ? d.Id : null,
                        VariantId = s.VariantId,
                        SerialId = s.SerialId,
                        SerialNumberRaw = s.SerialNumber,
                        OriginalStatus = (byte)SerialStatus.Available,
                        ScanStatus = (byte)InventoryScanStatus.Pending, // Bắt đầu ở trạng thái Pending (chờ nhân viên quét barcode)
                        ScannedAt = null
                    }).ToList();

                    await _checkRepo.AddDetailSerialsAsync(snapshotRows);
                    await _unitOfWork.SaveChangesAsync();

                    return (check, checkCode, availableSerials);
                }, retrySafe: true);

                _logger.LogInformation(
                    "Tạo phiếu kiểm kê: {CheckCode}, Phạm vi: {ScopeType}, Snapshot: {Total} serials",
                    checkCode, request.ScopeType, availableSerials.Count);

                var dto = await BuildCheckDtoAsync(check.Id);
                return ApiResult<InventoryCheckDto>.Ok(dto!, "Tạo phiếu kiểm kê thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo phiếu kiểm kê.");
                return ApiResult<InventoryCheckDto>.Fail("Đã xảy ra lỗi khi tạo phiếu kiểm kê. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // GET LIST
        // ========================================================
        public async Task<ApiResult<PagedResult<InventoryCheckListItemDto>>> GetPagedListAsync(InventoryCheckFilterRequest filter)
        {
            var (items, totalCount) = await _checkRepo.GetPagedListAsync(
                filter.Keyword, filter.Status, filter.FromDate, filter.ToDate,
                filter.EmployeeId, filter.PageNumber, filter.PageSize,
                filter.SortBy, filter.SortDescending);

            var employeeIds = items.Select(c => c.EmployeeId).Distinct().ToList();
            var employeeNames = await GetEmployeeNamesAsync(employeeIds);

            var dtos = items.Select(c => new InventoryCheckListItemDto
            {
                Id = c.Id,
                CheckCode = c.CheckCode,
                EmployeeName = employeeNames.GetValueOrDefault(c.EmployeeId, c.EmployeeId.ToString()),
                CheckDate = c.CheckDate,
                SnapshotAt = c.SnapshotAt,
                Status = c.Status,
                StatusName = GetStatusName(c.Status),
                ScopeType = c.ScopeType,
                ScopeCategoryName = c.ScopeCategory?.Name,
                TotalVariants = c.Details?.Count ?? 0,
                Note = c.Note
            }).ToList();

            return ApiResult<PagedResult<InventoryCheckListItemDto>>.Ok(new PagedResult<InventoryCheckListItemDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            });
        }

        // ========================================================
        // GET BY ID
        // ========================================================
        public async Task<ApiResult<InventoryCheckDto>> GetByIdAsync(int id)
        {
            var dto = await BuildCheckDtoAsync(id);
            if (dto == null)
                return ApiResult<InventoryCheckDto>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");
            return ApiResult<InventoryCheckDto>.Ok(dto);
        }

        // ========================================================
        // DASHBOARD
        // ========================================================
        public async Task<ApiResult<InventoryCheckDashboardDto>> GetDashboardAsync(int id)
        {
            var check = await _checkRepo.GetByIdWithDetailsAsync(id);
            if (check == null)
                return ApiResult<InventoryCheckDashboardDto>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            var counts = await _checkRepo.GetGroupedCountsByCheckAsync(id);

            var matched = counts.GetValueOrDefault((byte)InventoryScanStatus.Matched, 0);
            var missing = counts.GetValueOrDefault((byte)InventoryScanStatus.Missing, 0);
            var surplus = counts.GetValueOrDefault((byte)InventoryScanStatus.Surplus, 0);
            var unknown = counts.GetValueOrDefault((byte)InventoryScanStatus.UnknownSurplus, 0);
            var defective = counts.GetValueOrDefault((byte)InventoryScanStatus.Defective, 0);
            var pending = counts.GetValueOrDefault((byte)InventoryScanStatus.Pending, 0);

            int totalSystem = check.Details?.Sum(d => d.SystemQuantity) ?? 0;
            int totalScanned = matched + surplus + unknown + defective;

            var dashboard = new InventoryCheckDashboardDto
            {
                CheckId = check.Id,
                CheckCode = check.CheckCode,
                Status = check.Status,
                StatusName = GetStatusName(check.Status),
                SnapshotAt = check.SnapshotAt,
                TotalSystem = totalSystem,
                TotalScanned = totalScanned,
                MatchedCount = matched,
                MissingCount = missing + pending,
                SurplusCount = surplus,
                UnknownSurplusCount = unknown,
                DefectiveCount = defective
            };

            return ApiResult<InventoryCheckDashboardDto>.Ok(dashboard);
        }

        // ========================================================
        // GET SERIALS PAGED
        // ========================================================
        public async Task<ApiResult<PagedResult<InventoryCheckSerialDto>>> GetSerialsAsync(int checkId, InventoryCheckSerialFilterRequest filter)
        {
            var exists = await _context.InventoryChecks.AnyAsync(c => c.Id == checkId);
            if (!exists)
                return ApiResult<PagedResult<InventoryCheckSerialDto>>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            var (items, totalCount) = await _checkRepo.GetDetailSerialsPagedAsync(
                checkId, filter.ScanStatus, filter.VariantId, filter.PageNumber, filter.PageSize);

            var dtos = items.Select(MapSerialToDto).ToList();

            return ApiResult<PagedResult<InventoryCheckSerialDto>>.Ok(new PagedResult<InventoryCheckSerialDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            });
        }

        // ========================================================
        // SCAN SERIAL — Quét barcode ghi nhận thực tế kiểm kho
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Quét barcode Serial Number thực tế tại kệ kho để đối chiếu với sổ sách hệ thống.
        /// Chốt chặn nghiệp vụ chống quét trùng lắp mã trong cùng một phiếu kiểm kê.
        /// Phân nhánh xử lý các trường hợp thực tế tại kho bãi:
        /// 1. Khớp (Matched): Serial nằm trong danh sách chốt ban đầu (Pending) -> cập nhật trạng thái Khớp, tăng Actual và Matched Quantity.
        /// 2. Thừa (Surplus) nhưng có sẵn bán (Available) ngoài phạm vi kiểm kê -> Đăng ký quét thừa.
        /// 3. Thừa nhưng trạng thái khác (Sold, Reserved, Defective, Lost, Returned) -> Ghi nhận quét thừa kèm chú thích nguyên nhân gốc để đối chiếu.
        /// 4. Thừa mã lạ (UnknownSurplus): Serial hoàn toàn không có trong cơ sở dữ liệu -> Ghi nhận và chờ thủ kho bổ sung thông tin biến thể.
        /// </summary>
        public async Task<ApiResult<ScanResultDto>> ScanSerialAsync(int checkId, ScanSerialRequest request, Guid employeeId)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<ScanResultDto>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            // NGHIỆP VỤ: Chỉ cho phép quét barcode khi phiếu ở trạng thái Nháp (Draft).
            if (check.Status != (byte)InventoryCheckStatus.Draft)
                return ApiResult<ScanResultDto>.Fail("Chỉ có thể quét khi phiếu ở trạng thái Nháp.");

            var serialNumber = request.SerialNumber.Trim();

            // CHỐT NGHIỆP VỤ: Chống quét trùng (một mã serial không được đếm 2 lần trong cùng một phiếu)
            var alreadyScanned = await _checkRepo.IsSerialAlreadyScannedAsync(checkId, serialNumber);
            if (alreadyScanned)
            {
                return ApiResult<ScanResultDto>.Ok(new ScanResultDto
                {
                    Success = false,
                    IsDuplicateScan = true,
                    SerialNumberRaw = serialNumber,
                    Message = $"Serial [{serialNumber}] đã được quét trong phiếu này.",
                    ScanStatus = 0,
                    ScanStatusName = string.Empty,
                    MiniDashboard = await BuildMiniDashboardAsync(checkId, check)
                });
            }

            var now = DateTime.UtcNow;
            var dbSerial = await _serialRepo.GetBySerialNumberAsync(serialNumber);

            // ─── PHÂN NHÁNH 1 (Trường hợp A3): Serial không hề tồn tại trong DB hệ thống ───
            // Nghiệp vụ: Ghi nhận là "Thừa (Mã lạ) - UnknownSurplus" để kế toán kho kiểm tra sau.
            if (dbSerial == null)
            {
                if (request.VariantIdForUnknown.HasValue)
                {
                    var variantExists = await _context.ProductVariants
                        .AnyAsync(v => v.Id == request.VariantIdForUnknown.Value);
                    if (!variantExists)
                        return ApiResult<ScanResultDto>.Fail("Biến thể sản phẩm đề xuất không tồn tại.");
                }

                var unknownRow = new InventoryCheckDetailSerial
                {
                    CheckId = checkId,
                    DetailId = null,
                    VariantId = request.VariantIdForUnknown,
                    SerialId = null,
                    SerialNumberRaw = serialNumber,
                    OriginalStatus = null,
                    ScanStatus = (byte)InventoryScanStatus.UnknownSurplus, // Trạng thái kiểm kê: Thừa (Mã lạ)
                    ScannedAt = now,
                    ScannedByEmployeeId = employeeId,
                    Note = "Serial không tồn tại trong hệ thống."
                };

                await _checkRepo.AddDetailSerialAsync(unknownRow);

                // Nếu thủ kho quét và xác định được nó thuộc biến thể nào, ta cập nhật số lượng Thừa trên Variant đó
                if (request.VariantIdForUnknown.HasValue)
                {
                    var detail = await _checkRepo.GetDetailByCheckAndVariantAsync(checkId, request.VariantIdForUnknown.Value, withTracking: true);
                    if (detail != null)
                    {
                        detail.SurplusQuantity++;
                        unknownRow.DetailId = detail.Id;
                    }
                }

                await _checkRepo.SaveChangesAsync();

                return ApiResult<ScanResultDto>.Ok(new ScanResultDto
                {
                    Success = true,
                    IsDuplicateScan = false,
                    RequiresVariantInput = request.VariantIdForUnknown == null, // Nếu chưa gắn VariantId, yêu cầu Client hiển thị UI chọn Variant
                    SerialNumberRaw = serialNumber,
                    ScanStatus = (byte)InventoryScanStatus.UnknownSurplus,
                    ScanStatusName = "Thừa (Mã lạ)",
                    Message = "Serial không tồn tại trong hệ thống. Đã ghi nhận là Thừa kiểm kê (Mã lạ).",
                    MiniDashboard = await BuildMiniDashboardAsync(checkId, check)
                });
            }

            // ─── PHÂN NHÁNH 2: Serial tồn tại và đang ở trạng thái Available (Trong kho) ───
            if (dbSerial.Status == (byte)SerialStatus.Available)
            {
                // Tìm dòng snapshot chốt ban đầu của serial này
                var pendingRow = await _checkRepo.GetPendingDetailSerialBySerialIdAsync(checkId, dbSerial.Id);

                if (pendingRow != null)
                {
                    // Trường hợp 2.1: Khớp (Matched) - Serial nằm trong danh sách chốt sổ sách ban đầu
                    pendingRow.ScanStatus = (byte)InventoryScanStatus.Matched;
                    pendingRow.ScannedAt = now;
                    pendingRow.ScannedByEmployeeId = employeeId;

                    // Tăng số lượng thực tế (ActualQuantity) và số lượng khớp (MatchedQuantity) của Variant
                    if (pendingRow.DetailId.HasValue)
                    {
                        var detail = await _checkRepo.GetDetailByCheckAndVariantAsync(checkId, dbSerial.VariantId, withTracking: true);
                        if (detail != null)
                        {
                            detail.ActualQuantity++;
                            detail.MatchedQuantity++;
                        }
                    }

                    await _checkRepo.SaveChangesAsync();

                    return ApiResult<ScanResultDto>.Ok(new ScanResultDto
                    {
                        Success = true,
                        SerialNumberRaw = serialNumber,
                        ScanStatus = (byte)InventoryScanStatus.Matched,
                        ScanStatusName = "Khớp",
                        Message = "Khớp.",
                        FoundSerialNumber = dbSerial.SerialNumber,
                        FoundVariantName = dbSerial.Variant?.VariantName,
                        FoundOriginalStatus = (byte)SerialStatus.Available,
                        FoundOriginalStatusName = "Trong kho",
                        MiniDashboard = await BuildMiniDashboardAsync(checkId, check)
                    });
                }
                else
                {
                    // Trường hợp 2.2: Thừa (Surplus) - Serial sẵn bán trong kho nhưng không thuộc phạm vi kiểm kê ban đầu (ví dụ: danh mục khác)
                    var surplusRow = new InventoryCheckDetailSerial
                    {
                        CheckId = checkId,
                        VariantId = dbSerial.VariantId,
                        SerialId = dbSerial.Id,
                        SerialNumberRaw = serialNumber,
                        OriginalStatus = (byte)SerialStatus.Available,
                        ScanStatus = (byte)InventoryScanStatus.Surplus, // Trạng thái kiểm kê: Thừa
                        ScannedAt = now,
                        ScannedByEmployeeId = employeeId,
                        Note = "Serial sẵn bán nhưng nằm ngoài phạm vi kiểm kê."
                    };
                    await _checkRepo.AddDetailSerialAsync(surplusRow);
                    await _checkRepo.SaveChangesAsync();

                    return ApiResult<ScanResultDto>.Ok(new ScanResultDto
                    {
                        Success = true,
                        SerialNumberRaw = serialNumber,
                        ScanStatus = (byte)InventoryScanStatus.Surplus,
                        ScanStatusName = "Thừa",
                        Message = "Serial nằm ngoài phạm vi kiểm kê.",
                        FoundSerialNumber = dbSerial.SerialNumber,
                        MiniDashboard = await BuildMiniDashboardAsync(checkId, check)
                    });
                }
            }

            // ─── PHÂN NHÁNH 3: Serial tồn tại nhưng KHÔNG có trạng thái Available (Trường hợp A1/A2) ───
            // Nghiệp vụ: Serial thực tế đang nằm trên kệ hàng, nhưng trên phần mềm ghi nhận trạng thái khác (đã bán, lỗi, báo mất...).
            // Hệ thống ghi nhận là "Thừa" và tự động tìm lý do thực tế của trạng thái trên phần mềm để thủ kho đối chiếu.
            var surplusNote = dbSerial.Status switch
            {
                (byte)SerialStatus.Sold => $"Đã bán tại Đơn hàng #{dbSerial.OrderId} ngày {dbSerial.SoldDate?.ToString("dd/MM/yyyy") ?? "N/A"}.",
                (byte)SerialStatus.Reserved => $"Đang giữ chỗ cho Đơn hàng #{dbSerial.OrderId}.",
                (byte)SerialStatus.Defective => "Đang nằm trong kho lỗi.",
                (byte)SerialStatus.Returned => "Đã được ghi nhận trả lại.",
                (byte)SerialStatus.Lost => "Đã được ghi nhận thất thoát.",
                _ => $"Trạng thái hiện tại: {dbSerial.Status}."
            };

            var existingDetail = await _checkRepo.GetDetailByCheckAndVariantAsync(checkId, dbSerial.VariantId, withTracking: true);
            var surplusDetailId = existingDetail?.Id;

            var surplusEntry = new InventoryCheckDetailSerial
            {
                CheckId = checkId,
                DetailId = surplusDetailId,
                VariantId = dbSerial.VariantId,
                SerialId = dbSerial.Id,
                SerialNumberRaw = serialNumber,
                OriginalStatus = dbSerial.Status,
                ScanStatus = (byte)InventoryScanStatus.Surplus, // Trạng thái kiểm kê: Thừa
                ScannedAt = now,
                ScannedByEmployeeId = employeeId,
                Note = surplusNote
            };

            if (existingDetail != null)
                existingDetail.SurplusQuantity++;

            await _checkRepo.AddDetailSerialAsync(surplusEntry);
            await _checkRepo.SaveChangesAsync();

            var originalStatusName = ((SerialStatus)dbSerial.Status).ToString();
            return ApiResult<ScanResultDto>.Ok(new ScanResultDto
            {
                Success = true,
                SerialNumberRaw = serialNumber,
                ScanStatus = (byte)InventoryScanStatus.Surplus,
                ScanStatusName = "Thừa",
                Message = $"Serial tìm thấy nhưng đang ở trạng thái {originalStatusName}. {surplusNote}",
                FoundSerialNumber = dbSerial.SerialNumber,
                FoundVariantName = dbSerial.Variant?.VariantName,
                FoundOriginalStatus = dbSerial.Status,
                FoundOriginalStatusName = originalStatusName,
                SurplusNote = surplusNote,
                MiniDashboard = await BuildMiniDashboardAsync(checkId, check)
            });
        }

        // ========================================================
        // MARK DEFECTIVE — Đánh dấu hàng lỗi vật lý phát hiện khi quét
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Đánh dấu một Serial đang kiểm kê bị lỗi vật lý (Defective) phát hiện trực tiếp tại kệ kho.
        /// Ràng buộc: Chỉ áp dụng đối với Serial đang ở trạng thái Khớp (Matched) và phiếu đang ở trạng thái Nháp (Draft).
        /// Cập nhật số lượng của Variant: giảm MatchedQuantity và tăng DefectiveQuantity tương ứng (vẫn giữ nguyên tổng số thực tế ActualQuantity).
        /// </summary>
        public async Task<ApiResult<bool>> MarkDefectiveAsync(int checkId, int detailSerialId, Guid employeeId)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            // NGHIỆP VỤ: Chỉ có thể cập nhật trạng thái lỗi khi phiếu ở trạng thái Nháp (Draft).
            if (check.Status != (byte)InventoryCheckStatus.Draft)
                return ApiResult<bool>.Fail("Chỉ có thể đánh dấu lỗi khi phiếu ở trạng thái Nháp.");

            var row = await _checkRepo.GetDetailSerialAsync(detailSerialId, withTracking: true);
            if (row == null || row.CheckId != checkId)
                return ApiResult<bool>.Fail("Không tìm thấy dòng Serial trong phiếu kiểm kê.");

            // Chỉ cho phép báo lỗi với hàng thực tế đang được tính là Khớp (tức là hàng đang nằm sẵn trong kho sổ sách)
            if (row.ScanStatus != (byte)InventoryScanStatus.Matched)
                return ApiResult<bool>.Fail("Chỉ có thể đánh dấu lỗi cho Serial đang ở trạng thái Khớp.");

            row.ScanStatus = (byte)InventoryScanStatus.Defective;

            // Cập nhật lại số lượng đếm trên dòng chi tiết của Variant:
            // Hàng lỗi vật lý vẫn được tính trong thực tế đếm được (ActualQuantity), nhưng chuyển từ cột Khớp sang cột Lỗi vật lý.
            if (row.DetailId.HasValue && row.VariantId.HasValue)
            {
                var detail = await _checkRepo.GetDetailByCheckAndVariantAsync(checkId, row.VariantId.Value, withTracking: true);
                if (detail != null)
                {
                    detail.MatchedQuantity--;
                    detail.DefectiveQuantity++;
                }
            }

            await _checkRepo.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Đã đánh dấu Serial là hàng lỗi vật lý.");
        }

        // ========================================================
        // UPDATE REASON
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Cập nhật lý do chênh lệch hoặc đề xuất giải pháp xử lý thực tế cho từng mã Serial đang kiểm kê.
        /// Giúp kế toán kho hoặc ban giám đốc có cơ sở thông tin để thẩm định và phê duyệt phương án cân bằng kho.
        /// </summary>
        public async Task<ApiResult<bool>> UpdateReasonAsync(int checkId, int detailSerialId, UpdateScanReasonRequest request, Guid employeeId)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (check.Status != (byte)InventoryCheckStatus.Draft)
                return ApiResult<bool>.Fail("Chỉ có thể cập nhật lý do khi phiếu ở trạng thái Nháp.");

            var row = await _checkRepo.GetDetailSerialAsync(detailSerialId, withTracking: true);
            if (row == null || row.CheckId != checkId)
                return ApiResult<bool>.Fail("Không tìm thấy dòng Serial trong phiếu kiểm kê.");

            row.Note = request.Reason.Trim();
            row.ProposedActionNote = request.ProposedActionNote?.Trim();

            await _checkRepo.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Cập nhật lý do thành công.");
        }

        // ========================================================
        // SUBMIT — Gửi duyệt phiếu kiểm kê
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Khóa dữ liệu kiểm kê và gửi lên Ban giám đốc/Quản trị viên để chờ phê duyệt điều chỉnh kho.
        /// Thực hiện chốt chặn an toàn: chuyển toàn bộ các mã Serial chốt ban đầu mà không được quét thực tế (vẫn đang ở trạng thái Pending)
        /// sang trạng thái Thiếu (Missing), tính toán lại tổng số thiếu trên dòng tổng hợp của từng Variant.
        /// Chuyển trạng thái phiếu kiểm kê sang Chờ duyệt (AwaitingApproval) và ngăn chặn mọi hành vi chỉnh sửa hoặc quét thêm.
        /// </summary>
        public async Task<ApiResult<bool>> SubmitAsync(int checkId, Guid employeeId)
        {
            // Kiểm tra nghiệp vụ chạy NGOÀI transaction: projection AsNoTracking, chỉ để trả
            // lỗi đẹp mà không phải mở transaction, và để lấy CheckCode cho log. Không ghi gì.
            // Dùng projection thay vì nạp cả entity: rẻ hơn, và quan trọng hơn là không có
            // entity tracked nào lọt ra ngoài delegate để vô tình bị sửa.
            var precheck = await _context.InventoryChecks
                .AsNoTracking()
                .Where(c => c.Id == checkId)
                .Select(c => new { c.Status, c.EmployeeId, c.CheckCode })
                .FirstOrDefaultAsync();

            if (precheck == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (precheck.Status != (byte)InventoryCheckStatus.Draft)
                return ApiResult<bool>.Fail("Chỉ có thể gửi duyệt khi phiếu ở trạng thái Nháp.");

            // Chỉ người tạo phiếu mới có quyền gửi duyệt
            if (precheck.EmployeeId != employeeId)
                return ApiResult<bool>.Fail("Bạn không có quyền gửi duyệt phiếu này.");

            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `check` và `detail` đều nạp LẠI bên trong delegate;
                // (2) không sinh mã chứng từ hay mốc thời gian nào để ghi;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate.
                //
                // ⚠️ Ở ĐÂY NẠP LẠI LÀ BẮT BUỘC, KHÔNG PHẢI CHỈ ĐỔI CỜ.
                // `detail.MissingQuantity += missingCount` là phép tăng TƯƠNG ĐỐI trên entity
                // tracked. Nếu giữ entity của lần thử trước, lần thử này cộng chồng thành
                // `cũ + 2×missing` và EF THẤY CÓ THAY ĐỔI nên vẫn sinh UPDATE — với con số
                // sai. Đó là ghi sai số liệu âm thầm, tệ hơn mất dữ liệu vì kết quả trông
                // vẫn hợp lệ. ChangeTracker.Clear() (chạy khi retrySafe: true) + nạp lại
                // mới xử lý được ca này.
                var pendingRows = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Nạp LẠI bên trong delegate — mỗi lần thử đọc bản mới từ DB.
                    var check = await _checkRepo.GetByIdAsync(checkId);

                    // Chốt chống race. NÉM chứ không return: return thì transaction vẫn commit.
                    if (check is null || check.Status != (byte)InventoryCheckStatus.Draft)
                        throw new ConcurrentModificationException(
                            "Phiếu kiểm kê vừa được thay đổi bởi thao tác khác. Vui lòng tải lại trang.");

                    // NGHIỆP VỤ QUAN TRỌNG: Tất cả các mã Serial nằm trong danh sách chốt ban đầu (Pending)
                    // mà không được nhân viên quét barcode thực tế (chưa được tìm thấy tại kho)
                    // sẽ tự động được coi là thất thoát và chuyển sang trạng thái "Thiếu" (Missing).
                    var pendingRows = await _checkRepo.GetPendingDetailSerialsAsync(checkId);
                    var detailMissingCounts = new Dictionary<int, int>();

                    foreach (var row in pendingRows)
                    {
                        row.ScanStatus = (byte)InventoryScanStatus.Missing;
                        if (row.DetailId.HasValue)
                        {
                            detailMissingCounts.TryAdd(row.DetailId.Value, 0);
                            detailMissingCounts[row.DetailId.Value]++;
                        }
                    }

                    // Cập nhật lại số lượng Thiếu (MissingQuantity) trên dòng tổng hợp Detail của từng Variant
                    foreach (var (detailId, missingCount) in detailMissingCounts)
                    {
                        var detail = await _context.InventoryCheckDetails
                            .FirstOrDefaultAsync(d => d.Id == detailId);
                        if (detail != null)
                            detail.MissingQuantity += missingCount;
                    }

                    check.Status = (byte)InventoryCheckStatus.AwaitingApproval; // Chuyển trạng thái phiếu sang Chờ duyệt (AwaitingApproval)
                    await _unitOfWork.SaveChangesAsync();

                    return pendingRows;
                }, retrySafe: true);

                _logger.LogInformation(
                    "Gửi duyệt phiếu kiểm kê {CheckCode}: {MissingCount} serials thiếu",
                    precheck.CheckCode, pendingRows.Count);

                return ApiResult<bool>.Ok(true, "Đã gửi phiếu kiểm kê để phê duyệt thành công.");
            }
            catch (ConcurrentModificationException ex)
            {
                // Bắt TRƯỚC catch(Exception) để giữ nguyên thông báo cụ thể — nếu không,
                // người dùng nhận "đã xảy ra lỗi, thử lại" và bấm lại cũng hỏng y hệt.
                _logger.LogWarning(ex, "Xung đột đồng thời khi gửi duyệt phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi duyệt phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail("Đã xảy ra lỗi khi gửi duyệt. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // APPROVE — Phê duyệt phiếu & Cân bằng kho thực tế
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ CÂN BẰNG TỒN KHO THỰC TẾ (AUTO-BALANCE): Phê duyệt kết quả kiểm kho và tiến hành điều chỉnh tự động.
        /// Chạy dưới Transaction an toàn thực hiện các bước:
        /// 1. Với mã Serial thiếu (Missing): Đánh giá qua "Cửa sổ kiểm kê" (Inventory Window) - nếu bị bán/giữ chỗ bởi đơn hàng phát sinh trong lúc chờ duyệt,
        ///    hệ thống bỏ qua để bảo toàn đơn hàng cho khách. Các mã còn lại chuyển trạng thái phần mềm thành Mất (Lost) và tính tổn thất chi phí dựa trên giá vốn nhập kho gần nhất.
        /// 2. Với mã Serial lỗi vật lý (Defective): Chuyển trạng thái sang Lỗi hỏng (Defective) để loại bỏ khỏi kho sẵn sàng bán, lưu vết chi phí hao tổn.
        /// 3. Kích hoạt đồng bộ hóa tồn kho khả dụng (StockQuantity) hàng loạt cho các Variant bị ảnh hưởng để website cập nhật số lượng chuẩn xác lập tức.
        /// </summary>
        public async Task<ApiResult<bool>> ApproveAsync(int checkId, Guid adminId)
        {
            // Kiểm tra nghiệp vụ NGOÀI transaction: projection AsNoTracking, trả lỗi sớm và
            // lấy CheckCode cho log. Không entity tracked nào lọt ra ngoài delegate.
            var precheck = await _context.InventoryChecks
                .AsNoTracking()
                .Where(c => c.Id == checkId)
                .Select(c => new { c.Status, c.CheckCode })
                .FirstOrDefaultAsync();

            if (precheck == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            // NGHIỆP VỤ: Chỉ phê duyệt khi phiếu đang ở trạng thái Chờ duyệt (AwaitingApproval).
            if (precheck.Status != (byte)InventoryCheckStatus.AwaitingApproval)
                return ApiResult<bool>.Fail("Chỉ có thể phê duyệt phiếu ở trạng thái Chờ duyệt.");

            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `check`, các `row.Serial` và log điều chỉnh đều nạp/dựng bên trong;
                // (2) `DateTime.UtcNow` dùng để ghi (AdjustedDate, ApprovedAt) tính bên trong,
                //     nên lần thử lại lấy mốc mới — đúng ý nghĩa "lúc thực sự ghi được";
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate.
                //
                // `adjustmentLogs` được khởi tạo BÊN TRONG (dòng ngay dưới): bắt buộc, vì
                // nếu dựng ở ngoài thì lần thử lại sẽ Add chồng lên danh sách cũ và ghi
                // trùng bản ghi tổn thất.
                //
                // SyncStockBatchAsync vẫn nằm TRONG delegate một cách có chủ đích: nó đếm lại
                // Available từ DB (idempotent), nên chạy lại không sai; và để trong thì số
                // tồn được commit cùng transaction với việc đổi trạng thái serial.
                var adjustmentLogs = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Nạp LẠI bên trong delegate — mỗi lần thử đọc bản mới từ DB.
                    var check = await _checkRepo.GetByIdAsync(checkId);

                    // Chốt chống race. NÉM chứ không return, để transaction rollback.
                    if (check is null || check.Status != (byte)InventoryCheckStatus.AwaitingApproval)
                        throw new ConcurrentModificationException(
                            "Phiếu kiểm kê vừa được thay đổi bởi thao tác khác. Vui lòng tải lại trang.");

                    var adjustmentLogs = new List<InventoryAdjustmentLog>();
                    var affectedVariantIds = new HashSet<int>();

                    // ── BƯỚC 1: XỬ LÝ SERIAL THẤT THOÁT (Missing rows → Lost) ──
                    var missingRows = await _checkRepo.GetMissingDetailSerialsWithSerialAsync(checkId);
                    foreach (var row in missingRows)
                    {
                        if (row.Serial == null) continue;

                        var currentStatus = row.Serial.Status;

                        // QUY TẮC NGHIỆP VỤ CỐT LÕI (BR1):
                        // Chỉ cập nhật trạng thái Serial sang "Lost" (Mất) nếu tại thời điểm phê duyệt, serial đó VẪN ĐANG ở trạng thái "Available".
                        if (currentStatus == (byte)SerialStatus.Available)
                        {
                            // Lấy giá vốn của Serial từ Hóa đơn nhập kho gần nhất để làm cơ sở tính chi phí tổn thất
                            var costImpact = await GetSerialCostAsync(row.Serial);

                            row.Serial.Status = (byte)SerialStatus.Lost; // Chuyển trạng thái phần mềm sang Thất thoát
                            affectedVariantIds.Add(row.Serial.VariantId); // Đánh dấu Variant cần đồng bộ số lượng tồn

                            // Lưu log điều chỉnh kho phục vụ báo cáo tài chính/kiểm toán
                            adjustmentLogs.Add(new InventoryAdjustmentLog
                            {
                                AuditCheckId = checkId,
                                SerialId = row.Serial.Id,
                                VariantId = row.Serial.VariantId,
                                OldStatus = currentStatus,
                                NewStatus = (byte)SerialStatus.Lost,
                                AdjustmentType = (byte)InventoryAdjustmentType.Lost,
                                CostImpact = costImpact,
                                Reason = row.Note ?? "Không tìm thấy khi kiểm kê.",
                                AdjustedDate = DateTime.UtcNow,
                                AdjustedByEmployeeId = adminId
                            });
                        }
                        else
                        {
                            // "CỬA SỔ KIỂM KÊ" (CỰC KỲ QUAN TRỌNG):
                            // Nếu trong thời gian chờ duyệt phiếu, serial này đã được khách mua trực tuyến (Sold) hoặc đang được giữ chỗ (Reserved) trong một đơn hàng mới,
                            // ta KHÔNG được phép chuyển nó sang Lost nữa (để tránh làm hỏng đơn hàng của khách).
                            // Hệ thống ghi nhận trạng thái ResolvedDuringApproval = true để bỏ qua và cập nhật lý do rõ ràng.
                            row.ResolvedDuringApproval = true;
                            var resolveNote = currentStatus switch
                            {
                                (byte)SerialStatus.Sold => "Đã bán trong cửa sổ kiểm kê — không ghi lỗ.",
                                (byte)SerialStatus.Reserved => "Đã giữ chỗ trong cửa sổ kiểm kê — không ghi lỗ.",
                                _ => $"Trạng thái thay đổi trong cửa sổ kiểm kê ({currentStatus}) — không ghi lỗ."
                            };
                            row.Note = string.IsNullOrEmpty(row.Note)
                                ? resolveNote
                                : $"{row.Note} | {resolveNote}";
                        }
                    }

                    // ── BƯỚC 2: XỬ LÝ SERIAL LỖI VẬT LÝ (Defective rows → Defective) ──
                    var defectiveRows = await _checkRepo.GetDefectiveDetailSerialsWithSerialAsync(checkId);
                    foreach (var row in defectiveRows)
                    {
                        if (row.Serial == null) continue;

                        var currentStatus = row.Serial.Status;

                        // Chỉ chuyển sang lỗi hỏng vật lý nếu thực tế nó vẫn đang được coi là Available trên phần mềm
                        if (currentStatus == (byte)SerialStatus.Available)
                        {
                            var costImpact = await GetSerialCostAsync(row.Serial);

                            row.Serial.Status = (byte)SerialStatus.Defective; // Chuyển trạng thái sang Lỗi hỏng (không được bán nữa)
                            affectedVariantIds.Add(row.Serial.VariantId);

                            adjustmentLogs.Add(new InventoryAdjustmentLog
                            {
                                AuditCheckId = checkId,
                                SerialId = row.Serial.Id,
                                VariantId = row.Serial.VariantId,
                                OldStatus = currentStatus,
                                NewStatus = (byte)SerialStatus.Defective,
                                AdjustmentType = (byte)InventoryAdjustmentType.Defective,
                                CostImpact = costImpact,
                                Reason = row.Note ?? "Hàng lỗi vật lý phát hiện khi kiểm kê.",
                                AdjustedDate = DateTime.UtcNow,
                                AdjustedByEmployeeId = adminId
                            });
                        }
                    }

                    // Lưu toàn bộ lịch sử điều chỉnh kho
                    if (adjustmentLogs.Any())
                        await _checkRepo.AddAdjustmentLogsAsync(adjustmentLogs);

                    // Cập nhật thông tin phiếu kiểm kê sang Đã hoàn tất (Completed)
                    check.Status = (byte)InventoryCheckStatus.Completed;
                    check.ApprovedByEmployeeId = adminId;
                    check.ApprovedAt = DateTime.UtcNow;

                    await _unitOfWork.SaveChangesAsync();

                    // ── BƯỚC 3: ĐỒNG BỘ HÓA TỒN KHO THỰC TẾ (StockQuantity) ──
                    // Kích hoạt đồng bộ lại số lượng tồn khả dụng của các Variant bị ảnh hưởng để hiển thị đúng lên Website bán hàng
                    if (affectedVariantIds.Any())
                        await _inventorySyncService.SyncStockBatchAsync(affectedVariantIds);

                    return adjustmentLogs;
                }, retrySafe: true);

                _logger.LogInformation(
                    "Phê duyệt phiếu kiểm kê {CheckCode}: {Lost} lost, {Defective} defective. Admin: {AdminId}",
                    precheck.CheckCode,
                    adjustmentLogs.Count(l => l.AdjustmentType == (byte)InventoryAdjustmentType.Lost),
                    adjustmentLogs.Count(l => l.AdjustmentType == (byte)InventoryAdjustmentType.Defective),
                    adminId);

                return ApiResult<bool>.Ok(true, "Phê duyệt và cân bằng kho thành công.");
            }
            catch (ConcurrentModificationException ex)
            {
                _logger.LogWarning(ex, "Xung đột đồng thời khi phê duyệt phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phê duyệt phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail("Đã xảy ra lỗi khi phê duyệt. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // REJECT — Từ chối phê duyệt phiếu kiểm kê
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Từ chối kết quả kiểm kê từ thủ kho. Cung cấp 2 lựa chọn xử lý sau khi từ chối:
        /// 1. Trả về nháp (ReturnToDraft) để quét lại: Khôi phục toàn bộ serial thiếu (Missing) về chờ quét (Pending), xóa các serial quét thừa
        ///    phát sinh trước đó, reset các chỉ số lượng đếm trên dòng tổng hợp và khôi phục chỉ số của serial khớp (Matched)/lỗi (Defective) cũ.
        /// 2. Hủy bỏ hoàn toàn (Cancelled): Đóng vĩnh viễn phiếu và giữ nguyên hiện trạng số liệu để lưu vết lịch sử sai sót phục vụ đối chiếu sau này.
        /// </summary>
        public async Task<ApiResult<bool>> RejectAsync(int checkId, RejectInventoryCheckRequest request, Guid adminId)
        {
            // Kiểm tra nghiệp vụ NGOÀI transaction: projection AsNoTracking, trả lỗi sớm và
            // lấy CheckCode cho log. Không entity tracked nào lọt ra ngoài delegate.
            var precheck = await _context.InventoryChecks
                .AsNoTracking()
                .Where(c => c.Id == checkId)
                .Select(c => new { c.Status, c.CheckCode })
                .FirstOrDefaultAsync();

            if (precheck == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (precheck.Status != (byte)InventoryCheckStatus.AwaitingApproval)
                return ApiResult<bool>.Fail("Chỉ có thể từ chối phiếu ở trạng thái Chờ duyệt.");

            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `check`, các dòng Missing/Matched/Defective/Surplus và `details` đều nạp
                //     LẠI bên trong delegate; (2) không sinh mã chứng từ hay mốc thời gian;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate.
                //
                // ⚠️ Nhánh ReturnToDraft có `row.Detail.ActualQuantity++` — tăng TƯƠNG ĐỐI như
                // `+=` ở SubmitAsync. Nó chỉ đúng khi chạy lại vì các cột đếm đã được reset về 0
                // ngay trên đó TRONG CÙNG delegate, và `details` là bản nạp lại của lần thử này.
                // Đừng tách phần reset ra ngoài delegate.
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Nạp LẠI bên trong delegate — mỗi lần thử đọc bản mới từ DB.
                    var check = await _checkRepo.GetByIdAsync(checkId);

                    // Chốt chống race. NÉM chứ không return, để transaction rollback.
                    if (check is null || check.Status != (byte)InventoryCheckStatus.AwaitingApproval)
                        throw new ConcurrentModificationException(
                            "Phiếu kiểm kê vừa được thay đổi bởi thao tác khác. Vui lòng tải lại trang.");

                    check.RejectReason = request.Reason.Trim();

                    // NGHIỆP VỤ: Từ chối có 2 hướng đi: Trả về nháp để quét lại hoặc Hủy phiếu hoàn toàn
                    if (request.ReturnToDraft)
                    {
                        // ── PHƯƠNG ÁN 1: TRẢ VỀ TRẠNG THÁI NHÁP (Draft) ──
                        // Chuyển toàn bộ các dòng "Thiếu" (Missing) trở lại thành trạng thái "Chờ quét" (Pending)
                        var actualMissingRows = await _context.InventoryCheckDetailSerials
                            .Where(s => s.CheckId == checkId && s.ScanStatus == (byte)InventoryScanStatus.Missing)
                            .Include(s => s.Detail)
                            .ToListAsync();

                        foreach (var row in actualMissingRows)
                        {
                            row.ScanStatus = (byte)InventoryScanStatus.Pending;
                            if (row.Detail != null)
                                row.Detail.MissingQuantity = 0;
                        }

                        // Xóa hoàn toàn tất cả các dòng ghi nhận Thừa (Surplus) hoặc Thừa mã lạ (UnknownSurplus) phát sinh trong lần quét trước
                        var surplusRows = await _checkRepo.GetSurplusDetailSerialsAsync(checkId);
                        await _checkRepo.RemoveDetailSerialsAsync(surplusRows);

                        // Khởi động lại (Reset) toàn bộ các cột chỉ số lượng đếm trên bảng chi tiết
                        var details = await _context.InventoryCheckDetails
                            .Where(d => d.CheckId == checkId)
                            .ToListAsync();
                        foreach (var d in details)
                        {
                            d.ActualQuantity = 0;
                            d.MatchedQuantity = 0;
                            d.MissingQuantity = 0;
                            d.SurplusQuantity = 0;
                            d.DefectiveQuantity = 0;
                        }

                        // Đếm lại và điền lại số lượng cho các dòng đã quét "Khớp" (Matched) vẫn giữ nguyên kết quả
                        var matchedRows = await _context.InventoryCheckDetailSerials
                            .Where(s => s.CheckId == checkId && s.ScanStatus == (byte)InventoryScanStatus.Matched)
                            .Include(s => s.Detail)
                            .ToListAsync();
                        foreach (var row in matchedRows)
                        {
                            if (row.Detail != null)
                            {
                                row.Detail.ActualQuantity++;
                                row.Detail.MatchedQuantity++;
                            }
                        }

                        // Tương tự, điền lại số lượng đếm thực tế cho hàng Lỗi vật lý (Defective)
                        var defectiveRows = await _context.InventoryCheckDetailSerials
                            .Where(s => s.CheckId == checkId && s.ScanStatus == (byte)InventoryScanStatus.Defective)
                            .Include(s => s.Detail)
                            .ToListAsync();
                        foreach (var row in defectiveRows)
                        {
                            if (row.Detail != null)
                            {
                                row.Detail.ActualQuantity++;
                                row.Detail.DefectiveQuantity++;
                            }
                        }

                        check.Status = (byte)InventoryCheckStatus.Draft; // Chuyển trạng thái phiếu về Nháp
                    }
                    else
                    {
                        // ── PHƯƠNG ÁN 2: HỦY PHIẾU (Cancelled) ──
                        // Đóng vĩnh viễn phiếu và giữ nguyên hiện trạng số liệu đã ghi nhận để lưu vết lịch sử lỗi
                        check.Status = (byte)InventoryCheckStatus.Cancelled;
                    }

                    await _unitOfWork.SaveChangesAsync();
                }, retrySafe: true);

                var action = request.ReturnToDraft ? "trả về Nháp" : "hủy";
                _logger.LogInformation(
                    "Từ chối phiếu kiểm kê {CheckCode} ({Action}). Admin: {AdminId}. Lý do: {Reason}",
                    precheck.CheckCode, action, adminId, request.Reason);

                return ApiResult<bool>.Ok(true, $"Đã từ chối và {action} phiếu kiểm kê.");
            }
            catch (ConcurrentModificationException ex)
            {
                _logger.LogWarning(ex, "Xung đột đồng thời khi từ chối phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi từ chối phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail("Đã xảy ra lỗi khi từ chối. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // CANCEL
        // ========================================================
        /// <summary>
        /// NGHIỆP VỤ: Hủy bỏ phiếu kiểm kê đang lập dở. Chỉ được thực hiện khi phiếu đang ở trạng thái Nháp (Draft).
        /// </summary>
        public async Task<ApiResult<bool>> CancelAsync(int checkId, Guid employeeId, bool isAdmin)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (check.Status != (byte)InventoryCheckStatus.Draft)
                return ApiResult<bool>.Fail("Chỉ có thể hủy phiếu ở trạng thái Nháp.");

            if (!isAdmin && check.EmployeeId != employeeId)
                return ApiResult<bool>.Fail("Bạn không có quyền hủy phiếu này.");

            check.Status = (byte)InventoryCheckStatus.Cancelled;
            await _checkRepo.SaveChangesAsync();

            return ApiResult<bool>.Ok(true, "Đã hủy phiếu kiểm kê.");
        }

        // ========================================================
        // PRIVATE HELPERS
        // ========================================================

        private async Task<string> GenerateCheckCodeAsync()
        {
            return await _codeGenerator.NextAsync(DocumentCodeKind.InventoryCheck);
        }

        private async Task<List<int>> GetVariantIdsInScopeAsync(byte scopeType, int? scopeCategoryId)
        {
            if (scopeType == (byte)InventoryCheckScopeType.AllStore)
            {
                return await _context.ProductVariants
                    .AsNoTracking()
                    .Where(v => !v.IsDeleted)
                    .Select(v => v.Id)
                    .ToListAsync();
            }
            else
            {
                // ScopeType = Category: lấy cây con
                var categoryIds = await _productRepo.GetCategoryChildIdsAsync(scopeCategoryId!.Value);
                categoryIds.Add(scopeCategoryId.Value);

                return await _context.ProductVariants
                    .AsNoTracking()
                    .Where(v => !v.IsDeleted && categoryIds.Contains(v.Product.CategoryId))
                    .Select(v => v.Id)
                    .ToListAsync();
            }
        }

        private async Task<decimal> GetSerialCostAsync(ProductSerial serial)
        {
            // Lấy giá vốn từ ImportReceiptDetail tương ứng
            var cost = await _context.ImportReceiptDetails
                .AsNoTracking()
                .Where(d => d.ReceiptId == serial.ImportReceiptId && d.VariantId == serial.VariantId)
                .Select(d => (decimal?)d.ImportPrice)
                .FirstOrDefaultAsync();
            return cost ?? 0m;
        }

        private async Task<InventoryCheckDto?> BuildCheckDtoAsync(int checkId)
        {
            var check = await _checkRepo.GetByIdWithDetailsAsync(checkId);
            if (check == null) return null;

            var employeeNames = await GetEmployeeNamesAsync(new List<Guid> { check.EmployeeId });
            var approverNames = check.ApprovedByEmployeeId.HasValue
                ? await GetEmployeeNamesAsync(new List<Guid> { check.ApprovedByEmployeeId.Value })
                : new Dictionary<Guid, string>();

            return new InventoryCheckDto
            {
                Id = check.Id,
                CheckCode = check.CheckCode,
                EmployeeId = check.EmployeeId,
                EmployeeName = employeeNames.GetValueOrDefault(check.EmployeeId, check.EmployeeId.ToString()),
                CheckDate = check.CheckDate,
                SnapshotAt = check.SnapshotAt,
                Status = check.Status,
                StatusName = GetStatusName(check.Status),
                ScopeType = check.ScopeType,
                ScopeTypeName = check.ScopeType == 0 ? "Toàn bộ kho" : "Theo danh mục",
                ScopeCategoryId = check.ScopeCategoryId,
                ScopeCategoryName = check.ScopeCategory?.Name,
                Note = check.Note,
                RejectReason = check.RejectReason,
                ApprovedByEmployeeId = check.ApprovedByEmployeeId,
                ApprovedByEmployeeName = check.ApprovedByEmployeeId.HasValue
                    ? approverNames.GetValueOrDefault(check.ApprovedByEmployeeId.Value, check.ApprovedByEmployeeId.Value.ToString())
                    : null,
                ApprovedAt = check.ApprovedAt,
                Details = check.Details.Select(d => new InventoryCheckDetailLineDto
                {
                    Id = d.Id,
                    VariantId = d.VariantId,
                    VariantName = d.Variant?.VariantName ?? string.Empty,
                    SKU = d.Variant?.SKU ?? string.Empty,
                    SystemQuantity = d.SystemQuantity,
                    ActualQuantity = d.ActualQuantity,
                    Difference = d.Difference,
                    MatchedQuantity = d.MatchedQuantity,
                    MissingQuantity = d.MissingQuantity,
                    SurplusQuantity = d.SurplusQuantity,
                    DefectiveQuantity = d.DefectiveQuantity,
                    Reason = d.Reason
                }).ToList()
            };
        }

        private async Task<InventoryCheckDashboardDto> BuildMiniDashboardAsync(int checkId, InventoryCheck check)
        {
            var counts = await _checkRepo.GetGroupedCountsByCheckAsync(checkId);
            var matched = counts.GetValueOrDefault((byte)InventoryScanStatus.Matched, 0);
            var missing = counts.GetValueOrDefault((byte)InventoryScanStatus.Missing, 0);
            var surplus = counts.GetValueOrDefault((byte)InventoryScanStatus.Surplus, 0);
            var unknown = counts.GetValueOrDefault((byte)InventoryScanStatus.UnknownSurplus, 0);
            var defective = counts.GetValueOrDefault((byte)InventoryScanStatus.Defective, 0);
            var pending = counts.GetValueOrDefault((byte)InventoryScanStatus.Pending, 0);

            var totalSystem = await _context.InventoryCheckDetails
                .AsNoTracking()
                .Where(d => d.CheckId == checkId)
                .SumAsync(d => d.SystemQuantity);

            return new InventoryCheckDashboardDto
            {
                CheckId = checkId,
                CheckCode = check.CheckCode,
                Status = check.Status,
                StatusName = GetStatusName(check.Status),
                SnapshotAt = check.SnapshotAt,
                TotalSystem = totalSystem,
                TotalScanned = matched + surplus + unknown + defective,
                MatchedCount = matched,
                MissingCount = missing + pending,
                SurplusCount = surplus,
                UnknownSurplusCount = unknown,
                DefectiveCount = defective
            };
        }

        private static InventoryCheckSerialDto MapSerialToDto(InventoryCheckDetailSerial s)
        {
            return new InventoryCheckSerialDto
            {
                Id = s.Id,
                SerialId = s.SerialId,
                SerialNumberRaw = s.SerialNumberRaw,
                VariantId = s.VariantId,
                VariantName = s.Variant?.VariantName,
                SKU = s.Variant?.SKU,
                OriginalStatus = s.OriginalStatus,
                OriginalStatusName = s.OriginalStatus.HasValue
                    ? ((SerialStatus)s.OriginalStatus.Value).ToString()
                    : null,
                ScanStatus = s.ScanStatus,
                ScanStatusName = GetScanStatusName(s.ScanStatus),
                ScannedAt = s.ScannedAt,
                Note = s.Note,
                ProposedActionNote = s.ProposedActionNote,
                ResolvedDuringApproval = s.ResolvedDuringApproval
            };
        }

        private async Task<Dictionary<Guid, string>> GetEmployeeNamesAsync(List<Guid> employeeIds)
        {
            if (!employeeIds.Any()) return new Dictionary<Guid, string>();
            return await _context.Users
                .AsNoTracking()
                .Where(u => employeeIds.Contains(u.Id))
                .Join(_context.UserProfiles,
                    u => u.Id,
                    p => p.UserId,
                    (u, p) => new { u.Id, FullName = p.FullName ?? u.UserName ?? u.Id.ToString() })
                .ToDictionaryAsync(x => x.Id, x => x.FullName);
        }

        private static string GetStatusName(byte status) => status switch
        {
            0 => "Nháp",
            1 => "Chờ duyệt",
            2 => "Đã hoàn tất",
            3 => "Đã hủy",
            _ => "Không xác định"
        };

        private static string GetScanStatusName(byte status) => status switch
        {
            0 => "Chờ quét",
            1 => "Khớp",
            2 => "Thiếu",
            3 => "Thừa",
            4 => "Thừa (Mã lạ)",
            5 => "Lỗi vật lý",
            _ => "Không xác định"
        };
    }
}
