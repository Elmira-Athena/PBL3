using Microsoft.EntityFrameworkCore;
using Npgsql;
using PBL3.Core.Exceptions;

namespace PBL3.Infrastructure.Concurrency
{
    /// <summary>
    /// Nguồn sự thật DUY NHẤT cho câu hỏi "exception này có phải xung đột đồng thời không, và
    /// nếu phải thì nói gì với người dùng".
    /// </summary>
    /// <remarks>
    /// 🚨 <b>Vì sao phải gom vào một chỗ: hai danh sách đã trôi khỏi nhau, và không gì báo.</b>
    ///
    /// Trước lớp này, cùng một câu hỏi được trả lời ở hai nơi độc lập:
    /// <list type="bullet">
    ///   <item><b>Các chốt controller</b> quyết định cái gì được THOÁT ra —
    ///     <c>DbUpdateConcurrencyException</c> và vi phạm unique.</item>
    ///   <item><b><c>ConflictExceptionHandler</c></b> quyết định cái gì thành 409 — hai loại trên
    ///     <b>cộng thêm</b> deadlock và <see cref="ConcurrentModificationException"/>.</item>
    /// </list>
    /// Giao của hai danh sách mới là thứ thật sự chạy. Phần dôi ra ở handler là <b>code chết</b>:
    /// deadlock bị <c>catch (Exception)</c> của controller nuốt trước khi middleware kịp thấy.
    ///
    /// 🔴 <b>Và một đường thứ hai còn khó thấy hơn: <c>EnableRetryOnFailure</c> ĐÃ BẬT.</b>
    /// Deadlock nằm trong danh sách transient nên nó <b>bị thử lại</b>. Hết lượt thì EF Core bọc
    /// nguyên nhân gốc vào <c>RetryLimitExceededException</c>. Phép so khớp một tầng
    /// <b>không khớp cái nào</b> ⇒ rơi xuống handler tổng ⇒ <b>500</b>. Nói với người dùng
    /// "server hỏng" trong khi sự thật là "thử lại đi" — đúng lỗi mà 409 sinh ra để tránh.
    ///
    /// Vì vậy lớp này <b>đi hết chuỗi <c>InnerException</c></b> thay vì so khớp một tầng. Cách đó
    /// không cần nhắc tên <c>RetryLimitExceededException</c>, nên nó cũng đúng cho mọi lớp bọc
    /// khác xuất hiện sau này — kể cả lớp bọc do chính ta viết.
    ///
    /// ✅ <b>Đợt 7 — chuyển PostgreSQL: cấu trúc trên GIỮ NGUYÊN, chỉ đổi lõi nhận diện.</b>
    /// Đã tra: <c>NpgsqlRetryingExecutionStrategy</c> coi <c>40P01</c> (deadlock) là transient,
    /// y như SQL Server coi <c>1205</c> là transient. Nên đường bọc
    /// <c>RetryLimitExceededException</c> vẫn tồn tại nguyên vẹn và vòng duyệt trên vẫn cần thiết.
    ///
    /// ⚠️ <b>Hệ quả có chủ ý của việc đi hết chuỗi:</b> một <c>Exception</c> chung bọc quanh
    /// deadlock vẫn được xếp là xung đột. Đó là điều <em>mong muốn</em> — bản chất lỗi không đổi
    /// vì ai đó bọc nó lại. Ở khuôn của <c>CLAUDE.md</c>, chốt xung đột luôn đứng TRƯỚC
    /// <c>catch (Exception)</c> nên đường thường không bao giờ bọc nhầm.
    ///
    /// 🚨 <b>Phân loại theo MÃ LỖI, tuyệt đối không dò <c>ex.Message</c>.</b> Chuỗi lỗi là tiếng
    /// Anh và đổi theo phiên bản — dò chuỗi là dùng biểu diễn thay cho ngữ nghĩa.
    /// </remarks>
    public static class ConflictClassifier
    {
        /// <summary>
        /// <c>23505 unique_violation</c> — vi phạm unique index HOẶC unique constraint.
        /// </summary>
        /// <remarks>
        /// PostgreSQL <b>không phân biệt</b> hai thứ đó, khác SQL Server vốn có <c>2601</c> cho
        /// index và <c>2627</c> cho constraint. Mất phân biệt này <b>không tốn gì</b>: câu trả
        /// cho người dùng ở cả hai trường hợp vốn đã giống nhau.
        /// </remarks>
        private const string UniqueViolation = "23505";

        /// <summary>
        /// <c>40P01 deadlock_detected</c> — PostgreSQL đã chọn giao dịch này làm nạn nhân.
        /// Tương đương <c>1205</c> của SQL Server.
        /// </summary>
        private const string DeadlockDetected = "40P01";

        /// <summary>
        /// <c>40001 serialization_failure</c> — <b>mã MỚI, SQL Server không có tương đương.</b>
        /// </summary>
        /// <remarks>
        /// Chỉ sinh ra ở isolation level <c>REPEATABLE READ</c>/<c>SERIALIZABLE</c>. Repo hiện mở
        /// transaction không tham số ⇒ <c>ReadCommitted</c> ⇒ mã này <b>chưa bao giờ xảy ra</b>.
        /// Đưa vào ngay vì chi phí bằng 0, và vì ngày ai đó nâng isolation cho một đường nghiệp vụ
        /// thì lỗi sẽ ra <b>500 im lặng</b> chứ không ai nhớ quay lại sửa chỗ này.
        /// </remarks>
        private const string SerializationFailure = "40001";

        /// <summary>
        /// Chặn trên số tầng <c>InnerException</c> được duyệt.
        /// </summary>
        /// <remarks>
        /// Chuỗi <c>InnerException</c> về lý thuyết có thể tạo vòng lặp (không có gì trong CLR cấm
        /// điều đó), và một vòng lặp ở đây sẽ treo <b>bên trong exception filter</b> — chỗ khó chẩn
        /// đoán nhất có thể tưởng tượng. Chuỗi sâu nhất thật sự trong repo là 3 tầng
        /// (<c>RetryLimitExceededException → DbUpdateException → NpgsqlException</c>), nên 16 là dư dả.
        /// </remarks>
        private const int MaxDepth = 16;

        /// <summary>
        /// <c>true</c> nếu đây là xung đột đồng thời — dùng trong <c>catch (Exception ex) when (...)</c>
        /// ở controller để cho nó THOÁT ra tới <c>ConflictExceptionHandler</c>.
        /// </summary>
        public static bool IsConflict(Exception? exception) => Classify(exception) is not null;

        /// <summary>
        /// <c>true</c> nếu đây là vi phạm <b>đúng constraint được nêu tên</b>.
        /// </summary>
        /// <remarks>
        /// 🎁 <b>Đây là thứ SQL Server không làm được, và nó đóng lại một ngoại lệ có chủ ý.</b>
        /// <c>SqlException</c> không mang tên constraint, nên hai chỗ ở tầng Service <em>dịch</em>
        /// vi phạm unique thành câu nghiệp vụ riêng đã phải dựa vào một lập luận <b>cục bộ</b>:
        /// <i>"trong PHẠM VI hàm này, vi phạm unique chỉ có MỘT nguồn duy nhất"</i>, kèm cảnh báo
        /// <i>"đừng copy khuôn này sang hàm có nhiều index"</i>.
        ///
        /// Lập luận ấy đúng — nhưng <b>không có gì bắt lỗi khi nó hết đúng</b>. Thêm một unique
        /// index vào đúng đường đó thì khối <c>catch</c> âm thầm gán nhầm câu nghiệp vụ cho một vi
        /// phạm khác, và người dùng được báo sai sự thật.
        ///
        /// <c>PostgresException.ConstraintName</c> làm việc phân biệt trở thành đúng <b>theo cấu
        /// trúc</b> thay vì theo phạm vi — nên khuôn này <b>an toàn để copy</b>.
        ///
        /// 🚨 <b>PostgreSQL cắt identifier ở 63 byte.</b> Tên constraint dài hơn thì
        /// <c>ConstraintName</c> trả về tên ĐÃ CẮT trong khi hằng ở
        /// <see cref="Core.Constants.DbConstraints"/> là tên đầy đủ ⇒ so sánh luôn false ⇒
        /// exception <b>im lặng rơi xuống <c>catch</c> tổng</b>. Xem phần đếm độ dài ở lớp hằng đó.
        ///
        /// ⚠️ Chỉ dùng ở chỗ <b>dịch nghĩa</b>. Chỗ chỉ cần rethrow thì dùng
        /// <see cref="IsConflict"/> — hẹp hơn là sai, vì nó sẽ để lọt deadlock.
        /// </remarks>
        public static bool IsUniqueViolation(Exception? exception, string constraintName)
        {
            foreach (var pg in PostgresExceptionsIn(exception))
            {
                if (pg.SqlState == UniqueViolation && pg.ConstraintName == constraintName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Câu tiếng Việt an toàn để hiển thị nếu đây là xung đột đồng thời; <c>null</c> nếu không phải.
        /// </summary>
        public static string? Classify(Exception? exception)
        {
            var current = exception;

            for (var depth = 0; current is not null && depth < MaxDepth; depth++, current = current.InnerException)
            {
                switch (current)
                {
                    // Chốt chống race BÊN TRONG transaction đã tự phát hiện và tự soạn thông báo
                    // tiếng Việt theo ngữ cảnh — nó biết nó đang làm gì, handler thì không.
                    // Dùng nguyên văn, đừng thay bằng câu chung.
                    case ConcurrentModificationException concurrent:
                        return concurrent.Message;

                    // Concurrency token khớp 0 dòng => ai đó vừa sửa bản ghi trong lúc ta đang sửa.
                    case DbUpdateConcurrencyException:
                        return "Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại trang và thử lại.";

                    case PostgresException pg when FromSqlState(pg.SqlState) is { } message:
                        return message;
                }
            }

            return null;
        }

        /// <summary>Duyệt chuỗi <c>InnerException</c>, trả về mọi <see cref="PostgresException"/> gặp được.</summary>
        private static IEnumerable<PostgresException> PostgresExceptionsIn(Exception? exception)
        {
            var current = exception;

            for (var depth = 0; current is not null && depth < MaxDepth; depth++, current = current.InnerException)
            {
                if (current is PostgresException pg) yield return pg;
            }
        }

        private static string? FromSqlState(string sqlState) => sqlState switch
        {
            UniqueViolation =>
                "Dữ liệu này vừa được người khác tạo hoặc thay đổi. Vui lòng tải lại trang và thử lại.",

            // Khác hẳn vi phạm unique về mặt hành động: không ai làm gì sai, giao dịch chỉ xui.
            // Bấm lại gần như chắc chắn thành công, nên câu phải nói đúng điều đó.
            DeadlockDetected or SerializationFailure =>
                "Hệ thống đang bận, vui lòng thử lại sau giây lát.",

            _ => null
        };
    }
}
