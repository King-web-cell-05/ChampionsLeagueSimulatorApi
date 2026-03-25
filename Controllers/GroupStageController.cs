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

    // ✅ Generate fixtures (8 matches per team)
    // POST: api/GroupStage/{competitionId}/generate-fixtures
    [HttpPost("{competitionId}/generate-fixtures")]
    public async Task<IActionResult> GenerateFixtures(Guid competitionId)
    {
        await _simulationService.GenerateLeagueFixtures(competitionId);
        return Ok("Fixtures generated successfully");
    }

    // ✅ Simulate league matches
    // POST: api/GroupStage/{competitionId}/simulate
    [HttpPost("{competitionId}/simulate")]
    public async Task<IActionResult> SimulateLeague(Guid competitionId)
    {
        var result = await _simulationService.SimulateCompetition(competitionId);
        return Ok(result);
    }

    // ✅ Get league standings
    // GET: api/GroupStage/{competitionId}/standings
    [HttpGet("{competitionId}/standings")]
    public async Task<IActionResult> GetStandings(Guid competitionId)
    {
        var standings = await _tableService.GenerateTable(competitionId);
        return Ok(standings);
    }
}