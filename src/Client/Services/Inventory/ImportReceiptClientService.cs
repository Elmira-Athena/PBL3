using Client.Services.Common;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Inventory
{
    public class ImportReceiptClientService : IImportReceiptClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ImportReceiptClientService> _logger;
        private const string BaseUrl = "api/import-receipts";

        public ImportReceiptClientService(HttpClient httpClient, ILogger<ImportReceiptClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo phiếu nhập kho");
                return ApiResult<ImportReceiptDto>.Fail("Không tạo được phiếu nhập kho. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<PagedResult<ImportReceiptDto>>> GetListAsync(ImportReceiptFilterRequest filter, CancellationToken cancellationToken = default)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(filter.Keyword))
                    queryParams.Add($"Keyword={Uri.EscapeDataString(filter.Keyword)}");

                queryParams.Add($"PageNumber={filter.PageNumber}");
                queryParams.Add($"PageSize={filter.PageSize}");

                if (filter.FromDate.HasValue)
                    queryParams.Add($"FromDate={filter.FromDate.Value:yyyy-MM-dd}");
                if (filter.ToDate.HasValue)
                    queryParams.Add($"ToDate={filter.ToDate.Value:yyyy-MM-dd}");
                if (filter.SupplierId.HasValue)
                    queryParams.Add($"SupplierId={filter.SupplierId.Value}");
                if (!string.IsNullOrWhiteSpace(filter.SortBy))
                    queryParams.Add($"SortBy={Uri.EscapeDataString(filter.SortBy)}");
                if (filter.SortDescending)
                    queryParams.Add("SortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";
                return await ApiCall.SendAsync<PagedResult<ImportReceiptDto>>(
                    () => _httpClient.GetAsync(url, cancellationToken),
                    "tải danh sách phiếu nhập kho", _logger, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return ApiResult<PagedResult<ImportReceiptDto>>.Fail(string.Empty);
            }
        }

        public async Task<ApiResult<ImportReceiptDto>> GetByIdAsync(int id)
        {
            return await ApiCall.SendAsync<ImportReceiptDto>(
                () => _httpClient.GetAsync($"{BaseUrl}/{id}"),
                "tải chi tiết phiếu nhập kho", _logger);
        }
    }
}
