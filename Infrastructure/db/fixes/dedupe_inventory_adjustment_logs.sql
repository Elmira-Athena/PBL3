/* ============================================================================
   DỌN BẢN GHI TỔN THẤT NHÂN BẢN — chạy TRƯỚC migration
   AddConcurrencyTokensAndUniqueIndexes.

   🔴 ĐỌC HẾT KHỐI NÀY TRƯỚC KHI CHẠY. Script này XOÁ bản ghi kế toán.

   VÌ SAO CẦN: trước đợt 3, `InventoryCheckService.ApproveAsync` chốt điều kiện
   bằng check-then-act. LoadProbe S06 đo được: 5 lần phê duyệt song song cùng một
   phiếu → 5 bản ghi `InventoryAdjustmentLogs` cho CÙNG một (phiếu, serial), cả 5
   request đều trả HTTP 200. Sổ tổn thất bị cộng thừa gấp 5 lần.

   VÌ SAO KHÔNG NHÉT VÀO MIGRATION: đây là VIỆC NGHIỆP VỤ, không phải kỹ thuật.
   Số tiền tổn thất đã có thể vào báo cáo tài chính, đã có thể được đối chiếu và
   ký. Một migration im lặng xoá bản ghi kế toán là điều không được phép làm thay
   người chịu trách nhiệm. Migration chỉ CHẶN và chỉ sang đây.

   QUY TẮC GIỮ: giữ bản ghi có `Id` NHỎ NHẤT trong mỗi cặp (AuditCheckId, SerialId).
   Đó là bản ghi ĐẦU TIÊN — bản duy nhất đáng lẽ phải tồn tại; các bản sau là sản
   phẩm của cuộc đua. `CostImpact` của cả 5 bằng nhau (cùng đọc một giá vốn), nên
   giữ bản nào cũng ra cùng số tiền; chọn MIN(Id) để kết quả TIỀN ĐOÁN ĐƯỢC.

   TRƯỚC KHI CHẠY: đối chiếu số ở CHECK 1b của `../checks/pre_migration_checks.sql`
   với sổ tổn thất kế toán đang giữ. Nếu báo cáo đã phát hành dựa trên số CŨ (số bị
   thừa), việc điều chỉnh báo cáo là việc của kế toán — script này không làm.

   AN TOÀN: bản ghi bị xoá được SAO LƯU nguyên vẹn sang
   `InventoryAdjustmentLogs_DuplicateArchive` trước khi xoá. Bảng đó KHÔNG bị
   migration nào chạm tới; đừng xoá nó cho tới khi kế toán xác nhận xong.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- bất kỳ lỗi nào cũng rollback toàn bộ, không để dở dang

PRINT '=== DỌN InventoryAdjustmentLogs nhân bản — ' + CONVERT(varchar(30), SYSUTCDATETIME(), 126) + ' UTC ===';
PRINT '';

/* ── BƯỚC 0: đo trước ───────────────────────────────────────────────────── */
DECLARE @truocTong int = (SELECT COUNT(*) FROM InventoryAdjustmentLogs);
DECLARE @truocCapTrung int = (
    SELECT COUNT(*) FROM (
        SELECT AuditCheckId, SerialId
        FROM InventoryAdjustmentLogs
        GROUP BY AuditCheckId, SerialId
        HAVING COUNT(*) > 1
    ) x);

PRINT '--- TRƯỚC KHI DỌN ---';
PRINT '    Tổng bản ghi            : ' + CAST(@truocTong AS varchar(20));
PRINT '    Số cặp (phiếu, serial) trùng : ' + CAST(@truocCapTrung AS varchar(20));

IF @truocCapTrung = 0
BEGIN
    PRINT '';
    PRINT '✅ KHÔNG có bản ghi trùng. Không cần dọn — chạy migration được ngay.';
    RETURN;
END

/* ── BƯỚC 1: liệt kê thiệt hại, để người chạy ĐỌC trước khi nó xảy ra ──── */
PRINT '';
PRINT '--- CHI TIẾT CÁC CẶP SẼ BỊ DỌN ---';
SELECT  AuditCheckId,
        SerialId,
        COUNT(*)                              AS SoBanGhiHienCo,
        COUNT(*) - 1                          AS SoBanGhiSeXoa,
        MIN(Id)                               AS IdSeGiuLai,
        SUM(CostImpact)                       AS CostImpact_DangCong,
        MIN(CostImpact)                       AS CostImpact_SauKhiDon,
        SUM(CostImpact) - MIN(CostImpact)     AS CostImpact_SeGiamDi
FROM    InventoryAdjustmentLogs
GROUP BY AuditCheckId, SerialId
HAVING  COUNT(*) > 1
ORDER BY AuditCheckId, SerialId;

/* ── BƯỚC 2: sao lưu rồi xoá, trong MỘT transaction ────────────────────── */
BEGIN TRANSACTION;

    /* 🔴 Khai TƯỜNG MINH, không dùng `SELECT TOP(0) * INTO`.
       Lý do: `SELECT INTO` KẾ THỪA cả thuộc tính IDENTITY của cột `Id`, nên bảng lưu
       trữ cũng có identity và câu `INSERT ... SELECT l.*` sau đó chết với
       `Msg 8101 — An explicit value for the identity column ... IDENTITY_INSERT is OFF`.
       Bảng lưu trữ KHÔNG cần identity: nó phải giữ ĐÚNG `Id` gốc để đối chiếu. */
    IF OBJECT_ID('InventoryAdjustmentLogs_DuplicateArchive', 'U') IS NULL
    BEGIN
        CREATE TABLE InventoryAdjustmentLogs_DuplicateArchive (
            Id                   int             NOT NULL,   -- Id GỐC, không phải identity mới
            AuditCheckId         int             NOT NULL,
            SerialId             int             NOT NULL,
            VariantId            int             NOT NULL,
            OldStatus            tinyint         NOT NULL,
            NewStatus            tinyint         NOT NULL,
            AdjustmentType       tinyint         NOT NULL,
            CostImpact           decimal(18,2)   NOT NULL,
            Reason               nvarchar(500)   NULL,
            AdjustedDate         datetime2       NOT NULL,
            AdjustedByEmployeeId uniqueidentifier NOT NULL,
            ArchivedAt           datetime2       NOT NULL,
            ArchivedBy           nvarchar(128)   NOT NULL,
            CONSTRAINT PK_InventoryAdjustmentLogs_DuplicateArchive PRIMARY KEY (Id)
        );
        PRINT '';
        PRINT '    Đã tạo bảng lưu trữ InventoryAdjustmentLogs_DuplicateArchive.';
    END

    /* 🚨 CHỐT: danh sách cột ở trên là VIẾT TAY, nên nó có thể lạc hậu.
       Nếu ai đó thêm cột vào `InventoryAdjustmentLogs` mà quên sửa đây, bản lưu sẽ
       THIẾU cột đó — mất dữ liệu một cách im lặng, đúng loại lỗi tệ nhất. Đếm cột
       để hỏng ỒN ÀO thay vì im lặng. (11 = số cột lúc viết script.) */
    DECLARE @soCotNguon int = (
        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'InventoryAdjustmentLogs');
    IF @soCotNguon <> 11
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'DỪNG: InventoryAdjustmentLogs có %d cột, script viết cho 11. Cập nhật CREATE TABLE và câu INSERT trong script này trước khi chạy.', 16, 1, @soCotNguon);
        RETURN;
    END

    /* 🔴 Chốt Id của LẦN CHẠY NÀY vào bảng tạm.
       Đừng lấy danh sách xoá từ `InventoryAdjustmentLogs_DuplicateArchive`: bảng đó
       tích luỹ qua các lần chạy, nên lần chạy thứ hai sẽ quét cả Id đã xoá từ lần
       trước — và nếu IDENTITY đã cấp lại một Id nào đó cho bản ghi MỚI thì nó xoá
       oan dữ liệu hợp lệ. Bảng tạm khoanh đúng phạm vi một lần chạy. */
    CREATE TABLE #ThuaLanNay (Id int PRIMARY KEY);

    INSERT INTO #ThuaLanNay (Id)
    SELECT l.Id
    FROM   InventoryAdjustmentLogs l
    WHERE  l.Id > (SELECT MIN(m.Id) FROM InventoryAdjustmentLogs m
                   WHERE m.AuditCheckId = l.AuditCheckId
                     AND m.SerialId     = l.SerialId);

    INSERT INTO InventoryAdjustmentLogs_DuplicateArchive (
            Id, AuditCheckId, SerialId, VariantId, OldStatus, NewStatus,
            AdjustmentType, CostImpact, Reason, AdjustedDate, AdjustedByEmployeeId,
            ArchivedAt, ArchivedBy)
    SELECT  l.Id, l.AuditCheckId, l.SerialId, l.VariantId, l.OldStatus, l.NewStatus,
            l.AdjustmentType, l.CostImpact, l.Reason, l.AdjustedDate, l.AdjustedByEmployeeId,
            SYSUTCDATETIME(), SUSER_SNAME()
    FROM    InventoryAdjustmentLogs l
    WHERE   l.Id IN (SELECT Id FROM #ThuaLanNay);

    DECLARE @daLuu int = @@ROWCOUNT;

    DELETE l
    FROM   InventoryAdjustmentLogs l
    WHERE  l.Id IN (SELECT Id FROM #ThuaLanNay);

    DECLARE @daXoa int = @@ROWCOUNT;

    /* 🔴 CHỐT: sao lưu và xoá PHẢI khớp số. Lệch nghĩa là có bản ghi bị xoá mà
       không có bản lưu — rollback, không thương lượng. */
    IF @daLuu <> @daXoa
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'DỪNG: số bản ghi sao lưu (%d) khác số bản ghi xoá (%d). Đã rollback, không mất dữ liệu.', 16, 1, @daLuu, @daXoa);
        RETURN;
    END

COMMIT TRANSACTION;

/* ── BƯỚC 3: đo sau, và tự kiểm bất biến ───────────────────────────────── */
DECLARE @sauCapTrung int = (
    SELECT COUNT(*) FROM (
        SELECT AuditCheckId, SerialId
        FROM InventoryAdjustmentLogs
        GROUP BY AuditCheckId, SerialId
        HAVING COUNT(*) > 1
    ) x);

DECLARE @sauTong int = (SELECT COUNT(*) FROM InventoryAdjustmentLogs);

PRINT '';
PRINT '--- SAU KHI DỌN ---';
PRINT '    Bản ghi đã sao lưu + xoá     : ' + CAST(@daXoa AS varchar(20));
PRINT '    Tổng bản ghi còn lại         : ' + CAST(@sauTong AS varchar(20));
PRINT '    Số cặp (phiếu, serial) trùng : ' + CAST(@sauCapTrung AS varchar(20)) + '  (kỳ vọng 0)';

IF @sauCapTrung <> 0
BEGIN
    RAISERROR (N'DỪNG: vẫn còn %d cặp trùng sau khi dọn. Migration sẽ vẫn thất bại — điều tra trước.', 16, 1, @sauCapTrung);
    RETURN;
END

PRINT '';
PRINT '✅ XONG. Bản gốc nằm ở InventoryAdjustmentLogs_DuplicateArchive.';
PRINT '   Chạy migration được: dotnet ef database update';
