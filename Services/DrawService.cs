
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Google.OrTools.Sat;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ChampionsLeagueSimulatorAPI.Services;

public class DrawService
{
    private const int RequiredTeams = 36;
    private const int PotCount = 4;
    private const int TeamsPerPot = 9;
    private const int MatchdayCount = 8;
    private const int RequiredFixtures = 144;

    private readonly AppDbContext _context;

    public DrawService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Match>> GenerateLeagueFixtures(
        Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException("Invalid competition ID.");

        var competitionExists = await _context.Competitions
            .AnyAsync(c => c.Id == competitionId);

        if (!competitionExists)
            throw new KeyNotFoundException("Competition not found.");

        if (await _context.Matches.AnyAsync(
            m => m.CompetitionId == competitionId))
        {
            throw new InvalidOperationException(
                "This competition already has fixtures. " +
                "Existing matches have not been changed.");
        }

        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .AsNoTracking()
            .ToListAsync();

        if (teams.Count != RequiredTeams ||
            teams.Select(t => t.Id).Distinct().Count() != RequiredTeams)
        {
            throw new InvalidOperationException(
                "Exactly 36 unique clubs must be registered.");
        }

        if (teams.Any(t =>
            string.IsNullOrWhiteSpace(t.Name) ||
            string.IsNullOrWhiteSpace(t.Country)))
        {
            throw new InvalidOperationException(
                "Every club must have a name and country.");
        }

        // Assign coefficient-based pots.
        teams = teams
            .OrderByDescending(t => t.Coefficient)
            .ThenBy(t => t.Name)
            .ToList();

        var pots = Enumerable.Range(0, RequiredTeams)
            .Select(i => i / TeamsPerPot)
            .ToArray();

        var countries = teams
            .Select(t => t.Country.Trim().ToUpperInvariant())
            .ToArray();

        var model = new CpModel();

        // Key: (matchday, home team index, away team index)
        var variables = new Dictionary<
            (int Day, int Home, int Away), BoolVar>();

        for (int day = 0; day < MatchdayCount; day++)
        {
            for (int home = 0; home < RequiredTeams; home++)
            {
                for (int away = 0; away < RequiredTeams; away++)
                {
                    if (home == away)
                        continue;

                    // No same-country opponents.
                    if (countries[home] == countries[away])
                        continue;

                    variables[(day, home, away)] =
                        model.NewBoolVar($"m_{day}_{home}_{away}");
                }
            }
        }

        // Each club plays exactly once per matchday.
        for (int day = 0; day < MatchdayCount; day++)
        {
            for (int team = 0; team < RequiredTeams; team++)
            {
                var participation = variables
                    .Where(v =>
                        v.Key.Day == day &&
                        (v.Key.Home == team ||
                         v.Key.Away == team))
                    .Select(v => v.Value)
                    .ToArray();

                model.Add(LinearExpr.Sum(participation) == 1);
            }
        }

        // Each pair of clubs may meet at most once.
        for (int a = 0; a < RequiredTeams; a++)
        {
            for (int b = a + 1; b < RequiredTeams; b++)
            {
                var pairMatches = variables
                    .Where(v =>
                        (v.Key.Home == a && v.Key.Away == b) ||
                        (v.Key.Home == b && v.Key.Away == a))
                    .Select(v => v.Value)
                    .ToArray();

                model.Add(LinearExpr.Sum(pairMatches) <= 1);
            }
        }

        // One home and one away opponent from each pot.
        for (int team = 0; team < RequiredTeams; team++)
        {
            for (int pot = 0; pot < PotCount; pot++)
            {
                int currentTeam = team;
                int currentPot = pot;

                var homeGames = variables
                    .Where(v =>
                        v.Key.Home == currentTeam &&
                        pots[v.Key.Away] == currentPot)
                    .Select(v => v.Value)
                    .ToArray();

                var awayGames = variables
                    .Where(v =>
                        v.Key.Away == currentTeam &&
                        pots[v.Key.Home] == currentPot)
                    .Select(v => v.Value)
                    .ToArray();

                model.Add(LinearExpr.Sum(homeGames) == 1);
                model.Add(LinearExpr.Sum(awayGames) == 1);
            }
        }

        // No more than two opponents from any other country.
        var distinctCountries = countries.Distinct().ToArray();

        for (int team = 0; team < RequiredTeams; team++)
        {
            foreach (var country in distinctCountries)
            {
                if (country == countries[team])
                    continue;

                int currentTeam = team;
                string currentCountry = country;

                var games = variables
                    .Where(v =>
                        (v.Key.Home == currentTeam &&
                         countries[v.Key.Away] == currentCountry) ||
                        (v.Key.Away == currentTeam &&
                         countries[v.Key.Home] == currentCountry))
                    .Select(v => v.Value)
                    .ToArray();

                model.Add(LinearExpr.Sum(games) <= 2);
            }
        }

        var solver = new CpSolver
        {
            StringParameters =
                "max_time_in_seconds:60 " +
                "num_search_workers:8 " +
                $"random_seed:{Random.Shared.Next(1, 1000000)} " +
                "randomize_search:true"
        };

        // A time limit prevents Swagger from waiting indefinitely.
        var status = solver.Solve(model);

        if (status != CpSolverStatus.Optimal &&
            status != CpSolverStatus.Feasible)
        {
            throw new InvalidOperationException(
                $"The random draw did not complete. Solver status: {status}. " +
                "No matches were saved. Try again or review the club restrictions.");
        }

        var fixtures = new List<Match>();

        foreach (var entry in variables)
        {
            if (solver.Value(entry.Value) != 1)
                continue;

            fixtures.Add(new Match
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                HomeTeamId = teams[entry.Key.Home].Id,
                AwayTeamId = teams[entry.Key.Away].Id,
                MatchDay = entry.Key.Day + 1,
                KickoffUtc = null,
                HomeScore = null,
                AwayScore = null,
                IsPlayed = false
            });
        }

        ValidateDraw(fixtures, teams, pots, countries);

        // Begin a serializable transaction before checking again.
        // This protects against concurrent requests creating two draws.
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        if (await _context.Matches.AnyAsync(
            m => m.CompetitionId == competitionId))
        {
            throw new InvalidOperationException(
                "Fixtures were created by another request. " +
                "No additional matches were saved.");
        }

        _context.Matches.AddRange(fixtures);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return fixtures;
    }

    // Compatibility with existing callers.
    public Task<List<Match>> GenerateDoubleRoundRobin(
        Guid competitionId)
    {
        return GenerateLeagueFixtures(competitionId);
    }

    private static void ValidateDraw(
        List<Match> fixtures,
        List<Team> teams,
        int[] pots,
        string[] countries)
    {
        if (fixtures.Count != RequiredFixtures)
            throw new InvalidOperationException(
                "Invalid draw: expected 144 fixtures.");

        var teamIndex = teams
            .Select((team, index) => new { team.Id, index })
            .ToDictionary(x => x.Id, x => x.index);

        var uniquePairs = new HashSet<string>();

        foreach (var match in fixtures)
        {
            if (!teamIndex.TryGetValue(
                    match.HomeTeamId, out var home) ||
                !teamIndex.TryGetValue(
                    match.AwayTeamId, out var away))
            {
                throw new InvalidOperationException(
                    "Invalid draw: unknown club.");
            }

            if (home == away || countries[home] == countries[away])
                throw new InvalidOperationException(
                    "Invalid draw: prohibited opponent.");

            var pair = home < away
                ? $"{home}:{away}"
                : $"{away}:{home}";

            if (!uniquePairs.Add(pair))
                throw new InvalidOperationException(
                    "Invalid draw: repeated opponents.");
        }

        for (int day = 1; day <= MatchdayCount; day++)
        {
            var round = fixtures
                .Where(m => m.MatchDay == day)
                .ToList();

            var participants = round
                .SelectMany(m => new[]
                {
                    m.HomeTeamId,
                    m.AwayTeamId
                })
                .ToList();

            if (round.Count != 18 ||
                participants.Distinct().Count() != RequiredTeams)
            {
                throw new InvalidOperationException(
                    $"Invalid draw: matchday {day}.");
            }
        }

        for (int team = 0; team < RequiredTeams; team++)
        {
            var teamId = teams[team].Id;

            var games = fixtures
                .Where(m =>
                    m.HomeTeamId == teamId ||
                    m.AwayTeamId == teamId)
                .ToList();

            if (games.Count != 8 ||
                games.Count(m => m.HomeTeamId == teamId) != 4 ||
                games.Count(m => m.AwayTeamId == teamId) != 4)
            {
                throw new InvalidOperationException(
                    $"Invalid match balance for {teams[team].Name}.");
            }

            foreach (int pot in Enumerable.Range(0, PotCount))
            {
                int homeCount = games.Count(m =>
                    m.HomeTeamId == teamId &&
                    pots[teamIndex[m.AwayTeamId]] == pot);

                int awayCount = games.Count(m =>
                    m.AwayTeamId == teamId &&
                    pots[teamIndex[m.HomeTeamId]] == pot);

                if (homeCount != 1 || awayCount != 1)
                {
                    throw new InvalidOperationException(
                        $"Invalid pot allocation for {teams[team].Name}.");
                }
            }

            var opponentsByCountry = games
                .Select(m =>
                {
                    var opponentId = m.HomeTeamId == teamId
                        ? m.AwayTeamId
                        : m.HomeTeamId;

                    return countries[teamIndex[opponentId]];
                })
                .GroupBy(c => c);

            if (opponentsByCountry.Any(g => g.Count() > 2))
            {
                throw new InvalidOperationException(
                    $"Country limit exceeded for {teams[team].Name}.");
            }
        }
    }
}
