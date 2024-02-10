using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.CocApiCache
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DownloadMembers = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsWarLogPublic = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    Tag = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawContent = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DownloadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KeepUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Download = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clan", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "player",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Tag = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClanTag = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawContent = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DownloadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KeepUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Download = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "war",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClanTag = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpponentTag = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreparationStartTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WarTag = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<int>(type: "int", nullable: true),
                    IsFinal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Season = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Announcements = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    RawContent = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DownloadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KeepUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Download = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_war", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "current_war",
                columns: table => new
                {
                    CachedClanId = table.Column<int>(type: "int", nullable: false),
                    Added = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EnemyTag = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<int>(type: "int", nullable: true),
                    PreparationStartTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: true),
                    RawContent = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DownloadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KeepUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Download = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_current_war", x => x.CachedClanId);
                    table.ForeignKey(
                        name: "FK_current_war_clan_CachedClanId",
                        column: x => x.CachedClanId,
                        principalTable: "clan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "group",
                columns: table => new
                {
                    CachedClanId = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    State = table.Column<int>(type: "int", nullable: true),
                    Added = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RawContent = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DownloadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KeepUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Download = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group", x => x.CachedClanId);
                    table.ForeignKey(
                        name: "FK_group_clan_CachedClanId",
                        column: x => x.CachedClanId,
                        principalTable: "clan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "war_log",
                columns: table => new
                {
                    CachedClanId = table.Column<int>(type: "int", nullable: false),
                    RawContent = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DownloadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KeepUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Download = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_war_log", x => x.CachedClanId);
                    table.ForeignKey(
                        name: "FK_war_log_clan_CachedClanId",
                        column: x => x.CachedClanId,
                        principalTable: "clan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_clan_ExpiresAt",
                table: "clan",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_clan_Id",
                table: "clan",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clan_KeepUntil",
                table: "clan",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_clan_Tag",
                table: "clan",
                column: "Tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_current_war_Added_CachedClanId_State",
                table: "current_war",
                columns: new[] { "Added", "CachedClanId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_current_war_CachedClanId_Download",
                table: "current_war",
                columns: new[] { "CachedClanId", "Download" });

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
                name: "IX_group_ExpiresAt",
                table: "group",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_group_KeepUntil",
                table: "group",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_player_ClanTag",
                table: "player",
                column: "ClanTag");

            migrationBuilder.CreateIndex(
                name: "IX_player_ExpiresAt",
                table: "player",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_player_Id",
                table: "player",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_KeepUntil",
                table: "player",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_player_Tag",
                table: "player",
                column: "Tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_war_ClanTag",
                table: "war",
                column: "ClanTag");

            migrationBuilder.CreateIndex(
                name: "IX_war_ExpiresAt",
                table: "war",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_war_Id",
                table: "war",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_war_IsFinal",
                table: "war",
                column: "IsFinal");

            migrationBuilder.CreateIndex(
                name: "IX_war_KeepUntil",
                table: "war",
                column: "KeepUntil");

            migrationBuilder.CreateIndex(
                name: "IX_war_OpponentTag",
                table: "war",
                column: "OpponentTag");

            migrationBuilder.CreateIndex(
                name: "IX_war_PreparationStartTime_ClanTag_OpponentTag",
                table: "war",
                columns: new[] { "PreparationStartTime", "ClanTag", "OpponentTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_war_Season",
                table: "war",
                column: "Season");

            migrationBuilder.CreateIndex(
                name: "IX_war_WarTag",
                table: "war",
                column: "WarTag");

            migrationBuilder.CreateIndex(
                name: "IX_war_log_ExpiresAt",
                table: "war_log",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_war_log_KeepUntil",
                table: "war_log",
                column: "KeepUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "current_war");

            migrationBuilder.DropTable(
                name: "group");

            migrationBuilder.DropTable(
                name: "player");

            migrationBuilder.DropTable(
                name: "war");

            migrationBuilder.DropTable(
                name: "war_log");

            migrationBuilder.DropTable(
                name: "clan");
        }
    }
}
