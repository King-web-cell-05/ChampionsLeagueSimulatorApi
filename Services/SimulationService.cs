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

    public async Task SimulateCompetition(Guid competitionId)
    {
        // Get all unplayed matches for this competition, include teams
        var matches = await _context.Matches
            .Where(x => x.CompetitionId == competitionId && !x.IsPlayed)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .OrderBy(x => x.MatchDay) // simulate by matchday order
            .ToListAsync();

        foreach (var match in matches)
        {
            // Skip BYE matches
            if (match.HomeTeamId == Guid.Empty || match.AwayTeamId == Guid.Empty)
            {
                match.IsPlayed = true;
                match.HomeScore = 0;
                match.AwayScore = 0;
                continue;
            }

            match.HomeScore = GenerateGoals();
            match.AwayScore = GenerateGoals();
            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();
    }

    private int GenerateGoals()
    {
        return _random.Next(0, 5); // 0-4 goals per team
    }
}