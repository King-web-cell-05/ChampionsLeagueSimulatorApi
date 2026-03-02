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
            .ToListAsync();

        if (teams.Count < 2)
            throw new Exception("Not enough teams");

        // Add BYE if odd number of teams
        bool hasBye = false;
        if (teams.Count % 2 != 0)
        {
            teams.Add(new Team { Id = Guid.Empty, Name = "BYE" });
            hasBye = true;
        }

        int totalTeams = teams.Count;
        int totalRounds = totalTeams - 1;
        int matchesPerRound = totalTeams / 2;

        var matches = new List<Match>();
        var rotation = new List<Team>(teams);
        int matchDay = 1;

        // First leg
        for (int round = 0; round < totalRounds; round++)
        {
            for (int i = 0; i < matchesPerRound; i++)
            {
                var home = rotation[i];
                var away = rotation[totalTeams - 1 - i];

                if (home.Id == Guid.Empty || away.Id == Guid.Empty)
                    continue;

                matches.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    HomeTeamId = home.Id,
                    AwayTeamId = away.Id,
                    MatchDay = matchDay++,
                    IsPlayed = false
                });
            }

            // Rotate teams except the first
            var last = rotation.Last();
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }

        // Second leg (reverse home & away)
        rotation = new List<Team>(teams);
        for (int round = 0; round < totalRounds; round++)
        {
            for (int i = 0; i < matchesPerRound; i++)
            {
                var home = rotation[totalTeams - 1 - i];
                var away = rotation[i];

                if (home.Id == Guid.Empty || away.Id == Guid.Empty)
                    continue;

                matches.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    HomeTeamId = home.Id,
                    AwayTeamId = away.Id,
                    MatchDay = matchDay++,
                    IsPlayed = false
                });
            }

            // Rotate teams except the first
            var last = rotation.Last();
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }

        _context.Matches.AddRange(matches);
        await _context.SaveChangesAsync();

        return matches;
    }
}