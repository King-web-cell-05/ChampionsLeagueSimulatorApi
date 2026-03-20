using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChampionsLeagueSimulatorApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CompetitionTeams",
                table: "CompetitionTeams");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionTeams_CompetitionId",
                table: "CompetitionTeams");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "CompetitionTeams");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompetitionTeams",
                table: "CompetitionTeams",
                columns: new[] { "CompetitionId", "TeamId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CompetitionTeams",
                table: "CompetitionTeams");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "CompetitionTeams",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompetitionTeams",
                table: "CompetitionTeams",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeams_CompetitionId",
                table: "CompetitionTeams",
                column: "CompetitionId");
        }
    }
}
