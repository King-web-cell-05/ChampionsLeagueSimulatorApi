
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class DrawService
{
    private const int RequiredTeams = 36;
    private const int PotCount = 4;
    private const int TeamsPerPot = 9;
    private const int MatchdayCount = 8;
    private const int MaxSearchNodes = 500_000;
    private const int MaxAttempts = 12;

    private readonly AppDbContext _context;

    public DrawService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Match>> GenerateLeagueFixtures(Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException("Invalid competition ID.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var competitionExists = await _context.Competitions
            .AnyAsync(c => c.Id == competitionId);

        if (!competitionExists)
            throw new KeyNotFoundException("Competition not found.");

        var hasExistingMatches = await _context.Matches
            .AnyAsync(m => m.CompetitionId == competitionId);

        if (hasExistingMatches)
        {
            throw new InvalidOperationException(
                "This competition already has fixtures. " +
                "Existing fixtures and results have not been changed.");
        }

        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .Distinct()
            .ToListAsync();

        if (teams.Count != RequiredTeams)
        {
            throw new InvalidOperationException(
                $"The league phase requires exactly {RequiredTeams} clubs. " +
                $"Found {teams.Count}.");
        }

        if (teams.Select(t => t.Id).Distinct().Count() != RequiredTeams)
        {
            throw new InvalidOperationException(
                "Duplicate club registrations were detected.");
        }

        if (teams.Any(t => string.IsNullOrWhiteSpace(t.Country)))
        {
            throw new InvalidOperationException(
                "Every club must have a country assigned before the draw.");
        }

        // Assign the nine highest-coefficient clubs to Pot 1,
        // the next nine to Pot 2, and so on.
        var orderedTeams = teams
            .OrderByDescending(t => t.Coefficient)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var potByTeamId = new Dictionary<Guid, int>();

        for (var i = 0; i < orderedTeams.Count; i++)
            potByTeamId[orderedTeams[i].Id] = i / TeamsPerPot;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var search = new DrawSearch(
                competitionId,
                orderedTeams,
                potByTeamId,
                MaxSearchNodes);

            if (!search.TryGenerate(out var fixtures))
                continue;

            // Final validation happens before any database writes.
            ValidateDraw(fixtures, orderedTeams, potByTeamId);

            _context.Matches.AddRange(fixtures);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return fixtures;
        }

        throw new InvalidOperationException(
            "A valid draw could not be constructed within the search limits. " +
            "No fixtures were saved. Check club countries and try again.");
    }

    // Retained for compatibility with existing callers.
    public Task<List<Match>> GenerateDoubleRoundRobin(Guid competitionId)
    {
        return GenerateLeagueFixtures(competitionId);
    }

    private static void ValidateDraw(
        List<Match> fixtures,
        List<Team> teams,
        Dictionary<Guid, int> potByTeamId)
    {
        if (fixtures.Count != 144)
            throw new InvalidOperationException(
                "Draw validation failed: expected 144 fixtures.");

        var teamIds = teams.Select(t => t.Id).ToHashSet();

        foreach (var team in teams)
        {
            var teamMatches = fixtures
                .Where(m =>
                    m.HomeTeamId == team.Id ||
                    m.AwayTeamId == team.Id)
                .ToList();

            if (teamMatches.Count != 8)
                throw new InvalidOperationException(
                    $"Draw validation failed for {team.Name}: expected eight matches.");

            if (teamMatches.Count(m => m.HomeTeamId == team.Id) != 4 ||
                teamMatches.Count(m => m.AwayTeamId == team.Id) != 4)
            {
                throw new InvalidOperationException(
                    $"Draw validation failed for {team.Name}: expected four home and four away matches.");
            }

            var opponents = teamMatches
                .Select(m => m.HomeTeamId == team.Id
                    ? m.AwayTeamId
                    : m.HomeTeamId)
                .ToList();

            if (opponents.Distinct().Count() != 8)
                throw new InvalidOperationException(
                    $"Draw validation failed for {team.Name}: duplicate opponents detected.");

            foreach (var pot in Enumerable.Range(0, PotCount))
            {
                var matchesAgainstPot = teamMatches
                    .Where(m =>
                    {
                        var opponentId = m.HomeTeamId == team.Id
                            ? m.AwayTeamId
                            : m.HomeTeamId;

                        return potByTeamId[opponentId] == pot;
                    })
                    .ToList();

                if (matchesAgainstPot.Count != 2 ||
                    matchesAgainstPot.Count(m => m.HomeTeamId == team.Id) != 1 ||
                    matchesAgainstPot.Count(m => m.AwayTeamId == team.Id) != 1)
                {
                    throw new InvalidOperationException(
                        $"Draw validation failed for {team.Name}: " +
                        $"expected one home and one away opponent from Pot {pot + 1}.");
                }
            }
        }

        for (var matchday = 1; matchday <= MatchdayCount; matchday++)
        {
            var round = fixtures
                .Where(m => m.MatchDay == matchday)
                .ToList();

            if (round.Count != 18)
                throw new InvalidOperationException(
                    $"Draw validation failed: Matchday {matchday} must contain 18 fixtures.");

            var participants = round
                .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                .ToList();

            if (participants.Count != 36 ||
                participants.Distinct().Count() != 36 ||
                participants.Any(id => !teamIds.Contains(id)))
            {
                throw new InvalidOperationException(
                    $"Draw validation failed: Matchday {matchday} must contain every club exactly once.");
            }
        }

        var countryByTeamId = teams.ToDictionary(
            t => t.Id,
            t => t.Country.Trim());

        foreach (var match in fixtures)
        {
            if (match.HomeTeamId == match.AwayTeamId)
                throw new InvalidOperationException(
                    "Draw validation failed: a club cannot play itself.");

            if (string.Equals(
                countryByTeamId[match.HomeTeamId],
                countryByTeamId[match.AwayTeamId],
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Draw validation failed: clubs from the same country cannot play each other.");
            }
        }

        // No more than two opponents from any other country.
        foreach (var team in teams)
        {
            var opponentsByCountry = fixtures
                .Where(m =>
                    m.HomeTeamId == team.Id ||
                    m.AwayTeamId == team.Id)
                .Select(m =>
                {
                    var opponentId = m.HomeTeamId == team.Id
                        ? m.AwayTeamId
                        : m.HomeTeamId;

                    return countryByTeamId[opponentId];
                })
                .GroupBy(country => country, StringComparer.OrdinalIgnoreCase);

            if (opponentsByCountry.Any(group => group.Count() > 2))
            {
                throw new InvalidOperationException(
                    $"Draw validation failed: association limit exceeded for {team.Name}.");
            }
        }
    }

    private sealed class DrawSearch
    {
        private readonly Guid _competitionId;
        private readonly List<Team> _teams;
        private readonly Dictionary<Guid, int> _potByTeamId;
        private readonly Dictionary<Guid, string> _countryByTeamId;
        private readonly Dictionary<Guid, Team> _teamById;
        private readonly HashSet<Guid> _teamIds;
        private readonly int _maxNodes;

        private readonly Dictionary<Guid, HashSet<Guid>> _opponents = new();
        private readonly Dictionary<Guid, int[]> _potMatchCounts = new();
        private readonly Dictionary<Guid, int[]> _homeByPot = new();
        private readonly Dictionary<Guid, int[]> _awayByPot = new();

        private readonly Dictionary<Guid, Dictionary<string, int>>
            _countryCounts = new();

        private readonly List<Match> _fixtures = new();

        private int _nodes;

        public DrawSearch(
            Guid competitionId,
            List<Team> teams,
            Dictionary<Guid, int> potByTeamId,
            int maxNodes)
        {
            _competitionId = competitionId;
            _teams = teams;
            _potByTeamId = potByTeamId;
            _maxNodes = maxNodes;

            _teamIds = teams.Select(t => t.Id).ToHashSet();

            _teamById = teams.ToDictionary(t => t.Id);
            _countryByTeamId = teams.ToDictionary(
                t => t.Id,
                t => t.Country.Trim());

            foreach (var team in teams)
            {
                _opponents[team.Id] = new HashSet<Guid>();
                _potMatchCounts[team.Id] = new int[PotCount];
                _homeByPot[team.Id] = new int[PotCount];
                _awayByPot[team.Id] = new int[PotCount];

                _countryCounts[team.Id] =
                    new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase);
            }
        }

        public bool TryGenerate(out List<Match> fixtures)
        {
            fixtures = new List<Match>();

            var firstMatchdayTeams = _teamIds.ToHashSet();

            if (!BuildRound(1, firstMatchdayTeams))
                return false;

            if (!ValidateInternalState())
                return false;

            fixtures = _fixtures.ToList();
            return true;
        }

        private bool BuildRound(int matchday, HashSet<Guid> remaining)
        {
            if (++_nodes > _maxNodes)
                return false;

            if (remaining.Count == 0)
            {
                if (matchday == MatchdayCount)
                    return _fixtures.Count == 144;

                return BuildRound(matchday + 1, _teamIds.ToHashSet());
            }

            // Select the team with the fewest legal fixture options.
            Guid selectedTeamId = Guid.Empty;
            List<FixtureOption>? selectedOptions = null;

            foreach (var teamId in remaining)
            {
                var options = GetOptions(teamId, remaining);

                if (options.Count == 0)
                    return false;

                if (selectedOptions == null ||
                    options.Count < selectedOptions.Count)
                {
                    selectedTeamId = teamId;
                    selectedOptions = options;

                    if (options.Count == 1)
                        break;
                }
            }

            if (selectedOptions == null ||
                selectedTeamId == Guid.Empty)
                return false;

            var shuffledOptions = selectedOptions
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            foreach (var option in shuffledOptions)
            {
                if (!CanPlay(option.Home.Id, option.Away.Id))
                    continue;

                var match = new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = _competitionId,
                    HomeTeamId = option.Home.Id,
                    AwayTeamId = option.Away.Id,
                    MatchDay = matchday,
                    KickoffUtc = null,
                    HomeScore = null,
                    AwayScore = null,
                    IsPlayed = false
                };

                AddFixture(match);

                remaining.Remove(option.Home.Id);
                remaining.Remove(option.Away.Id);

                if (BuildRound(matchday, remaining))
                    return true;

                remaining.Add(option.Home.Id);
                remaining.Add(option.Away.Id);

                RemoveFixture(match);
            }

            return false;
        }

        private List<FixtureOption> GetOptions(
            Guid teamId,
            HashSet<Guid> remaining)
        {
            var options = new List<FixtureOption>();

            foreach (var opponentId in remaining)
            {
                if (opponentId == teamId)
                    continue;

                var team = _teamById[teamId];
                var opponent = _teamById[opponentId];

                if (CanPlay(teamId, opponentId))
                    options.Add(new FixtureOption(team, opponent));

                if (CanPlay(opponentId, teamId))
                    options.Add(new FixtureOption(opponent, team));
            }

            return options;
        }

        private bool CanPlay(Guid homeId, Guid awayId)
        {
            if (homeId == awayId)
                return false;

            var homeCountry = _countryByTeamId[homeId];
            var awayCountry = _countryByTeamId[awayId];

            // Clubs from the same national association cannot meet.
            if (string.Equals(
                homeCountry,
                awayCountry,
                StringComparison.OrdinalIgnoreCase))
                return false;

            if (_opponents[homeId].Contains(awayId))
                return false;

            var awayPot = _potByTeamId[awayId];
            var homePot = _potByTeamId[homeId];

            // Each club must play exactly two opponents from each pot.
            if (_potMatchCounts[homeId][awayPot] >= 2 ||
                _potMatchCounts[awayId][homePot] >= 2)
                return false;

            // One home and one away opponent from each pot.
            if (_homeByPot[homeId][awayPot] >= 1 ||
                _awayByPot[awayId][homePot] >= 1)
                return false;

            // No more than two opponents from the same other country.
            if (_countryCounts[homeId].GetValueOrDefault(awayCountry) >= 2 ||
                _countryCounts[awayId].GetValueOrDefault(homeCountry) >= 2)
                return false;

            return true;
        }

        private void AddFixture(Match match)
        {
            var homeId = match.HomeTeamId;
            var awayId = match.AwayTeamId;

            var homeCountry = _countryByTeamId[homeId];
            var awayCountry = _countryByTeamId[awayId];

            var awayPot = _potByTeamId[awayId];
            var homePot = _potByTeamId[homeId];

            _opponents[homeId].Add(awayId);
            _opponents[awayId].Add(homeId);

            _potMatchCounts[homeId][awayPot]++;
            _potMatchCounts[awayId][homePot]++;

            _homeByPot[homeId][awayPot]++;
            _awayByPot[awayId][homePot]++;

            IncrementCountry(homeId, awayCountry);
            IncrementCountry(awayId, homeCountry);

            _fixtures.Add(match);
        }

        private void RemoveFixture(Match match)
        {
            var homeId = match.HomeTeamId;
            var awayId = match.AwayTeamId;

            var homeCountry = _countryByTeamId[homeId];
            var awayCountry = _countryByTeamId[awayId];

            var awayPot = _potByTeamId[awayId];
            var homePot = _potByTeamId[homeId];

            _opponents[homeId].Remove(awayId);
            _opponents[awayId].Remove(homeId);

            _potMatchCounts[homeId][awayPot]--;
            _potMatchCounts[awayId][homePot]--;

            _homeByPot[homeId][awayPot]--;
            _awayByPot[awayId][homePot]--;

            DecrementCountry(homeId, awayCountry);
            DecrementCountry(awayId, homeCountry);

            _fixtures.Remove(match);
        }

        private void IncrementCountry(Guid teamId, string country)
        {
            var counts = _countryCounts[teamId];
            counts[country] = counts.GetValueOrDefault(country) + 1;
        }

        private void DecrementCountry(Guid teamId, string country)
        {
            var counts = _countryCounts[teamId];
            var count = counts[country] - 1;

            if (count == 0)
                counts.Remove(country);
            else
                counts[country] = count;
        }

        private bool ValidateInternalState()
        {
            if (_fixtures.Count != 144)
                return false;

            foreach (var team in _teams)
            {
                if (_opponents[team.Id].Count != 8)
                    return false;

                for (var pot = 0; pot < PotCount; pot++)
                {
                    if (_potMatchCounts[team.Id][pot] != 2 ||
                        _homeByPot[team.Id][pot] != 1 ||
                        _awayByPot[team.Id][pot] != 1)
                        return false;
                }
            }

            return true;
        }

        private sealed record FixtureOption(Team Home, Team Away);
    }
}
