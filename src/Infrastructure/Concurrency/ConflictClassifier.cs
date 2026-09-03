using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
    ///   <item><b>27 chốt controller</b> quyết định cái gì được THOÁT ra —
    ///     <c>DbUpdateConcurrencyException</c> và <c>SqlException 2601/2627</c>.</item>
    ///   <item><b><c>ConflictExceptionHandler</c></b> quyết định cái gì thành 409 — ba loại trên
    ///     <b>cộng thêm</b> <c>1205</c> (deadlock) và <see cref="ConcurrentModificationException"/>.</item>
    /// </list>
    /// Giao của hai danh sách mới là thứ thật sự chạy. Phần dôi ra ở handler là <b>code chết</b>:
    /// deadlock bị <c>catch (Exception)</c> của controller nuốt trước khi middleware kịp thấy.
    /// Đo được: <c>grep 1205 src/</c> ra đúng <b>1</b> chỗ — chính dòng khai báo trong handler.
    ///
    /// 🔴 <b>Và một đường thứ hai còn khó thấy hơn: <c>EnableRetryOnFailure</c> ĐÃ BẬT.</b>
    /// Deadlock nằm trong danh sách transient của SQL Server, nên nó <b>bị thử lại</b>. Hết lượt
    /// thì EF Core bọc nguyên nhân gốc vào <c>RetryLimitExceededException</c>. Phép so khớp cũ
    /// (<c>DbUpdateException { InnerException: SqlException }</c> hoặc <c>SqlException</c> trần)
    /// <b>không khớp cái nào</b> ⇒ rơi xuống handler tổng ⇒ <b>500</b>. Nói với người dùng
    /// "server hỏng" trong khi sự thật là "thử lại đi" — đúng lỗi mà 409 sinh ra để tránh.
    ///
    /// Vì vậy lớp này <b>đi hết chuỗi <c>InnerException</c></b> thay vì so khớp một tầng. Cách đó
    /// không cần nhắc tên <c>RetryLimitExceededException</c>, nên nó cũng đúng cho mọi lớp bọc
    /// khác xuất hiện sau này — kể cả lớp bọc do chính ta viết.
    ///
    /// ⚠️ <b>Hệ quả có chủ ý của việc đi hết chuỗi:</b> một <c>Exception</c> chung bọc quanh
    /// deadlock vẫn được xếp là xung đột. Đó là điều <em>mong muốn</em> — bản chất lỗi không đổi
    /// vì ai đó bọc nó lại. Ở khuôn của <c>CLAUDE.md</c>, chốt xung đột luôn đứng TRƯỚC
    /// <c>catch (Exception)</c> nên đường thường không bao giờ bọc nhầm.
    ///
    /// 🚨 <b>Phân loại theo SỐ LỖI, tuyệt đối không dò <c>ex.Message</c>.</b> Chuỗi của SQL Server
    /// là tiếng Anh và đổi theo phiên bản — dò chuỗi là dùng biểu diễn thay cho ngữ nghĩa.
    /// </remarks>
    public static class ConflictClassifier
    {
        /// <summary>Vi phạm unique index (2601) / unique constraint (2627).</summary>
        private const int UniqueIndexViolation = 2601;
        private const int UniqueConstraintViolation = 2627;

        /// <summary>Deadlock — SQL Server đã chọn giao dịch này làm nạn nhân.</summary>
        private const int DeadlockVictim = 1205;

        /// <summary>
        /// Chặn trên số tầng <c>InnerException</c> được duyệt.
        /// </summary>
        /// <remarks>
        /// Chuỗi <c>InnerException</c> về lý thuyết có thể tạo vòng lặp (không có gì trong CLR cấm
        /// điều đó), và một vòng lặp ở đây sẽ treo <b>bên trong exception filter</b> — chỗ khó chẩn
        /// đoán nhất có thể tưởng tượng. Chuỗi sâu nhất thật sự trong repo là 3 tầng
        /// (<c>RetryLimitExceededException → DbUpdateException → SqlException</c>), nên 16 là dư dả.
        /// </remarks>
        private const int MaxDepth = 16;

        /// <summary>
        /// <c>true</c> nếu đây là xung đột đồng thời — dùng trong <c>catch (Exception ex) when (...)</c>
        /// ở controller để cho nó THOÁT ra tới <c>ConflictExceptionHandler</c>.
        /// </summary>
        public static bool IsConflict(Exception? exception) => Classify(exception) is not null;

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

                    // RowVersion khớp 0 dòng => ai đó vừa sửa bản ghi trong lúc ta đang sửa.
                    case DbUpdateConcurrencyException:
                        return "Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại trang và thử lại.";

                    case SqlException sql when FromSqlError(sql.Number) is { } message:
                        return message;
                }
            }

            return null;
        }

        private static string? FromSqlError(int number) => number switch
        {
            UniqueIndexViolation or UniqueConstraintViolation =>
                "Dữ liệu này vừa được người khác tạo hoặc thay đổi. Vui lòng tải lại trang và thử lại.",

            // Khác hẳn hai số trên về mặt hành động: không ai làm gì sai, giao dịch chỉ xui.
            // Bấm lại gần như chắc chắn thành công, nên câu phải nói đúng điều đó.
            DeadlockVictim =>
                "Hệ thống đang bận, vui lòng thử lại sau giây lát.",

            _ => null
        };
    }
}
