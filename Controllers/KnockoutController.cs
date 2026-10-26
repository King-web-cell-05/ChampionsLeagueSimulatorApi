
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnockoutController : ControllerBase
{
    private readonly KnockoutDrawService _drawService;
    private readonly AppDbContext _context;

    public KnockoutController(
        KnockoutDrawService drawService,
        AppDbContext context)
    {
        _drawService = drawService;
        _context = context;
    }

    [HttpPost("{competitionId:guid}/playoffs/draw")]
    public async Task<IActionResult> GeneratePlayoffDraw(
        Guid competitionId)
    {
        try
        {
            var ties =
                await _drawService.GeneratePlayoffDraw(
                    competitionId);

            return Ok(new
            {
                Message = "Play-off draw generated successfully.",
                TotalTies = ties.Count,
                TotalMatches = ties.Sum(t => t.Matches.Count),
                Ties = ties.Select(t => new
                {
                    t.Id,
                    Round = t.Round.ToString(),
                    t.TieNumber,
                    SeededTeamId = t.HomeTeamId,
                    UnseededTeamId = t.AwayTeamId,
                    Matches = t.Matches
                        .OrderBy(m => m.Leg)
                        .Select(m => new
                        {
                            m.Id,
                            m.Leg,
                            m.HomeTeamId,
                            m.AwayTeamId
                        })
                })
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("{competitionId:guid}/playoffs")]
    public async Task<IActionResult> GetPlayoffs(
        Guid competitionId)
    {
        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Playoff)
            .Include(t => t.HomeTeam)
            .Include(t => t.AwayTeam)
            .Include(t => t.Matches)
                .ThenInclude(m => m.HomeTeam)
            .Include(t => t.Matches)
                .ThenInclude(m => m.AwayTeam)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        return Ok(ties.Select(t => new
        {
            t.Id,
            t.TieNumber,
            SeededTeam = t.HomeTeam.Name,
            UnseededTeam = t.AwayTeam.Name,
            t.IsCompleted,
            Matches = t.Matches
                .OrderBy(m => m.Leg)
                .Select(m => new
                {
                    m.Id,
                    m.Leg,
                    HomeTeam = m.HomeTeam.Name,
                    AwayTeam = m.AwayTeam.Name,
                    m.HomeScore,
                    m.AwayScore,
                    m.IsPlayed,
                    m.KickoffUtc
                })
        }));
    }
}
