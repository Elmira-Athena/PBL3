using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Sale;

namespace Client.Services.Orders
{
    public interface IOrderClientService
    {
        Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id);
        Task<PagedResult<OrderSummaryResponse>> GetPagedOrdersAsync(OrderFilterRequest request);
        Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetMyOrdersAsync(OrderFilterRequest request);
        Task<ApiResult<bool>> CancelOrderAsync(int id, string cancelReason);
        Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request);
        Task<ApiResult<bool>> CompleteOrderAsync(int id);
        Task<ApiResult<bool>> ConfirmOrderAsync(int id);

        // Customer self-service
        Task<ApiResult<bool>> CancelMyOrderAsync(int id, string cancelReason);
        Task<ApiResult<bool>> ConfirmReceivedAsync(int id);
    }
}
