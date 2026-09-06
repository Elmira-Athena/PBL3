using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using PBL3.API.Middleware;
using PBL3.Core.RateLimiting;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Filters
{
    /// <summary>
    /// Rate limit theo IP với bộ đếm **dùng chung giữa mọi task API** (đặt ở PostgreSQL).
    /// Thay cho <c>[EnableRateLimiting]</c> ở những endpoint mà hạn mức phải đúng thật.
    /// </summary>
    /// <remarks>
    /// 🎯 <b>Vì sao là action filter mà không phải một <c>RateLimiter</c> tuỳ biến.</b>
    /// <c>System.Threading.RateLimiting</c> đòi cài <c>AttemptAcquireCore</c> — một hàm
    /// <b>đồng bộ</b>. Bộ đếm nằm ở DB nên phép kiểm vốn là bất đồng bộ, và cách duy nhất
    /// nhét nó vào chữ ký đồng bộ là <c>.Result</c>/<c>.Wait()</c> — thứ CLAUDE.md cấm thẳng
    /// vì gây deadlock. Action filter là async từ đầu tới cuối, nên không có chỗ nào phải
    /// gian lận.
    ///
    /// Đổi lấy: mất tích hợp với <c>OnRejected</c> của middleware, nên thân 429 phải tự dựng
    /// ở đây. Đã dựng đúng khuôn cũ — <b>ApiResult tiếng Việt + header Retry-After</b> — vì
    /// trả 429 thân rỗng là vi phạm luật "mọi thông báo lỗi cho người dùng phải bằng tiếng
    /// Việt có dấu", và đó đúng là lỗi mà bản trước của <c>OnRejected</c> đã từng mắc.
    ///
    /// 🚨 <b>Đặt attribute này KHÔNG được kèm <c>[EnableRateLimiting]</c> trên cùng một
    /// action.</b> Hai cơ chế sẽ cùng đếm, mỗi cái theo cách riêng, và hạn mức thật thành
    /// giá trị nhỏ hơn của hai — không ai đoán được là bao nhiêu.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class DbRateLimitAttribute : Attribute, IFilterFactory
    {
        /// <param name="policyName">
        /// Tên policy, đi vào khoá phân vùng. Giữ ĐÚNG tên cũ (<c>LoginRateLimit</c>…) để
        /// báo cáo bảo mật và tài liệu không phải đổi từ vựng.
        /// </param>
        /// <param name="permitLimit">Số request tối đa trong một cửa sổ.</param>
        /// <param name="windowSeconds">Độ dài cửa sổ, tính bằng giây.</param>
        public DbRateLimitAttribute(string policyName, int permitLimit, int windowSeconds)
        {
            PolicyName = policyName;
            PermitLimit = permitLimit;
            WindowSeconds = windowSeconds;
        }

        public string PolicyName { get; }
        public int PermitLimit { get; }
        public int WindowSeconds { get; }

        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
            => new DbRateLimitFilter(
                serviceProvider.GetRequiredService<IRateLimitStore>(),
                serviceProvider.GetRequiredService<ILogger<DbRateLimitFilter>>(),
                PolicyName,
                PermitLimit,
                TimeSpan.FromSeconds(WindowSeconds));
    }

    /// <summary>Phần thực thi của <see cref="DbRateLimitAttribute"/>.</summary>
    public sealed class DbRateLimitFilter : IAsyncActionFilter
    {
        private readonly IRateLimitStore _store;
        private readonly ILogger<DbRateLimitFilter> _logger;
        private readonly string _policyName;
        private readonly int _permitLimit;
        private readonly TimeSpan _window;

        public DbRateLimitFilter(
            IRateLimitStore store,
            ILogger<DbRateLimitFilter> logger,
            string policyName,
            int permitLimit,
            TimeSpan window)
        {
            _store = store;
            _logger = logger;
            _policyName = policyName;
            _permitLimit = permitLimit;
            _window = window;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var ip = GetClientIp(context.HttpContext);
            var partitionKey = $"{_policyName}:{ip}";

            var decision = await _store.AcquireAsync(
                partitionKey, _permitLimit, _window, context.HttpContext.RequestAborted);

            if (decision.Degraded)
            {
                // Store đã log chi tiết kỹ thuật. Dòng này để đứng ở tầng API cho ai đọc log
                // của request thấy ngay: hạn mức KHÔNG có hiệu lực cho lượt này.
                _logger.LogWarning(
                    "Rate limit {Policy} không kiểm được (fail-open) — request đi qua mà "
                    + "KHÔNG bị tính hạn mức.", _policyName);
            }

            if (!decision.Allowed)
            {
                if (decision.RetryAfterSeconds > 0)
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        decision.RetryAfterSeconds.ToString();
                }

                // ErrorResponseJson.Options bỏ khoá "data" khỏi thân lỗi. Thiếu nó thì client
                // đọc ApiResult<T> ném JsonException và người dùng KHÔNG BAO GIỜ thấy câu này
                // — đúng lỗi đã gặp một lần ở khối OnRejected cũ.
                context.Result = new JsonResult(
                    ApiResult<object>.Fail(
                        "Bạn thao tác quá nhanh. Vui lòng chờ trong giây lát rồi thử lại."))
                {
                    StatusCode = StatusCodes.Status429TooManyRequests,
                    SerializerSettings = ErrorResponseJson.Options,
                };
                return;
            }

            await next();
        }

        /// <summary>
        /// IP client. Phải đứng SAU <c>UseForwardedHeaders</c> trong pipeline — action filter
        /// luôn chạy sau middleware nên điều đó được bảo đảm.
        /// </summary>
        /// <remarks>
        /// ⚠️ Độ tin cậy của toàn bộ cơ chế phụ thuộc <c>ForwardLimit = 1</c> ở
        /// <c>ForwardedHeadersOptions</c>. Đổi nó thành &gt;= 2 là cho kẻ tấn công tự chọn
        /// phân vùng của mình bằng cách bơm <c>X-Forwarded-For</c>, và mọi hạn mức thành vô
        /// nghĩa — kể cả khi bộ đếm đã dùng chung hoàn hảo.
        /// </remarks>
        private static string GetClientIp(HttpContext context)
            => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
