
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class KnockoutBatchSimulationService
{
    private readonly AppDbContext _context;
    private readonly KnockoutSimulationService _simulationService;

    public KnockoutBatchSimulationService(
        AppDbContext context,
        KnockoutSimulationService simulationService)
    {
        _context = context;
        _simulationService = simulationService;
    }

    // ==========================================
    // EXISTING PLAY-OFF SIMULATION
    // ==========================================

    public Task<object> SimulatePlayoffLegsAsync(
        Guid competitionId,
        int leg)
    {
        return SimulateRoundLegsAsync(
            competitionId,
            KnockoutRound.Playoff,
            leg);
    }

    // ==========================================
    // ROUND OF 16 SIMULATION
    // ==========================================

    public Task<object> SimulateRoundOf16LegsAsync(
        Guid competitionId,
        int leg)
    {
        return SimulateRoundLegsAsync(
            competitionId,
            KnockoutRound.RoundOf16,
            leg);
    }

    // ==========================================
    // REUSABLE KNOCKOUT SIMULATION
    // ==========================================

    public async Task<object> SimulateRoundLegsAsync(
        Guid competitionId,
        KnockoutRound round,
        int leg)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException(
                "Invalid competition ID.");

        if (leg != 1 && leg != 2)
            throw new ArgumentException(
                "Leg must be 1 or 2.");

        if (round == KnockoutRound.Final)
            throw new InvalidOperationException(
                "The final requires single-match simulation.");

        int expectedTies = round switch
        {
            KnockoutRound.Playoff => 8,
            KnockoutRound.RoundOf16 => 8,
            KnockoutRound.QuarterFinal => 4,
            KnockoutRound.SemiFinal => 2,
            _ => throw new ArgumentException(
                "Unsupported knockout round.")
        };

        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == round)
            .Include(t => t.Matches)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        if (ties.Count != expectedTies)
            throw new InvalidOperationException(
                $"Expected {expectedTies} ties for {round}.");

        if (ties.Any(t =>
            t.Matches.Count != 2 ||
            t.Matches.Count(m => m.Leg == 1) != 1 ||
            t.Matches.Count(m => m.Leg == 2) != 1))
        {
            throw new InvalidOperationException(
                "Every tie must contain two valid legs.");
        }

        if (leg == 2 && ties.Any(t =>
            !t.Matches.Single(m => m.Leg == 1).IsPlayed))
        {
            throw new InvalidOperationException(
                "Complete all first legs before " +
                "simulating second legs.");
        }

        var pendingMatches = ties
            .Where(t => !t.IsCompleted)
            .SelectMany(t => t.Matches
                .Where(m =>
                    m.Leg == leg &&
                    !m.IsPlayed)
                .Select(m => new
                {
                    t.TieNumber,
                    MatchId = m.Id
                }))
            .OrderBy(m => m.TieNumber)
            .ToList();

        var results = new List<object>();

        foreach (var pending in pendingMatches)
        {
            var result = await _simulationService
                .SimulateMatchAsync(pending.MatchId);

            results.Add(result);
        }

        // Reload the updated stage state.
        var updatedTies = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == round)
            .Include(t => t.Matches)
            .ToListAsync();

        return new
        {
            CompetitionId = competitionId,
            Round = round.ToString(),
            Leg = leg,

            SimulatedMatches = results.Count,
            TotalTies = updatedTies.Count,

            CompletedFirstLegs = updatedTies
                .SelectMany(t => t.Matches)
                .Count(m =>
                    m.Leg == 1 &&
                    m.IsPlayed),

            CompletedSecondLegs = updatedTies
                .SelectMany(t => t.Matches)
                .Count(m =>
                    m.Leg == 2 &&
                    m.IsPlayed),

            CompletedTies = updatedTies
                .Count(t => t.IsCompleted),

            IsRoundCompleted = updatedTies.Count == expectedTies &&
                updatedTies.All(t =>
                    t.IsCompleted &&
                    t.WinnerTeamId.HasValue),

            Results = results
        };
    }
}
