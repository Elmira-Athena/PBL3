namespace PBL3.Core.Exceptions
{
    /// <summary>
    /// Ném ra khi một luật NGHIỆP VỤ bị vi phạm và thông báo kèm theo là thông báo
    /// ĐÃ SOẠN CHO NGƯỜI DÙNG CUỐI: tiếng Việt có dấu, không lộ chi tiết kỹ thuật.
    /// </summary>
    /// <remarks>
    /// VÌ SAO CẦN KIỂU RIÊNG:
    ///
    /// Các phương thức đặt hàng bọc toàn bộ thân hàm trong <c>try { … } catch (Exception ex)</c>
    /// rồi ném lại, và controller trả thẳng <c>ex.Message</c> về cho client. Nghĩa là MỌI
    /// exception lọt vào khối catch đó đều thành thông báo người dùng đọc được.
    ///
    /// Trước đây khối catch ném lại <c>"Lỗi hệ thống khi đặt hàng: " + ex.Message</c>, nên khi
    /// nguồn lỗi là hạ tầng (EF Core, SQL Server) thì người dùng nhận nguyên văn tiếng Anh —
    /// <c>"An error occurred while saving the entity changes. See the inner exception for
    /// details."</c> — vừa vi phạm quy tắc tiếng Việt của CLAUDE.md, vừa lộ nội tạng ORM.
    /// LoadProbe S01/S02 tái hiện được đúng chuỗi đó ở nhánh thua cuộc đua sinh mã chứng từ.
    ///
    /// Không thể sửa bằng cách thay cả khối catch bằng một câu tiếng Việt cố định: khối đó
    /// cũng là đường đi của những thông báo nghiệp vụ ĐÚNG và HỮU ÍCH ("Mã 'X' đã hết hạn",
    /// "Mã 'X' đã hết lượt sử dụng") — nuốt chúng thành "đã xảy ra lỗi" là hồi quy UX, vì
    /// người dùng mất thông tin cần để tự sửa.
    ///
    /// Kiểu riêng tách được hai nhóm: <see cref="BusinessRuleException"/> đi ra NGUYÊN VĂN,
    /// còn mọi <see cref="System.Exception"/> khác bị ghi log rồi thay bằng một câu chung.
    /// Cùng lý lẽ với <see cref="ConcurrentModificationException"/>: bắt
    /// <see cref="System.InvalidOperationException"/> thay thế là KHÔNG an toàn vì EF Core
    /// dùng chính kiểu đó cho chuyện khác.
    ///
    /// QUY TẮC KHI DÙNG: message truyền vào <b>bắt buộc</b> là tiếng Việt có dấu và an toàn
    /// để hiển thị cho người ngoài. Đừng nối <c>ex.Message</c> của hạ tầng vào đây.
    /// </remarks>
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message) : base(message) { }
    }
}
