using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeparateUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Tạo bảng UserProfiles trước
            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<byte>(type: "tinyint", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserProfiles_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2. Migrate dữ liệu từ AppUsers sang UserProfiles
            migrationBuilder.Sql(@"
                INSERT INTO [UserProfiles] ([UserId], [FullName], [Gender], [DateOfBirth], [AvatarUrl], [Address], [City])
                SELECT [Id], [FullName], [Gender], [DateOfBirth], [AvatarUrl], [Address], [City]
                FROM [AppUsers]
            ");

            // 3. Sau khi đã copy xong, drop các cột cũ
            migrationBuilder.DropColumn(
                name: "Address",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "AppUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "AppUsers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "AppUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "AppUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AppUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte>(
                name: "Gender",
                table: "AppUsers",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }
    }
}
