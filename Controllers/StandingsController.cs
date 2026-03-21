using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Services;
using ChampionsLeagueSimulatorApi.DTOs;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StandingsController : ControllerBase
{
    private readonly TableService _tableService;

    public StandingsController(TableService tableService)
    {
        _tableService = tableService;
    }

    // ✅ GET: api/standings/{competitionId}
    [HttpGet("{competitionId}")]
    public async Task<IActionResult> GetStandings(Guid competitionId)
    {
        var standings = await _tableService.GenerateGroupedTable(competitionId); // ✅ FIXED

        if (standings == null || !standings.Any())
        {
            return Ok(new
            {
                message = "No standings available yet for this competition.",
                data = new Dictionary<string, List<StandingDto>>() // ✅ FIXED TYPE
            });
        }

        return Ok(standings);
    }
}