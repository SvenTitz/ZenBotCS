using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class AddMainRosterTargetSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 15, not EF's 0 default: existing clans have to land on a real war size, and 15v15 is
            // what the roster page assumed before this was stored.
            migrationBuilder.AddColumn<int>(
                name: "CwlRosterTargetSize",
                table: "ClanSettings",
                type: "int",
                nullable: false,
                defaultValue: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CwlRosterTargetSize",
                table: "ClanSettings");
        }
    }
}
