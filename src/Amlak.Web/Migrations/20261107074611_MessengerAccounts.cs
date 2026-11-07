using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amlak.Web.Migrations
{
    /// <inheritdoc />
    public partial class MessengerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessengerAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Messenger = table.Column<int>(type: "int", nullable: false),
                    ChatId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReplyingToTicket = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessengerAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessengerInbox",
                columns: table => new
                {
                    Messenger = table.Column<int>(type: "int", nullable: false),
                    UpdateId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessengerInbox", x => new { x.Messenger, x.UpdateId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessengerAccounts_Messenger_ChatId",
                table: "MessengerAccounts",
                columns: new[] { "Messenger", "ChatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessengerAccounts_Phone",
                table: "MessengerAccounts",
                column: "Phone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessengerAccounts");

            migrationBuilder.DropTable(
                name: "MessengerInbox");
        }
    }
}
