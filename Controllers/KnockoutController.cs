
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
    private readonly KnockoutBatchSimulationService _batchSimulationService;
    private readonly AppDbContext _context;

    public KnockoutController(
        KnockoutDrawService drawService,
        KnockoutSimulationService simulationService,
        KnockoutBatchSimulationService batchSimulationService,
        AppDbContext context)
    {
        _drawService = drawService;
        _simulationService = simulationService;
        _batchSimulationService = batchSimulationService;
        _context = context;
    }

    // ==========================================
    // PLAY-OFF DRAW
    // ==========================================

    [HttpPost("{competitionId:guid}/playoffs/draw")]
    public async Task<IActionResult> GeneratePlayoffDraw(
        Guid competitionId)
    {
        try
        {
            var ties = await _drawService
                .GeneratePlayoffDraw(competitionId);

            return Ok(new
            {
                Message = "Play-off draw generated successfully.",
                CompetitionId = competitionId,
                Round = "Playoff",
                TotalTies = ties.Count,
                TotalMatches = ties.Sum(t => t.Matches.Count),

                Ties = ties.Select(t => new
                {
                    t.Id,
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
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    // ==========================================
    // PLAY-OFF FIXTURES
    // ==========================================

    [HttpGet("{competitionId:guid}/playoffs")]
    public Task<IActionResult> GetPlayoffs(Guid competitionId)
    {
        return GetRoundFixturesAsync(
            competitionId,
            KnockoutRound.Playoff);
    }

    // ==========================================
    // PLAY-OFF FIRST LEGS
    // ==========================================

    [HttpPost(
        "{competitionId:guid}/playoffs/simulate-first-legs")]
    public Task<IActionResult> SimulatePlayoffFirstLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.Playoff,
            1);
    }

    // ==========================================
    // PLAY-OFF SECOND LEGS
    // ==========================================

    [HttpPost(
        "{competitionId:guid}/playoffs/simulate-second-legs")]
    public Task<IActionResult> SimulatePlayoffSecondLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.Playoff,
            2);
    }

    // ==========================================
    // PLAY-OFF WINNERS
    // ==========================================

    [HttpGet("{competitionId:guid}/playoffs/winners")]
    public Task<IActionResult> GetPlayoffWinners(
        Guid competitionId)
    {
        return GetRoundWinnersAsync(
            competitionId,
            KnockoutRound.Playoff,
            8);
    }

    // ==========================================
    // ROUND OF 16 DRAW
    // ==========================================

    [HttpPost("{competitionId:guid}/round-of-16/draw")]
    public async Task<IActionResult> GenerateRoundOf16Draw(
        Guid competitionId)
    {
        try
        {
            await _drawService
                .GenerateRoundOf16DrawAsync(competitionId);

            return await GetDrawResponseAsync(
                competitionId,
                KnockoutRound.RoundOf16,
                "Round of 16 draw generated successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    // ==========================================
    // ROUND OF 16 FIXTURES
    // ==========================================

    [HttpGet("{competitionId:guid}/round-of-16")]
    public Task<IActionResult> GetRoundOf16(
        Guid competitionId)
    {
        return GetRoundFixturesAsync(
            competitionId,
            KnockoutRound.RoundOf16);
    }

    // ==========================================
    // ROUND OF 16 FIRST LEGS
    // ==========================================

    [HttpPost(
        "{competitionId:guid}/round-of-16/simulate-first-legs")]
    public Task<IActionResult> SimulateRoundOf16FirstLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.RoundOf16,
            1);
    }

    // ==========================================
    // ROUND OF 16 SECOND LEGS
    // ==========================================

    [HttpPost(
        "{competitionId:guid}/round-of-16/simulate-second-legs")]
    public Task<IActionResult> SimulateRoundOf16SecondLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.RoundOf16,
            2);
    }

    // ==========================================
    // ROUND OF 16 WINNERS
    // ==========================================

    [HttpGet("{competitionId:guid}/round-of-16/winners")]
    public Task<IActionResult> GetRoundOf16Winners(
        Guid competitionId)
    {
        return GetRoundWinnersAsync(
            competitionId,
            KnockoutRound.RoundOf16,
            8);
    }

    // ==========================================
    // QUARTER-FINAL DRAW
    // ==========================================

    [HttpPost("{competitionId:guid}/quarter-finals/draw")]
    public async Task<IActionResult> GenerateQuarterFinalDraw(
        Guid competitionId)
    {
        try
        {
            await _drawService
                .GenerateQuarterFinalDrawAsync(competitionId);

            return await GetDrawResponseAsync(
                competitionId,
                KnockoutRound.QuarterFinal,
                "Quarter-final draw generated successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    // ==========================================
    // QUARTER-FINAL FIXTURES
    // ==========================================

    [HttpGet("{competitionId:guid}/quarter-finals")]
    public Task<IActionResult> GetQuarterFinals(
        Guid competitionId)
    {
        return GetRoundFixturesAsync(
            competitionId,
            KnockoutRound.QuarterFinal);
    }

    // ==========================================
    // QUARTER-FINAL FIRST LEGS
    // ==========================================

    [HttpPost(
        "{competitionId:guid}/quarter-finals/simulate-first-legs")]
    public Task<IActionResult> SimulateQuarterFinalFirstLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.QuarterFinal,
            1);
    }

    // ==========================================
    // QUARTER-FINAL SECOND LEGS
    // ==========================================

    [HttpPost(
        "{competitionId:guid}/quarter-finals/simulate-second-legs")]
    public Task<IActionResult> SimulateQuarterFinalSecondLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.QuarterFinal,
            2);
    }

    // ==========================================
    // QUARTER-FINAL WINNERS
    // ==========================================

    [HttpGet("{competitionId:guid}/quarter-finals/winners")]
    public Task<IActionResult> GetQuarterFinalWinners(
        Guid competitionId)
    {
        return GetRoundWinnersAsync(
            competitionId,
            KnockoutRound.QuarterFinal,
            4);
    }

    // ==========================================
    // SIMULATE SINGLE KNOCKOUT MATCH
    // ==========================================

    [HttpPost("matches/{matchId:guid}/simulate")]
    public async Task<IActionResult> SimulateMatch(Guid matchId)
    {
        try
        {
            var result = await _simulationService
                .SimulateMatchAsync(matchId);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    // ==========================================
    // SHARED DRAW RESPONSE
    // ==========================================

    private async Task<IActionResult> GetDrawResponseAsync(
        Guid competitionId,
        KnockoutRound round,
        string message)
    {
        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == round)
            .Include(t => t.HomeTeam)
            .Include(t => t.AwayTeam)
            .Include(t => t.Matches)
                .ThenInclude(m => m.HomeTeam)
            .Include(t => t.Matches)
                .ThenInclude(m => m.AwayTeam)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        return Ok(new
        {
            Message = message,
            CompetitionId = competitionId,
            Round = round.ToString(),
            TotalTies = ties.Count,
            TotalMatches = ties.Sum(t => t.Matches.Count),

            Ties = ties.Select(t => new
            {
                t.Id,
                t.TieNumber,
                Team1Id = t.HomeTeamId,
                Team1 = t.HomeTeam.Name,
                Team2Id = t.AwayTeamId,
                Team2 = t.AwayTeam.Name,

                Matches = t.Matches
                    .OrderBy(m => m.Leg)
                    .Select(m => new
                    {
                        m.Id,
                        m.Leg,
                        m.HomeTeamId,
                        HomeTeam = m.HomeTeam.Name,
                        m.AwayTeamId,
                        AwayTeam = m.AwayTeam.Name,
                        m.IsPlayed
                    })
            })
        });
    }

    // ==========================================
    // SHARED FIXTURES AND RESULTS
    // ==========================================

    private async Task<IActionResult> GetRoundFixturesAsync(
        Guid competitionId,
        KnockoutRound round)
    {
        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == round)
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
            Round = t.Round.ToString(),

            Team1Id = t.HomeTeamId,
            Team1 = t.HomeTeam.Name,

            Team2Id = t.AwayTeamId,
            Team2 = t.AwayTeam.Name,

            t.IsCompleted,
            t.WinnerTeamId,
            Winner = t.WinnerTeam?.Name,

            Matches = t.Matches
                .OrderBy(m => m.Leg)
                .Select(m => new
                {
                    m.Id,
                    m.Leg,

                    m.HomeTeamId,
                    HomeTeam = m.HomeTeam.Name,

                    m.AwayTeamId,
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
    // SHARED BATCH SIMULATION
    // ==========================================

    private async Task<IActionResult> SimulateRoundLegsResponseAsync(
        Guid competitionId,
        KnockoutRound round,
        int leg)
    {
        try
        {
            var result = await _batchSimulationService
                .SimulateRoundLegsAsync(
                    competitionId,
                    round,
                    leg);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    // ==========================================
    // SHARED QUALIFIED TEAMS RESPONSE
    // ==========================================

    private async Task<IActionResult> GetRoundWinnersAsync(
        Guid competitionId,
        KnockoutRound round,
        int expectedTies)
    {
        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == round)
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

        bool isCompleted =
            ties.Count == expectedTies &&
            ties.All(t =>
                t.IsCompleted &&
                t.WinnerTeamId.HasValue) &&
            winners
                .Select(w => w.TeamId)
                .Distinct()
                .Count() == expectedTies;

        return Ok(new
        {
            CompetitionId = competitionId,
            Round = round.ToString(),

            TotalTies = ties.Count,
            CompletedTies = ties.Count(t => t.IsCompleted),
            QualifiedTeams = winners.Count,

            IsRoundCompleted = isCompleted,

            Winners = winners
        });
    }
}
