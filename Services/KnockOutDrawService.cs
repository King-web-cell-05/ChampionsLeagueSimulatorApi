
using System.Data;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class KnockoutDrawService
{
    private readonly AppDbContext _context;
    private readonly TableService _tableService;

    public KnockoutDrawService(
        AppDbContext context,
        TableService tableService)
    {
        _context = context;
        _tableService = tableService;
    }

    // ==========================================
    // GENERATE PLAY-OFF DRAW
    // ==========================================

    public async Task<List<KnockoutTie>>
        GeneratePlayoffDraw(Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException(
                "Invalid competition ID.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        bool competitionExists =
            await _context.Competitions
                .AnyAsync(c => c.Id == competitionId);

        if (!competitionExists)
            throw new InvalidOperationException(
                "Competition not found.");

        bool drawExists = await _context.KnockoutTies
            .AnyAsync(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Playoff);

        if (drawExists)
            throw new InvalidOperationException(
                "Play-off draw already exists.");

        var leagueMatches = await _context.Matches
            .AsNoTracking()
            .Where(m => m.CompetitionId == competitionId)
            .Select(m => new
            {
                m.IsPlayed,
                m.HomeScore,
                m.AwayScore
            })
            .ToListAsync();

        if (leagueMatches.Count != 144 ||
            leagueMatches.Any(m =>
                !m.IsPlayed ||
                !m.HomeScore.HasValue ||
                !m.AwayScore.HasValue))
        {
            throw new InvalidOperationException(
                "The 36-team league phase must have " +
                "144 completed matches before the draw.");
        }

        var standings =
            await _tableService.GenerateTable(competitionId);

        if (standings.Count != 36 ||
            standings.Any(s => s.Played != 8))
        {
            throw new InvalidOperationException(
                "Expected 36 clubs with 8 matches each.");
        }

        var brackets = new[]
        {
            (SeedStart: 9, OpponentStart: 23),
            (SeedStart: 11, OpponentStart: 21),
            (SeedStart: 13, OpponentStart: 19),
            (SeedStart: 15, OpponentStart: 17)
        };

        var ties = new List<KnockoutTie>();
        int tieNumber = 1;

        foreach (var bracket in brackets)
        {
            var seeds = standings
                .Where(s =>
                    s.Position >= bracket.SeedStart &&
                    s.Position <= bracket.SeedStart + 1)
                .OrderBy(s => s.Position)
                .ToList();

            var opponents = standings
                .Where(s =>
                    s.Position >= bracket.OpponentStart &&
                    s.Position <= bracket.OpponentStart + 1)
                .OrderBy(s => s.Position)
                .ToList();

            if (seeds.Count != 2 || opponents.Count != 2)
                throw new InvalidOperationException(
                    "Invalid play-off seeding bracket.");

            if (Random.Shared.Next(2) == 1)
                opponents.Reverse();

            for (int i = 0; i < 2; i++)
            {
                var seeded = seeds[i];
                var unseeded = opponents[i];

                var tie = CreateTwoLeggedTie(
                    competitionId,
                    KnockoutRound.Playoff,
                    tieNumber++,
                    seeded.TeamId,
                    unseeded.TeamId);

                ties.Add(tie);
            }
        }

        _context.KnockoutTies.AddRange(ties);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ties;
    }

    // ==========================================
    // GENERATE ROUND OF 16 DRAW
    // ==========================================

    public async Task<List<KnockoutTie>>
        GenerateRoundOf16DrawAsync(Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException(
                "Invalid competition ID.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        bool competitionExists =
            await _context.Competitions
                .AnyAsync(c => c.Id == competitionId);

        if (!competitionExists)
            throw new InvalidOperationException(
                "Competition not found.");

        // Prevent duplicate Round of 16 draws.
        bool drawExists = await _context.KnockoutTies
            .AnyAsync(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.RoundOf16);

        if (drawExists)
            throw new InvalidOperationException(
                "Round of 16 draw already exists.");

        // Load completed play-off ties.
        var playoffTies = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Playoff)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        if (playoffTies.Count != 8 ||
            playoffTies.Any(t =>
                !t.IsCompleted ||
                !t.WinnerTeamId.HasValue))
        {
            throw new InvalidOperationException(
                "All eight play-off ties must be " +
                "completed before the Round of 16 draw.");
        }

        var playoffWinnerIds = playoffTies
            .Select(t => t.WinnerTeamId!.Value)
            .ToList();

        if (playoffWinnerIds.Distinct().Count() != 8)
            throw new InvalidOperationException(
                "Play-off winners must be eight unique teams.");

        // Retrieve the final league standings.
        var standings =
            await _tableService.GenerateTable(competitionId);

        if (standings.Count != 36 ||
            standings.Any(s => s.Played != 8) ||
            standings.Select(s => s.Position).Distinct().Count() != 36 ||
            standings.Select(s => s.TeamId).Distinct().Count() != 36)
        {
            throw new InvalidOperationException(
                "Invalid final league-phase standings.");
        }

        // Top eight qualify directly.
        var directQualifiers = standings
            .Where(s => s.Position >= 1 && s.Position <= 8)
            .OrderBy(s => s.Position)
            .ToList();

        if (directQualifiers.Count != 8)
            throw new InvalidOperationException(
                "Expected eight direct qualifiers.");

        var directIds = directQualifiers
            .Select(s => s.TeamId)
            .ToHashSet();

        if (playoffWinnerIds.Any(directIds.Contains))
            throw new InvalidOperationException(
                "A team cannot qualify both directly " +
                "and through the play-offs.");

        // Validate that every play-off winner originally
        // finished between positions 9 and 24.
        var standingsByTeam = standings
            .ToDictionary(s => s.TeamId);

        foreach (var winnerId in playoffWinnerIds)
        {
            if (!standingsByTeam.TryGetValue(
                    winnerId, out var standing) ||
                standing.Position < 9 ||
                standing.Position > 24)
            {
                throw new InvalidOperationException(
                    "A play-off winner has an invalid " +
                    "league-phase position.");
            }
        }

        // UEFA-inspired bracket structure:
        //
        // Direct seeds 1-2 face winners from
        // play-off bracket 15-16 vs 17-18.
        //
        // Direct seeds 3-4 face winners from
        // play-off bracket 13-14 vs 19-20.
        //
        // Direct seeds 5-6 face winners from
        // play-off bracket 11-12 vs 21-22.
        //
        // Direct seeds 7-8 face winners from
        // play-off bracket 9-10 vs 23-24.
        //
        // The play-off draw created two ties
        // for each of those four brackets.

        var roundOf16Brackets = new[]
        {
            (SeedStart: 1, PlayoffTieStart: 7),
            (SeedStart: 3, PlayoffTieStart: 5),
            (SeedStart: 5, PlayoffTieStart: 3),
            (SeedStart: 7, PlayoffTieStart: 1)
        };

        var roundOf16Ties = new List<KnockoutTie>();
        int tieNumber = 1;

        foreach (var bracket in roundOf16Brackets)
        {
            var seeds = directQualifiers
                .Where(s =>
                    s.Position >= bracket.SeedStart &&
                    s.Position <= bracket.SeedStart + 1)
                .OrderBy(s => s.Position)
                .ToList();

            var eligiblePlayoffTies = playoffTies
                .Where(t =>
                    t.TieNumber >= bracket.PlayoffTieStart &&
                    t.TieNumber <= bracket.PlayoffTieStart + 1)
                .OrderBy(t => t.TieNumber)
                .ToList();

            if (seeds.Count != 2 ||
                eligiblePlayoffTies.Count != 2)
            {
                throw new InvalidOperationException(
                    "Invalid Round of 16 pairing bracket.");
            }

            var opponents = eligiblePlayoffTies
                .Select(t => t.WinnerTeamId!.Value)
                .ToList();

            // Randomly assign the two eligible winners
            // to the two direct qualifiers.
            if (Random.Shared.Next(2) == 1)
                opponents.Reverse();

            for (int i = 0; i < 2; i++)
            {
                var seededTeamId = seeds[i].TeamId;
                var opponentTeamId = opponents[i];

                var tie = CreateTwoLeggedTie(
                    competitionId,
                    KnockoutRound.RoundOf16,
                    tieNumber++,
                    seededTeamId,
                    opponentTeamId);

                roundOf16Ties.Add(tie);
            }
        }

        if (roundOf16Ties.Count != 8 ||
            roundOf16Ties
                .SelectMany(t => new[]
                {
                    t.HomeTeamId,
                    t.AwayTeamId
                })
                .Distinct()
                .Count() != 16)
        {
            throw new InvalidOperationException(
                "Round of 16 must contain " +
                "eight ties and 16 unique clubs.");
        }

        _context.KnockoutTies.AddRange(roundOf16Ties);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return roundOf16Ties;
    }

    // ==========================================
    // SHARED TWO-LEGGED FIXTURE GENERATOR
    // ==========================================

    private static KnockoutTie CreateTwoLeggedTie(
        Guid competitionId,
        KnockoutRound round,
        int tieNumber,
        Guid seededTeamId,
        Guid unseededTeamId)
    {
        var tie = new KnockoutTie
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            Round = round,
            TieNumber = tieNumber,

            // Seeded team hosts the second leg.
            HomeTeamId = seededTeamId,
            AwayTeamId = unseededTeamId,

            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        // First leg: unseeded team hosts.
        tie.Matches.Add(new KnockoutMatch
        {
            Id = Guid.NewGuid(),
            Leg = 1,
            HomeTeamId = unseededTeamId,
            AwayTeamId = seededTeamId,
            IsPlayed = false
        });

        // Second leg: seeded team hosts.
        tie.Matches.Add(new KnockoutMatch
        {
            Id = Guid.NewGuid(),
            Leg = 2,
            HomeTeamId = seededTeamId,
            AwayTeamId = unseededTeamId,
            IsPlayed = false
        });

        return tie;
    }

    public async Task<List<KnockoutTie>> GenerateQuarterFinalDrawAsync(
        Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException(
                "Invalid competition ID.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

        // 1. Verify competition exists.
        bool competitionExists = await _context.Competitions
            .AnyAsync(c => c.Id == competitionId);

        if (!competitionExists)
            throw new ArgumentException(
                "Competition not found.");

        // 2. Prevent duplicate quarter-final draws.
        bool drawExists = await _context.KnockoutTies
            .AnyAsync(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.QuarterFinal);

        if (drawExists)
            throw new InvalidOperationException(
                "Quarter-final draw has already been generated.");

        // 3. Load all Round of 16 ties.
        var roundOf16Ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.RoundOf16)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        if (roundOf16Ties.Count != 8)
            throw new InvalidOperationException(
                "Exactly eight Round of 16 ties are required.");

        // 4. Ensure every Round of 16 tie is complete.
        if (roundOf16Ties.Any(t =>
            !t.IsCompleted ||
            !t.WinnerTeamId.HasValue))
        {
            throw new InvalidOperationException(
                "Complete all Round of 16 matches " +
                "before generating the quarter-finals.");
        }

        // 5. Validate tie numbering.
        var expectedTieNumbers =
            Enumerable.Range(1, 8).ToArray();

        var actualTieNumbers = roundOf16Ties
            .Select(t => t.TieNumber)
            .ToArray();

        if (!actualTieNumbers.SequenceEqual(expectedTieNumbers))
            throw new InvalidOperationException(
                "Invalid Round of 16 bracket numbering.");

        // 6. Ensure eight unique qualified teams.
        var winnerIds = roundOf16Ties
            .Select(t => t.WinnerTeamId!.Value)
            .ToList();

        if (winnerIds.Distinct().Count() != 8)
            throw new InvalidOperationException(
                "Round of 16 winners must be unique.");

        // 7. Generate fixed quarter-final bracket.
        // R16 Tie 1 winner vs R16 Tie 2 winner
        // R16 Tie 3 winner vs R16 Tie 4 winner
        // R16 Tie 5 winner vs R16 Tie 6 winner
        // R16 Tie 7 winner vs R16 Tie 8 winner

        var quarterFinalTies = new List<KnockoutTie>();

        for (int i = 0; i < 4; i++)
        {
            var firstWinner = winnerIds[i * 2];
            var secondWinner = winnerIds[i * 2 + 1];

            // Randomize home advantage once.
            bool swap = Random.Shared.Next(2) == 0;

            var firstTeamId = swap
                ? secondWinner
                : firstWinner;

            var secondTeamId = swap
                ? firstWinner
                : secondWinner;

            var tie = CreateTwoLeggedTie(
                competitionId,
                KnockoutRound.QuarterFinal,
                i + 1,
                firstTeamId,
                secondTeamId);

            quarterFinalTies.Add(tie);
        }

        // 8. Save all four ties and their eight matches.
        _context.KnockoutTies.AddRange(quarterFinalTies);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return quarterFinalTies;
    }

}
