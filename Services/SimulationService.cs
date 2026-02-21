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
        var matches = await _context.Matches
            .Where(x => x.CompetitionId == competitionId && !x.IsPlayed)
            .ToListAsync();

        foreach (var match in matches)
        {
            match.HomeScore = GenerateGoals();

            match.AwayScore = GenerateGoals();

            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();
    }


    private int GenerateGoals()
    {
        return _random.Next(0, 5);
    }
}