-- =============================================
-- KIỂM DỮ LIỆU TRƯỚC KHI THÊM RÀNG BUỘC (Đợt 0)
-- =============================================
-- Chạy script này TRƯỚC khi thêm các unique index ở đợt 3.
-- Nếu bất kỳ truy vấn nào trả về dòng, migration sẽ FAIL ở task migrator
-- lúc deploy — tức lúc tệ nhất để phát hiện. Biết bây giờ thì còn thời gian xử.
--
-- Cách chạy (local):
--   sqlcmd -S localhost,1433 -U sa -P "$SA_PASSWORD" -C -d HushStoreDb \
--          -i Infrastructure/db/checks/pre_migration_checks.sql
--
-- Cách chạy (RDS): thay -S bằng endpoint, bỏ -C nếu đã cài RDS CA bundle.
-- =============================================

SET NOCOUNT ON;
PRINT '===============================================';
PRINT 'KIEM DU LIEU TRUOC MIGRATION - ' + CONVERT(NVARCHAR(30), GETUTCDATE(), 126) + ' UTC';
PRINT '===============================================';
PRINT '';

-- ---------------------------------------------------------------
-- CHECK 1 — InventoryAdjustmentLogs trùng (AuditCheckId, SerialId)
-- ---------------------------------------------------------------
-- Vì sao: InventoryCheckService duyệt phiếu kiểm kê theo mẫu check-then-act
-- (đọc Status ở dòng ~650, ghi ở ~748). Không có unique index trên cặp cột này,
-- nên phê duyệt hai lần ghi trùng log => KE TOAN TON THAT BI NHAN DOI.
--
-- Đây là check QUAN TRỌNG NHẤT trong ba cái: lỗi đã chạy trên production nên
-- khả năng có dữ liệu bẩn là cao thật, và việc dọn nó là VIỆC NGHIỆP VỤ
-- (phải đối chiếu sổ tổn thất), không phải việc kỹ thuật.
PRINT '--- CHECK 1: InventoryAdjustmentLogs trung (AuditCheckId, SerialId) ---';

SELECT
    l.AuditCheckId,
    l.SerialId,
    COUNT(*)                AS SoBanGhiTrung,
    SUM(l.CostImpact)       AS TongCostImpact_DangBiTinhTrung,
    MIN(l.AdjustedDate)     AS LanGhiDauTien,
    MAX(l.AdjustedDate)     AS LanGhiCuoiCung
FROM InventoryAdjustmentLogs AS l
INNER JOIN InventoryChecks AS c ON c.Id = l.AuditCheckId
WHERE c.IsDeleted = 0                  -- khớp global query filter: !l.AuditCheck.IsDeleted
GROUP BY l.AuditCheckId, l.SerialId
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC, l.AuditCheckId;

PRINT '  => RONG = an toan, tao unique index duoc.';
PRINT '  => CO DONG = phai don du lieu VA doi chieu so ton that truoc khi migrate.';
PRINT '';

-- Tổng thiệt hại kế toán do ghi trùng, để biết quy mô việc phải đối chiếu.
PRINT '--- CHECK 1b: Quy mo sai lech ke toan do ghi trung ---';
;WITH Dup AS (
    SELECT l.AuditCheckId, l.SerialId, COUNT(*) AS n, SUM(l.CostImpact) AS tong
    FROM InventoryAdjustmentLogs AS l
    INNER JOIN InventoryChecks AS c ON c.Id = l.AuditCheckId
    WHERE c.IsDeleted = 0
    GROUP BY l.AuditCheckId, l.SerialId
    HAVING COUNT(*) > 1
)
SELECT
    COUNT(*)                                    AS SoCapBiTrung,
    ISNULL(SUM(n), 0)                           AS TongSoBanGhi,
    ISNULL(SUM(n) - COUNT(*), 0)                AS SoBanGhiThua,
    ISNULL(SUM(tong - (tong / n)), 0)           AS CostImpact_BI_TINH_THUA
FROM Dup;
PRINT '';

-- ---------------------------------------------------------------
-- CHECK 2 — Vouchers.MaxUsesPerUser có giá trị > 1 không
-- ---------------------------------------------------------------
-- Vì sao: quyết định này chọn giữa hai cách sửa, chênh nhau ~1 ngày công.
--
--   Toàn NULL hoặc 1  -> unique index (UserId, VoucherId) la du.
--                        Chot luat "1 luot/khach/ma" va ghi vao CLAUDE.md.
--
--   Co gia tri > 1    -> unique index don gian se EP SAI NGHIEP VU.
--                        Phai them cot SeqPerUser + unique (UserId, VoucherId, SeqPerUser),
--                        backfill bang ROW_NUMBER() OVER (PARTITION BY UserId, VoucherId).
PRINT '--- CHECK 2: Phan bo Vouchers.MaxUsesPerUser ---';

SELECT
    CASE
        WHEN MaxUsesPerUser IS NULL THEN 'NULL (khong gioi han)'
        ELSE CAST(MaxUsesPerUser AS NVARCHAR(20))
    END                     AS MaxUsesPerUser,
    COUNT(*)                AS SoVoucher,
    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS DangHoatDong
FROM Vouchers
WHERE IsDeleted = 0
GROUP BY MaxUsesPerUser
ORDER BY
    CASE WHEN MaxUsesPerUser IS NULL THEN -1 ELSE MaxUsesPerUser END;

PRINT '  => Neu KHONG co dong nao MaxUsesPerUser > 1: dung unique index don gian.';
PRINT '  => Neu CO: phai them cot SeqPerUser.';
PRINT '';

-- Đã có ai vượt hạn mức chưa (bằng chứng thực nghiệm cho race MaxUsesPerUser)
PRINT '--- CHECK 2b: Khach da dung VUOT MaxUsesPerUser (race da xay ra thuc te) ---';
SELECT
    vu.UserId,
    vu.VoucherId,
    v.Code,
    v.MaxUsesPerUser        AS HanMuc,
    COUNT(*)                AS SoLanDaDung
FROM VoucherUsages AS vu
INNER JOIN Vouchers AS v ON v.Id = vu.VoucherId
WHERE v.IsDeleted = 0
  AND v.MaxUsesPerUser IS NOT NULL
GROUP BY vu.UserId, vu.VoucherId, v.Code, v.MaxUsesPerUser
HAVING COUNT(*) > MIN(v.MaxUsesPerUser)
ORDER BY COUNT(*) DESC;

PRINT '  => CO DONG = race check-then-act da gay thiet hai tien that roi.';
PRINT '';

-- Voucher đã dùng vượt tổng số lượng phát hành
PRINT '--- CHECK 2c: Voucher da dung VUOT tong so luong (Quantity) ---';
SELECT
    v.Id,
    v.Code,
    v.Quantity              AS SoLuongPhatHanh,
    v.UsedCount             AS UsedCount_CotDem,
    COUNT(vu.VoucherId)     AS SoLanDungThucTe
FROM Vouchers AS v
LEFT JOIN VoucherUsages AS vu ON vu.VoucherId = v.Id
WHERE v.IsDeleted = 0
  AND v.Quantity IS NOT NULL
GROUP BY v.Id, v.Code, v.Quantity, v.UsedCount
HAVING v.UsedCount > v.Quantity
    OR COUNT(vu.VoucherId) > v.Quantity
    OR v.UsedCount <> COUNT(vu.VoucherId)   -- UsedCount lech so lan dung that = lost update
ORDER BY v.Id;

PRINT '  => UsedCount <> SoLanDungThucTe la dau hieu truc tiep cua lost update.';
PRINT '';

-- ---------------------------------------------------------------
-- CHECK 3 — ServiceTickets: nhiều phiếu chưa đóng trên cùng một serial
-- ---------------------------------------------------------------
-- Vì sao: ServiceTicketService kiểm HasOpenTicketForSerialAsync rồi mới AddAsync,
-- và index trên SerialId KHÔNG unique => race tao hai phieu cung ton tai, IM LANG.
--
-- Vị từ dưới đây phải khớp CHÍNH XÁC với ServiceTicketRepository
-- .HasOpenTicketForSerialAsync (terminal = 3 QuoteRejected, 8 Swapped,
-- 9 Completed, 10 Cancelled) cộng global query filter !IsDeleted.
-- Khi tạo filtered index ở đợt 3, vị từ index cũng phải khớp đúng bộ này.
PRINT '--- CHECK 3: Nhieu phieu dich vu CHUA DONG tren cung mot serial ---';

SELECT
    t.SerialId,
    COUNT(*)                                  AS SoPhieuChuaDong,
    STRING_AGG(CAST(t.TicketCode AS NVARCHAR(MAX)), ', ')
        WITHIN GROUP (ORDER BY t.Id)          AS DanhSachMaPhieu,
    STRING_AGG(CAST(t.Status AS NVARCHAR(10)), ', ')
        WITHIN GROUP (ORDER BY t.Id)          AS CacTrangThai
FROM ServiceTickets AS t
WHERE t.IsDeleted = 0
  AND t.Status NOT IN (3, 8, 9, 10)
GROUP BY t.SerialId
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC, t.SerialId;

PRINT '  => RONG = an toan, tao filtered unique index duoc.';
PRINT '  => CO DONG = phai dong bot phieu thua truoc khi migrate.';
PRINT '';

-- ---------------------------------------------------------------
-- CHECK 4 — Mã chứng từ trùng (bằng chứng cho race sinh mã)
-- LƯU Ý SCHEMA: Orders và InventoryChecks KHÔNG có cột IsDeleted (khác với
-- khẳng định "mọi entity đều có IsDeleted" trong CLAUDE.md), nên không lọc soft-delete ở đây.
-- ---------------------------------------------------------------
-- Không chặn migration (các cột này đã có unique index), nhưng cho biết
-- race sinh mã đã bắn ra HTTP 500 bao nhiêu lần trong thực tế.
PRINT '--- CHECK 4: Ma chung tu trung (dang le khong the co, unique index da chan) ---';

SELECT 'Orders.OrderCode' AS Cot, OrderCode AS Ma, COUNT(*) AS SoLan
FROM Orders GROUP BY OrderCode HAVING COUNT(*) > 1
UNION ALL
SELECT 'ServiceTickets.TicketCode', TicketCode, COUNT(*)
FROM ServiceTickets WHERE IsDeleted = 0 GROUP BY TicketCode HAVING COUNT(*) > 1
UNION ALL
SELECT 'ImportReceipts.ReceiptCode', ReceiptCode, COUNT(*)
FROM ImportReceipts WHERE IsDeleted = 0 GROUP BY ReceiptCode HAVING COUNT(*) > 1
UNION ALL
SELECT 'InventoryChecks.CheckCode', CheckCode, COUNT(*)
FROM InventoryChecks GROUP BY CheckCode HAVING COUNT(*) > 1;

PRINT '';

-- Số chứng từ mỗi ngày: nếu đã có ngày nào > 999 thì bom hen gio {n:D3} da no.
PRINT '--- CHECK 4b: Ngay nao da vuot 999 chung tu (bom {n:D3}) ---';
SELECT CAST(OrderDate AS DATE) AS Ngay, COUNT(*) AS SoDon
FROM Orders
GROUP BY CAST(OrderDate AS DATE)
HAVING COUNT(*) > 900          -- canh bao som tu 900
ORDER BY COUNT(*) DESC;

PRINT '';

-- ---------------------------------------------------------------
-- CHECK 5 — Serial bị ghi đè trạng thái mù quáng
-- ---------------------------------------------------------------
-- Serial vừa nằm trong đơn đã bán, vừa bị đánh dấu Lost/Defective bởi kiểm kê.
-- Đây là dấu vết của lỗi ghi đè trạng thái (đọc dòng 665, SaveChanges dòng 752).
-- SerialStatus: 0 Available, 1 Reserved, 2 Sold, 3 Defective, 4 Returned, 5 Lost
PRINT '--- CHECK 5: Serial vua ban vua bi danh dau Lost/Defective ---';

SELECT
    ps.Id                   AS SerialId,
    ps.SerialNumber,
    ps.Status               AS TrangThaiHienTai,
    o.OrderCode             AS DonHangDaBan,
    l.AdjustmentType        AS LoaiDieuChinh,
    l.AdjustedDate
FROM ProductSerials AS ps
INNER JOIN OrderSerials AS os ON os.SerialId = ps.Id
INNER JOIN OrderDetails AS od ON od.Id = os.OrderDetailId
INNER JOIN Orders AS o ON o.Id = od.OrderId
INNER JOIN InventoryAdjustmentLogs AS l ON l.SerialId = ps.Id
WHERE ps.Status IN (3, 5)          -- Defective hoac Lost
ORDER BY l.AdjustedDate DESC;

PRINT '  => CO DONG = serial khach da mua bi ghi de thanh Lost/Defective.';
PRINT '';

PRINT '===============================================';
PRINT 'XONG. Moi truy van RONG => an toan chay migration dot 3.';
PRINT 'Bat ky truy van nao co dong => xu ly truoc, xem chu thich tung check.';
PRINT '===============================================';
