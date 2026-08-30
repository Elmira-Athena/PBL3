using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Sale;

namespace Client.Services.Orders
{
    public interface IOrderClientService
    {
        Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id);
        /// <summary>
        /// Trả về ApiResult chứ KHÔNG phải PagedResult trần.
        ///
        /// Bản cũ trả PagedResult trần nên khi request hỏng nó chỉ còn cách trả
        /// một trang RỖNG — và giao diện hiển thị "không có đơn hàng nào", tức
        /// NÓI DỐI người dùng: thật ra là mất mạng hoặc hết phiên. Người dùng
        /// không có lý do gì để thử lại. Kiểu trả về mới buộc call-site phải
        /// phân biệt "rỗng thật" với "không lấy được".
        /// </summary>
        Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetPagedOrdersAsync(OrderFilterRequest request);
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
