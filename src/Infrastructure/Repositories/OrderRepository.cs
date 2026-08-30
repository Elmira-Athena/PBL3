using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public OrderRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IQueryable<Order> GetQueryable()
        {
            return _dbContext.Orders.AsQueryable();
        }

        public async Task<Order?> GetByIdAsync(int id)
        {
            return await _dbContext.Orders.FindAsync(id);
        }

        public async Task<Order?> GetByIdWithDetailsAsync(int id)
        {
            return await _dbContext.Orders.AsNoTracking()
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Images)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.OrderSerials)
                        .ThenInclude(os => os.Serial)
                .Include(o => o.VoucherUsages)
                    .ThenInclude(vu => vu.Voucher)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        /// <summary>
        /// Load Order kèm Details/Serials WITH TRACKING — dùng cho các thao tác ghi (xuất kho).
        /// </summary>
        public async Task<Order?> GetByIdWithDetailsTrackedAsync(int id)
        {
            return await _dbContext.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.OrderSerials)
                        .ThenInclude(os => os.Serial)
                .Include(o => o.VoucherUsages)
                    .ThenInclude(vu => vu.Voucher)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);
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
            return await _dbContext.Orders
                .AsNoTracking()
                .Where(o => o.OrderCode.StartsWith(datePrefix))
                .Select(o => o.OrderCode)
                .ToListAsync();
        }

        public async Task AddAsync(Order order)
        {
            await _dbContext.Orders.AddAsync(order);
        }

        public async Task<List<Order>> GetDraftsByEmployeeAsync(Guid employeeId)
        {
            return await _dbContext.Orders
                .Where(o => o.Status == 6 && o.EmployeeId == employeeId) // 6: PosDraft
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<Dictionary<int, int>> GetActiveOrderQuantitiesByVariantIdsAsync(List<int> variantIds)
        {
            // Single query: JOIN Orders + OrderDetails
            // WHERE Order.Status IN (0, 1) AND OrderDetail.VariantId IN (@variantIds)
            // GROUP BY VariantId -> SUM(Quantity)
            return await _dbContext.OrderDetails
                .Where(od => (od.Order.Status == 0 || od.Order.Status == 1)
                             && variantIds.Contains(od.VariantId))
                .GroupBy(od => od.VariantId)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(od => od.Quantity));
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
