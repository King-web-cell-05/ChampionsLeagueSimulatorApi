
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
public class DrawController : ControllerBase
{
    private readonly DrawService _drawService;
    private readonly AppDbContext _context;

    public DrawController(
        DrawService drawService,
        AppDbContext context)
    {
        _drawService = drawService;
        _context = context;
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
    // GENERATE CHAMPIONS LEAGUE SWISS DRAW
    // POST: api/Draw/{competitionId}
    // ==========================================

    [HttpPost("{competitionId:guid}")]
    public async Task<IActionResult> GenerateDraw(
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
                message = "A valid competition ID is required."
            });
        }

        // Only the competition owner may generate fixtures.
        var competition = await _context.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == competitionId &&
                c.OwnerId == userId.Value);

        if (competition is null)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        // Do not generate another draw if fixtures exist.
        var fixturesExist = await _context.Matches
            .AsNoTracking()
            .AnyAsync(m =>
                m.CompetitionId == competitionId);

        if (fixturesExist)
        {
            return Conflict(new
            {
                message =
                    "This competition already has fixtures. " +
                    "Existing matches have not been changed."
            });
        }

        // Confirm that exactly 36 unique clubs are registered.
        var registeredTeamIds = await _context.CompetitionTeams
            .AsNoTracking()
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.TeamId)
            .ToListAsync();

        if (registeredTeamIds.Count != 36 ||
            registeredTeamIds.Distinct().Count() != 36)
        {
            return BadRequest(new
            {
                message =
                    "Exactly 36 unique clubs must be registered " +
                    "before generating the Champions League draw."
            });
        }

        // Ensure every participating club is accessible
        // to the authenticated competition owner.
        var accessibleTeamCount = await _context.Teams
            .AsNoTracking()
            .CountAsync(t =>
                registeredTeamIds.Contains(t.Id) &&
                (t.IsDefault || t.OwnerId == userId.Value));

        if (accessibleTeamCount != 36)
        {
            return BadRequest(new
            {
                message =
                    "The competition contains clubs that are " +
                    "not available to your account."
            });
        }

        try
        {
            // Preserve the existing Google OR-Tools
            // Swiss-system fixture-generation service.
            var matches =
                await _drawService.GenerateDoubleRoundRobin(
                    competitionId);

            return Ok(new
            {
                message = "Draw generated successfully.",
                competitionId,
                competitionName = competition.Name,
                totalFixtures = matches.Count,
                totalMatchdays = 8,
                registeredTeams = 36
            });
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
            return Conflict(new
            {
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}
