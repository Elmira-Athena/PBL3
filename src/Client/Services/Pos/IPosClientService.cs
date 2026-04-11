using PBL3.Shared.DTOs.Pos;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Pos
{
    // Giả định dùng ApiResult<T> hoặc tương tự, nhưng tạm dùng direct model hoặc dynamic.
    // Thực tế sẽ dùng ApiResult.
    public interface IPosClientService
    {
        Task<ApiResult<PosScanResponse>> ScanBarcodeAsync(string serialNumber);
        Task<ApiResult<PosCustomerDto>> SearchCustomerByPhoneAsync(string phone);
        Task<ApiResult<VoucherValidationDto>> ValidateVoucherAsync(string code, decimal subTotal);
        Task<ApiResult<PosOrderDto>> CheckoutAsync(PosCheckoutRequest request);
        Task<ApiResult<int>> SaveDraftAsync(PosCheckoutRequest request);
        Task<ApiResult<List<PosDraftDto>>> GetDraftsAsync();
    }
}
