using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChampionsLeagueSimulatorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_CompetitionId",
                table: "Matches");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPlayed",
                table: "Matches",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.CreateTable(
                name: "MatchStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomePossession = table.Column<int>(type: "integer", nullable: false),
                    AwayPossession = table.Column<int>(type: "integer", nullable: false),
                    HomeShots = table.Column<int>(type: "integer", nullable: false),
                    AwayShots = table.Column<int>(type: "integer", nullable: false),
                    HomeShotsOnTarget = table.Column<int>(type: "integer", nullable: false),
                    AwayShotsOnTarget = table.Column<int>(type: "integer", nullable: false),
                    HomeCorners = table.Column<int>(type: "integer", nullable: false),
                    AwayCorners = table.Column<int>(type: "integer", nullable: false),
                    HomeFouls = table.Column<int>(type: "integer", nullable: false),
                    AwayFouls = table.Column<int>(type: "integer", nullable: false),
                    HomeYellowCards = table.Column<int>(type: "integer", nullable: false),
                    AwayYellowCards = table.Column<int>(type: "integer", nullable: false),
                    HomeRedCards = table.Column<int>(type: "integer", nullable: false),
                    AwayRedCards = table.Column<int>(type: "integer", nullable: false),
                    HomeExpectedGoals = table.Column<double>(type: "double precision", nullable: false),
                    AwayExpectedGoals = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStatistics", x => x.Id);
                    table.CheckConstraint("CK_MatchStatistics_AwayPossession", "\"AwayPossession\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_MatchStatistics_AwayShots", "\"AwayShots\" >= 0 AND \"AwayShotsOnTarget\" >= 0 AND \"AwayShotsOnTarget\" <= \"AwayShots\"");
                    table.CheckConstraint("CK_MatchStatistics_ExpectedGoals", "\"HomeExpectedGoals\" >= 0 AND \"AwayExpectedGoals\" >= 0");
                    table.CheckConstraint("CK_MatchStatistics_HomePossession", "\"HomePossession\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_MatchStatistics_HomeShots", "\"HomeShots\" >= 0 AND \"HomeShotsOnTarget\" >= 0 AND \"HomeShotsOnTarget\" <= \"HomeShots\"");
                    table.CheckConstraint("CK_MatchStatistics_TotalPossession", "\"HomePossession\" + \"AwayPossession\" = 100");
                    table.ForeignKey(
                        name: "FK_MatchStatistics_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CompetitionId_IsPlayed",
                table: "Matches",
                columns: new[] { "CompetitionId", "IsPlayed" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CompetitionId_MatchDay",
                table: "Matches",
                columns: new[] { "CompetitionId", "MatchDay" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatistics_MatchId",
                table: "MatchStatistics",
                column: "MatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchStatistics");

            migrationBuilder.DropIndex(
                name: "IX_Matches_CompetitionId_IsPlayed",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_CompetitionId_MatchDay",
                table: "Matches");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPlayed",
                table: "Matches",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CompetitionId",
                table: "Matches",
                column: "CompetitionId");
        }
    }
}
