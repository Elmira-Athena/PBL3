using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace PBL3.Service.Inventory
{
    public interface IInventoryExportService
    {
        /// <summary>
        /// Xử lý nghiệp vụ xuất kho cho đơn hàng (UC012).
        /// Gắn Serial vật lý vào đơn hàng Online và chuyển trạng thái đơn hàng.
        /// </summary>
        Task<ApiResult<bool>> ExportOrderAsync(ExportOrderRequest request);
    }
}
