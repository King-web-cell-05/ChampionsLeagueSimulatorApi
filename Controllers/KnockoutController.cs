
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
    private readonly KnockoutSimulationService _simulationService;
    private readonly AppDbContext _context;

    public KnockoutController(
        KnockoutDrawService drawService,
        KnockoutSimulationService simulationService,
        AppDbContext context)
    {
        _drawService = drawService;
        _simulationService = simulationService;
        _context = context;
    }

    // ==========================================
    // GENERATE PLAY-OFF DRAW
    // ==========================================

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

    // ==========================================
    // GET PLAY-OFF FIXTURES AND RESULTS
    // ==========================================

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
            .Include(t => t.WinnerTeam)
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
            Winner = t.WinnerTeam?.Name,

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

                    m.WentToExtraTime,
                    m.HomeExtraTimeScore,
                    m.AwayExtraTimeScore,

                    m.WentToPenalties,
                    m.HomePenaltyScore,
                    m.AwayPenaltyScore,

                    m.IsPlayed,
                    m.KickoffUtc
                })
        }));
    }

    // ==========================================
    // SIMULATE A KNOCKOUT MATCH
    // ==========================================

    [HttpPost("matches/{matchId:guid}/simulate")]
    public async Task<IActionResult> SimulateMatch(
        Guid matchId)
    {
        try
        {
            var result =
                await _simulationService.SimulateMatchAsync(
                    matchId);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                Message = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                Message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                Message = ex.Message
            });
        }
    }

    // ==========================================
    // GET QUALIFIED PLAY-OFF WINNERS
    // ==========================================

    [HttpGet("{competitionId:guid}/playoffs/winners")]
    public async Task<IActionResult> GetPlayoffWinners(
        Guid competitionId)
    {
        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Playoff)
            .Include(t => t.WinnerTeam)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        var winners = ties
            .Where(t =>
                t.IsCompleted &&
                t.WinnerTeamId.HasValue)
            .Select(t => new
            {
                t.TieNumber,
                TeamId = t.WinnerTeamId,
                TeamName = t.WinnerTeam!.Name
            })
            .ToList();

        return Ok(new
        {
            TotalTies = ties.Count,
            CompletedTies = ties.Count(t => t.IsCompleted),
            QualifiedTeams = winners.Count,
            IsPlayoffStageCompleted =
                ties.Count == 8 &&
                winners.Count == 8,
            Winners = winners
        });
    }
}
