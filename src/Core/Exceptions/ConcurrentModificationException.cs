namespace PBL3.Core.Exceptions
{
    /// <summary>
    /// Ném ra khi chốt chống race BÊN TRONG một transaction phát hiện dữ liệu đã bị người
    /// khác thay đổi giữa lúc kiểm tra nghiệp vụ (ngoài transaction) và lúc ghi (trong
    /// transaction).
    /// </summary>
    /// <remarks>
    /// VÌ SAO CẦN MỘT KIỂU RIÊNG THAY VÌ <see cref="InvalidOperationException"/>:
    ///
    /// Các call-site retry-safe đều theo khuôn "kiểm tra nghiệp vụ ngoài (rẻ, trả lỗi đẹp)
    /// → nạp lại + kiểm tra lại bên trong (chốt chống race)". Chốt bên trong phải NÉM chứ
    /// không được <c>return</c>, vì <c>return</c> thì transaction vẫn commit.
    ///
    /// Nhưng mọi call-site đều có sẵn một <c>catch (Exception)</c> bọc ngoài để nuốt lỗi
    /// hạ tầng thành thông báo chung. Nếu chốt ném <see cref="InvalidOperationException"/>
    /// thì thông báo cụ thể ("phiếu vừa đổi trạng thái, tải lại trang") bị nuốt mất, và
    /// người dùng thấy "đã xảy ra lỗi, vui lòng thử lại" — bấm lại cũng hỏng y hệt.
    ///
    /// Kiểu riêng cho phép bắt nó TRƯỚC <c>catch (Exception)</c> và trả đúng thông báo.
    /// Bắt <see cref="InvalidOperationException"/> thay thế thì KHÔNG an toàn: EF Core và
    /// <c>IUnitOfWork</c> cũng ném đúng kiểu đó cho những chuyện hoàn toàn khác, nên sẽ
    /// báo nhầm lỗi hạ tầng thành xung đột đồng thời.
    /// </remarks>
    public class ConcurrentModificationException : Exception
    {
        public ConcurrentModificationException(string message) : base(message) { }
    }
}
