using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class AddSubRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubRosterId",
                table: "CwlSignups",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubRosters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClanTag = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GameClanTag = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    TargetSize = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubRosters", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CwlSignups_SubRosterId",
                table: "CwlSignups",
                column: "SubRosterId");

            migrationBuilder.CreateIndex(
                name: "IX_SubRosters_ClanTag",
                table: "SubRosters",
                column: "ClanTag");

            migrationBuilder.CreateIndex(
                name: "IX_SubRosters_GameClanTag",
                table: "SubRosters",
                column: "GameClanTag",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CwlSignups_SubRosters_SubRosterId",
                table: "CwlSignups",
                column: "SubRosterId",
                principalTable: "SubRosters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CwlSignups_SubRosters_SubRosterId",
                table: "CwlSignups");

            migrationBuilder.DropTable(
                name: "SubRosters");

            migrationBuilder.DropIndex(
                name: "IX_CwlSignups_SubRosterId",
                table: "CwlSignups");

            migrationBuilder.DropColumn(
                name: "SubRosterId",
                table: "CwlSignups");
        }
    }
}
