using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Service.ImportReceipts
{
    public interface IImportReceiptService
    {
        /// <summary>
        /// Tạo phiếu nhập kho mới. Logic nằm trong Transaction.
        /// </summary>
        Task<ApiResult<ImportReceiptDto>> CreateAsync(CreateImportReceiptRequest request);

        /// <summary>
        /// Lấy danh sách phiếu nhập kho có phân trang và tìm kiếm.
        /// </summary>
        Task<ApiResult<PagedResult<ImportReceiptDto>>> GetPagedListAsync(ImportReceiptFilterRequest filter);

        /// <summary>
        /// Lấy chi tiết phiếu nhập kho theo Id (bao gồm danh sách Serial).
        /// </summary>
        Task<ApiResult<ImportReceiptDto>> GetByIdAsync(int id);
    }
}
