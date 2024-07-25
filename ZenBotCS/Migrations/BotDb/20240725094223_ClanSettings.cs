using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class ClanSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClanSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClanTag = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    ClanType = table.Column<int>(type: "int", nullable: false),
                    MemberRoleId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    ElderRoleId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    LeaderRoleId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    CwlRoleId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    ColorHex = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EnableCwlSignup = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ChampStyleCwlRoster = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanSettings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClanSettings");
        }
    }
}
