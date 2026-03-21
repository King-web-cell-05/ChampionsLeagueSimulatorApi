using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Services;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupStageController : ControllerBase
{
    private readonly SimulationService _simulationService;
    private readonly TableService _tableService;

    public GroupStageController(
        SimulationService simulationService,
        TableService tableService)
    {
        _simulationService = simulationService;
        _tableService = tableService;
    }

    // ✅ POST: api/GroupStage/{competitionId}/simulate
    [HttpPost("{competitionId}/simulate")]
    public async Task<IActionResult> SimulateGroupStage(Guid competitionId)
    {
        var result = await _simulationService.SimulateGroupStage(competitionId);
        return Ok(result);
    }

    // ✅ GET: api/GroupStage/{competitionId}/standings
    [HttpGet("{competitionId}/standings")]
    public async Task<IActionResult> GetGroupedStandings(Guid competitionId)
    {
        var standings = await _tableService.GenerateGroupedTable(competitionId);
        return Ok(standings);
    }
}   