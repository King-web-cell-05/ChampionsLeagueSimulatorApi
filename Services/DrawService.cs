
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class DrawService
{
    private const int RequiredTeams = 36;
    private const int PotSize = 9;
    private const int MatchdayCount = 8;
    private const int MaxSearchNodesPerAttempt = 250_000;
    private const int MaxAttempts = 8;

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

        var existingMatches = await _context.Matches
            .AnyAsync(m => m.CompetitionId == competitionId);

        if (existingMatches)
        {
            throw new InvalidOperationException(
                "This competition already has fixtures. Existing matches and results were not changed.");
        }

        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .Distinct()
            .ToListAsync();

        if (teams.Count != RequiredTeams)
        {
            throw new InvalidOperationException(
                $"The UEFA-style league phase requires exactly {RequiredTeams} registered clubs. Found {teams.Count}.");
        }

        if (teams.Any(t => string.IsNullOrWhiteSpace(t.Country)))
        {
            throw new InvalidOperationException(
                "Every registered club must have a country before the draw can be generated.");
        }

        // Pot 1 contains the highest coefficients, followed by Pots 2–4.
        var orderedTeams = teams
            .OrderByDescending(t => t.Coefficient)
            .ThenBy(t => t.Name)
            .ToList();

        var potIndex = new Dictionary<Guid, int>();

        for (var i = 0; i < orderedTeams.Count; i++)
            potIndex[orderedTeams[i].Id] = i / PotSize;

        var countryById = orderedTeams.ToDictionary(
            t => t.Id,
            t => t.Country.Trim());

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var search = new DrawSearch(
                orderedTeams,
                potIndex,
                countryById,
                MaxSearchNodesPerAttempt);

            if (!search.TryGenerate(out var fixtures))
                continue;

            if (fixtures.Count != 144)
                continue;

            // All validation takes place before any new fixture is saved.
            _context.Matches.AddRange(fixtures);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return fixtures;
        }

        throw new InvalidOperationException(
            "A valid draw could not be constructed within the search limits. " +
            "No fixtures were saved. Check the clubs' countries and coefficients, then try again.");
    }

    // Compatibility wrapper for any existing code calling the old method.
    public Task<List<Match>> GenerateDoubleRoundRobin(Guid competitionId)
    {
        return GenerateLeagueFixtures(competitionId);
    }

    private sealed class DrawSearch
    {
        private readonly List<Team> _teams;
        private readonly Dictionary<Guid, int> _potIndex;
        private readonly Dictionary<Guid, string> _countryById;
        private readonly HashSet<Guid> _teamIds;
        private readonly int _maxNodes;

        private readonly Dictionary<Guid, HashSet<Guid>> _opponents = new();
        private readonly Dictionary<Guid, int[]> _potMatches = new();
        private readonly Dictionary<Guid, int[]> _homeByPot = new();
        private readonly Dictionary<Guid, int[]> _awayByPot = new();

        private readonly Dictionary<Guid, Dictionary<string, int>>
            _countryCounts = new();

        private readonly List<Match> _fixtures = new();
        private int _nodes;

        public DrawSearch(
            List<Team> teams,
            Dictionary<Guid, int> potIndex,
            Dictionary<Guid, string> countryById,
            int maxNodes)
        {
            _teams = teams;
            _potIndex = potIndex;
            _countryById = countryById;
            _teamIds = teams.Select(t => t.Id).ToHashSet();
            _maxNodes = maxNodes;

            foreach (var team in teams)
            {
                _opponents[team.Id] = new HashSet<Guid>();
                _potMatches[team.Id] = new int[4];
                _homeByPot[team.Id] = new int[4];
                _awayByPot[team.Id] = new int[4];

                _countryCounts[team.Id] =
                    new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase);
            }
        }

        public bool TryGenerate(out List<Match> fixtures)
        {
            fixtures = new List<Match>();

            var remaining = _teamIds.ToHashSet();

            if (!BuildRound(1, remaining))
                return false;

            if (!ValidateCompletedDraw())
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
                    return ValidateCompletedDraw();

                return BuildRound(
                    matchday + 1,
                    _teamIds.ToHashSet());
            }

            // Minimum-remaining-values heuristic: choose the club with
            // the fewest currently legal fixtures.
            List<FixtureOption>? bestOptions = null;

            foreach (var teamId in remaining)
            {
                var options = GetOptions(teamId, remaining);

                if (options.Count == 0)
                    return false;

                if (bestOptions is null ||
                    options.Count < bestOptions.Count)
                {
                    bestOptions = options;

                    if (bestOptions.Count == 1)
                        break;
                }
            }

            if (bestOptions is null)
                return false;

            // Randomize equally valid alternatives.
            var shuffledOptions = bestOptions
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            foreach (var option in shuffledOptions)
            {
                if (!CanPlay(option.Home, option.Away))
                    continue;

                var match = new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = option.CompetitionId,
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
            var team = _teams.First(t => t.Id == teamId);

            foreach (var opponentId in remaining)
            {
                if (opponentId == teamId)
                    continue;

                var opponent = _teams.First(t => t.Id == opponentId);

                // Try this team at home.
                if (CanPlay(team, opponent))
                {
                    options.Add(new FixtureOption(
                        team,
                        opponent,
                        teamId == Guid.Empty
                            ? Guid.Empty
                            : _fixtures.Count >= 0
                                ? _currentCompetitionId
                                : Guid.Empty));
                }

                // Try this team away.
                if (CanPlay(opponent, team))
                {
                    options.Add(new FixtureOption(
                        opponent,
                        team,
                        _currentCompetitionId));
                }
            }

            return options;
        }

        private Guid _currentCompetitionId;

        private bool CanPlay(Team home, Team away)
        {
            var homeCountry = _countryById[home.Id];
            var awayCountry = _countryById[away.Id];

            // No clubs from the same national association.
            if (string.Equals(
                homeCountry,
                awayCountry,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_opponents[home.Id].Contains(away.Id))
                return false;

            var homePot = _potIndex[away.Id];
            var awayPot = _potIndex[home.Id];

            // At most two opponents from any one pot.
            if (_potMatches[home.Id][homePot] >= 2 ||
                _potMatches[away.Id][awayPot] >= 2)
            {
                return false;
            }

            // Exactly one home and one away match against each pot.
            if (_homeByPot[home.Id][homePot] >= 1 ||
                _awayByPot[away.Id][awayPot] >= 1)
            {
                return false;
            }

            // No more than two opponents from another association.
            if (_countryCounts[home.Id].GetValueOrDefault(awayCountry) >= 2 ||
                _countryCounts[away.Id].GetValueOrDefault(homeCountry) >= 2)
            {
                return false;
            }

            return true;
        }

        private void AddFixture(Match match)
        {
            var homeId = match.HomeTeamId;
            var awayId = match.AwayTeamId;

            var homeCountry = _countryById[homeId];
            var awayCountry = _countryById[awayId];

            var homePot = _potIndex[awayId];
            var awayPot = _potIndex[homeId];

            _opponents[homeId].Add(awayId);
            _opponents[awayId].Add(homeId);

            _potMatches[homeId][homePot]++;
            _potMatches[awayId][awayPot]++;

            _homeByPot[homeId][homePot]++;
            _awayByPot[awayId][awayPot]++;

            IncrementCountry(homeId, awayCountry);
            IncrementCountry(awayId, homeCountry);

            _fixtures.Add(match);
        }

        private void RemoveFixture(Match match)
        {
            var homeId = match.HomeTeamId;
            var awayId = match.AwayTeamId;

            var homeCountry = _countryById[homeId];
            var awayCountry = _countryById[awayId];

            var homePot = _potIndex[awayId];
            var awayPot = _potIndex[homeId];

            _opponents[homeId].Remove(awayId);
            _opponents[awayId].Remove(homeId);

            _potMatches[homeId][homePot]--;
            _potMatches[awayId][awayPot]--;

            _homeByPot[homeId][homePot]--;
            _awayByPot[awayId][awayPot]--;

            DecrementCountry(homeId, awayCountry);
            DecrementCountry(awayId, homeCountry);

            _fixtures.RemoveAt(_fixtures.Count - 1);
        }

        private void IncrementCountry(Guid teamId, string country)
        {
            var counts = _countryCounts[teamId];
            counts[country] = counts.GetValueOrDefault(country) + 1;
        }

        private void DecrementCountry(Guid teamId, string country)
        {
            var counts = _countryCounts[teamId];
            var value = counts[country] - 1;

            if (value == 0)
                counts.Remove(country);
            else
                counts[country] = value;
        }

        private bool ValidateCompletedDraw()
        {
            if (_fixtures.Count != 144)
                return false;

            foreach (var team in _teams)
            {
                if (_opponents[team.Id].Count != 8)
                    return false;

                for (var pot = 0; pot < 4; pot++)
                {
                    if (_potMatches[team.Id][pot] != 2 ||
                        _homeByPot[team.Id][pot] != 1 ||
                        _awayByPot[team.Id][pot] != 1)
                    {
                        return false;
                    }
                }
            }

            for (var day = 1; day <= MatchdayCount; day++)
            {
                var round = _fixtures
                    .Where(m => m.MatchDay == day)
                    .ToList();

                if (round.Count != 18)
                    return false;

                var participants = round
                    .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                    .ToList();

                if (participants.Count != 36 ||
                    participants.Distinct().Count() != 36)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed record FixtureOption(
            Team Home,
            Team Away,
            Guid CompetitionId);
    }
}
