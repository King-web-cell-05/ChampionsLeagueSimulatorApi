
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

    public async Task<object> SimulatePlayoffLegsAsync(
        Guid competitionId,
        int leg)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException(
                "A valid competition ID is required.");

        if (leg != 1 && leg != 2)
            throw new ArgumentException(
                "Leg must be 1 or 2.");

        // Load all eight play-off ties.
        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Playoff)
            .Include(t => t.Matches)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        if (ties.Count != 8)
            throw new InvalidOperationException(
                "Expected exactly eight play-off ties.");

        if (ties.Any(t =>
            t.Matches.Count != 2 ||
            t.Matches.Count(m => m.Leg == 1) != 1 ||
            t.Matches.Count(m => m.Leg == 2) != 1))
        {
            throw new InvalidOperationException(
                "Every play-off tie must contain two valid legs.");
        }

        // Require every first leg to finish before
        // batch-simulating second legs.
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
                .Where(m => m.Leg == leg && !m.IsPlayed)
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
            // Each match is committed by the existing
            // simulation service.
            var result =
                await _simulationService.SimulateMatchAsync(
                    pending.MatchId);

            results.Add(result);
        }

        // Requery to obtain the updated stage status.
        var updatedTies = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Playoff)
            .Include(t => t.Matches)
            .ToListAsync();

        return new
        {
            CompetitionId = competitionId,
            Round = "Playoff",
            Leg = leg,
            SimulatedMatches = results.Count,
            TotalTies = updatedTies.Count,

            CompletedFirstLegs = updatedTies
                .SelectMany(t => t.Matches)
                .Count(m => m.Leg == 1 && m.IsPlayed),

            CompletedSecondLegs = updatedTies
                .SelectMany(t => t.Matches)
                .Count(m => m.Leg == 2 && m.IsPlayed),

            CompletedTies = updatedTies
                .Count(t => t.IsCompleted),

            Results = results
        };
    }
}
