using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class SimulationService
{
    private readonly AppDbContext _context;
    private readonly Random _random = new();

    public SimulationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Match>> SimulateCompetition(Guid competitionId)
    {
        // Load ALL matches for this competition
        var matches = await _context.Matches
            .Where(x => x.CompetitionId == competitionId)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .Include(x => x.Competition)
            .OrderBy(x => x.MatchDay)
            .ToListAsync();

        foreach (var match in matches.Where(m => !m.IsPlayed))
        {
            match.HomeScore = GenerateGoals();
            match.AwayScore = GenerateGoals();
            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();

        return matches;
    }
    private int GenerateGoals()
    {
        return _random.Next(0, 5); // 0-4 goals per team
    }
}