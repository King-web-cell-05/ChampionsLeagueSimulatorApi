using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChampionsLeagueSimulatorApi.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTeamCoefficientToDecimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Coefficient",
                table: "Teams",
                type: "numeric(7,3)",
                precision: 7,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Coefficient",
                table: "Teams",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,3)",
                oldPrecision: 7,
                oldScale: 3);
        }
    }
}
