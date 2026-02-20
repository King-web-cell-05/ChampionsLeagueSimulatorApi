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
}