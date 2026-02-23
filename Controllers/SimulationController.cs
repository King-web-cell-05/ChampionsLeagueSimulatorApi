using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Services;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    private readonly SimulationService _simulationService;

    public SimulationController(SimulationService simulationService)
    {
        _simulationService = simulationService;
    }

    [HttpPost("{competitionId}")]
    public async Task<IActionResult> SimulateCompetition(Guid competitionId)
    {
        var matches = await _simulationService.SimulateCompetition(competitionId);

        return Ok(matches);
    }
}