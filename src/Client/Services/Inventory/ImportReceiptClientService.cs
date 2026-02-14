using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Inventory
{
    public class ImportReceiptClientService : IImportReceiptClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/import-receipts";

        public ImportReceiptClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<ImportReceiptDto>> CreateAsync(CreateImportReceiptRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ImportReceiptDto>>();
                return result ?? ApiResult<ImportReceiptDto>.Fail("Không thể tạo phiếu nhập kho.");
            }
            catch (Exception ex)
            {
                return ApiResult<ImportReceiptDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<PagedResult<ImportReceiptDto>>> GetListAsync(ImportReceiptFilterRequest filter)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(filter.Keyword))
                    queryParams.Add($"Keyword={Uri.EscapeDataString(filter.Keyword)}");

                queryParams.Add($"PageNumber={filter.PageNumber}");
                queryParams.Add($"PageSize={filter.PageSize}");

                if (!string.IsNullOrWhiteSpace(filter.SortBy))
                    queryParams.Add($"SortBy={Uri.EscapeDataString(filter.SortBy)}");
                if (filter.SortDescending)
                    queryParams.Add("SortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";
                var result = await _httpClient.GetFromJsonAsync<ApiResult<PagedResult<ImportReceiptDto>>>(url);
                return result ?? ApiResult<PagedResult<ImportReceiptDto>>.Fail("Không thể tải danh sách phiếu nhập.");
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<ImportReceiptDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<ImportReceiptDto>> GetByIdAsync(int id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<ImportReceiptDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<ImportReceiptDto>.Fail("Không tìm thấy phiếu nhập.");
            }
            catch (Exception ex)
            {
                return ApiResult<ImportReceiptDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
