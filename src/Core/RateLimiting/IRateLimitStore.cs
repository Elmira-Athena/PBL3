namespace PBL3.Core.RateLimiting
{
    /// <summary>Kết quả một lượt xin quota.</summary>
    /// <param name="Allowed">Có được đi tiếp không.</param>
    /// <param name="RetryAfterSeconds">
    /// Số giây tới khi cửa sổ hiện tại đóng. Chỉ có nghĩa khi <paramref name="Allowed"/> là
    /// <c>false</c>; dùng cho header <c>Retry-After</c>.
    /// </param>
    /// <param name="Degraded">
    /// <c>true</c> nghĩa là store KHÔNG kiểm được (thường là DB không tới được) và đã
    /// <b>fail-open</b>. Không được lẫn với <c>Allowed = true</c> bình thường: chỗ gọi phải
    /// LOG cờ này, nếu không một sự cố DB sẽ âm thầm tắt rate limit mà không ai biết.
    /// </param>
    public readonly record struct RateLimitDecision(
        bool Allowed, int RetryAfterSeconds, bool Degraded);

    /// <summary>
    /// Bộ đếm rate limit dùng chung giữa các task API.
    /// </summary>
    /// <remarks>
    /// 🎯 <b>Vì sao là một lời gọi duy nhất trả về quyết định, chứ không phải cặp
    /// đọc-rồi-ghi.</b> Cặp "đọc số hiện tại → so với hạn mức → tăng lên" là
    /// <b>check-then-act</b>, và LoadProbe của repo này tồn tại chính để bắt loại đó: hai
    /// request song song cùng đọc ra 4, cùng kết luận "chưa tới 5", cùng ghi 5 — hạn mức bị
    /// vượt mà không có lỗi nào. Cài đặt PHẢI tăng và trả về giá trị sau khi tăng trong
    /// <b>một câu lệnh nguyên tử</b>.
    /// </remarks>
    public interface IRateLimitStore
    {
        /// <summary>
        /// Tăng bộ đếm của <paramref name="partitionKey"/> lên 1 trong cửa sổ hiện tại rồi
        /// quyết định cho đi hay chặn.
        /// </summary>
        /// <param name="partitionKey">Khoá phân vùng, thường là <c>"policy:ip"</c>.</param>
        /// <param name="permitLimit">Số request tối đa trong một cửa sổ.</param>
        /// <param name="window">Độ dài cửa sổ.</param>
        Task<RateLimitDecision> AcquireAsync(
            string partitionKey,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default);
    }
}
