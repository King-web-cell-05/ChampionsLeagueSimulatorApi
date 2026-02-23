using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Data;
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

    // Manual result entry (existing)
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

    // NEW — Automatic simulation
    [HttpPost("simulate/{matchId}")]
    public async Task<IActionResult> SimulateMatch(Guid matchId)
    {
        var match = await _context.Matches.FindAsync(matchId);

        if (match == null)
            return NotFound();

        var random = new Random();

        match.HomeScore = random.Next(0, 5);
        match.AwayScore = random.Next(0, 5);
        match.IsPlayed = true;

        await _context.SaveChangesAsync();

        return Ok(match);
    }
}