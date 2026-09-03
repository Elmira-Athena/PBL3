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
            var (_, value) = await TryGetAsync<T>(key, cancellationToken);
            return value;
        }

        /// <summary>
        /// Đọc cache và trả về CẢ tín hiệu hit/miss, không chỉ giá trị.
        /// </summary>
        /// <remarks>
        /// 🚨 <b>Đây là lý do <see cref="GetOrCreateAsync"/> không được dùng thẳng
        /// <see cref="GetAsync"/>.</b>
        ///
        /// <c>GetAsync</c> trả <c>default(T)</c> khi miss. Với <c>T</c> là kiểu THAM CHIẾU thì
        /// <c>default(T)</c> là <c>null</c>, phân biệt được với giá trị thật. Với <c>T</c> là kiểu
        /// GIÁ TRỊ (<c>int</c>, <c>bool</c>, <c>decimal</c>, <c>Guid</c>, struct) thì
        /// <c>default(T)</c> là <c>0</c>/<c>false</c> — <b>không phân biệt được với một giá trị
        /// hợp lệ</b>, và phép kiểm <c>cached is not null</c> luôn đúng.
        ///
        /// Bản trước viết đúng như vậy, và đã đo trên cache HOÀN TOÀN RỖNG:
        /// <code>
        /// GetOrCreateAsync&lt;int&gt;   -> trả 0     | factory gọi 0 lần   (kỳ vọng 42 / 1 lần)
        /// GetOrCreateAsync&lt;bool&gt;  -> trả False | factory gọi 0 lần   (kỳ vọng True / 1 lần)
        /// GetOrCreateAsync&lt;List&gt;  -> trả 2     | factory gọi 1 lần   ✓
        /// </code>
        /// Tức <c>GetOrCreateAsync&lt;int&gt;("ton-kho", …)</c> trả <c>0</c> mà KHÔNG hề chạm DB,
        /// mãi mãi. Đúng lớp lỗi "nói dối" mà XML doc của interface tuyên bố sẽ không phạm.
        ///
        /// Tín hiệu hit/miss đáng tin DUY NHẤT là ở tầng byte: <c>byte[]</c> null hoặc rỗng = miss.
        /// Vì vậy phải kiểm ở đó, TRƯỚC khi giải tuần tự. Đừng "sửa" bằng cách so với
        /// <c>default(T)</c> — làm thế thì một giá trị <c>0</c> hợp lệ bị coi là miss mãi mãi.
        /// </remarks>
        private async Task<(bool Found, T? Value)> TryGetAsync<T>(
            string key, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _cache.GetAsync(key, cancellationToken);
                if (bytes is null || bytes.Length == 0) return (false, default);

                return (true, JsonSerializer.Deserialize<T>(bytes));
            }
            catch (Exception ex)
            {
                // Cả lỗi hạ tầng (Redis sập) và lỗi giải tuần tự (JSON cũ không còn khớp kiểu
                // sau khi đổi DTO) đều rơi vào đây, và cách xử lý ĐÚNG cho cả hai là giống nhau:
                // coi như cache miss. Với lỗi giải tuần tự, cách này còn tự chữa — khoá xấu sẽ
                // bị ghi đè ở lượt SetAsync ngay sau đó.
                _logger.LogWarning(ex, "Không đọc được cache cho khoá {CacheKey} — coi như miss.", key);
                return (false, default);
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
            // TryGetAsync chứ không phải GetAsync: hit/miss phải đọc ở tầng byte, không suy ra
            // từ giá trị. Xem chú thích ở TryGetAsync — dùng GetAsync ở đây làm factory KHÔNG BAO
            // GIỜ chạy với kiểu giá trị, đã đo.
            var (found, cached) = await TryGetAsync<T>(key, cancellationToken);
            if (found) return cached!;

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
