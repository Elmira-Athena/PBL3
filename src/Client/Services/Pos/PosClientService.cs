using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using PBL3.Shared.DTOs.Pos;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Pos
{
    public class PosClientService : IPosClientService
    {
        private readonly HttpClient _httpClient;

        public PosClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<PosScanResponse>> ScanBarcodeAsync(string serialNumber)
        {
            var request = new PosScanRequest { SerialNumber = serialNumber };
            var response = await _httpClient.PostAsJsonAsync($"/api/pos/scan", request);
            return await response.Content.ReadFromJsonAsync<ApiResult<PosScanResponse>>() 
                ?? ApiResult<PosScanResponse>.Fail("Lỗi hệ thống khi quét mã.");
        }

        public async Task<ApiResult<PosCustomerDto>> SearchCustomerByPhoneAsync(string phone)
        {
            var response = await _httpClient.GetAsync($"/api/pos/customer/{phone}");
            if (!response.IsSuccessStatusCode) return new ApiResult<PosCustomerDto> { Success = false, Message = "Không tìm thấy khách hàng." };
            return await response.Content.ReadFromJsonAsync<ApiResult<PosCustomerDto>>()
                ?? ApiResult<PosCustomerDto>.Fail("Lỗi hệ thống khi tìm kiếm khách hàng.");
        }

        public async Task<ApiResult<VoucherValidationDto>> ValidateVoucherAsync(string code, decimal subTotal)
        {
            var response = await _httpClient.GetAsync($"/api/pos/voucher/validate?code={code}&subTotal={subTotal}");
             if (!response.IsSuccessStatusCode) return new ApiResult<VoucherValidationDto> { Success = false, Message = "Lỗi khi kiểm tra voucher." };
            return await response.Content.ReadFromJsonAsync<ApiResult<VoucherValidationDto>>()
                ?? ApiResult<VoucherValidationDto>.Fail("Lỗi hệ thống khi kiểm tra voucher.");
        }

        public async Task<ApiResult<PosOrderDto>> CheckoutAsync(PosCheckoutRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/pos/checkout", request);
            return await response.Content.ReadFromJsonAsync<ApiResult<PosOrderDto>>()
                ?? ApiResult<PosOrderDto>.Fail("Lỗi hệ thống khi thanh toán.");
        }

        public async Task<ApiResult<int>> SaveDraftAsync(PosCheckoutRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/pos/draft", request);
            return await response.Content.ReadFromJsonAsync<ApiResult<int>>()
                ?? ApiResult<int>.Fail("Lỗi hệ thống khi lưu tạm.");
        }

        public async Task<ApiResult<List<PosDraftDto>>> GetDraftsAsync()
        {
            return await _httpClient.GetFromJsonAsync<ApiResult<List<PosDraftDto>>>("/api/pos/drafts")
                ?? ApiResult<List<PosDraftDto>>.Fail("Lỗi hệ thống khi lấy danh sách đơn chờ.");
        }
    }
}
