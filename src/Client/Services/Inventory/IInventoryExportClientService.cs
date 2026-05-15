using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace Client.Services.Inventory
{
    public interface IInventoryExportClientService
    {
        Task<ApiResult<bool>> ExportOrderAsync(ExportOrderRequest request);
        Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId);
    }
}
