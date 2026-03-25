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

    // ✅ STEP 1: GENERATE 8 MATCHES PER TEAM
    public async Task GenerateLeagueFixtures(Guid competitionId)
    {
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .ToListAsync();

        var matches = new List<Match>();
        var existingPairs = new HashSet<string>();

        foreach (var team in teams)
        {
            int matchesNeeded = 8;

            while (matchesNeeded > 0)
            {
                var opponent = teams[_random.Next(teams.Count)];

                if (team.Id == opponent.Id)
                    continue;

                var key1 = $"{team.Id}-{opponent.Id}";
                var key2 = $"{opponent.Id}-{team.Id}";

                if (existingPairs.Contains(key1) || existingPairs.Contains(key2))
                    continue;

                matches.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    HomeTeamId = team.Id,
                    AwayTeamId = opponent.Id,
                    MatchDay = _random.Next(1, 9),
                    IsPlayed = false
                });

                existingPairs.Add(key1);
                matchesNeeded--;
            }
        }

        _context.Matches.AddRange(matches);
        await _context.SaveChangesAsync();
    }

    // ✅ STEP 2: SIMULATE MATCHES
    public async Task<SimulationResponseDto> SimulateCompetition(Guid competitionId)
    {
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId && !m.IsPlayed)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Competition)
            .ToListAsync();

        foreach (var match in matches)
        {
            match.HomeScore = GenerateGoals();
            match.AwayScore = GenerateGoals();
            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();

        var standings = await _tableService.GenerateTable(competitionId);

        return new SimulationResponseDto
        {
            Matches = matches.Select(m => new MatchDto
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
            }).ToList(),

            Standings = standings
        };
    }

    private int GenerateGoals() => _random.Next(0, 5);
}