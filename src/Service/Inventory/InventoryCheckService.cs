using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.Enums;

namespace PBL3.Service.Inventory
{
    public class InventoryCheckService : IInventoryCheckService
    {
        private readonly IInventoryCheckRepository _checkRepo;
        private readonly IProductSerialRepository _serialRepo;
        private readonly IProductRepository _productRepo;
        private readonly IInventorySyncService _inventorySyncService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HushStoreDbContext _context;
        private readonly ILogger<InventoryCheckService> _logger;

        public InventoryCheckService(
            IInventoryCheckRepository checkRepo,
            IProductSerialRepository serialRepo,
            IProductRepository productRepo,
            IInventorySyncService inventorySyncService,
            IUnitOfWork unitOfWork,
            HushStoreDbContext context,
            ILogger<InventoryCheckService> logger)
        {
            _checkRepo = checkRepo;
            _serialRepo = serialRepo;
            _productRepo = productRepo;
            _inventorySyncService = inventorySyncService;
            _unitOfWork = unitOfWork;
            _context = context;
            _logger = logger;
        }

        // ========================================================
        // CREATE — Tạo phiếu + chốt snapshot
        // ========================================================
        public async Task<ApiResult<InventoryCheckDto>> CreateAsync(CreateInventoryCheckRequest request, Guid employeeId)
        {
            // Validate Category nếu ScopeType = 1
            if (request.ScopeType == (byte)InventoryCheckScopeType.Category)
            {
                if (!request.ScopeCategoryId.HasValue)
                    return ApiResult<InventoryCheckDto>.Fail("Phải chọn danh mục khi phạm vi kiểm kê là theo danh mục.");

                var categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == request.ScopeCategoryId.Value);
                if (!categoryExists)
                    return ApiResult<InventoryCheckDto>.Fail("Danh mục kiểm kê không tồn tại.");
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var checkCode = await GenerateCheckCodeAsync();

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

                // Lấy danh sách VariantId trong phạm vi
                var variantIds = await GetVariantIdsInScopeAsync(request.ScopeType, request.ScopeCategoryId);
                if (!variantIds.Any())
                {
                    await _unitOfWork.RollbackAsync();
                    return ApiResult<InventoryCheckDto>.Fail("Không có sản phẩm nào trong phạm vi kiểm kê.");
                }

                // Lấy tất cả serial Available trong phạm vi
                var availableSerials = await _serialRepo.GetAvailableSerialsBatchAsync(variantIds);

                // GROUP BY VariantId → tạo InventoryCheckDetail
                var detailMap = new Dictionary<int, InventoryCheckDetail>();
                foreach (var variantId in variantIds)
                {
                    var sysQty = availableSerials.Count(s => s.VariantId == variantId);
                    var detail = new InventoryCheckDetail
                    {
                        CheckId = check.Id,
                        VariantId = variantId,
                        SystemQuantity = sysQty,
                        ActualQuantity = 0,
                        MatchedQuantity = 0,
                        MissingQuantity = 0,
                        SurplusQuantity = 0,
                        DefectiveQuantity = 0
                    };
                    await _checkRepo.AddDetailAsync(detail);
                    detailMap[variantId] = detail;
                }

                await _unitOfWork.SaveChangesAsync(); // Lấy Detail.Id

                // Bulk insert InventoryCheckDetailSerial: 1 row / serial Available (Pending)
                var snapshotRows = availableSerials.Select(s => new InventoryCheckDetailSerial
                {
                    CheckId = check.Id,
                    DetailId = detailMap.TryGetValue(s.VariantId, out var d) ? d.Id : null,
                    VariantId = s.VariantId,
                    SerialId = s.SerialId,
                    SerialNumberRaw = s.SerialNumber,
                    OriginalStatus = (byte)SerialStatus.Available,
                    ScanStatus = (byte)InventoryScanStatus.Pending,
                    ScannedAt = null
                }).ToList();

                await _checkRepo.AddDetailSerialsAsync(snapshotRows);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                _logger.LogInformation(
                    "Tạo phiếu kiểm kê: {CheckCode}, Phạm vi: {ScopeType}, Snapshot: {Total} serials",
                    checkCode, request.ScopeType, availableSerials.Count);

                var dto = await BuildCheckDtoAsync(check.Id);
                return ApiResult<InventoryCheckDto>.Ok(dto!, "Tạo phiếu kiểm kê thành công.");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
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
        // SCAN SERIAL
        // ========================================================
        public async Task<ApiResult<ScanResultDto>> ScanSerialAsync(int checkId, ScanSerialRequest request, Guid employeeId)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<ScanResultDto>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (check.Status != (byte)InventoryCheckStatus.Draft)
                return ApiResult<ScanResultDto>.Fail("Chỉ có thể quét khi phiếu ở trạng thái Nháp.");

            var serialNumber = request.SerialNumber.Trim();

            // Chống quét trùng
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

            // ─── A3: Serial không tồn tại trong DB ───
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
                    ScanStatus = (byte)InventoryScanStatus.UnknownSurplus,
                    ScannedAt = now,
                    ScannedByEmployeeId = employeeId,
                    Note = "Serial không tồn tại trong hệ thống."
                };

                await _checkRepo.AddDetailSerialAsync(unknownRow);

                // Cập nhật SurplusQuantity trên Detail nếu biết variant
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
                    RequiresVariantInput = request.VariantIdForUnknown == null,
                    SerialNumberRaw = serialNumber,
                    ScanStatus = (byte)InventoryScanStatus.UnknownSurplus,
                    ScanStatusName = "Thừa (Mã lạ)",
                    Message = "Serial không tồn tại trong hệ thống. Đã ghi nhận là Thừa kiểm kê (Mã lạ).",
                    MiniDashboard = await BuildMiniDashboardAsync(checkId, check)
                });
            }

            // ─── Serial Available trong snapshot → Matched ───
            if (dbSerial.Status == (byte)SerialStatus.Available)
            {
                var pendingRow = await _checkRepo.GetPendingDetailSerialBySerialIdAsync(checkId, dbSerial.Id);

                if (pendingRow != null)
                {
                    // Serial nằm trong scope → Matched
                    pendingRow.ScanStatus = (byte)InventoryScanStatus.Matched;
                    pendingRow.ScannedAt = now;
                    pendingRow.ScannedByEmployeeId = employeeId;

                    // Cập nhật Detail counts
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
                    // Serial Available nhưng nằm ngoài phạm vi scope → Surplus
                    var surplusRow = new InventoryCheckDetailSerial
                    {
                        CheckId = checkId,
                        VariantId = dbSerial.VariantId,
                        SerialId = dbSerial.Id,
                        SerialNumberRaw = serialNumber,
                        OriginalStatus = (byte)SerialStatus.Available,
                        ScanStatus = (byte)InventoryScanStatus.Surplus,
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

            // ─── Serial tồn tại nhưng Status không phải Available (A1, A2) → Surplus ───
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
                ScanStatus = (byte)InventoryScanStatus.Surplus,
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
        // MARK DEFECTIVE
        // ========================================================
        public async Task<ApiResult<bool>> MarkDefectiveAsync(int checkId, int detailSerialId, Guid employeeId)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (check.Status != (byte)InventoryCheckStatus.Draft)
                return ApiResult<bool>.Fail("Chỉ có thể đánh dấu lỗi khi phiếu ở trạng thái Nháp.");

            var row = await _checkRepo.GetDetailSerialAsync(detailSerialId, withTracking: true);
            if (row == null || row.CheckId != checkId)
                return ApiResult<bool>.Fail("Không tìm thấy dòng Serial trong phiếu kiểm kê.");

            if (row.ScanStatus != (byte)InventoryScanStatus.Matched)
                return ApiResult<bool>.Fail("Chỉ có thể đánh dấu lỗi cho Serial đang ở trạng thái Khớp.");

            row.ScanStatus = (byte)InventoryScanStatus.Defective;

            if (row.DetailId.HasValue && row.VariantId.HasValue)
            {
                var detail = await _checkRepo.GetDetailByCheckAndVariantAsync(checkId, row.VariantId.Value, withTracking: true);
                if (detail != null)
                {
                    detail.MatchedQuantity--;
                    detail.DefectiveQuantity++;
                    // ActualQuantity thực tế: hàng lỗi vẫn được đếm vào actual
                }
            }

            await _checkRepo.SaveChangesAsync();
            return ApiResult<bool>.Ok(true, "Đã đánh dấu Serial là hàng lỗi vật lý.");
        }

        // ========================================================
        // UPDATE REASON
        // ========================================================
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
        // SUBMIT — Gửi duyệt
        // ========================================================
        public async Task<ApiResult<bool>> SubmitAsync(int checkId, Guid employeeId)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (check.Status != (byte)InventoryCheckStatus.Draft)
                return ApiResult<bool>.Fail("Chỉ có thể gửi duyệt khi phiếu ở trạng thái Nháp.");

            if (check.EmployeeId != employeeId)
                return ApiResult<bool>.Fail("Bạn không có quyền gửi duyệt phiếu này.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Chuyển tất cả row Pending → Missing
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

                // Cập nhật MissingQuantity trên Details
                foreach (var (detailId, missingCount) in detailMissingCounts)
                {
                    var detail = await _context.InventoryCheckDetails
                        .FirstOrDefaultAsync(d => d.Id == detailId);
                    if (detail != null)
                        detail.MissingQuantity += missingCount;
                }

                check.Status = (byte)InventoryCheckStatus.AwaitingApproval;
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                _logger.LogInformation(
                    "Gửi duyệt phiếu kiểm kê {CheckCode}: {MissingCount} serials thiếu",
                    check.CheckCode, pendingRows.Count);

                return ApiResult<bool>.Ok(true, "Đã gửi phiếu kiểm kê để phê duyệt thành công.");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi gửi duyệt phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail("Đã xảy ra lỗi khi gửi duyệt. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // APPROVE — Phê duyệt & cân bằng kho
        // ========================================================
        public async Task<ApiResult<bool>> ApproveAsync(int checkId, Guid adminId)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (check.Status != (byte)InventoryCheckStatus.AwaitingApproval)
                return ApiResult<bool>.Fail("Chỉ có thể phê duyệt phiếu ở trạng thái Chờ duyệt.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var adjustmentLogs = new List<InventoryAdjustmentLog>();
                var affectedVariantIds = new HashSet<int>();

                // ── Xử lý Missing rows → Lost ──
                var missingRows = await _checkRepo.GetMissingDetailSerialsWithSerialAsync(checkId);
                foreach (var row in missingRows)
                {
                    if (row.Serial == null) continue;

                    var currentStatus = row.Serial.Status;

                    // BR1: kiểm tra lại trạng thái hiện tại — chỉ mark Lost nếu vẫn Available
                    if (currentStatus == (byte)SerialStatus.Available)
                    {
                        // Lấy giá vốn snapshot từ ImportReceiptDetail
                        var costImpact = await GetSerialCostAsync(row.Serial);

                        row.Serial.Status = (byte)SerialStatus.Lost;
                        affectedVariantIds.Add(row.Serial.VariantId);

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
                        // Serial đã bán/giữ chỗ trong cửa sổ kiểm kê — không mark Lost
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

                // ── Xử lý Defective rows → Defective ──
                var defectiveRows = await _checkRepo.GetDefectiveDetailSerialsWithSerialAsync(checkId);
                foreach (var row in defectiveRows)
                {
                    if (row.Serial == null) continue;

                    var currentStatus = row.Serial.Status;

                    if (currentStatus == (byte)SerialStatus.Available)
                    {
                        var costImpact = await GetSerialCostAsync(row.Serial);

                        row.Serial.Status = (byte)SerialStatus.Defective;
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

                // Lưu adjustment logs
                if (adjustmentLogs.Any())
                    await _checkRepo.AddAdjustmentLogsAsync(adjustmentLogs);

                // Cập nhật phiếu
                check.Status = (byte)InventoryCheckStatus.Completed;
                check.ApprovedByEmployeeId = adminId;
                check.ApprovedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                // Đồng bộ StockQuantity
                if (affectedVariantIds.Any())
                    await _inventorySyncService.SyncStockBatchAsync(affectedVariantIds);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation(
                    "Phê duyệt phiếu kiểm kê {CheckCode}: {Lost} lost, {Defective} defective. Admin: {AdminId}",
                    check.CheckCode,
                    adjustmentLogs.Count(l => l.AdjustmentType == (byte)InventoryAdjustmentType.Lost),
                    adjustmentLogs.Count(l => l.AdjustmentType == (byte)InventoryAdjustmentType.Defective),
                    adminId);

                return ApiResult<bool>.Ok(true, "Phê duyệt và cân bằng kho thành công.");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi phê duyệt phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail("Đã xảy ra lỗi khi phê duyệt. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // REJECT
        // ========================================================
        public async Task<ApiResult<bool>> RejectAsync(int checkId, RejectInventoryCheckRequest request, Guid adminId)
        {
            var check = await _checkRepo.GetByIdAsync(checkId);
            if (check == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiếu kiểm kê yêu cầu.");

            if (check.Status != (byte)InventoryCheckStatus.AwaitingApproval)
                return ApiResult<bool>.Fail("Chỉ có thể từ chối phiếu ở trạng thái Chờ duyệt.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                check.RejectReason = request.Reason.Trim();

                if (request.ReturnToDraft)
                {
                    // Trả về Draft: chuyển Missing → Pending, xóa Surplus/UnknownSurplus
                    var missingRows = await _checkRepo.GetPendingDetailSerialsAsync(checkId);
                    // Lấy Missing rows (sau submit chúng là Missing, không phải Pending)
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

                    // Xóa Surplus và UnknownSurplus
                    var surplusRows = await _checkRepo.GetSurplusDetailSerialsAsync(checkId);
                    await _checkRepo.RemoveDetailSerialsAsync(surplusRows);

                    // Reset counts trên all Details
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

                    // Reset Matched rows (Defective cũng reset về Matched? Không — giữ nguyên)
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

                    check.Status = (byte)InventoryCheckStatus.Draft;
                }
                else
                {
                    // Cancelled: chỉ đổi trạng thái, giữ dữ liệu
                    check.Status = (byte)InventoryCheckStatus.Cancelled;
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                var action = request.ReturnToDraft ? "trả về Nháp" : "hủy";
                _logger.LogInformation(
                    "Từ chối phiếu kiểm kê {CheckCode} ({Action}). Admin: {AdminId}. Lý do: {Reason}",
                    check.CheckCode, action, adminId, request.Reason);

                return ApiResult<bool>.Ok(true, $"Đã từ chối và {action} phiếu kiểm kê.");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi từ chối phiếu kiểm kê {CheckId}.", checkId);
                return ApiResult<bool>.Fail("Đã xảy ra lỗi khi từ chối. Vui lòng thử lại.");
            }
        }

        // ========================================================
        // CANCEL
        // ========================================================
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
            var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"KK-{dateStr}-";
            var lastCode = await _checkRepo.GetLastCheckCodeByDateAsync(prefix);

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                var lastPart = lastCode.Substring(prefix.Length);
                if (int.TryParse(lastPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }
            return $"{prefix}{nextNumber:D3}";
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
