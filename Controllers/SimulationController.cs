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

    [HttpPost("{competitionId}/simulate")]
    public async Task<IActionResult> SimulateCompetition(Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            return BadRequest("Invalid competition ID.");

        try
        {
            var result = await _simulationService.SimulateCompetition(competitionId); // ✅ FIXED
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error simulating competition: {ex.Message}");
        }
    }

    [HttpPost("{competitionId}/generate-fixtures")]
    public async Task<IActionResult> GenerateFixtures(Guid competitionId)
    {
        await _simulationService.GenerateLeagueFixtures(competitionId);
        return Ok("Fixtures generated successfully");
    }
}