namespace PBL3.Core.Constants
{
    /// <summary>
    /// Tên các unique index / check constraint được đặt tường minh trong <c>HushStoreDbContext</c>.
    /// </summary>
    /// <remarks>
    /// 🎯 <b>Vì sao gom vào đây thay vì để chuỗi rải rác.</b> Sau khi chuyển PostgreSQL,
    /// <c>PostgresException.ConstraintName</c> cho biết <b>tên constraint bị vi phạm</b> — thứ
    /// <c>SqlException</c> của SQL Server không có. Điều đó cho phép
    /// <c>ConflictClassifier.IsUniqueViolation(ex, tên)</c> thay cho lối viết hiện tại, vốn chỉ
    /// bắt "một vi phạm unique nào đó" rồi dựa vào một lập luận cục bộ để đoán là cái nào.
    ///
    /// Lập luận cục bộ ấy có thật, và nó đang được ghi thành comment ở
    /// <c>InventoryCheckService</c>: <i>"trong PHẠM VI hàm này 2601 chỉ có MỘT nguồn duy nhất"</i>,
    /// kèm cảnh báo <i>"đừng copy khuôn này sang hàm có nhiều index"</i>. Lập luận đúng — nhưng
    /// <b>không có gì bắt lỗi khi nó hết đúng</b>: thêm một unique index vào đúng đường đó thì
    /// khối <c>catch</c> âm thầm gán nhầm câu nghiệp vụ cho một vi phạm khác. So theo tên biến nó
    /// thành đúng <b>theo cấu trúc</b>, và cảnh báo "đừng copy" được xoá vì khuôn nay an toàn để copy.
    ///
    /// Chuỗi ở đây phải trùng <b>nguyên văn</b> với <c>HasDatabaseName(...)</c> trong
    /// <c>HushStoreDbContext</c>. Không có compiler nào kiểm chéo hai bên — đó là lý do cả hai bên
    /// phải dùng chung hằng trong file này, chứ không phải chép chuỗi.
    ///
    /// 🚨 <b>PostgreSQL cắt identifier ở 63 byte.</b> Tên dài hơn thì
    /// <c>ConstraintName</c> trả về tên <b>ĐÃ CẮT</b> trong khi hằng ở đây là tên đầy đủ ⇒ phép so
    /// sánh luôn false ⇒ exception <b>im lặng rơi xuống <c>catch</c> tổng</b> và người dùng nhận
    /// "lỗi hệ thống" thay vì câu nghiệp vụ. Độ dài hiện tại (ký tự = byte vì toàn ASCII):
    /// <list type="bullet">
    ///   <item><c>UQ_InventoryCheckDetailSerials_CheckId_SerialNumberRaw</c> — 54</item>
    ///   <item><c>UQ_VoucherUsages_UserId_VoucherId_SeqPerUser</c> — 44</item>
    ///   <item><c>UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId</c> — 47</item>
    /// </list>
    /// Tất cả dưới 63. <b>Đặt thêm tên nào cũng phải đếm lại</b> — giới hạn này không báo lỗi khi
    /// vượt, nó chỉ lặng lẽ cắt.
    /// </remarks>
    public static class DbConstraints
    {
        /// <summary>Một serial chỉ được có tối đa một phiếu dịch vụ CHƯA ĐÓNG.</summary>
        /// <remarks>
        /// Filtered index (PostgreSQL gọi là partial index). Vị từ của nó phải khớp
        /// <b>nguyên văn</b> với <c>ServiceTicketRepository.HasOpenTicketForSerialAsync</c> —
        /// thêm một trạng thái terminal mà quên sửa index thì bất biến âm thầm nới ra.
        /// </remarks>
        public const string ServiceTicketSerialOpen = "UQ_ServiceTickets_SerialId_Open";

        /// <summary>Chặn ghi trùng sổ điều chỉnh tồn kho khi phê duyệt hai lần.</summary>
        public const string InventoryAdjustmentLogAuditSerial =
            "UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId";

        /// <summary>Chặn hai dòng cùng serial trong một phiếu kiểm kê.</summary>
        public const string InventoryCheckDetailSerial =
            "UQ_InventoryCheckDetailSerials_CheckId_SerialNumberRaw";

        /// <summary>
        /// Chặn hai lượt dùng voucher cùng <c>SeqPerUser</c> cho một cặp (khách, voucher).
        /// </summary>
        /// <remarks>
        /// ⚠️ Index này <b>KHÔNG</b> biết <c>Voucher.MaxUsesPerUser</c> — nó chỉ chặn hai bản ghi
        /// cùng khoá. Hạn mức đếm được vẫn phải kiểm lại <b>bên trong</b> transaction.
        /// </remarks>
        public const string VoucherUsagePerUserSeq = "UQ_VoucherUsages_UserId_VoucherId_SeqPerUser";

        /// <summary>Một khách chỉ đánh giá một sản phẩm một lần.</summary>
        public const string ProductReviewProductUser = "UQ_ProductReviews_ProductId_UserId";
    }
}
