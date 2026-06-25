using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class AddCwlRosterReminderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "CwlRosterReminderChannelId",
                table: "ClanSettings",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CwlRosterReminderEnabled",
                table: "ClanSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CwlRosterReminderLeadHours",
                table: "ClanSettings",
                type: "int",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<ulong>(
                name: "CwlRosterReminderPingRoleId",
                table: "ClanSettings",
                type: "bigint unsigned",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CwlRosterReminderChannelId",
                table: "ClanSettings");

            migrationBuilder.DropColumn(
                name: "CwlRosterReminderEnabled",
                table: "ClanSettings");

            migrationBuilder.DropColumn(
                name: "CwlRosterReminderLeadHours",
                table: "ClanSettings");

            migrationBuilder.DropColumn(
                name: "CwlRosterReminderPingRoleId",
                table: "ClanSettings");
        }
    }
}
