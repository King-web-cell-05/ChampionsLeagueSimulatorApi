using ChampionsLeagueSimulatorApi.DTOs;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
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

    public async Task<SimulationResponseDto> SimulateCompetition(Guid competitionId)
    {
        // ✅ Load all matches for the competition
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Competition)
            .OrderBy(m => m.MatchDay)
            .ToListAsync();

        // ✅ Simulate only unplayed matches
        foreach (var match in matches.Where(m => !m.IsPlayed))
        {
            match.HomeScore = GenerateGoals();
            match.AwayScore = GenerateGoals();
            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();

        // ✅ Generate updated league standings using TableService
        var standings = await _tableService.GenerateTable(competitionId);

        // ✅ Map matches to DTO
        var matchDtos = matches.Select(m => new MatchDto
        {
            Id = m.Id,
            CompetitionId = m.CompetitionId,
            CompetitionName = m.Competition?.Name ?? "",
            HomeTeamId = m.HomeTeamId,
            HomeTeamName = m.HomeTeam?.Name ?? "",
            AwayTeamId = m.AwayTeamId,
            AwayTeamName = m.AwayTeam?.Name ?? "",
            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore,
            IsPlayed = m.IsPlayed,
            MatchDay = m.MatchDay
        }).ToList();

        // ✅ Return simulation results + standings
        return new SimulationResponseDto
        {
            Matches = matchDtos,
            Standings = standings
        };
    }

    // ✅ Random goal generator (0-4 goals)
    private int GenerateGoals()
    {
        return _random.Next(0, 5);
    }
}