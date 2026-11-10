
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CompetitionTeamsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CompetitionTeamsController(AppDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // AUTHENTICATED USER ID
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
    // COMPETITION OWNERSHIP CHECK
    // ==========================================

    private async Task<bool> OwnsCompetitionAsync(
        Guid competitionId,
        Guid userId)
    {
        return await _context.Competitions
            .AsNoTracking()
            .AnyAsync(c =>
                c.Id == competitionId &&
                c.OwnerId == userId);
    }

    // ==========================================
    // ADD TEAM TO OWN COMPETITION
    // POST: api/CompetitionTeams
    // ==========================================

    [HttpPost]
    public async Task<IActionResult> AddTeam(
        [FromBody] AddCompetitionTeamRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        if (request is null ||
            request.CompetitionId == Guid.Empty ||
            request.TeamId == Guid.Empty)
        {
            return BadRequest(new
            {
                message =
                    "A valid competition ID and team ID are required."
            });
        }

        // Verify that this competition belongs
        // to the authenticated user.
        var competition = await _context.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == request.CompetitionId &&
                c.OwnerId == userId.Value);

        if (competition is null)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        // A team must be either a shared default
        // or a private team owned by this user.
        var team = await _context.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.Id == request.TeamId &&
                (t.IsDefault ||
                 t.OwnerId == userId.Value));

        if (team is null)
        {
            return NotFound(new
            {
                message = "Team not found."
            });
        }

        // Prevent duplicate registrations.
        var exists = await _context.CompetitionTeams
            .AnyAsync(ct =>
                ct.CompetitionId == request.CompetitionId &&
                ct.TeamId == request.TeamId);

        if (exists)
        {
            return Conflict(new
            {
                message =
                    "Team already exists in this competition."
            });
        }

        var entry = new CompetitionTeam
        {
            CompetitionId = request.CompetitionId,
            TeamId = request.TeamId
        };

        _context.CompetitionTeams.Add(entry);

        await _context.SaveChangesAsync();

        // Preserve the original response DTO.
        var response = new CompetitionTeamResponse
        {
            CompetitionId = competition.Id,
            CompetitionName = competition.Name,
            TeamId = team.Id,
            TeamName = team.Name
        };

        return Ok(response);
    }

    // ==========================================
    // GET TEAMS IN OWN COMPETITION
    // GET: api/CompetitionTeams/{competitionId}
    // ==========================================

    [HttpGet("{competitionId:guid}")]
    public async Task<IActionResult> GetTeamsByCompetition(
        Guid competitionId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        if (!await OwnsCompetitionAsync(
                competitionId,
                userId.Value))
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        var entries = await _context.CompetitionTeams
            .AsNoTracking()
            .Where(ct =>
                ct.CompetitionId == competitionId)
            .OrderBy(ct => ct.Team.Name)
            .Select(ct => new CompetitionTeamResponse
            {
                CompetitionId = ct.CompetitionId,
                CompetitionName =
                    ct.Competition != null
                        ? ct.Competition.Name
                        : "",
                TeamId = ct.TeamId,
                TeamName =
                    ct.Team != null
                        ? ct.Team.Name
                        : ""
            })
            .ToListAsync();

        return Ok(entries);
    }

    // ==========================================
    // REMOVE TEAM FROM OWN COMPETITION
    // DELETE: api/CompetitionTeams
    // ?competitionId={id}&teamId={id}
    // ==========================================

    [HttpDelete]
    public async Task<IActionResult> RemoveTeam(
        [FromQuery] Guid competitionId,
        [FromQuery] Guid teamId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        if (competitionId == Guid.Empty ||
            teamId == Guid.Empty)
        {
            return BadRequest(new
            {
                message =
                    "Valid competition and team IDs are required."
            });
        }

        if (!await OwnsCompetitionAsync(
                competitionId,
                userId.Value))
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        var entry = await _context.CompetitionTeams
            .FirstOrDefaultAsync(ct =>
                ct.CompetitionId == competitionId &&
                ct.TeamId == teamId);

        if (entry is null)
        {
            return NotFound(new
            {
                message =
                    "Team is not registered in this competition."
            });
        }

        _context.CompetitionTeams.Remove(entry);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}
