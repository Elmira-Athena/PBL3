using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PBL3.Core.Interfaces;

namespace PBL3.Infrastructure.Caching
{
    /// <inheritdoc cref="ICacheService"/>
    public class CacheService : ICacheService
    {
        /// <summary>
        /// TTL mặc định khi lời gọi không nêu. 5 phút — đủ ngắn để một lần sửa danh mục hiện ra
        /// trong vòng một tách cà phê, đủ dài để chặn phần lớn lượt đọc lặp.
        /// </summary>
        /// <remarks>
        /// 🔴 <b>KHÔNG có TTL vô hạn, và đó là lựa chọn có ý thức.</b> Không có đường nào để
        /// invalidate xuyên tiến trình ở thời điểm này, nên TTL <b>là</b> cơ chế nhất quán duy
        /// nhất. Một khoá không hết hạn là một khoá sai vĩnh viễn sau lần sửa đầu tiên.
        /// </remarks>
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

        private readonly IDistributedCache _cache;
        private readonly ILogger<CacheService> _logger;

        public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var bytes = await _cache.GetAsync(key, cancellationToken);
                if (bytes is null || bytes.Length == 0) return default;

                return JsonSerializer.Deserialize<T>(bytes);
            }
            catch (Exception ex)
            {
                // Cả lỗi hạ tầng (Redis sập) và lỗi giải tuần tự (JSON cũ không còn khớp kiểu
                // sau khi đổi DTO) đều rơi vào đây, và cách xử lý ĐÚNG cho cả hai là giống nhau:
                // coi như cache miss. Với lỗi giải tuần tự, cách này còn tự chữa — khoá xấu sẽ
                // bị ghi đè ở lượt SetAsync ngay sau đó.
                _logger.LogWarning(ex, "Không đọc được cache cho khoá {CacheKey} — coi như miss.", key);
                return default;
            }
        }

        public async Task SetAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl
                };

                await _cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không ghi được cache cho khoá {CacheKey} — bỏ qua.", key);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                // 🚨 Đây là khối catch ĐÁNG LO NHẤT trong file, và nó cố ý vẫn nuốt.
                // Xoá cache thất bại nghĩa là dữ liệu CŨ còn nằm đó tới khi TTL hết. Với dữ
                // liệu công khai ít đổi thì chấp nhận được; với bất cứ thứ gì mà "cũ" là SAI,
                // đừng dùng lớp này — đọc thẳng DB. Log ở mức Warning để nó lên được dashboard.
                _logger.LogWarning(ex, "Không xoá được cache cho khoá {CacheKey} — dữ liệu cũ còn tới khi TTL hết.", key);
            }
        }

        public async Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default)
        {
            var cached = await GetAsync<T>(key, cancellationToken);
            if (cached is not null) return cached;

            // 🔴 KHÔNG bọc factory trong try/catch. Exception ở đây là lỗi nghiệp vụ hoặc lỗi
            // DB thật — nuốt nó là biến "DB sập" thành "danh mục rỗng", đúng loại lỗi tệ nhất
            // vì nó NÓI DỐI: người dùng tưởng không có dữ liệu chứ không phải hệ thống đang lỗi.
            // (Cùng lỗi đã sửa ở đợt 2 tại OrderClientService, chỗ nuốt lỗi thành "bảng rỗng".)
            var value = await factory(cancellationToken);

            await SetAsync(key, value, ttl, cancellationToken);
            return value;
        }
    }
}
