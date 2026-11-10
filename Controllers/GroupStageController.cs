
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
public class GroupStageController : ControllerBase
{
    private readonly SimulationService _simulationService;
    private readonly TableService _tableService;
    private readonly AppDbContext _context;
    private readonly ILogger<GroupStageController> _logger;

    public GroupStageController(
        SimulationService simulationService,
        TableService tableService,
        AppDbContext context,
        ILogger<GroupStageController> logger)
    {
        _simulationService = simulationService;
        _tableService = tableService;
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

    private async Task<IActionResult?> CheckAccessAsync(
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

        return null;
    }

    // ==========================================
    // GENERATE LEAGUE FIXTURES
    // POST: api/GroupStage/{competitionId}/generate-fixtures
    // ==========================================

    [HttpPost("{competitionId:guid}/generate-fixtures")]
    public async Task<IActionResult> GenerateFixtures(
        Guid competitionId)
    {
        var accessError = await CheckAccessAsync(competitionId);

        if (accessError is not null)
            return accessError;

        try
        {
            await _simulationService
                .GenerateLeagueFixtures(competitionId);

            // Preserve original successful response.
            return Ok("Fixtures generated successfully");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Fixture generation failed for {CompetitionId}",
                competitionId);

            return StatusCode(500, new
            {
                message = "An unexpected error occurred while generating fixtures."
            });
        }
    }

    // ==========================================
    // SIMULATE LEAGUE MATCHES
    // POST: api/GroupStage/{competitionId}/simulate
    // ==========================================

    [HttpPost("{competitionId:guid}/simulate")]
    public async Task<IActionResult> SimulateLeague(
        Guid competitionId)
    {
        var accessError = await CheckAccessAsync(competitionId);

        if (accessError is not null)
            return accessError;

        try
        {
            var result = await _simulationService
                .SimulateCompetition(competitionId);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "League simulation failed for {CompetitionId}",
                competitionId);

            return StatusCode(500, new
            {
                message = "An unexpected error occurred while simulating the league."
            });
        }
    }

    // ==========================================
    // GET LEAGUE STANDINGS
    // GET: api/GroupStage/{competitionId}/standings
    // ==========================================

    [HttpGet("{competitionId:guid}/standings")]
    public async Task<IActionResult> GetStandings(
        Guid competitionId)
    {
        var accessError = await CheckAccessAsync(competitionId);

        if (accessError is not null)
            return accessError;

        try
        {
            var standings = await _tableService
                .GenerateTable(competitionId);

            return Ok(standings);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Standings retrieval failed for {CompetitionId}",
                competitionId);

            return StatusCode(500, new
            {
                message = "An unexpected error occurred while retrieving standings."
            });
        }
    }
}
