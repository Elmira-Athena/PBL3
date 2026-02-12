using System.Net.Http.Json;
using PBL3.Shared.DTOs.Categories;
using PBL3.Shared.DTOs.Common;

namespace Client.Services
{
    public class CategoryClientService : ICategoryClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/categories";

        public CategoryClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<List<CategoryTreeDto>>> GetTreeAsync()
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<List<CategoryTreeDto>>>($"{BaseUrl}/tree");
                return result ?? ApiResult<List<CategoryTreeDto>>.Fail("Không thể tải danh sách danh mục.");
            }
            catch (Exception ex)
            {
                return ApiResult<List<CategoryTreeDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<CategoryDto>> GetByIdAsync(int id)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<CategoryDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<CategoryDto>.Fail("Không tìm thấy danh mục.");
            }
            catch (Exception ex)
            {
                return ApiResult<CategoryDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<CategoryDto>> CreateAsync(CreateCategoryRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<CategoryDto>>();
                return result ?? ApiResult<CategoryDto>.Fail("Không thể tạo danh mục.");
            }
            catch (Exception ex)
            {
                return ApiResult<CategoryDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<CategoryDto>>();
                return result ?? ApiResult<CategoryDto>.Fail("Không thể cập nhật danh mục.");
            }
            catch (Exception ex)
            {
                return ApiResult<CategoryDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/{id}");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể xóa danh mục.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
