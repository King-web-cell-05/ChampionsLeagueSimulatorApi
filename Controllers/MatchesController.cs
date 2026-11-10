
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MatchesController(AppDbContext context)
    {
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
    // VERIFY COMPETITION OWNERSHIP
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
    // GET MATCHES FOR OWN COMPETITION
    // GET: api/Matches/competition/{competitionId}
    // ==========================================

    [HttpGet("competition/{competitionId:guid}")]
    public async Task<IActionResult> GetMatches(
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

        // Ensure the authenticated user owns
        // the requested competition.
        if (!await OwnsCompetitionAsync(
                competitionId,
                userId.Value))
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        var matches = await _context.Matches
            .AsNoTracking()
            .Where(m =>
                m.CompetitionId == competitionId &&
                m.Competition.OwnerId == userId.Value)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Competition)
            .OrderBy(m => m.MatchDay)
            .ThenBy(m => m.KickoffUtc)
            .ToListAsync();

        var result = matches.Select(m => new MatchDto
        {
            Id = m.Id,
            CompetitionId = m.CompetitionId,
            CompetitionName =
                m.Competition?.Name ?? "Unknown",

            HomeTeamId = m.HomeTeamId,
            HomeTeamName =
                m.HomeTeam?.Name ?? "Unknown",

            AwayTeamId = m.AwayTeamId,
            AwayTeamName =
                m.AwayTeam?.Name ?? "Unknown",

            KickoffUtc = m.KickoffUtc,

            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore,

            IsPlayed = m.IsPlayed,
            MatchDay = m.MatchDay
        }).ToList();

        return Ok(result);
    }

    // ==========================================
    // RECORD MATCH RESULT
    // POST: api/Matches/{matchId}/result
    // ==========================================

    [HttpPost("{matchId:guid}/result")]
    public async Task<IActionResult> AddResult(
        Guid matchId,
        [FromQuery] int homeScore,
        [FromQuery] int awayScore)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid or expired authentication session."
            });
        }

        if (matchId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Invalid match ID."
            });
        }

        if (homeScore < 0 || awayScore < 0)
        {
            return BadRequest(new
            {
                message = "Scores cannot be negative."
            });
        }

        // Match must belong to a competition
        // owned by the authenticated user.
        var match = await _context.Matches
            .FirstOrDefaultAsync(m =>
                m.Id == matchId &&
                m.Competition.OwnerId == userId.Value);

        if (match is null)
        {
            return NotFound(new
            {
                message = "Match not found."
            });
        }

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;
        match.IsPlayed = true;

        await _context.SaveChangesAsync();

        // Preserve the original response format.
        return Ok(new
        {
            match.Id,
            match.HomeTeamId,
            match.AwayTeamId,
            match.KickoffUtc,
            match.HomeScore,
            match.AwayScore,
            match.IsPlayed,
            match.MatchDay
        });
    }

    // ==========================================
    // DELETE MATCHES FROM OWN COMPETITION
    // DELETE: api/Matches/competition/{competitionId}
    // ==========================================

    [HttpDelete("competition/{competitionId:guid}")]
    public async Task<IActionResult> DeleteMatchesByCompetition(
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

        // Verify ownership before deleting anything.
        if (!await OwnsCompetitionAsync(
                competitionId,
                userId.Value))
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        var matches = await _context.Matches
            .Where(m =>
                m.CompetitionId == competitionId &&
                m.Competition.OwnerId == userId.Value)
            .ToListAsync();

        if (matches.Count == 0)
        {
            return NotFound(new
            {
                message =
                    "No matches found for this competition."
            });
        }

        _context.Matches.RemoveRange(matches);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "All matches for the competition have been deleted.",
            competitionId,
            deletedCount = matches.Count
        });
    }
}
