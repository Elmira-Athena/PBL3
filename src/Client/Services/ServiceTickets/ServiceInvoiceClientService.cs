using Microsoft.Extensions.Logging;
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
        private readonly ILogger<ServiceInvoiceClientService> _logger;

        public ServiceInvoiceClientService(HttpClient httpClient, ILogger<ServiceInvoiceClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ApiResult<PagedResult<ServiceInvoiceListDto>>> GetPagedListAsync(
            string? keyword, byte? paymentStatus, DateTime? fromDate, DateTime? toDate,
            int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            try
            {
                var url = $"api/service-invoices?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(keyword))
                    url += $"&keyword={Uri.EscapeDataString(keyword)}";
                if (paymentStatus.HasValue)
                    url += $"&paymentStatus={paymentStatus.Value}";
                if (fromDate.HasValue)
                    url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
                if (toDate.HasValue)
                    url += $"&toDate={toDate.Value:yyyy-MM-dd}";
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách hoá đơn dịch vụ");
                return new ApiResult<PagedResult<ServiceInvoiceListDto>> { Message = "Không tải được danh sách hoá đơn dịch vụ. Vui lòng thử lại." };
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải chi tiết hoá đơn dịch vụ");
                return new ApiResult<ServiceInvoiceDetailDto> { Message = "Không tải được chi tiết hoá đơn dịch vụ. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> MarkInvoicePaidAsync(int id)
        {
            try
            {
                var response = await _httpClient.PatchAsync($"api/service-invoices/{id}/mark-paid", null);
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<bool> { Message = $"Lỗi HTTP {(int)response.StatusCode}." };

                try
                {
                    return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                        ?? new ApiResult<bool> { Message = "Lỗi xác nhận thanh toán." };
                }
                catch
                {
                    return new ApiResult<bool> { Message = "Lỗi xác nhận thanh toán." };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "xác nhận hoá đơn dịch vụ đã thanh toán");
                return new ApiResult<bool> { Message = "Không xác nhận được hoá đơn đã thanh toán. Vui lòng tải lại trang để kiểm tra hoá đơn trước khi thử lại." };
            }
        }
    }
}
