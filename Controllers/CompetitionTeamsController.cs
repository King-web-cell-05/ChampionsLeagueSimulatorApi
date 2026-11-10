using System.Data;
using System.Security.Claims;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;
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
    private readonly DrawService _drawService;
    private readonly ILogger<CompetitionTeamsController> _logger;

    public CompetitionTeamsController(
        AppDbContext context,
        DrawService drawService,
        ILogger<CompetitionTeamsController> logger)
    {
        _context = context;
        _drawService = drawService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private Task<bool> OwnsCompetitionAsync(Guid competitionId, Guid userId)
        => _context.Competitions.AsNoTracking()
            .AnyAsync(c => c.Id == competitionId && c.OwnerId == userId);

    [HttpPost]
    public async Task<IActionResult> AddTeam(
        [FromBody] AddCompetitionTeamRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (request is null || request.CompetitionId == Guid.Empty ||
            request.TeamId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "A valid competition ID and team ID are required."
            });
        }

        try
        {
            // The registration and fixture inserts must succeed or fail together.
            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var competition = await _context.Competitions
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CompetitionId &&
                                          c.OwnerId == userId.Value);
            if (competition is null)
                return NotFound(new { message = "Competition not found." });

            var team = await _context.Teams.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.TeamId &&
                    (t.IsDefault || t.OwnerId == userId.Value));
            if (team is null)
                return NotFound(new { message = "Team not found." });

            var exists = await _context.CompetitionTeams.AnyAsync(ct =>
                ct.CompetitionId == request.CompetitionId &&
                ct.TeamId == request.TeamId);
            if (exists)
                return Conflict(new
                {
                    message = "Team already exists in this competition."
                });

            _context.CompetitionTeams.Add(new CompetitionTeam
            {
                CompetitionId = request.CompetitionId,
                TeamId = request.TeamId
            });
            await _context.SaveChangesAsync();

            var count = await _context.CompetitionTeams
                .Where(ct => ct.CompetitionId == request.CompetitionId)
                .Select(ct => ct.TeamId)
                .Distinct()
                .CountAsync();

            var generatedCount = 0;
            if (count >= 2)
            {
                // DrawService must reuse CurrentTransaction instead of
                // opening a nested transaction. See companion file.
                var generated = await _drawService.GenerateLeagueFixtures(
                    request.CompetitionId, userId.Value);
                generatedCount = generated.Count;
            }

            await transaction.CommitAsync();

            // Preserve the existing response shape used by Next.js.
            return Ok(new CompetitionTeamResponse
            {
                CompetitionId = competition.Id,
                CompetitionName = competition.Name,
                TeamId = team.Id,
                TeamName = team.Name
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "Registration conflict in competition {CompetitionId}",
                request.CompetitionId);
            return Conflict(new
            {
                message = "Unable to register this team. Refresh and try again."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Registration and fixture generation failed for {CompetitionId}",
                request.CompetitionId);
            return StatusCode(500, new
            {
                message = "Unable to register the club and schedule fixtures. Please retry."
            });
        }
    }

    [HttpGet("{competitionId:guid}")]
    public async Task<IActionResult> GetTeamsByCompetition(Guid competitionId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        if (!await OwnsCompetitionAsync(competitionId, userId.Value))
            return NotFound(new { message = "Competition not found." });

        var entries = await _context.CompetitionTeams.AsNoTracking()
            .Where(ct => ct.CompetitionId == competitionId)
            .OrderBy(ct => ct.Team.Name)
            .Select(ct => new CompetitionTeamResponse
            {
                CompetitionId = ct.CompetitionId,
                CompetitionName = ct.Competition != null
                    ? ct.Competition.Name : "",
                TeamId = ct.TeamId,
                TeamName = ct.Team != null ? ct.Team.Name : ""
            })
            .ToListAsync();
        return Ok(entries);
    }

    [HttpDelete]
    public async Task<IActionResult> RemoveTeam(
        [FromQuery] Guid competitionId,
        [FromQuery] Guid teamId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        if (competitionId == Guid.Empty || teamId == Guid.Empty)
            return BadRequest(new
            {
                message = "Valid competition and team IDs are required."
            });

        if (!await OwnsCompetitionAsync(competitionId, userId.Value))
            return NotFound(new { message = "Competition not found." });

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var entry = await _context.CompetitionTeams.FirstOrDefaultAsync(ct =>
            ct.CompetitionId == competitionId && ct.TeamId == teamId);
        if (entry is null)
            return NotFound(new
            {
                message = "Team is not registered in this competition."
            });

        var hasFixtures = await _context.Matches.AnyAsync(m =>
            m.CompetitionId == competitionId &&
            (m.HomeTeamId == teamId || m.AwayTeamId == teamId));
        if (hasFixtures)
            return Conflict(new
            {
                message = "This club already has fixtures. Its registration cannot be removed without affecting the competition schedule."
            });

        _context.CompetitionTeams.Remove(entry);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }
}
