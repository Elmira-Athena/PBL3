namespace PBL3.Core.Entities
{
    /// <summary>
    /// Một ô đếm rate limit, dùng chung giữa MỌI task API.
    /// </summary>
    /// <remarks>
    /// 🎯 <b>Vì sao bộ đếm phải nằm ở DB chứ không trong RAM.</b> Bản trước đếm bằng
    /// <c>System.Threading.RateLimiting</c>, tức trong RAM của <b>một tiến trình</b>. Với một
    /// task thì đúng; với N task sau ALB thì mỗi task đếm riêng và <b>mọi hạn mức nhân N</b> —
    /// "5 lần đăng nhập/phút" thành 5N. Đó không chỉ là lỗi hiệu năng: kịch bản <b>KB6</b> của
    /// <c>docs/security-validation-report.md</c> đã nộp con số đo <i>"req 1-5 → 400, req 6-20 →
    /// 429"</i>, nên scale ra mà không sửa là biến <b>một bằng chứng đã nộp thành lời khai
    /// sai</b> — và sai âm thầm: không log, không alarm, ALB vẫn xanh.
    ///
    /// Đây cũng là điều kiện mà <c>infra/tf/modules/ecs/variables.tf</c> đòi trước khi cho
    /// <c>max_instance_count &gt; 1</c>.
    ///
    /// 🚨 <b>KHÔNG dùng bảng này cho <c>GlobalLimiter</c> và <c>PublicReadRateLimit</c>.</b>
    /// Hai cái đó chạm mọi request duyệt catalogue; ghi DB trên đường nóng đó là biến DB thành
    /// cổ chai — đúng ngược mục đích của việc scale ra. Chúng cố ý ở lại trong RAM và cố ý là
    /// per-instance: chúng là chốt chặn burst thô, không phải kịch bản đang được chấm.
    ///
    /// ⚠️ <b>Bảng này KHÔNG có <c>IsDeleted</c>.</b> Nó là dữ liệu vận hành sống ngắn, bị dọn
    /// theo <c>WindowStart</c>. Soft-delete ở đây chỉ làm bảng phình.
    /// </remarks>
    public class RateLimitCounter
    {
        /// <summary>
        /// Khoá phân vùng: <c>"&lt;tên policy&gt;:&lt;IP client&gt;"</c>. Ghép cả tên policy
        /// vào để hai policy trên cùng một IP không dùng chung ô đếm.
        /// </summary>
        public string PartitionKey { get; set; } = string.Empty;

        /// <summary>
        /// Mốc BẮT ĐẦU của cửa sổ, đã làm tròn xuống theo độ dài cửa sổ (fixed window).
        /// Luôn là UTC — <c>HushStoreDbContext</c> có converter ép <c>Kind=Utc</c>.
        /// </summary>
        public DateTime WindowStart { get; set; }

        /// <summary>Số request đã tính trong cửa sổ này.</summary>
        public int Count { get; set; }
    }
}
