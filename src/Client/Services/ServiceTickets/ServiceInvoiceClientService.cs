using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.ServiceTickets;

namespace Client.Services.ServiceTickets
{
    public class ServiceInvoiceClientService : IServiceInvoiceClientService
    {
        private readonly HttpClient _httpClient;

        public ServiceInvoiceClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<PagedResult<ServiceInvoiceListDto>>> GetPagedListAsync(
            string? keyword, int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            try
            {
                var url = $"api/service-invoices?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(keyword))
                    url += $"&keyword={Uri.EscapeDataString(keyword)}";
                if (!string.IsNullOrEmpty(sortBy))
                    url += $"&sortBy={Uri.EscapeDataString(sortBy)}&sortDescending={sortDescending}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<PagedResult<ServiceInvoiceListDto>> { Message = $"Lỗi HTTP {(int)response.StatusCode}." };

                try
                {
                    return await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<ServiceInvoiceListDto>>>()
                        ?? new ApiResult<PagedResult<ServiceInvoiceListDto>> { Message = "Lỗi lấy danh sách hóa đơn." };
                }
                catch
                {
                    return new ApiResult<PagedResult<ServiceInvoiceListDto>> { Message = "Lỗi lấy danh sách hóa đơn." };
                }
            }
            catch (Exception ex)
            {
                return new ApiResult<PagedResult<ServiceInvoiceListDto>> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceInvoiceDetailDto>> GetByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/service-invoices/{id}");
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<ServiceInvoiceDetailDto> { Message = $"Lỗi HTTP {(int)response.StatusCode}." };

                try
                {
                    return await response.Content.ReadFromJsonAsync<ApiResult<ServiceInvoiceDetailDto>>()
                        ?? new ApiResult<ServiceInvoiceDetailDto> { Message = "Không tìm thấy hóa đơn." };
                }
                catch
                {
                    return new ApiResult<ServiceInvoiceDetailDto> { Message = "Không tìm thấy hóa đơn." };
                }
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceInvoiceDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }
    }
}
