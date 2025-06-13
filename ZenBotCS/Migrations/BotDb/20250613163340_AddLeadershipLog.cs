using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class AddLeadershipLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeadershipLogMessages",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    FullContent = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipLogMessages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeadershipLogPlayerTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Tag = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipLogPlayerTags", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeadershipLogUsers",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipLogUsers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeadershipLogMessageLeadershipLogPlayerTag",
                columns: table => new
                {
                    MentionedPlayerTagsId = table.Column<int>(type: "int", nullable: false),
                    MessagesMentionedInId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipLogMessageLeadershipLogPlayerTag", x => new { x.MentionedPlayerTagsId, x.MessagesMentionedInId });
                    table.ForeignKey(
                        name: "FK_LeadershipLogMessageLeadershipLogPlayerTag_LeadershipLogMess~",
                        column: x => x.MessagesMentionedInId,
                        principalTable: "LeadershipLogMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadershipLogMessageLeadershipLogPlayerTag_LeadershipLogPlay~",
                        column: x => x.MentionedPlayerTagsId,
                        principalTable: "LeadershipLogPlayerTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeadershipLogMessageLeadershipLogUser",
                columns: table => new
                {
                    MentionedUsersId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    MessagesMentionedInId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipLogMessageLeadershipLogUser", x => new { x.MentionedUsersId, x.MessagesMentionedInId });
                    table.ForeignKey(
                        name: "FK_LeadershipLogMessageLeadershipLogUser_LeadershipLogMessages_~",
                        column: x => x.MessagesMentionedInId,
                        principalTable: "LeadershipLogMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadershipLogMessageLeadershipLogUser_LeadershipLogUsers_Men~",
                        column: x => x.MentionedUsersId,
                        principalTable: "LeadershipLogUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipLogMessageLeadershipLogPlayerTag_MessagesMentioned~",
                table: "LeadershipLogMessageLeadershipLogPlayerTag",
                column: "MessagesMentionedInId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipLogMessageLeadershipLogUser_MessagesMentionedInId",
                table: "LeadershipLogMessageLeadershipLogUser",
                column: "MessagesMentionedInId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipLogPlayerTags_Tag",
                table: "LeadershipLogPlayerTags",
                column: "Tag",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadershipLogMessageLeadershipLogPlayerTag");

            migrationBuilder.DropTable(
                name: "LeadershipLogMessageLeadershipLogUser");

            migrationBuilder.DropTable(
                name: "LeadershipLogPlayerTags");

            migrationBuilder.DropTable(
                name: "LeadershipLogMessages");

            migrationBuilder.DropTable(
                name: "LeadershipLogUsers");
        }
    }
}
