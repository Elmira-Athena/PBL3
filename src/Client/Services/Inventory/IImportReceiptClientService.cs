using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Inventory
{
    public interface IImportReceiptClientService
    {
        Task<ApiResult<ImportReceiptDto>> CreateAsync(CreateImportReceiptRequest request);
        Task<ApiResult<PagedResult<ImportReceiptDto>>> GetListAsync(ImportReceiptFilterRequest filter, CancellationToken cancellationToken = default);
        Task<ApiResult<ImportReceiptDto>> GetByIdAsync(int id);
    }
}
