using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Manufacturer (Hãng sản xuất).
    /// </summary>
    public interface IManufacturerRepository
    {
        /// <summary>
        /// Lấy danh sách hãng sản xuất có phân trang và tìm kiếm.
        /// </summary>
        Task<(List<Manufacturer> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết hãng sản xuất theo Id.
        /// </summary>
        Task<Manufacturer?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy danh sách tất cả hãng (Id + Name) dùng cho dropdown.
        /// </summary>
        Task<List<Manufacturer>> GetAllActiveAsync();

        /// <summary>
        /// Kiểm tra hãng có sản phẩm nào đang active không (dùng cho logic xoá).
        /// </summary>
        Task<bool> HasProductsAsync(int manufacturerId);

        /// <summary>
        /// Kiểm tra tên hãng đã tồn tại chưa (tránh trùng lặp).
        /// </summary>
        Task<bool> IsDuplicateNameAsync(string name, int? excludeId = null);

        Task AddAsync(Manufacturer manufacturer);
        Task SaveChangesAsync();
    }

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
        /// Lấy danh sách CategoryId (distinct) của các sản phẩm chứa Variant trong danh sách.
        /// Dùng để kiểm tra điều kiện danh mục áp dụng của voucher trong checkout.
        /// </summary>
        Task<List<int>> GetCategoryIdsByVariantIdsAsync(List<int> variantIds);

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
        Task ReplaceProductImagesAsync(int productId, List<ProductImage> newImages);
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
        
        /// <summary>
        /// Đếm số Serial Available (Status=0) theo danh sách VariantId — batch query.
        /// </summary>
        Task<Dictionary<int, int>> CountAvailableByVariantIdsAsync(List<int> variantIds);
        
        Task SaveChangesAsync();

        /// <summary>
        /// Danh sách phân trang ProductSerial với bộ lọc đa điều kiện.
        /// </summary>
        Task<(List<ProductSerial> Items, int TotalCount)> GetPagedListAsync(
            string? keyword, int? productId, int? variantId,
            byte? status, DateTime? fromDate, DateTime? toDate,
            int pageNumber, int pageSize, string? sortBy, bool sortDescending);

        /// <summary>
        /// Đếm số Serial theo trạng thái (GROUP BY Status). Có thể lọc theo productId hoặc variantId.
        /// </summary>
        Task<Dictionary<byte, int>> GetStatusCountsAsync(int? productId, int? variantId);

        /// <summary>
        /// Lấy chi tiết Serial kèm Variant, Product, ImportReceipt, Supplier (read-only).
        /// </summary>
        Task<ProductSerial?> GetByIdWithDetailsAsync(int id);
    }

    /// <summary>
    /// Repository interface cho Voucher & VoucherUsage.
    /// </summary>
    public interface IVoucherRepository
    {
        // ==================== MANAGEMENT CRUD ====================

        /// <summary>
        /// Lấy danh sách voucher phân trang, hỗ trợ lọc theo keyword, trạng thái, date range.
        /// </summary>
        Task<(List<Voucher> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            bool? isActive,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy voucher theo Id, bao gồm VoucherCategories. Có tracking để update.
        /// </summary>
        Task<Voucher?> GetByIdWithCategoriesAsync(int id);

        /// <summary>
        /// Lấy voucher theo Id, không tracking. Dùng cho read-only operations.
        /// </summary>
        Task<Voucher?> GetByIdNoTrackingAsync(int id);

        /// <summary>
        /// Kiểm tra mã Code đã tồn tại chưa (excludeId bỏ qua chính nó khi update).
        /// </summary>
        Task<bool> IsDuplicateCodeAsync(string code, int? excludeId = null);

        /// <summary>
        /// Thêm voucher mới vào context.
        /// </summary>
        Task AddAsync(Voucher voucher);

        // ==================== CHECKOUT USAGE ====================

        /// <summary>
        /// Lấy danh sách Voucher theo danh sách mã Code, bao gồm VoucherCategories.
        /// Dùng cho checkout với category restriction check.
        /// </summary>
        Task<List<Voucher>> GetByCodesWithCategoriesAsync(List<string> codes);

        /// <summary>
        /// Lấy danh sách Voucher theo danh sách mã Code (không include categories).
        /// </summary>
        Task<List<Voucher>> GetByCodesAsync(List<string> codes);

        /// <summary>
        /// Đếm số lần user đã dùng mỗi voucher trong danh sách.
        /// Trả về Dictionary(VoucherId → số lần dùng).
        /// Dùng thay GetUsedVoucherIdsByUserAsync để hỗ trợ MaxUsesPerUser.
        /// </summary>
        Task<Dictionary<int, int>> GetUserVoucherUsageCountsAsync(Guid userId, List<int> voucherIds);

        /// <summary>
        /// Kiểm tra danh sách cặp (UserId, VoucherId) đã tồn tại trong VoucherUsages chưa.
        /// Trả về danh sách VoucherId mà User này đã dùng.
        /// </summary>
        Task<List<int>> GetUsedVoucherIdsByUserAsync(Guid userId, List<int> voucherIds);

        /// <summary>
        /// Thêm danh sách VoucherUsage vào context.
        /// </summary>
        Task AddUsagesAsync(IEnumerable<VoucherUsage> usages);

        /// <summary>
        /// Lấy tất cả voucher active, trong thời hạn hiệu lực, chưa hết số lượng.
        /// Dùng cho popup chọn voucher ở trang Checkout.
        /// </summary>
        Task<List<Voucher>> GetActiveVouchersForCustomerAsync();

        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Order.
    /// </summary>
    public interface IOrderRepository
    {
        IQueryable<Order> GetQueryable();
        Task<Order?> GetByIdAsync(int id);
        Task<Order?> GetByIdWithDetailsAsync(int id);
        Task<string?> GetLastOrderCodeByDateAsync(string datePrefix);
        
        /// <summary>
        /// Lấy danh sách các đơn POS đang lưu nháp bởi một nhân viên.
        /// </summary>
        Task<List<Order>> GetDraftsByEmployeeAsync(Guid employeeId);
        
        /// <summary>
        /// Tính tổng Quantity đã đặt theo VariantId cho các đơn Active (Status 0 hoặc 1).
        /// Dùng cho Virtual Inventory Hold — 1 query batch, không N+1.
        /// </summary>
        Task<Dictionary<int, int>> GetActiveOrderQuantitiesByVariantIdsAsync(List<int> variantIds);
        
        Task AddAsync(Order order);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Warranty.
    /// </summary>
    public interface IWarrantyRepository
    {
        /// <summary>
        /// Lấy danh sách bảo hành active (status != Claimed) của một serial, sắp xếp theo EndDate giảm dần.
        /// </summary>
        Task<List<Warranty>> GetActiveBySerialIdAsync(int serialId);

        /// <summary>
        /// Lấy bảo hành theo Id, có tracking để update.
        /// </summary>
        Task<Warranty?> GetByIdWithTrackingAsync(int id);

        Task AddAsync(Warranty warranty);
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
        /// Kiểm tra xem người dùng có đơn hàng nào chưa hoàn tất không.
        /// (0: Pending, 1: Confirmed, 2: Shipping)
        /// </summary>
        Task<bool> HasPendingOrdersAsync(Guid userId);
    }

    /// <summary>
    /// Repository interface cho Employee (Type = 1).
    /// </summary>
    public interface IEmployeeRepository
    {
        Task<(List<AppUser> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            bool? isActive,
            byte? gender,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        Task<AppUser?> GetByIdWithProfileAsync(Guid id);
    }

    /// <summary>
    /// Repository interface cho Cart.
    /// </summary>
    public interface ICartRepository
    {
        Task<List<Cart>> GetCartItemsByUserAsync(Guid userId);
        Task<List<Cart>> GetCartItemsWithTrackingAsync(Guid userId);

        /// <summary>
        /// Lấy 1 item trong giỏ theo Id (WITH TRACKING để update/delete).
        /// </summary>
        Task<Cart?> GetCartItemAsync(int cartItemId, Guid userId);

        /// <summary>
        /// Tìm item trong giỏ theo UserId + VariantId (WITH TRACKING để cộng dồn Quantity).
        /// </summary>
        Task<Cart?> FindByUserAndVariantAsync(Guid userId, int variantId);

        Task AddAsync(Cart cart);
        void Remove(Cart cart);
        void RemoveRange(IEnumerable<Cart> carts);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho UserAddress.
    /// </summary>
    public interface IUserAddressRepository
    {
        Task<UserAddress?> GetByIdAsync(int id);
        Task<List<UserAddress>> GetByUserIdAsync(Guid userId);
        Task AddAsync(UserAddress address);
        Task ClearUserDefaultsAsync(Guid userId);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho ProductReview (Đánh giá sản phẩm).
    /// </summary>
    public interface IProductReviewRepository
    {
        /// <summary>
        /// Lấy danh sách đánh giá theo productId có phân trang, sắp xếp mới nhất trước.
        /// </summary>
        Task<(List<ProductReview> Items, int TotalCount)> GetPagedByProductIdAsync(
            int productId, int pageNumber, int pageSize);

        /// <summary>
        /// Kiểm tra khách hàng đã đánh giá sản phẩm này chưa.
        /// </summary>
        Task<bool> ExistsAsync(int productId, Guid userId);

        Task<ProductReview?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy đánh giá theo Id (WITH TRACKING để soft-delete).
        /// </summary>
        Task<ProductReview?> GetByIdWithTrackingAsync(int id);

        Task AddAsync(ProductReview review);
        Task SaveChangesAsync();
    }
}
