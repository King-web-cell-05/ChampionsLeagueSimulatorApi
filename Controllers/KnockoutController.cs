
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
    // KNOCKOUT PLAY-OFFS
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

    [HttpGet("{competitionId:guid}/playoffs")]
    public Task<IActionResult> GetPlayoffs(
        Guid competitionId)
    {
        return GetRoundFixturesAsync(
            competitionId,
            KnockoutRound.Playoff);
    }

    [HttpPost("{competitionId:guid}/playoffs/simulate-first-legs")]
    public Task<IActionResult> SimulatePlayoffFirstLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.Playoff,
            1);
    }

    [HttpPost("{competitionId:guid}/playoffs/simulate-second-legs")]
    public Task<IActionResult> SimulatePlayoffSecondLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.Playoff,
            2);
    }

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
    // ROUND OF 16
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

    [HttpGet("{competitionId:guid}/round-of-16")]
    public Task<IActionResult> GetRoundOf16(
        Guid competitionId)
    {
        return GetRoundFixturesAsync(
            competitionId,
            KnockoutRound.RoundOf16);
    }

    [HttpPost("{competitionId:guid}/round-of-16/simulate-first-legs")]
    public Task<IActionResult> SimulateRoundOf16FirstLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.RoundOf16,
            1);
    }

    [HttpPost("{competitionId:guid}/round-of-16/simulate-second-legs")]
    public Task<IActionResult> SimulateRoundOf16SecondLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.RoundOf16,
            2);
    }

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
    // QUARTER-FINALS
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

    [HttpGet("{competitionId:guid}/quarter-finals")]
    public Task<IActionResult> GetQuarterFinals(
        Guid competitionId)
    {
        return GetRoundFixturesAsync(
            competitionId,
            KnockoutRound.QuarterFinal);
    }

    [HttpPost("{competitionId:guid}/quarter-finals/simulate-first-legs")]
    public Task<IActionResult> SimulateQuarterFinalFirstLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.QuarterFinal,
            1);
    }

    [HttpPost("{competitionId:guid}/quarter-finals/simulate-second-legs")]
    public Task<IActionResult> SimulateQuarterFinalSecondLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.QuarterFinal,
            2);
    }

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
    // SEMI-FINALS
    // ==========================================

    [HttpPost("{competitionId:guid}/semi-finals/draw")]
    public async Task<IActionResult> GenerateSemiFinalDraw(
        Guid competitionId)
    {
        try
        {
            await _drawService
                .GenerateSemiFinalDrawAsync(competitionId);

            return await GetDrawResponseAsync(
                competitionId,
                KnockoutRound.SemiFinal,
                "Semi-final draw generated successfully.");
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

    [HttpGet("{competitionId:guid}/semi-finals")]
    public Task<IActionResult> GetSemiFinals(
        Guid competitionId)
    {
        return GetRoundFixturesAsync(
            competitionId,
            KnockoutRound.SemiFinal);
    }

    [HttpPost("{competitionId:guid}/semi-finals/simulate-first-legs")]
    public Task<IActionResult> SimulateSemiFinalFirstLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.SemiFinal,
            1);
    }

    [HttpPost("{competitionId:guid}/semi-finals/simulate-second-legs")]
    public Task<IActionResult> SimulateSemiFinalSecondLegs(
        Guid competitionId)
    {
        return SimulateRoundLegsResponseAsync(
            competitionId,
            KnockoutRound.SemiFinal,
            2);
    }

    [HttpGet("{competitionId:guid}/semi-finals/winners")]
    public Task<IActionResult> GetSemiFinalWinners(
        Guid competitionId)
    {
        return GetRoundWinnersAsync(
            competitionId,
            KnockoutRound.SemiFinal,
            2);
    }

    // ==========================================
    // CHAMPIONSHIP FINAL
    // ==========================================

    [HttpPost("{competitionId:guid}/final/draw")]
    public async Task<IActionResult> GenerateFinalDraw(
        Guid competitionId)
    {
        try
        {
            await _drawService
                .GenerateFinalAsync(competitionId);

            return await GetDrawResponseAsync(
                competitionId,
                KnockoutRound.Final,
                "Championship final generated successfully.");
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

    [HttpGet("{competitionId:guid}/final")]
    public Task<IActionResult> GetFinal(
        Guid competitionId)
    {
        return GetRoundFixturesAsync(
            competitionId,
            KnockoutRound.Final);
    }

    // ==========================================
    // SIMULATE CHAMPIONSHIP FINAL
    // ==========================================

    [HttpPost("{competitionId:guid}/final/simulate")]
    public async Task<IActionResult> SimulateFinal(
        Guid competitionId)
    {
        try
        {
            var result = await _simulationService
                .SimulateFinalAsync(competitionId);

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
    // GET CHAMPION AND RUNNER-UP
    // ==========================================

    [HttpGet("{competitionId:guid}/champion")]
    public async Task<IActionResult> GetChampion(
        Guid competitionId)
    {
        if (competitionId == Guid.Empty)
        {
            return BadRequest(new
            {
                Message = "A valid competition ID is required."
            });
        }

        var finalTie = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Final)
            .Include(t => t.HomeTeam)
            .Include(t => t.AwayTeam)
            .Include(t => t.WinnerTeam)
            .Include(t => t.Matches)
            .SingleOrDefaultAsync();

        if (finalTie == null)
        {
            return NotFound(new
            {
                Message = "Championship final not found."
            });
        }

        if (!finalTie.IsCompleted ||
            !finalTie.WinnerTeamId.HasValue)
        {
            return Conflict(new
            {
                Message =
                    "The championship final has not been completed."
            });
        }

        if (finalTie.Matches.Count != 1)
        {
            return Conflict(new
            {
                Message =
                    "The final must contain exactly one match."
            });
        }

        var finalMatch = finalTie.Matches.Single();

        if (!finalMatch.IsPlayed)
        {
            return Conflict(new
            {
                Message = "The final match has not been played."
            });
        }

        if (finalTie.WinnerTeamId != finalTie.HomeTeamId &&
            finalTie.WinnerTeamId != finalTie.AwayTeamId)
        {
            return Conflict(new
            {
                Message = "Invalid championship winner."
            });
        }

        var runnerUpId =
            finalTie.WinnerTeamId == finalTie.HomeTeamId
                ? finalTie.AwayTeamId
                : finalTie.HomeTeamId;

        var runnerUpName =
            finalTie.WinnerTeamId == finalTie.HomeTeamId
                ? finalTie.AwayTeam.Name
                : finalTie.HomeTeam.Name;

        string decision;

        if (finalMatch.WentToPenalties)
        {
            decision = "Penalties";
        }
        else if (finalMatch.WentToExtraTime)
        {
            decision = "ExtraTime";
        }
        else
        {
            decision = "Regulation";
        }

        int homeTotalGoals =
            finalMatch.HomeScore.GetValueOrDefault() +
            finalMatch.HomeExtraTimeScore.GetValueOrDefault();

        int awayTotalGoals =
            finalMatch.AwayScore.GetValueOrDefault() +
            finalMatch.AwayExtraTimeScore.GetValueOrDefault();

        return Ok(new
        {
            CompetitionId = competitionId,
            Status = "Completed",
            Round = "Final",

            Champion = new
            {
                TeamId = finalTie.WinnerTeamId,
                TeamName = finalTie.WinnerTeam!.Name
            },

            RunnerUp = new
            {
                TeamId = runnerUpId,
                TeamName = runnerUpName
            },

            Final = new
            {
                MatchId = finalMatch.Id,

                HomeTeamId = finalTie.HomeTeamId,
                HomeTeam = finalTie.HomeTeam.Name,

                AwayTeamId = finalTie.AwayTeamId,
                AwayTeam = finalTie.AwayTeam.Name,

                finalMatch.HomeScore,
                finalMatch.AwayScore,

                finalMatch.WentToExtraTime,
                finalMatch.HomeExtraTimeScore,
                finalMatch.AwayExtraTimeScore,

                HomeTotalGoals = homeTotalGoals,
                AwayTotalGoals = awayTotalGoals,

                finalMatch.WentToPenalties,
                finalMatch.HomePenaltyScore,
                finalMatch.AwayPenaltyScore,

                finalMatch.IsPlayed,

                Decision = decision
            }
        });
    }

    // ==========================================
    // SIMULATE A SINGLE KNOCKOUT MATCH
    // ==========================================

    [HttpPost("matches/{matchId:guid}/simulate")]
    public async Task<IActionResult> SimulateMatch(
        Guid matchId)
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

    private async Task<IActionResult>
        SimulateRoundLegsResponseAsync(
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
    // SHARED ROUND WINNERS
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
