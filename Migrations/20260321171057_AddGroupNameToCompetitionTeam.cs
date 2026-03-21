using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChampionsLeagueSimulatorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupNameToCompetitionTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "CompetitionTeams",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "CompetitionTeams");
        }
    }
}
