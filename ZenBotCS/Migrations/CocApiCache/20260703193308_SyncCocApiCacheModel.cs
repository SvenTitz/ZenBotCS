using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.CocApiCache
{
    /// <inheritdoc />
    public partial class SyncCocApiCacheModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_war_log_ExpiresAt",
                table: "war_log");

            migrationBuilder.DropIndex(
                name: "IX_war_log_KeepUntil",
                table: "war_log");

            migrationBuilder.DropIndex(
                name: "IX_war_ExpiresAt",
                table: "war");

            migrationBuilder.DropIndex(
                name: "IX_war_KeepUntil",
                table: "war");

            migrationBuilder.DropIndex(
                name: "IX_war_Season",
                table: "war");

            migrationBuilder.DropIndex(
                name: "IX_war_WarTag",
                table: "war");

            migrationBuilder.DropIndex(
                name: "IX_player_ExpiresAt",
                table: "player");

            migrationBuilder.DropIndex(
                name: "IX_player_KeepUntil",
                table: "player");

            migrationBuilder.DropIndex(
                name: "IX_group_ExpiresAt",
                table: "group");

            migrationBuilder.DropIndex(
                name: "IX_group_KeepUntil",
                table: "group");

            migrationBuilder.DropIndex(
                name: "IX_current_war_EnemyTag",
                table: "current_war");

            migrationBuilder.DropIndex(
                name: "IX_current_war_ExpiresAt",
                table: "current_war");

            migrationBuilder.DropIndex(
                name: "IX_current_war_KeepUntil",
                table: "current_war");

            migrationBuilder.DropIndex(
                name: "IX_clan_ExpiresAt",
                table: "clan");

            migrationBuilder.DropIndex(
                name: "IX_clan_KeepUntil",
                table: "clan");

            migrationBuilder.AlterColumn<string>(
                name: "WarTag",
                table: "war",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "war",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "player",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "EnemyTag",
                table: "current_war",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "clan",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "WarTag",
                table: "war",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "war",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "player",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "EnemyTag",
                table: "current_war",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "clan",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.CreateIndex(
                name: "IX_war_log_ExpiresAt",
                table: "war_log",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_war_log_KeepUntil",
                table: "war_log",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_war_ExpiresAt",
                table: "war",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_war_KeepUntil",
                table: "war",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_war_Season",
                table: "war",
                column: "Season");

            migrationBuilder.CreateIndex(
                name: "IX_war_WarTag",
                table: "war",
                column: "WarTag");

            migrationBuilder.CreateIndex(
                name: "IX_player_ExpiresAt",
                table: "player",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_player_KeepUntil",
                table: "player",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_group_ExpiresAt",
                table: "group",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_group_KeepUntil",
                table: "group",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_current_war_EnemyTag",
                table: "current_war",
                column: "EnemyTag");

            migrationBuilder.CreateIndex(
                name: "IX_current_war_ExpiresAt",
                table: "current_war",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_current_war_KeepUntil",
                table: "current_war",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_clan_ExpiresAt",
                table: "clan",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_clan_KeepUntil",
                table: "clan",
                column: "KeepUntil");
        }
    }
}
