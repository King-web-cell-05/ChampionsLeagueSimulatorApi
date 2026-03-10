using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class SimulationService
{
    private readonly AppDbContext _context;
    private readonly TableService _tableService;
    private readonly Random _random = new();

    public SimulationService(AppDbContext context, TableService tableService)
    {
        _context = context;
        _tableService = tableService;
    }

    public async Task<object> SimulateCompetition(Guid competitionId)
    {
        // Load ALL matches for this competition
        var matches = await _context.Matches
            .Where(x => x.CompetitionId == competitionId)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .Include(x => x.Competition)
            .OrderBy(x => x.MatchDay)
            .ToListAsync();

        // Simulate only unplayed matches
        foreach (var match in matches.Where(m => !m.IsPlayed))
        {
            match.HomeScore = GenerateGoals();
            match.AwayScore = GenerateGoals();
            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();

        // 🔥 Automatically calculate updated league table
        var standings = await _tableService.GenerateTable(competitionId);

        return new
        {
            Matches = matches,
            Standings = standings
        };
    }

    private int GenerateGoals()
    {
        return _random.Next(0, 5); // 0–4 goals
    }
}