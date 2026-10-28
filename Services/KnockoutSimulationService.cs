
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

    // ==========================================
    // SIMULATE ANY KNOCKOUT MATCH
    // ==========================================

    public async Task<object> SimulateMatchAsync(Guid matchId)
    {
        if (matchId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid knockout match ID is required.");
        }

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
        {
            throw new KeyNotFoundException(
                "Knockout match not found.");
        }

        var tie = match.KnockoutTie;

        if (tie.IsCompleted)
        {
            throw new InvalidOperationException(
                "This knockout tie is already completed.");
        }

        if (match.IsPlayed)
        {
            throw new InvalidOperationException(
                "This match has already been simulated.");
        }

        // ==========================================
        // VALIDATE MATCH CONFIGURATION
        // ==========================================

        var legs = tie.Matches
            .OrderBy(m => m.Leg)
            .ToList();

        bool isFinal = tie.Round == KnockoutRound.Final;

        if (isFinal)
        {
            if (legs.Count != 1 ||
                legs[0].Leg != 1 ||
                match.Leg != 1)
            {
                throw new InvalidOperationException(
                    "The final must contain exactly one match.");
            }

            if (match.HomeTeamId != tie.HomeTeamId ||
                match.AwayTeamId != tie.AwayTeamId)
            {
                throw new InvalidOperationException(
                    "The final teams do not match the tie.");
            }
        }
        else
        {
            if (legs.Count != 2 ||
                legs[0].Leg != 1 ||
                legs[1].Leg != 2)
            {
                throw new InvalidOperationException(
                    "A two-legged tie must contain legs 1 and 2.");
            }

            if (match.Leg == 2 && !legs[0].IsPlayed)
            {
                throw new InvalidOperationException(
                    "Simulate the first leg before the second leg.");
            }

            if (match.Leg != 1 && match.Leg != 2)
            {
                throw new InvalidOperationException(
                    "Invalid knockout match leg.");
            }
        }

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
        // CHAMPIONSHIP FINAL
        // ==========================================

        if (isFinal)
        {
            int homeGoals = match.HomeScore.Value;
            int awayGoals = match.AwayScore.Value;

            // --------------------------------------
            // EXTRA TIME
            // --------------------------------------

            if (homeGoals == awayGoals)
            {
                match.WentToExtraTime = true;

                match.HomeExtraTimeScore =
                    Random.Shared.Next(0, 3);

                match.AwayExtraTimeScore =
                    Random.Shared.Next(0, 3);

                homeGoals += match.HomeExtraTimeScore.Value;
                awayGoals += match.AwayExtraTimeScore.Value;

                decision = "ExtraTime";

                // ----------------------------------
                // PENALTY SHOOTOUT
                // ----------------------------------

                if (homeGoals == awayGoals)
                {
                    match.WentToPenalties = true;

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

            // --------------------------------------
            // DETERMINE FINAL WINNER
            // --------------------------------------

            if (!match.WentToPenalties)
            {
                tie.WinnerTeamId =
                    homeGoals > awayGoals
                        ? match.HomeTeamId
                        : match.AwayTeamId;

                decision ??= "Regulation";
            }

            tie.IsCompleted = true;
        }

        // ==========================================
        // TWO-LEGGED KNOCKOUT ROUNDS
        // ==========================================

        else if (match.Leg == 2)
        {
            var firstLeg = legs[0];

            if (!firstLeg.IsPlayed ||
                !firstLeg.HomeScore.HasValue ||
                !firstLeg.AwayScore.HasValue)
            {
                throw new InvalidOperationException(
                    "First-leg scores are incomplete.");
            }

            // Leg 1: tie.AwayTeam at home.
            // Leg 2: tie.HomeTeam at home.

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

            // --------------------------------------
            // EXTRA TIME
            // --------------------------------------

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

                // ----------------------------------
                // PENALTY SHOOTOUT
                // ----------------------------------

                if (seededGoals == unseededGoals)
                {
                    match.WentToPenalties = true;

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

            // --------------------------------------
            // DETERMINE AGGREGATE WINNER
            // --------------------------------------

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

        // ==========================================
        // SAVE MATCH AND TIE RESULTS
        // ==========================================

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        // ==========================================
        // RETURN SIMULATION RESULT
        // ==========================================

        return new
        {
            MatchId = match.Id,
            TieId = tie.Id,
            Round = tie.Round.ToString(),

            match.Leg,

            HomeTeamId = match.HomeTeamId,
            HomeTeam = match.HomeTeam.Name,

            AwayTeamId = match.AwayTeamId,
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

            WinnerTeam = tie.WinnerTeamId == match.HomeTeamId
                ? match.HomeTeam.Name
                : tie.WinnerTeamId == match.AwayTeamId
                    ? match.AwayTeam.Name
                    : null,

            TieCompleted = tie.IsCompleted,

            IsChampionDecided =
                isFinal && tie.IsCompleted,

            Decision = decision
        };
    }

    // ==========================================
    // SIMULATE CHAMPIONSHIP FINAL
    // ==========================================

    public async Task<object> SimulateFinalAsync(
        Guid competitionId)
    {
        if (competitionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid competition ID is required.");
        }

        var finalTie = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Final)
            .Select(t => new
            {
                t.Id,
                t.IsCompleted,

                Matches = t.Matches
                    .Select(m => new
                    {
                        m.Id,
                        m.Leg
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync();

        if (finalTie == null)
        {
            throw new KeyNotFoundException(
                "Championship final has not been generated.");
        }

        if (finalTie.IsCompleted)
        {
            throw new InvalidOperationException(
                "The championship final has already been completed.");
        }

        if (finalTie.Matches.Count != 1 ||
            finalTie.Matches[0].Leg != 1)
        {
            throw new InvalidOperationException(
                "The championship final must contain exactly one match.");
        }

        return await SimulateMatchAsync(
            finalTie.Matches[0].Id);
    }
}
