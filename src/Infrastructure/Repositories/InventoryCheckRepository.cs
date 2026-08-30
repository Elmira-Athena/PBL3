using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class InventoryCheckRepository : IInventoryCheckRepository
    {
        private readonly HushStoreDbContext _context;

        public InventoryCheckRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryCheck?> GetByIdAsync(int id)
        {
            return await _context.InventoryChecks
                .Include(c => c.Details)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<InventoryCheck?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.InventoryChecks
                .AsNoTracking()
                .Include(c => c.ScopeCategory)
                .Include(c => c.Details)
                    .ThenInclude(d => d.Variant)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<(List<InventoryCheck> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            byte? status,
            DateTime? fromDate,
            DateTime? toDate,
            Guid? employeeId,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _context.InventoryChecks
                .AsNoTracking()
                .Include(c => c.ScopeCategory)
                .Include(c => c.Details)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(c => c.CheckCode.ToLower().Contains(kw) ||
                                        (c.Note != null && c.Note.ToLower().Contains(kw)));
            }

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            if (fromDate.HasValue)
                query = query.Where(c => c.CheckDate >= fromDate.Value.ToUniversalTime());

            if (toDate.HasValue)
                query = query.Where(c => c.CheckDate < toDate.Value.AddDays(1).ToUniversalTime());

            if (employeeId.HasValue)
                query = query.Where(c => c.EmployeeId == employeeId.Value);

            var totalCount = await query.CountAsync();

            query = sortBy?.ToLower() switch
            {
                "code" => sortDescending ? query.OrderByDescending(c => c.CheckCode) : query.OrderBy(c => c.CheckCode),
                "status" => sortDescending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
                _ => sortDescending ? query.OrderByDescending(c => c.CheckDate) : query.OrderBy(c => c.CheckDate)
            };

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        /// <summary>
        /// Trả về TẤT CẢ mã chứng từ trong ngày khớp tiền tố, để
        /// <c>DocumentCodeGenerator</c> tự lấy max theo SỐ.
        /// </summary>
        /// <remarks>
        /// Không dùng <c>OrderByDescending(Code).First()</c> nữa: đó là so sánh CHUỖI,
        /// nên khi hai độ rộng số cùng tồn tại ("-001" cũ và "-000002" mới) thì mã cũ
        /// luôn sắp trên => luôn trả về mã cũ => sinh mã trùng vĩnh viễn.
        /// Số chứng từ mỗi ngày là hữu hạn và nhỏ, nên nạp về RAM rồi so sánh số là an toàn.
        /// </remarks>
        public async Task<List<string>> GetCodesByDatePrefixAsync(string datePrefix)
        {
            return await _context.InventoryChecks
                .AsNoTracking().IgnoreQueryFilters()
                .Where(c => c.CheckCode.StartsWith(datePrefix))
                .Select(c => c.CheckCode)
                .ToListAsync();
        }

        public async Task<InventoryCheckDetailSerial?> GetDetailSerialAsync(int detailSerialId, bool withTracking = false)
        {
            var query = _context.InventoryCheckDetailSerials.AsQueryable();
            if (!withTracking)
                query = query.AsNoTracking();

            return await query
                .Include(s => s.Variant)
                .FirstOrDefaultAsync(s => s.Id == detailSerialId);
        }

        public async Task<InventoryCheckDetailSerial?> GetPendingDetailSerialBySerialIdAsync(int checkId, int serialId)
        {
            return await _context.InventoryCheckDetailSerials
                .FirstOrDefaultAsync(s => s.CheckId == checkId &&
                                          s.SerialId == serialId &&
                                          s.ScanStatus == 0); // Pending
        }

        public async Task<bool> IsSerialAlreadyScannedAsync(int checkId, string serialNumberRaw)
        {
            return await _context.InventoryCheckDetailSerials
                .AnyAsync(s => s.CheckId == checkId &&
                               s.SerialNumberRaw == serialNumberRaw &&
                               s.ScanStatus != 0); // Chỉ tính là trùng nếu đã thực sự được quét (không phải Pending)
        }

        public async Task<List<InventoryCheckDetailSerial>> GetPendingDetailSerialsAsync(int checkId)
        {
            return await _context.InventoryCheckDetailSerials
                .Where(s => s.CheckId == checkId && s.ScanStatus == 0) // Pending
                .Include(s => s.Detail)
                .ToListAsync();
        }

        public async Task<List<InventoryCheckDetailSerial>> GetSurplusDetailSerialsAsync(int checkId)
        {
            return await _context.InventoryCheckDetailSerials
                .Where(s => s.CheckId == checkId &&
                            (s.ScanStatus == 3 || s.ScanStatus == 4)) // Surplus, UnknownSurplus
                .ToListAsync();
        }

        public async Task<(List<InventoryCheckDetailSerial> Items, int TotalCount)> GetDetailSerialsPagedAsync(
            int checkId,
            byte? scanStatus,
            int? variantId,
            int pageNumber,
            int pageSize)
        {
            var query = _context.InventoryCheckDetailSerials
                .AsNoTracking()
                .Where(s => s.CheckId == checkId)
                .Include(s => s.Variant)
                .AsQueryable();

            if (scanStatus.HasValue)
                query = query.Where(s => s.ScanStatus == scanStatus.Value);

            if (variantId.HasValue)
                query = query.Where(s => s.VariantId == variantId.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(s => s.ScanStatus)
                .ThenBy(s => s.SerialNumberRaw)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Dictionary<byte, int>> GetGroupedCountsByCheckAsync(int checkId)
        {
            return await _context.InventoryCheckDetailSerials
                .AsNoTracking()
                .Where(s => s.CheckId == checkId)
                .GroupBy(s => s.ScanStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);
        }

        public async Task<InventoryCheckDetail?> GetDetailByCheckAndVariantAsync(int checkId, int variantId, bool withTracking = false)
        {
            var query = _context.InventoryCheckDetails.AsQueryable();
            if (!withTracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(d => d.CheckId == checkId && d.VariantId == variantId);
        }

        public async Task<List<InventoryCheckDetailSerial>> GetMissingDetailSerialsWithSerialAsync(int checkId)
        {
            return await _context.InventoryCheckDetailSerials
                .Where(s => s.CheckId == checkId && s.ScanStatus == 2) // Missing
                .Include(s => s.Serial)
                    .ThenInclude(ps => ps!.ImportReceipt)
                        .ThenInclude(r => r.Details)
                .ToListAsync();
        }

        public async Task<List<InventoryCheckDetailSerial>> GetDefectiveDetailSerialsWithSerialAsync(int checkId)
        {
            return await _context.InventoryCheckDetailSerials
                .Where(s => s.CheckId == checkId && s.ScanStatus == 5) // Defective
                .Include(s => s.Serial)
                    .ThenInclude(ps => ps!.ImportReceipt)
                        .ThenInclude(r => r.Details)
                .ToListAsync();
        }

        public async Task AddAsync(InventoryCheck check)
        {
            _context.InventoryChecks.Add(check);
            await Task.CompletedTask;
        }

        public async Task AddDetailAsync(InventoryCheckDetail detail)
        {
            _context.InventoryCheckDetails.Add(detail);
            await Task.CompletedTask;
        }

        public async Task AddDetailSerialAsync(InventoryCheckDetailSerial detailSerial)
        {
            _context.InventoryCheckDetailSerials.Add(detailSerial);
            await Task.CompletedTask;
        }

        public async Task AddDetailSerialsAsync(IEnumerable<InventoryCheckDetailSerial> detailSerials)
        {
            await _context.InventoryCheckDetailSerials.AddRangeAsync(detailSerials);
        }

        public async Task AddAdjustmentLogsAsync(IEnumerable<InventoryAdjustmentLog> logs)
        {
            await _context.InventoryAdjustmentLogs.AddRangeAsync(logs);
        }

        public async Task RemoveDetailSerialsAsync(IEnumerable<InventoryCheckDetailSerial> serials)
        {
            _context.InventoryCheckDetailSerials.RemoveRange(serials);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
