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
            try
            {
                var request = new PosScanRequest { SerialNumber = serialNumber };
                var response = await _httpClient.PostAsJsonAsync($"/api/pos/scan", request);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadFromJsonAsync<ApiResult<PosScanResponse>>();
                    return err ?? ApiResult<PosScanResponse>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                }
                return await response.Content.ReadFromJsonAsync<ApiResult<PosScanResponse>>()
                    ?? ApiResult<PosScanResponse>.Fail("Lỗi hệ thống khi quét mã.");
            }
            catch (Exception ex)
            {
                return ApiResult<PosScanResponse>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<PosCustomerDto>> SearchCustomerByPhoneAsync(string phone)
        {
            var response = await _httpClient.GetAsync($"/api/pos/customer?phone={Uri.EscapeDataString(phone)}");
            if (!response.IsSuccessStatusCode) return new ApiResult<PosCustomerDto> { Success = false, Message = "Không tìm thấy khách hàng." };
            return await response.Content.ReadFromJsonAsync<ApiResult<PosCustomerDto>>()
                ?? ApiResult<PosCustomerDto>.Fail("Lỗi hệ thống khi tìm kiếm khách hàng.");
        }

        public async Task<ApiResult<VoucherValidationDto>> ValidateVoucherAsync(string code, decimal subTotal)
        {
            var response = await _httpClient.PostAsync(
                $"/api/pos/voucher/validate?code={Uri.EscapeDataString(code)}&subTotal={subTotal}", null);
            if (!response.IsSuccessStatusCode) return new ApiResult<VoucherValidationDto> { Success = false, Message = "Lỗi khi kiểm tra voucher." };
            return await response.Content.ReadFromJsonAsync<ApiResult<VoucherValidationDto>>()
                ?? ApiResult<VoucherValidationDto>.Fail("Lỗi hệ thống khi kiểm tra voucher.");
        }

        public async Task<ApiResult<PosOrderDto>> CheckoutAsync(PosCheckoutRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/pos/checkout", request);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadFromJsonAsync<ApiResult<PosOrderDto>>();
                    return err ?? ApiResult<PosOrderDto>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                }
                return await response.Content.ReadFromJsonAsync<ApiResult<PosOrderDto>>()
                    ?? ApiResult<PosOrderDto>.Fail("Lỗi hệ thống khi thanh toán.");
            }
            catch (Exception ex)
            {
                return ApiResult<PosOrderDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<int>> SaveDraftAsync(PosCheckoutRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/pos/draft", request);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<int>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                return await response.Content.ReadFromJsonAsync<ApiResult<int>>()
                    ?? ApiResult<int>.Fail("Lỗi hệ thống khi lưu tạm.");
            }
            catch (Exception ex)
            {
                return ApiResult<int>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<List<PosDraftDto>>> GetDraftsAsync()
        {
            return await _httpClient.GetFromJsonAsync<ApiResult<List<PosDraftDto>>>("/api/pos/drafts")
                ?? ApiResult<List<PosDraftDto>>.Fail("Lỗi hệ thống khi lấy danh sách đơn chờ.");
        }
    }
}
