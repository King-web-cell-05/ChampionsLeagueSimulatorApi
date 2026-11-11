
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
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
    // GET COMPLETE MATCH DETAILS
    // GET: api/Matches/{matchId}/details
    // ==========================================

    [HttpGet("{matchId:guid}/details")]
    public async Task<IActionResult> GetMatchDetails(
        Guid matchId)
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

        if (matchId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Invalid match ID."
            });
        }

        // Only retrieve a match belonging to
        // a competition owned by this user.

        var match = await _context.Matches
            .AsNoTracking()
            .Where(m =>
                m.Id == matchId &&
                m.Competition.OwnerId == userId.Value)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Competition)
            .Include(m => m.Statistics)
            .FirstOrDefaultAsync();

        if (match is null)
        {
            return NotFound(new
            {
                message =
                    "Match not found or access denied."
            });
        }

        // ======================================
        // BUILD STATISTICS RESPONSE
        // ======================================

        // Older matches and unplayed fixtures
        // may not have statistics.

        object? statistics = null;

        if (match.Statistics is not null)
        {
            var s = match.Statistics;

            statistics = new
            {
                s.HomePossession,
                s.AwayPossession,

                s.HomeShots,
                s.AwayShots,

                s.HomeShotsOnTarget,
                s.AwayShotsOnTarget,

                s.HomeCorners,
                s.AwayCorners,

                s.HomeFouls,
                s.AwayFouls,

                s.HomeYellowCards,
                s.AwayYellowCards,

                s.HomeRedCards,
                s.AwayRedCards,

                s.HomeExpectedGoals,
                s.AwayExpectedGoals
            };
        }

        // ======================================
        // COMPLETE MATCH DETAILS RESPONSE
        // ======================================

        return Ok(new
        {
            MatchId = match.Id,

            match.CompetitionId,

            CompetitionName =
                match.Competition?.Name ?? "Unknown",

            match.MatchDay,
            match.KickoffUtc,

            match.IsPlayed,

            match.HomeScore,
            match.AwayScore,

            HomeTeam = new
            {
                Id = match.HomeTeamId,

                Name =
                    match.HomeTeam?.Name ?? "Unknown",

                Country =
                    match.HomeTeam?.Country ?? "",

                LogoUrl =
                    match.HomeTeam?.LogoUrl,

                Coefficient =
                    match.HomeTeam?.Coefficient ?? 0m
            },

            AwayTeam = new
            {
                Id = match.AwayTeamId,

                Name =
                    match.AwayTeam?.Name ?? "Unknown",

                Country =
                    match.AwayTeam?.Country ?? "",

                LogoUrl =
                    match.AwayTeam?.LogoUrl,

                Coefficient =
                    match.AwayTeam?.Coefficient ?? 0m
            },

            Statistics = statistics
        });
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
                message =
                    "Invalid or expired authentication session."
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

        // Match must belong to the
        // authenticated user's competition.

        var match = await _context.Matches
            .Include(m => m.Statistics)
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

        // ======================================
        // UPDATE FINAL SCORE
        // ======================================

        bool scoreChanged =
            match.HomeScore != homeScore ||
            match.AwayScore != awayScore;

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;
        match.IsPlayed = true;

        // ======================================
        // CLEAR OUTDATED STATISTICS
        // ======================================

        // If an existing score is changed
        // manually, previously simulated
        // statistics no longer describe
        // the updated result.

        if (
            scoreChanged &&
            match.Statistics is not null)
        {
            _context.MatchStatistics.Remove(
                match.Statistics);
        }

        await _context.SaveChangesAsync();

        // Preserve the original response.

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

        // Verify ownership before deletion.

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

        // Statistics are deleted automatically
        // through the configured cascade
        // relationship.

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
