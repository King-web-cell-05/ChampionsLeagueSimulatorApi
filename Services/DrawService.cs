using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class DrawService
{
    private readonly AppDbContext _context;
    private readonly Random _random = new();

    public DrawService(AppDbContext context)
    {
        _context = context;
    }

    public async Task GenerateDraw(Guid competitionId)
    {
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .Distinct()
            .ToListAsync();

        if (teams.Count < 2)
            throw new Exception("Not enough teams");

        var matches = new List<Match>();

        var shuffled = teams
            .OrderBy(x => _random.Next())
            .ToList();

        int matchDay = 1;

        for (int i = 0; i < shuffled.Count; i++)
        {
            for (int j = i + 1; j < shuffled.Count; j++)
            {
                if (shuffled[i].Id == shuffled[j].Id)
                    continue;

                matches.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    HomeTeamId = shuffled[i].Id,
                    AwayTeamId = shuffled[j].Id,
                    MatchDay = matchDay++,
                    IsPlayed = false
                });
            }
        }

        _context.Matches.AddRange(matches);

        await _context.SaveChangesAsync();
    }
}