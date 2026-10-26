using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChampionsLeagueSimulatorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddKnockoutExtraTimeAndPenalties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayExtraTimeScore",
                table: "KnockoutMatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayPenaltyScore",
                table: "KnockoutMatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeExtraTimeScore",
                table: "KnockoutMatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomePenaltyScore",
                table: "KnockoutMatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WentToExtraTime",
                table: "KnockoutMatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WentToPenalties",
                table: "KnockoutMatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayExtraTimeScore",
                table: "KnockoutMatches");

            migrationBuilder.DropColumn(
                name: "AwayPenaltyScore",
                table: "KnockoutMatches");

            migrationBuilder.DropColumn(
                name: "HomeExtraTimeScore",
                table: "KnockoutMatches");

            migrationBuilder.DropColumn(
                name: "HomePenaltyScore",
                table: "KnockoutMatches");

            migrationBuilder.DropColumn(
                name: "WentToExtraTime",
                table: "KnockoutMatches");

            migrationBuilder.DropColumn(
                name: "WentToPenalties",
                table: "KnockoutMatches");
        }
    }
}
