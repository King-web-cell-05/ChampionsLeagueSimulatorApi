using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class DrawService
{
    private readonly AppDbContext _context;

    public DrawService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Match>> GenerateDoubleRoundRobin(Guid competitionId)
    {
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Include(ct => ct.Team)
            .Select(ct => ct.Team)
            .Distinct()
            .ToListAsync();

        if (teams.Count < 2)
            throw new Exception("Not enough teams");

        // Add BYE if odd number of teams
        if (teams.Count % 2 != 0)
        {
            teams.Add(new Team { Id = Guid.Empty, Name = "BYE" });
        }

        int totalTeams = teams.Count;
        int totalRounds = totalTeams - 1;
        int matchesPerRound = totalTeams / 2;

        var rotation = new List<Team>(teams);

        var firstLegMatches = new List<Match>();
        var allMatches = new List<Match>();

        int matchDay = 1;

        // FIRST LEG
        for (int round = 0; round < totalRounds; round++)
        {
            for (int i = 0; i < matchesPerRound; i++)
            {
                var home = rotation[i];
                var away = rotation[totalTeams - 1 - i];

                if (home.Id == Guid.Empty || away.Id == Guid.Empty)
                    continue;

                var match = new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    HomeTeamId = home.Id,
                    AwayTeamId = away.Id,
                    MatchDay = matchDay,
                    IsPlayed = false
                };

                firstLegMatches.Add(match);
                allMatches.Add(match);
            }

            matchDay++;

            // rotate teams except first
            var last = rotation.Last();
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }

        // SECOND LEG (reverse fixtures)
        foreach (var match in firstLegMatches)
        {
            allMatches.Add(new Match
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                HomeTeamId = match.AwayTeamId,
                AwayTeamId = match.HomeTeamId,
                MatchDay = matchDay++,
                IsPlayed = false
            });
        }

        _context.Matches.AddRange(allMatches);
        await _context.SaveChangesAsync();

        return allMatches;
    }
}