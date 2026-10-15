using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class AddStrikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Strikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StrikeId = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlayerTag = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlayerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    RolloverDays = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByDiscordId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RemovedByDiscordId = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strikes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Strikes_PlayerTag",
                table: "Strikes",
                column: "PlayerTag");

            migrationBuilder.CreateIndex(
                name: "IX_Strikes_StrikeId",
                table: "Strikes",
                column: "StrikeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Strikes");
        }
    }
}
