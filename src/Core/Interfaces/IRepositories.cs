using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Category.
    /// </summary>
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllActiveAsync();
        Task<Category?> GetByIdAsync(int id, bool includeParent = false);
        Task<bool> IsDuplicateNameAsync(int? parentId, string name, int? excludeId = null);
        Task<bool> IsDuplicateSlugAsync(string slug, int? excludeId = null);
        Task<bool> HasActiveChildrenAsync(int id);
        Task<bool> HasProductsAsync(int id);
        Task<Dictionary<int, int?>> GetAllCategoryParentMapAsync();
        Task<List<Category>> GetChildrenAsync(int parentId);
        Task AddAsync(Category category);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Product.
    /// </summary>
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByIdWithDetailsAsync(int id);
        Task<bool> IsSkuExistsAsync(string sku, int? excludeVariantId = null);
        Task<bool> ManufacturerExistsAsync(int manufacturerId);
        Task<bool> CategoryExistsAsync(int categoryId);

        /// <summary>
        /// Lấy danh sách Id category con (đệ quy) từ một category cha.
        /// </summary>
        Task<List<int>> GetCategoryChildIdsAsync(int categoryId);

        /// <summary>
        /// Lấy danh sách sản phẩm có phân trang và bộ lọc.
        /// Trả về (items, totalCount).
        /// </summary>
        Task<(List<Product> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            List<int>? categoryIds,
            int? manufacturerId,
            decimal? priceMin,
            decimal? priceMax,
            int? status,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy danh sách VariantId tồn tại (chưa bị xoá) từ danh sách Id.
        /// </summary>
        Task<List<int>> GetExistingVariantIdsAsync(List<int> variantIds);

        /// <summary>
        /// Lọc ProductVariant theo thông số kỹ thuật JSON.
        /// </summary>
        Task<List<ProductVariant>> FilterBySpecificationAsync(string specKey, string specValue);

        /// <summary>
        /// Lấy Variant theo Id (có tracking để update).
        /// </summary>
        Task<ProductVariant?> GetVariantByIdAsync(int variantId);

        Task AddAsync(Product product);
        Task AddVariantAsync(ProductVariant variant);
        Task RemoveVariant(ProductVariant variant);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Supplier.
    /// </summary>
    public interface ISupplierRepository
    {
        /// <summary>
        /// Lấy danh sách nhà cung cấp có phân trang và tìm kiếm.
        /// </summary>
        Task<(List<Supplier> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết nhà cung cấp theo Id.
        /// </summary>
        Task<Supplier?> GetByIdAsync(int id);

        /// <summary>
        /// Kiểm tra nhà cung cấp có phiếu nhập kho nào không (dùng cho logic xoá).
        /// </summary>
        Task<bool> HasImportReceiptsAsync(int supplierId);

        Task AddAsync(Supplier supplier);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho ImportReceipt.
    /// </summary>
    public interface IImportReceiptRepository
    {
        /// <summary>
        /// Lấy danh sách phiếu nhập có phân trang, tìm kiếm và Include Supplier.
        /// </summary>
        Task<(List<ImportReceipt> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết phiếu nhập theo Id, bao gồm Details, Variant, và ProductSerials.
        /// </summary>
        Task<ImportReceipt?> GetByIdWithDetailsAsync(int id);

        /// <summary>
        /// Lấy mã phiếu nhập cuối cùng theo ngày (để sinh mã tự động).
        /// </summary>
        Task<string?> GetLastReceiptCodeByDateAsync(string datePrefix);

        Task AddAsync(ImportReceipt receipt);
        Task AddDetailAsync(ImportReceiptDetail detail);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho ProductSerial.
    /// </summary>
    public interface IProductSerialRepository
    {
        /// <summary>
        /// Kiểm tra danh sách Serial đã tồn tại trong DB chưa.
        /// Trả về danh sách Serial bị trùng.
        /// </summary>
        Task<List<string>> GetExistingSerialsAsync(List<string> serialNumbers);

        /// <summary>
        /// Kiểm tra một Serial Number đã tồn tại trong DB chưa (theo VariantId).
        /// Dùng cho check real-time khi quét mã vạch.
        /// </summary>
        Task<bool> ExistsAsync(string serialNumber, int variantId);

        /// <summary>
        /// Lấy danh sách SerialNumber theo ReceiptId và VariantId.
        /// </summary>
        Task<List<string>> GetSerialsByReceiptAndVariantAsync(int receiptId, int variantId);

        Task AddRangeAsync(IEnumerable<ProductSerial> serials);

        /// <summary>
        /// Lấy chi tiết Serial kèm Variant và Product.
        /// </summary>
        Task<ProductSerial?> GetBySerialNumberAsync(string serialNumber);

        /// <summary>
        /// Lấy danh sách ProductSerial theo SerialNumbers (WITH TRACKING để update).
        /// Dùng cho luồng xuất kho — cần ghi nhận trạng thái Sold.
        /// </summary>
        Task<List<ProductSerial>> GetSerialsWithTrackingAsync(List<string> serialNumbers);

        /// <summary>
        /// Lấy chi tiết Serial theo Id (WITH TRACKING để update).
        /// </summary>
        Task<ProductSerial?> GetByIdWithTrackingAsync(int id);

        /// <summary>
        /// Lấy N Serials đang Available của một Variant (dùng cho hàng generic khi checkout).
        /// </summary>
        Task<List<ProductSerial>> GetAvailableSerialsByVariantAsync(int variantId, int count);
        
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Voucher & VoucherUsage.
    /// </summary>
    public interface IVoucherRepository
    {
        /// <summary>
        /// Lấy danh sách Voucher theo danh sách mã Code.
        /// Dùng 1 query duy nhất bằng WHERE IN để tránh N+1.
        /// </summary>
        Task<List<Voucher>> GetByCodesAsync(List<string> codes);

        /// <summary>
        /// Kiểm tra danh sách cặp (UserId, VoucherId) đã tồn tại trong VoucherUsages chưa.
        /// Trả về danh sách VoucherId mà User này đã dùng.
        /// Batch query — 1 lần duy nhất, không loop.
        /// </summary>
        Task<List<int>> GetUsedVoucherIdsByUserAsync(Guid userId, List<int> voucherIds);

        /// <summary>
        /// Thêm danh sách VoucherUsage vào context.
        /// </summary>
        Task AddUsagesAsync(IEnumerable<VoucherUsage> usages);

        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Order.
    /// </summary>
    public interface IOrderRepository
    {
        Task<Order?> GetByIdWithDetailsAsync(int id);
        Task<string?> GetLastOrderCodeByDateAsync(string datePrefix);
        
        /// <summary>
        /// Lấy danh sách các đơn POS đang lưu nháp bởi một nhân viên.
        /// </summary>
        Task<List<Order>> GetDraftsByEmployeeAsync(Guid employeeId);
        
        Task AddAsync(Order order);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Warranty.
    /// </summary>
    public interface IWarrantyRepository
    {
        Task AddRangeAsync(IEnumerable<Warranty> warranties);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Customer (Quản lý User).
    /// </summary>
    public interface ICustomerRepository
    {
        /// <summary>
        /// Lấy danh sách khách hàng có phân trang và bộ lọc.
        /// </summary>
        Task<(List<AppUser> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            bool? isActive,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết khách hàng và profile.
        /// </summary>
        Task<AppUser?> GetByIdWithProfileAsync(Guid id);

        /// <summary>
        /// Lấy danh sách N đơn hàng mới nhất của người dùng.
        /// </summary>
        Task<List<Order>> GetRecentOrdersAsync(Guid userId, int count);

        /// <summary>
        /// Lấy danh sách thiết bị/sản phẩm trong giỏ hàng.
        /// </summary>
        Task<List<Cart>> GetCartItemsAsync(Guid userId);

        /// <summary>
        /// Kiểm tra xem người dùng có đơn hàng nào chưa hoàn tất không.
        /// (0: Pending, 1: Confirmed, 2: Shipping)
        /// </summary>
        Task<bool> HasPendingOrdersAsync(Guid userId);
    }
}
