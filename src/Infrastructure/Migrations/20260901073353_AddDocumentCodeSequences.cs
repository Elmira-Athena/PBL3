using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCodeSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "SeqImportReceiptCode",
                startValue: 1000L);

            migrationBuilder.CreateSequence(
                name: "SeqInventoryCheckCode",
                startValue: 1000L);

            migrationBuilder.CreateSequence(
                name: "SeqOrderCode",
                startValue: 1000L);

            migrationBuilder.CreateSequence(
                name: "SeqServiceInvoiceCode",
                startValue: 1000L);

            migrationBuilder.CreateSequence(
                name: "SeqServiceTicketCode",
                startValue: 1000L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "SeqImportReceiptCode");

            migrationBuilder.DropSequence(
                name: "SeqInventoryCheckCode");

            migrationBuilder.DropSequence(
                name: "SeqOrderCode");

            migrationBuilder.DropSequence(
                name: "SeqServiceInvoiceCode");

            migrationBuilder.DropSequence(
                name: "SeqServiceTicketCode");
        }
    }
}
