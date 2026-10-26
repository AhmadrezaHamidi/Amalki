using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amlak.Web.Migrations
{
    /// <inheritdoc />
    public partial class PhotoVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "PropertyPhotos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Variants",
                table: "PropertyPhotos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "PropertyPhotos",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Height",
                table: "PropertyPhotos");

            migrationBuilder.DropColumn(
                name: "Variants",
                table: "PropertyPhotos");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "PropertyPhotos");
        }
    }
}
