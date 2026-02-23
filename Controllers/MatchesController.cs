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


    // ✅ GET all matches for a competition
    [HttpGet("competition/{competitionId}")]
    public async Task<IActionResult> GetMatches(Guid competitionId)
    {
        var matches = await _context.Matches
            .Where(x => x.CompetitionId == competitionId)
            .Include(x => x.Competition)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .ToListAsync();

        return Ok(matches);
    }

    // existing manual result endpoint
    [HttpPost("{matchId}/result")]
    public async Task<IActionResult> AddResult(Guid matchId, int homeScore, int awayScore)
    {
        var match = await _context.Matches.FindAsync(matchId);

        if (match == null)
            return NotFound();

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;
        match.IsPlayed = true;

        await _context.SaveChangesAsync();

        return Ok(match);
    }
    [HttpDelete("competition/{competitionId}")]
    public async Task<IActionResult> DeleteMatchesByCompetition(Guid competitionId)
    {
        var matches = _context.Matches.Where(m => m.CompetitionId == competitionId);

        if (!matches.Any())
            return NotFound("No matches found for this competition");

        _context.Matches.RemoveRange(matches);
        await _context.SaveChangesAsync();

        return Ok($"All matches for competition {competitionId} have been deleted.");
    }
}