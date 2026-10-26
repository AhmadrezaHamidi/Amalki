using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amlak.Web.Migrations
{
    /// <inheritdoc />
    public partial class ChannelAlbums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraMessageIds",
                table: "ChannelPosts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoKey",
                table: "ChannelPosts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraMessageIds",
                table: "ChannelPosts");

            migrationBuilder.DropColumn(
                name: "PhotoKey",
                table: "ChannelPosts");
        }
    }
}
