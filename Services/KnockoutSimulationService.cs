
using System.Data;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class KnockoutSimulationService
{
    private readonly AppDbContext _context;

    public KnockoutSimulationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<object> SimulateMatchAsync(
        Guid matchId)
    {
        if (matchId == Guid.Empty)
            throw new ArgumentException(
                "A valid knockout match ID is required.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var match = await _context.KnockoutMatches
            .Include(m => m.KnockoutTie)
                .ThenInclude(t => t.Matches)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null)
            throw new KeyNotFoundException(
                "Knockout match not found.");

        var tie = match.KnockoutTie;

        if (tie.IsCompleted)
            throw new InvalidOperationException(
                "This knockout tie is already completed.");

        if (match.IsPlayed)
            throw new InvalidOperationException(
                "This match has already been simulated.");

        // Currently support two-legged knockout ties.
        // The single-match final will be handled separately.
        if (tie.Round == KnockoutRound.Final)
            throw new InvalidOperationException(
                "Final simulation requires single-match logic.");

        var legs = tie.Matches
            .OrderBy(m => m.Leg)
            .ToList();

        if (legs.Count != 2 ||
            legs[0].Leg != 1 ||
            legs[1].Leg != 2)
        {
            throw new InvalidOperationException(
                "A two-legged tie must contain legs 1 and 2.");
        }

        if (match.Leg == 2 && !legs[0].IsPlayed)
            throw new InvalidOperationException(
                "Simulate the first leg before the second leg.");

        if (match.Leg != 1 && match.Leg != 2)
            throw new InvalidOperationException(
                "Invalid knockout match leg.");

        // ==========================================
        // REGULATION TIME (90 MINUTES)
        // ==========================================

        match.HomeScore = Random.Shared.Next(0, 5);
        match.AwayScore = Random.Shared.Next(0, 5);

        match.WentToExtraTime = false;
        match.WentToPenalties = false;

        match.HomeExtraTimeScore = null;
        match.AwayExtraTimeScore = null;
        match.HomePenaltyScore = null;
        match.AwayPenaltyScore = null;

        match.IsPlayed = true;

        int? seededAggregate = null;
        int? unseededAggregate = null;
        string? decision = null;

        // ==========================================
        // SECOND LEG: AGGREGATE CALCULATION
        // ==========================================

        if (match.Leg == 2)
        {
            var firstLeg = legs[0];

            if (!firstLeg.IsPlayed ||
                !firstLeg.HomeScore.HasValue ||
                !firstLeg.AwayScore.HasValue)
            {
                throw new InvalidOperationException(
                    "First-leg scores are incomplete.");
            }

            // In the current draw:
            // Leg 1: unseeded at home.
            // Leg 2: seeded at home.
            if (firstLeg.HomeTeamId != tie.AwayTeamId ||
                firstLeg.AwayTeamId != tie.HomeTeamId ||
                match.HomeTeamId != tie.HomeTeamId ||
                match.AwayTeamId != tie.AwayTeamId)
            {
                throw new InvalidOperationException(
                    "The tie's home/away configuration is invalid.");
            }

            int seededGoals =
                firstLeg.AwayScore.Value +
                match.HomeScore.Value;

            int unseededGoals =
                firstLeg.HomeScore.Value +
                match.AwayScore.Value;

            // ======================================
            // EXTRA TIME
            // ======================================

            if (seededGoals == unseededGoals)
            {
                match.WentToExtraTime = true;

                match.HomeExtraTimeScore =
                    Random.Shared.Next(0, 3);

                match.AwayExtraTimeScore =
                    Random.Shared.Next(0, 3);

                seededGoals +=
                    match.HomeExtraTimeScore.Value;

                unseededGoals +=
                    match.AwayExtraTimeScore.Value;

                decision = "ExtraTime";

                // ==================================
                // PENALTY SHOOTOUT
                // ==================================

                if (seededGoals == unseededGoals)
                {
                    match.WentToPenalties = true;

                    // Simplified shootout result.
                    // This records a decisive score rather
                    // than simulating individual penalty kicks.
                    int winnerScore =
                        Random.Shared.Next(4, 7);

                    int loserScore =
                        Random.Shared.Next(2, winnerScore);

                    bool homeWins =
                        Random.Shared.Next(2) == 0;

                    match.HomePenaltyScore =
                        homeWins
                            ? winnerScore
                            : loserScore;

                    match.AwayPenaltyScore =
                        homeWins
                            ? loserScore
                            : winnerScore;

                    tie.WinnerTeamId =
                        homeWins
                            ? match.HomeTeamId
                            : match.AwayTeamId;

                    decision = "Penalties";
                }
            }

            // ======================================
            // DETERMINE THE WINNER
            // ======================================

            if (!match.WentToPenalties)
            {
                tie.WinnerTeamId =
                    seededGoals > unseededGoals
                        ? tie.HomeTeamId
                        : tie.AwayTeamId;

                decision ??= "Aggregate";
            }

            seededAggregate = seededGoals;
            unseededAggregate = unseededGoals;

            tie.IsCompleted = true;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new
        {
            MatchId = match.Id,
            TieId = tie.Id,
            Round = tie.Round.ToString(),
            match.Leg,

            HomeTeam = match.HomeTeam.Name,
            AwayTeam = match.AwayTeam.Name,

            match.HomeScore,
            match.AwayScore,

            match.WentToExtraTime,
            match.HomeExtraTimeScore,
            match.AwayExtraTimeScore,

            match.WentToPenalties,
            match.HomePenaltyScore,
            match.AwayPenaltyScore,

            match.IsPlayed,

            SeededAggregate = seededAggregate,
            UnseededAggregate = unseededAggregate,

            WinnerTeamId = tie.WinnerTeamId,
            TieCompleted = tie.IsCompleted,
            Decision = decision
        };
    }
}
