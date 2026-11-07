using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amlak.Web.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Properties_OwnerPhone",
                table: "Properties");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_CreatedAtUtc_Id",
                table: "Properties",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_OwnerPhone_CreatedAtUtc",
                table: "Properties",
                columns: new[] { "OwnerPhone", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CreatedAtUtc",
                table: "Leads",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Properties_CreatedAtUtc_Id",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_OwnerPhone_CreatedAtUtc",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Leads_CreatedAtUtc",
                table: "Leads");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_OwnerPhone",
                table: "Properties",
                column: "OwnerPhone");
        }
    }
}
