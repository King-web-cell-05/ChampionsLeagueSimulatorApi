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

    [HttpGet("{competitionId}")]
    public async Task<IActionResult> GetStandings(Guid competitionId)
    {
        var standings = await _tableService.GenerateTable(competitionId);

        if (!standings.Any())
        {
            return Ok(new
            {
                message = "No standings yet.",
                data = new List<StandingDto>()
            });
        }

        return Ok(standings);
    }
}