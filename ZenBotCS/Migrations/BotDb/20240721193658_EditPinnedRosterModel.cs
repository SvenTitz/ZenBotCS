using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class EditPinnedRosterModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Url",
                table: "PinnedRosters");

            migrationBuilder.AddColumn<string>(
                name: "SpreadsheetId",
                table: "PinnedRosters",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Gid",
                table: "PinnedRosters",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpreadsheetId",
                table: "PinnedRosters");

            migrationBuilder.DropColumn(
                name: "Gid",
                table: "PinnedRosters");

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "PinnedRosters",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
