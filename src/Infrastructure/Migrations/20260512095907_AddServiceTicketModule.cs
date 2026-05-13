using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTicketModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SerialId = table.Column<int>(type: "int", nullable: false),
                    OriginalOrderId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IntakeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IntakeEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HasScratches = table.Column<bool>(type: "bit", nullable: false),
                    HasDents = table.Column<bool>(type: "bit", nullable: false),
                    HasBurnMarks = table.Column<bool>(type: "bit", nullable: false),
                    HasMissingAccessories = table.Column<bool>(type: "bit", nullable: false),
                    CosmeticNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomerReportedIssue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    WalkInCustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WalkInCustomerPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WasInWarrantyAtIntake = table.Column<bool>(type: "bit", nullable: false),
                    WarrantyEndDateAtIntake = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarrantyEvalSource = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ResolutionType = table.Column<byte>(type: "tinyint", nullable: false),
                    AssignedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DiagnosisFindings = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DiagnosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiagnosedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReplacementSerialId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTickets_AppUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceTickets_Orders_OriginalOrderId",
                        column: x => x.OriginalOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceTickets_ProductSerials_ReplacementSerialId",
                        column: x => x.ReplacementSerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceTickets_ProductSerials_SerialId",
                        column: x => x.SerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LaborCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PartsTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CustomerDecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomerDecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotations_ServiceTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RmaShipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrackingCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShippedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShippedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedBackDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManufacturerResolution = table.Column<byte>(type: "tinyint", nullable: false),
                    ManufacturerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RmaShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RmaShipments_ServiceTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SerialRepairLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SerialId = table.Column<int>(type: "int", nullable: false),
                    TicketId = table.Column<int>(type: "int", nullable: true),
                    ResolutionType = table.Column<byte>(type: "tinyint", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LoggedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReplacedBySerialId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SerialRepairLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SerialRepairLogs_ProductSerials_ReplacedBySerialId",
                        column: x => x.ReplacedBySerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SerialRepairLogs_ProductSerials_SerialId",
                        column: x => x.SerialId,
                        principalTable: "ProductSerials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SerialRepairLogs_ServiceTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTicketStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTicketStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTicketStatusHistory_ServiceTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<int>(type: "int", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, computedColumnSql: "([Quantity] * [UnitPrice])")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationItems_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuotationItems_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    QuotationId = table.Column<int>(type: "int", nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LaborCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PartsTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceInvoices_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceInvoices_ServiceTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, computedColumnSql: "([Quantity] * [UnitPrice])")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceInvoiceItems_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceInvoiceItems_ServiceInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "ServiceInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_QuotationId",
                table: "QuotationItems",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_VariantId",
                table: "QuotationItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_TicketId",
                table: "Quotations",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_RmaShipments_TicketId",
                table: "RmaShipments",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SerialRepairLogs_ReplacedBySerialId",
                table: "SerialRepairLogs",
                column: "ReplacedBySerialId");

            migrationBuilder.CreateIndex(
                name: "IX_SerialRepairLogs_SerialId",
                table: "SerialRepairLogs",
                column: "SerialId");

            migrationBuilder.CreateIndex(
                name: "IX_SerialRepairLogs_TicketId",
                table: "SerialRepairLogs",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInvoiceItems_InvoiceId",
                table: "ServiceInvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInvoiceItems_VariantId",
                table: "ServiceInvoiceItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInvoices_InvoiceCode",
                table: "ServiceInvoices",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInvoices_QuotationId",
                table: "ServiceInvoices",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInvoices_TicketId",
                table: "ServiceInvoices",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_AssignedEmployeeId",
                table: "ServiceTickets",
                column: "AssignedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_CustomerId",
                table: "ServiceTickets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_IntakeDate",
                table: "ServiceTickets",
                column: "IntakeDate");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_OriginalOrderId",
                table: "ServiceTickets",
                column: "OriginalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_ReplacementSerialId",
                table: "ServiceTickets",
                column: "ReplacementSerialId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_SerialId",
                table: "ServiceTickets",
                column: "SerialId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_Status",
                table: "ServiceTickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_TicketCode",
                table: "ServiceTickets",
                column: "TicketCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTicketStatusHistory_TicketId_ChangedAt",
                table: "ServiceTicketStatusHistory",
                columns: new[] { "TicketId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationItems");

            migrationBuilder.DropTable(
                name: "RmaShipments");

            migrationBuilder.DropTable(
                name: "SerialRepairLogs");

            migrationBuilder.DropTable(
                name: "ServiceInvoiceItems");

            migrationBuilder.DropTable(
                name: "ServiceTicketStatusHistory");

            migrationBuilder.DropTable(
                name: "ServiceInvoices");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropTable(
                name: "ServiceTickets");
        }
    }
}
