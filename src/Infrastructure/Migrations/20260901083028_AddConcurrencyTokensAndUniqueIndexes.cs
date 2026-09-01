using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokensAndUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ════════════════════════════════════════════════════════════════════
            // BƯỚC 0 — CHỐT CHẶN DỮ LIỆU, phải chạy TRƯỚC mọi thay đổi schema.
            //
            // 🔴 Vì sao chặn thay vì tự dọn: hai chốt dưới đây phát hiện dữ liệu mà
            // unique index mới không chấp nhận được. Dọn nó là QUYẾT ĐỊNH NGHIỆP VỤ —
            // bản ghi tổn thất có thể đã vào báo cáo tài chính đã ký, phiếu dịch vụ
            // thừa có thể đang có kỹ thuật viên làm dở. Migration không được quyết
            // thay người chịu trách nhiệm, nên nó DỪNG và chỉ đúng script phải chạy.
            //
            // ✅ Đã đo là an toàn: EF bọc cả migration trong MỘT transaction. Lần chạy
            // thật trên DB có 5 bản ghi trùng đã ném `Error 1505` và rollback SẠCH —
            // 0 cột, 0 index, `__EFMigrationsHistory` không ghi nhận, index cũ còn
            // nguyên. Nên `THROW` ở đây không để lại schema dở dang.
            //
            // ⚠️ Không thay `THROW` bằng `PRINT`: một cảnh báo bị cuộn qua trong log
            // CI thì migration sẽ chạy tiếp rồi chết ở `CREATE UNIQUE INDEX` với một
            // câu tiếng Anh không nói được phải làm gì.
            // ════════════════════════════════════════════════════════════════════

            // Chốt 1 — bản ghi tổn thất nhân bản (LoadProbe S06 đo được nhân 5).
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM InventoryAdjustmentLogs
                    GROUP BY AuditCheckId, SerialId HAVING COUNT(*) > 1)
                BEGIN
                    DECLARE @soCap int = (SELECT COUNT(*) FROM (
                        SELECT AuditCheckId, SerialId FROM InventoryAdjustmentLogs
                        GROUP BY AuditCheckId, SerialId HAVING COUNT(*) > 1) x);
                    DECLARE @msg nvarchar(1000) = CONCAT(
                        N'DUNG MIGRATION: co ', @soCap,
                        N' cap (AuditCheckId, SerialId) trung trong InventoryAdjustmentLogs. ',
                        N'Unique index UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId khong tao duoc. ',
                        N'Chay truoc: Infrastructure/db/fixes/dedupe_inventory_adjustment_logs.sql ',
                        N'(no sao luu sang InventoryAdjustmentLogs_DuplicateArchive roi moi xoa). ',
                        N'Doi chieu so tien o CHECK 1b cua pre_migration_checks.sql voi so ton that ke toan TRUOC KHI don.');
                    THROW 50001, @msg, 1;
                END");

            // Chốt 2 — nhiều phiếu dịch vụ chưa đóng trên cùng một serial (S04).
            // Vị từ phải KHỚP với filter của UQ_ServiceTickets_SerialId_Open bên dưới.
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM ServiceTickets
                    WHERE Status NOT IN (3, 8, 9, 10) AND IsDeleted = 0
                    GROUP BY SerialId HAVING COUNT(*) > 1)
                BEGIN
                    DECLARE @soSerial int = (SELECT COUNT(*) FROM (
                        SELECT SerialId FROM ServiceTickets
                        WHERE Status NOT IN (3, 8, 9, 10) AND IsDeleted = 0
                        GROUP BY SerialId HAVING COUNT(*) > 1) x);
                    DECLARE @msg2 nvarchar(1000) = CONCAT(
                        N'DUNG MIGRATION: co ', @soSerial,
                        N' serial dang co NHIEU HON MOT phieu dich vu chua dong. ',
                        N'Unique index UQ_ServiceTickets_SerialId_Open khong tao duoc. ',
                        N'Xem CHECK 3 cua Infrastructure/db/checks/pre_migration_checks.sql de lay danh sach ma phieu, ',
                        N'roi dong bot phieu thua qua giao dien (Huy = 10) truoc khi migrate. ',
                        N'DUNG xoa truc tiep bang SQL: phieu dich vu co lich su trang thai va co the co bao gia.');
                    THROW 50002, @msg2, 1;
                END");

            migrationBuilder.DropIndex(
                name: "IX_VoucherUsages_UserId_VoucherId",
                table: "VoucherUsages");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_SerialId",
                table: "ServiceTickets");

            migrationBuilder.AddColumn<int>(
                name: "SeqPerUser",
                table: "VoucherUsages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ════════════════════════════════════════════════════════════════════
            // BACKFILL SeqPerUser — BẮT BUỘC, và phải nằm TRƯỚC CreateIndex.
            //
            // 🔴 `defaultValue: 0` ở trên đặt MỌI hàng cũ về 0. Nếu một khách đã dùng
            // cùng một voucher 2 lần (hoàn toàn hợp lệ khi MaxUsesPerUser > 1) thì cả
            // hai hàng cùng (UserId, VoucherId, 0) → `CREATE UNIQUE INDEX` chết với
            // `Error 1505`. Đây là cái bẫy do CHÍNH migration tự tạo ra, không phải
            // do dữ liệu bẩn sẵn — nên nó được vá TẠI ĐÂY chứ không đẩy sang script.
            //
            // Khác chốt 1 và chốt 2 ở trên một điểm quyết định: việc này KHÔNG XOÁ GÌ
            // và không mất thông tin — nó chỉ đánh số các hàng đang có theo đúng thứ
            // tự đã xảy ra. Vì vậy tự động hoá được, và ROW_NUMBER bảo đảm
            // (UserId, VoucherId, SeqPerUser) là duy nhất TỰ THÂN CẤU TRÚC.
            //
            // ORDER BY Id: `Id` là IDENTITY tăng dần nên nó chính là thứ tự chèn thật.
            // Đừng đổi sang `UsedDate` — cột đó do code gán và hai hàng có thể cùng mốc.
            // ════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                WITH danhSo AS (
                    SELECT SeqPerUser,
                           ROW_NUMBER() OVER (PARTITION BY UserId, VoucherId ORDER BY Id) AS rn
                    FROM   VoucherUsages)
                UPDATE danhSo SET SeqPerUser = rn;
                -- ⚠️ Cột đích PHẢI có trong phần chiếu của CTE. Bản đầu chỉ chiếu `Id` và
                -- `rn` rồi `SET SeqPerUser = rn` — SQL Server báo `Invalid column name
                -- 'SeqPerUser'`, nghe như cột chưa được tạo (nó đã tạo rồi), nên rất dễ
                -- đi sai hướng sang nghi ngờ thứ tự lệnh trong migration.");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ServiceTickets",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RmaShipments",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Quotations",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProductSerials",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Orders",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InventoryChecks",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_VoucherUsages_UserId_VoucherId_SeqPerUser",
                table: "VoucherUsages",
                columns: new[] { "UserId", "VoucherId", "SeqPerUser" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ServiceTickets_SerialId_Open",
                table: "ServiceTickets",
                column: "SerialId",
                unique: true,
                filter: "[Status] <> 3 AND [Status] <> 8 AND [Status] <> 9 AND [Status] <> 10 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId",
                table: "InventoryAdjustmentLogs",
                columns: new[] { "AuditCheckId", "SerialId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_VoucherUsages_UserId_VoucherId_SeqPerUser",
                table: "VoucherUsages");

            migrationBuilder.DropIndex(
                name: "UQ_ServiceTickets_SerialId_Open",
                table: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "UQ_InventoryAdjustmentLogs_AuditCheckId_SerialId",
                table: "InventoryAdjustmentLogs");

            migrationBuilder.DropColumn(
                name: "SeqPerUser",
                table: "VoucherUsages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RmaShipments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductSerials");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryChecks");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_UserId_VoucherId",
                table: "VoucherUsages",
                columns: new[] { "UserId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_SerialId",
                table: "ServiceTickets",
                column: "SerialId");
        }
    }
}
