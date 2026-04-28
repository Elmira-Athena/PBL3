using System;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Sale;
using PBL3.Shared.DTOs.Common; 

namespace PBL3.Service.Orders
{
    public interface IOrderService
    {
        Task<ApiResult<OrderDetailDto>> PlaceOrderAsync(CreateOrderRequest request, Guid userId);
        Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id);
    }
}
