using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amlak.Web.Migrations
{
    /// <inheritdoc />
    public partial class ChannelPostUniqueAndPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChannelPosts_PropertyId",
                table: "ChannelPosts");

            migrationBuilder.AddColumn<bool>(
                name: "HasPhoto",
                table: "ChannelPosts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelPosts_PropertyId_Channel",
                table: "ChannelPosts",
                columns: new[] { "PropertyId", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChannelPosts_PropertyId_Channel",
                table: "ChannelPosts");

            migrationBuilder.DropColumn(
                name: "HasPhoto",
                table: "ChannelPosts");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelPosts_PropertyId",
                table: "ChannelPosts",
                column: "PropertyId");
        }
    }
}
