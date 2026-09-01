namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Lớp trừu tượng cache dùng chung. Cài đặt hiện tại bọc <c>IDistributedCache</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>VÌ SAO CÓ LỚP NÀY khi repo đã có <c>IMemoryCache</c>.</b> <c>MemoryCache</c> nằm
    /// trong RAM của <b>một</b> tiến trình. Với nhiều task, hai request của cùng một người có
    /// thể vào hai instance khác nhau và thấy hai câu trả lời khác nhau — và với dữ liệu
    /// phân quyền thì hướng nguy hiểm là hướng <b>mở khoá</b> (xem luật ở <c>CLAUDE.md</c>).
    /// Lớp này dựng sẵn để đợt 8 chỉ cần bật một biến Terraform là chuyển sang Redis,
    /// <b>không phải sửa code</b>.
    ///
    /// 🚨 <b>Vẫn KHÔNG được cache trạng thái phân quyền hay khoá tài khoản qua đây.</b> Đổi
    /// `MemoryCache` thành Redis không làm luật đó hết đúng: một tài khoản vừa bị khoá vẫn còn
    /// "hoạt động" trong cache cho tới khi TTL hết. `IsActive`, role, quyền — đọc thẳng DB.
    /// Lớp này dành cho dữ liệu <b>công khai, ít đổi</b>: danh mục, menu, banner.
    ///
    /// ⚠️ <b>Mọi lỗi hạ tầng cache đều bị NUỐT + log warning.</b> Cache hỏng phải làm hệ thống
    /// <em>chậm</em>, không được làm nó <em>chết</em>. Đổi lại: khi Redis sập, mọi request rơi
    /// về factory cùng lúc và DB nhận đủ tải gốc — biết trước điều đó, đừng dùng lớp này để
    /// che một truy vấn mà DB không chịu nổi khi không có cache.
    /// </remarks>
    public interface ICacheService
    {
        /// <summary>Đọc giá trị. Trả <c>default</c> nếu không có HOẶC nếu cache lỗi.</summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>Ghi giá trị. Lỗi cache bị nuốt — lời gọi này không bao giờ ném.</summary>
        Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

        /// <summary>Xoá một khoá. Lỗi cache bị nuốt.</summary>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Đọc từ cache; nếu chưa có (hoặc cache lỗi) thì gọi <paramref name="factory"/> và ghi lại.
        /// </summary>
        /// <remarks>
        /// 🔴 Exception từ <paramref name="factory"/> <b>KHÔNG</b> bị nuốt — đó là lỗi nghiệp vụ
        /// hoặc lỗi DB thật, không phải lỗi cache. Chỉ lỗi của tầng cache mới bị nuốt.
        /// </remarks>
        Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default);
    }
}
