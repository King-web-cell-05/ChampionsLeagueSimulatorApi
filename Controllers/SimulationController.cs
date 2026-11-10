
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    private readonly SimulationService _simulationService;
    private readonly AppDbContext _context;
    private readonly ILogger<SimulationController> _logger;

    public SimulationController(
        SimulationService simulationService,
        AppDbContext context,
        ILogger<SimulationController> logger)
    {
        _simulationService = simulationService;
        _context = context;
        _logger = logger;
    }

    // ==========================================
    // GET AUTHENTICATED USER ID
    // ==========================================

    private Guid? GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    // ==========================================
    // SIMULATE OWN COMPETITION
    // POST: api/Simulation/{competitionId}/simulate
    // ==========================================

    [HttpPost("{competitionId:guid}/simulate")]
    public async Task<IActionResult> SimulateCompetition(
        Guid competitionId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid or expired authentication session."
            });
        }

        if (competitionId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Invalid competition ID."
            });
        }

        // Verify ownership before running
        // any competition simulation.
        var ownsCompetition = await _context.Competitions
            .AsNoTracking()
            .AnyAsync(c =>
                c.Id == competitionId &&
                c.OwnerId == userId.Value);

        if (!ownsCompetition)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        try
        {
            // Preserve the existing simulation service.
            var result = await _simulationService
                .SimulateCompetition(competitionId);

            // Preserve the original response data.
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Competition simulation failed for {CompetitionId}",
                competitionId);

            return StatusCode(500, new
            {
                message =
                    "An unexpected error occurred while simulating the competition."
            });
        }
    }
}
