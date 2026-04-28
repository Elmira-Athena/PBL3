using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Sale;

namespace Client.Services.Orders
{
    public interface IOrderClientService
    {
        Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id);
        Task<PagedResult<OrderSummaryResponse>> GetPagedOrdersAsync(OrderFilterRequest request);
        Task<ApiResult<bool>> CancelOrderAsync(int id, string cancelReason);
    }
}
