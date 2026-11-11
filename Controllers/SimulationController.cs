
using System;
using System.Security.Claims;
using System.Threading.Tasks;

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
    // VERIFY COMPETITION OWNERSHIP
    // ==========================================

    private async Task<IActionResult?> CheckAccess(
        Guid competitionId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid or expired authentication session."
            });
        }

        if (competitionId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Invalid competition ID."
            });
        }

        bool ownsCompetition = await _context.Competitions
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

        return null;
    }

    // ==========================================
    // SIMULATE SINGLE MATCH
    //
    // POST:
    // api/Simulation/{competitionId}/matches/{matchId}/simulate
    // ==========================================

    [HttpPost(
        "{competitionId:guid}/matches/{matchId:guid}/simulate")]
    public async Task<IActionResult> SimulateMatch(
        Guid competitionId,
        Guid matchId)
    {
        var accessError = await CheckAccess(
            competitionId);

        if (accessError != null)
        {
            return accessError;
        }

        try
        {
            var result = await _simulationService
                .SimulateMatch(
                    competitionId,
                    matchId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return HandleSimulationError(
                ex,
                competitionId,
                "single match");
        }
    }

    // ==========================================
    // SIMULATE MATCHDAY
    //
    // POST:
    // api/Simulation/{competitionId}/matchdays/{matchday}/simulate
    // ==========================================

    [HttpPost(
        "{competitionId:guid}/matchdays/{matchday:int}/simulate")]
    public async Task<IActionResult> SimulateMatchday(
        Guid competitionId,
        int matchday)
    {
        var accessError = await CheckAccess(
            competitionId);

        if (accessError != null)
        {
            return accessError;
        }

        if (matchday < 1)
        {
            return BadRequest(new
            {
                message =
                    "Matchday must be greater than zero."
            });
        }

        try
        {
            var result = await _simulationService
                .SimulateMatchday(
                    competitionId,
                    matchday);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return HandleSimulationError(
                ex,
                competitionId,
                "matchday");
        }
    }

    // ==========================================
    // SIMULATE ALL REMAINING MATCHES
    //
    // POST:
    // api/Simulation/{competitionId}/simulate
    // ==========================================

    [HttpPost("{competitionId:guid}/simulate")]
    public async Task<IActionResult> SimulateCompetition(
        Guid competitionId)
    {
        var accessError = await CheckAccess(
            competitionId);

        if (accessError != null)
        {
            return accessError;
        }

        try
        {
            var result = await _simulationService
                .SimulateCompetition(
                    competitionId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return HandleSimulationError(
                ex,
                competitionId,
                "competition");
        }
    }

    // ==========================================
    // SHARED ERROR HANDLING
    // ==========================================

    private IActionResult HandleSimulationError(
        Exception ex,
        Guid competitionId,
        string simulationType)
    {
        if (ex is KeyNotFoundException)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }

        if (ex is ArgumentException)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }

        if (ex is InvalidOperationException)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }

        if (ex is DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message =
                    "The match was updated by another request. " +
                    "Refresh the fixtures and try again."
            });
        }

        _logger.LogError(
            ex,
            "Failed to simulate {SimulationType} for competition {CompetitionId}",
            simulationType,
            competitionId);

        return StatusCode(500, new
        {
            message =
                "An unexpected error occurred during simulation."
        });
    }
}
