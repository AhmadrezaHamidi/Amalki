using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amlak.Web.Migrations
{
    /// <inheritdoc />
    public partial class ListingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Convertible",
                table: "Properties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Deed",
                table: "Properties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBalcony",
                table: "Properties",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasElevator",
                table: "Properties",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasStorage",
                table: "Properties",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Status_City_District",
                table: "Properties",
                columns: new[] { "Status", "City", "District" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Properties_Status_City_District",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Convertible",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Deed",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "HasBalcony",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "HasElevator",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "HasStorage",
                table: "Properties");
        }
    }
}
