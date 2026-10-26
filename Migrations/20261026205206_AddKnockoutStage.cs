using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChampionsLeagueSimulatorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddKnockoutStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnockoutTies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    TieNumber = table.Column<int>(type: "integer", nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinnerTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnockoutTies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnockoutTies_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnockoutTies_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnockoutTies_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnockoutTies_Teams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnockoutMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KnockoutTieId = table.Column<Guid>(type: "uuid", nullable: false),
                    Leg = table.Column<int>(type: "integer", nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: true),
                    IsPlayed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    KickoffUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnockoutMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnockoutMatches_KnockoutTies_KnockoutTieId",
                        column: x => x.KnockoutTieId,
                        principalTable: "KnockoutTies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnockoutMatches_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnockoutMatches_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutMatches_AwayTeamId",
                table: "KnockoutMatches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutMatches_HomeTeamId",
                table: "KnockoutMatches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutMatches_KnockoutTieId_Leg",
                table: "KnockoutMatches",
                columns: new[] { "KnockoutTieId", "Leg" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutTies_AwayTeamId",
                table: "KnockoutTies",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutTies_CompetitionId_Round_TieNumber",
                table: "KnockoutTies",
                columns: new[] { "CompetitionId", "Round", "TieNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutTies_HomeTeamId",
                table: "KnockoutTies",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_KnockoutTies_WinnerTeamId",
                table: "KnockoutTies",
                column: "WinnerTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnockoutMatches");

            migrationBuilder.DropTable(
                name: "KnockoutTies");
        }
    }
}
