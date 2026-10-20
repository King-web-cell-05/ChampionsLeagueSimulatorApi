using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MatchesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Matches/competition/{competitionId}
    // Get all matches for a competition.
    [HttpGet("competition/{competitionId:guid}")]
    public async Task<IActionResult> GetMatches(Guid competitionId)
    {
        if (competitionId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Invalid competition ID."
            });
        }

        var matches = await _context.Matches
            .AsNoTracking()
            .Where(m => m.CompetitionId == competitionId)
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
            CompetitionName = m.Competition?.Name ?? "Unknown",

            HomeTeamId = m.HomeTeamId,
            HomeTeamName = m.HomeTeam?.Name ?? "Unknown",

            AwayTeamId = m.AwayTeamId,
            AwayTeamName = m.AwayTeam?.Name ?? "Unknown",

            KickoffUtc = m.KickoffUtc,

            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore,

            IsPlayed = m.IsPlayed,
            MatchDay = m.MatchDay
        }).ToList();

        return Ok(result);
    }

    // POST: api/Matches/{matchId}/result?homeScore=2&awayScore=1
    // Record a match result.
    [HttpPost("{matchId:guid}/result")]
    public async Task<IActionResult> AddResult(
        Guid matchId,
        [FromQuery] int homeScore,
        [FromQuery] int awayScore)
    {
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

        var match = await _context.Matches
            .FirstOrDefaultAsync(m => m.Id == matchId);

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

    // DELETE: api/Matches/competition/{competitionId}
    // Deletes every match in the specified competition.
    [HttpDelete("competition/{competitionId:guid}")]
    public async Task<IActionResult> DeleteMatchesByCompetition(
        Guid competitionId)
    {
        if (competitionId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Invalid competition ID."
            });
        }

        var matches = _context.Matches
            .Where(m => m.CompetitionId == competitionId);

        if (!await matches.AnyAsync())
        {
            return NotFound(new
            {
                message = "No matches found for this competition."
            });
        }

        var matchList = await matches.ToListAsync();

        _context.Matches.RemoveRange(matchList);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "All matches for the competition have been deleted.",
            competitionId,
            deletedCount = matchList.Count
        });
    }
}