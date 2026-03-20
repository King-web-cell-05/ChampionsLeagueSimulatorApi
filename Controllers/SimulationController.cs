using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Services;
using ChampionsLeagueSimulatorApi.DTOs;

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

    /// <summary>
    /// Simulates all unplayed matches for a competition and returns updated matches and league standings.
    /// </summary>
    /// <param name="competitionId">The ID of the competition to simulate.</param>
    /// <returns>
    /// A SimulationResponseDto containing:
    /// - Matches: all matches for the competition
    /// - Standings: updated league table after simulation
    /// </returns>
    [HttpPost("{competitionId}/simulate")]
    public async Task<IActionResult> SimulateCompetition(Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            return BadRequest("Invalid competition ID.");

        SimulationResponseDto result;

        try
        {
            result = await _simulationService.SimulateCompetition(competitionId);
        }
        catch (Exception ex)
        {
            // Optional: log exception here
            return StatusCode(500, $"Error simulating competition: {ex.Message}");
        }

        return Ok(result);
    }
}