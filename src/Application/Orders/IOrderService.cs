using System;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Sale;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Application.Orders
{
    public interface IOrderService
    {
        Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request, Guid userId);
        Task<ApiResult<OrderDetailDto>> PlaceOrderAsync(CreateOrderRequest request, Guid userId);
        Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id);
        Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetPagedOrdersAsync(OrderFilterRequest request);
        Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetMyOrdersAsync(Guid userId, OrderFilterRequest request);
        Task<ApiResult<bool>> CancelOrderAsync(int id, CancelOrderRequest request);
        Task<ApiResult<bool>> CompleteOrderAsync(int id);
        Task<ApiResult<bool>> ConfirmOrderAsync(int id);

        // Customer self-service (enforce ownership)
        Task<ApiResult<OrderDetailDto>> GetMyOrderByIdAsync(int id, Guid userId);
        Task<ApiResult<bool>> CancelMyOrderAsync(int id, Guid userId, string cancelReason);
        Task<ApiResult<bool>> ConfirmReceivedByCustomerAsync(int id, Guid userId);
    }
}
