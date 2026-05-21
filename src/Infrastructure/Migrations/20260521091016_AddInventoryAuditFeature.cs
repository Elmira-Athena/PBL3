using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAuditFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "InventoryChecks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByEmployeeId",
                table: "InventoryChecks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "InventoryChecks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "InventoryChecks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeCategoryId",
                table: "InventoryChecks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ScopeType",
                table: "InventoryChecks",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotAt",
                table: "InventoryChecks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DefectiveQuantity",
                table: "InventoryCheckDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MatchedQuantity",
                table: "InventoryCheckDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MissingQuantity",
                table: "InventoryCheckDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SurplusQuantity",
                table: "InventoryCheckDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "InventoryAdjustmentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditCheckId = table.Column<int>(type: "int", nullable: false),
                    SerialId = table.Column<int>(type: "int", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    NewStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    AdjustmentType = table.Column<byte>(type: "tinyint", nullable: false),
                    CostImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdjustedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdjustedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAdjustmentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryAdjustmentLogs_InventoryChecks_AuditCheckId",
                        column: x => x.AuditCheckId,
                        principalTable: "InventoryChecks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryAdjustmentLogs_ProductSerials_SerialId",
                        column: x => x.SerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryAdjustmentLogs_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryCheckDetailSerials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckId = table.Column<int>(type: "int", nullable: false),
                    DetailId = table.Column<int>(type: "int", nullable: true),
                    VariantId = table.Column<int>(type: "int", nullable: true),
                    SerialId = table.Column<int>(type: "int", nullable: true),
                    SerialNumberRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginalStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ScanStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScannedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProposedActionNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedDuringApproval = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCheckDetailSerials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCheckDetailSerials_InventoryCheckDetails_DetailId",
                        column: x => x.DetailId,
                        principalTable: "InventoryCheckDetails",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCheckDetailSerials_InventoryChecks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "InventoryChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryCheckDetailSerials_ProductSerials_SerialId",
                        column: x => x.SerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCheckDetailSerials_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryChecks_CheckCode",
                table: "InventoryChecks",
                column: "CheckCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryChecks_CheckDate",
                table: "InventoryChecks",
                column: "CheckDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryChecks_ScopeCategoryId",
                table: "InventoryChecks",
                column: "ScopeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryChecks_Status",
                table: "InventoryChecks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustmentLogs_AdjustedDate",
                table: "InventoryAdjustmentLogs",
                column: "AdjustedDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustmentLogs_AuditCheckId",
                table: "InventoryAdjustmentLogs",
                column: "AuditCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustmentLogs_SerialId",
                table: "InventoryAdjustmentLogs",
                column: "SerialId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustmentLogs_VariantId",
                table: "InventoryAdjustmentLogs",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCheckDetailSerials_CheckId",
                table: "InventoryCheckDetailSerials",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCheckDetailSerials_CheckId_ScanStatus",
                table: "InventoryCheckDetailSerials",
                columns: new[] { "CheckId", "ScanStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCheckDetailSerials_DetailId",
                table: "InventoryCheckDetailSerials",
                column: "DetailId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCheckDetailSerials_SerialId",
                table: "InventoryCheckDetailSerials",
                column: "SerialId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCheckDetailSerials_VariantId",
                table: "InventoryCheckDetailSerials",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "UQ_InventoryCheckDetailSerials_CheckId_SerialNumberRaw",
                table: "InventoryCheckDetailSerials",
                columns: new[] { "CheckId", "SerialNumberRaw" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryChecks_Categories_ScopeCategoryId",
                table: "InventoryChecks",
                column: "ScopeCategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryChecks_Categories_ScopeCategoryId",
                table: "InventoryChecks");

            migrationBuilder.DropTable(
                name: "InventoryAdjustmentLogs");

            migrationBuilder.DropTable(
                name: "InventoryCheckDetailSerials");

            migrationBuilder.DropIndex(
                name: "IX_InventoryChecks_CheckCode",
                table: "InventoryChecks");

            migrationBuilder.DropIndex(
                name: "IX_InventoryChecks_CheckDate",
                table: "InventoryChecks");

            migrationBuilder.DropIndex(
                name: "IX_InventoryChecks_ScopeCategoryId",
                table: "InventoryChecks");

            migrationBuilder.DropIndex(
                name: "IX_InventoryChecks_Status",
                table: "InventoryChecks");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "InventoryChecks");

            migrationBuilder.DropColumn(
                name: "ApprovedByEmployeeId",
                table: "InventoryChecks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "InventoryChecks");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "InventoryChecks");

            migrationBuilder.DropColumn(
                name: "ScopeCategoryId",
                table: "InventoryChecks");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "InventoryChecks");

            migrationBuilder.DropColumn(
                name: "SnapshotAt",
                table: "InventoryChecks");

            migrationBuilder.DropColumn(
                name: "DefectiveQuantity",
                table: "InventoryCheckDetails");

            migrationBuilder.DropColumn(
                name: "MatchedQuantity",
                table: "InventoryCheckDetails");

            migrationBuilder.DropColumn(
                name: "MissingQuantity",
                table: "InventoryCheckDetails");

            migrationBuilder.DropColumn(
                name: "SurplusQuantity",
                table: "InventoryCheckDetails");
        }
    }
}
