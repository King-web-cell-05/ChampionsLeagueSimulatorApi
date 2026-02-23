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

    public async Task GenerateDraw(Guid competitionId)
    {
        // Load teams with their names
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Include(ct => ct.Team)
            .Select(ct => ct.Team)
            .ToListAsync();

        if (teams.Count < 2)
            throw new Exception("Not enough teams");

        var matches = new List<Match>();

        // Round-robin style scheduling
        int matchDay = 1;
        for (int i = 0; i < teams.Count; i++)
        {
            for (int j = i + 1; j < teams.Count; j++)
            {
                matches.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    HomeTeamId = teams[i].Id,
                    AwayTeamId = teams[j].Id,
                    MatchDay = matchDay++,
                    IsPlayed = false
                });
            }
        }

        await _context.Matches.AddRangeAsync(matches);
        await _context.SaveChangesAsync(); // <-- use await
    }
}