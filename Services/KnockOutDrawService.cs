
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

    public async Task<List<KnockoutTie>>
        GeneratePlayoffDraw(Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException(
                "Invalid competition ID.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        // Verify competition exists.
        bool competitionExists =
            await _context.Competitions
                .AnyAsync(c => c.Id == competitionId);

        if (!competitionExists)
            throw new InvalidOperationException(
                "Competition not found.");

        // Prevent generating the draw twice.
        bool drawExists = await _context.KnockoutTies
            .AnyAsync(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Playoff);

        if (drawExists)
            throw new InvalidOperationException(
                "Play-off draw already exists.");

        // Verify all league fixtures are completed.
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

        // Get the final league standings.
        var standings =
            await _tableService.GenerateTable(competitionId);

        if (standings.Count != 36 ||
            standings.Any(s => s.Played != 8))
        {
            throw new InvalidOperationException(
                "Expected 36 clubs with 8 matches each.");
        }

        // UEFA-style pairing brackets.
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

            // Randomly choose which opponent faces
            // the first seeded club.
            if (Random.Shared.Next(2) == 1)
                opponents.Reverse();

            for (int i = 0; i < 2; i++)
            {
                var seeded = seeds[i];
                var unseeded = opponents[i];

                var tie = new KnockoutTie
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    Round = KnockoutRound.Playoff,
                    TieNumber = tieNumber++,

                    // Tie-level home/away represents
                    // seeded and unseeded club.
                    HomeTeamId = seeded.TeamId,
                    AwayTeamId = unseeded.TeamId,

                    IsCompleted = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                // First leg: unseeded club at home.
                tie.Matches.Add(new KnockoutMatch
                {
                    Id = Guid.NewGuid(),
                    Leg = 1,
                    HomeTeamId = unseeded.TeamId,
                    AwayTeamId = seeded.TeamId,
                    IsPlayed = false
                });

                // Second leg: seeded club at home.
                tie.Matches.Add(new KnockoutMatch
                {
                    Id = Guid.NewGuid(),
                    Leg = 2,
                    HomeTeamId = seeded.TeamId,
                    AwayTeamId = unseeded.TeamId,
                    IsPlayed = false
                });

                ties.Add(tie);
            }
        }

        _context.KnockoutTies.AddRange(ties);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ties;
    }
}
