using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class AddMaxDefToSignup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MaxDefeneses",
                table: "CwlSignups",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxDefeneses",
                table: "CwlSignups");
        }
    }
}
