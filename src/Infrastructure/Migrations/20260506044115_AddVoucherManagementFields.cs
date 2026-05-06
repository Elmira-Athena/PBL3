using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherManagementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoucherUsages_UserId_VoucherId",
                table: "VoucherUsages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vouchers_Quantity",
                table: "Vouchers");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "Vouchers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<byte>(
                name: "ApplyFor",
                table: "Vouchers",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Vouchers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "Vouchers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Vouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Vouchers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStackable",
                table: "Vouchers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsesPerUser",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VoucherCategories",
                columns: table => new
                {
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherCategories", x => new { x.VoucherId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_VoucherCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoucherCategories_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_UserId_VoucherId",
                table: "VoucherUsages",
                columns: new[] { "UserId", "VoucherId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vouchers_Quantity",
                table: "Vouchers",
                sql: "[Quantity] IS NULL OR [UsedCount] <= [Quantity]");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherCategories_CategoryId",
                table: "VoucherCategories",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoucherCategories");

            migrationBuilder.DropIndex(
                name: "IX_VoucherUsages_UserId_VoucherId",
                table: "VoucherUsages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vouchers_Quantity",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "ApplyFor",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "DeletedDate",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "IsStackable",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "MaxUsesPerUser",
                table: "Vouchers");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_UserId_VoucherId",
                table: "VoucherUsages",
                columns: new[] { "UserId", "VoucherId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vouchers_Quantity",
                table: "Vouchers",
                sql: "[UsedCount] <= [Quantity]");
        }
    }
}
