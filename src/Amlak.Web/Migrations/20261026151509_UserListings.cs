using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amlak.Web.Migrations
{
    /// <inheritdoc />
    public partial class UserListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Properties",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerPhone",
                table: "Properties",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhoneCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_OwnerPhone",
                table: "Properties",
                column: "OwnerPhone");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneCodes_Phone_CreatedAtUtc",
                table: "PhoneCodes",
                columns: new[] { "Phone", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhoneCodes");

            migrationBuilder.DropIndex(
                name: "IX_Properties_OwnerPhone",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "OwnerPhone",
                table: "Properties");
        }
    }
}
