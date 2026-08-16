using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class RenameLeadershipChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CwlRosterReminderChannelId",
                table: "ClanSettings",
                newName: "LeadershipChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LeadershipChannelId",
                table: "ClanSettings",
                newName: "CwlRosterReminderChannelId");
        }
    }
}
