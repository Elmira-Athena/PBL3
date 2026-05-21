using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace Client.Services.Inventory
{
    public interface IInventoryCheckClientService
    {
        Task<ApiResult<InventoryCheckDto>> CreateAsync(CreateInventoryCheckRequest request);
        Task<ApiResult<PagedResult<InventoryCheckListItemDto>>> GetListAsync(InventoryCheckFilterRequest filter, CancellationToken cancellationToken = default);
        Task<ApiResult<InventoryCheckDto>> GetByIdAsync(int id);
        Task<ApiResult<InventoryCheckDashboardDto>> GetDashboardAsync(int id);
        Task<ApiResult<PagedResult<InventoryCheckSerialDto>>> GetSerialsAsync(int checkId, InventoryCheckSerialFilterRequest filter, CancellationToken cancellationToken = default);
        Task<ApiResult<ScanResultDto>> ScanSerialAsync(int checkId, ScanSerialRequest request);
        Task<ApiResult<bool>> MarkDefectiveAsync(int checkId, int detailSerialId);
        Task<ApiResult<bool>> UpdateReasonAsync(int checkId, int detailSerialId, UpdateScanReasonRequest request);
        Task<ApiResult<bool>> SubmitAsync(int checkId);
        Task<ApiResult<bool>> ApproveAsync(int checkId);
        Task<ApiResult<bool>> RejectAsync(int checkId, RejectInventoryCheckRequest request);
        Task<ApiResult<bool>> CancelAsync(int checkId);
    }
}
