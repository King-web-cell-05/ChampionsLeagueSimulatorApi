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

    // ✅ STEP 1: GENERATE 8 MATCHES PER TEAM (ONLY 8 RANDOM TEAMS)
    public async Task GenerateLeagueFixtures(Guid competitionId)
    {
        var allTeams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .ToListAsync();

        if (allTeams.Count < 8)
            throw new Exception("At least 8 teams are required");

        // 🎯 Pick ONLY 8 RANDOM TEAMS
        var teams = allTeams
            .OrderBy(x => Guid.NewGuid())
            .Take(8)
            .ToList();

        // 🧹 Clear old fixtures (VERY IMPORTANT)
        var existingMatches = _context.Matches
            .Where(m => m.CompetitionId == competitionId);

        _context.Matches.RemoveRange(existingMatches);
        await _context.SaveChangesAsync();

        var matches = new List<Match>();
        var playedPairs = new HashSet<string>();

        // Track matches per team
        var matchesPerTeam = teams.ToDictionary(t => t.Id, t => 0);

        int matchDay = 1;

        while (matchesPerTeam.Any(t => t.Value < 8))
        {
            var availableTeams = teams
                .Where(t => matchesPerTeam[t.Id] < 8)
                .OrderBy(x => Guid.NewGuid())
                .ToList();

            for (int i = 0; i < availableTeams.Count - 1; i++)
            {
                var home = availableTeams[i];

                if (matchesPerTeam[home.Id] >= 8)
                    continue;

                for (int j = i + 1; j < availableTeams.Count; j++)
                {
                    var away = availableTeams[j];

                    if (matchesPerTeam[away.Id] >= 8)
                        continue;

                    var key1 = $"{home.Id}-{away.Id}";
                    var key2 = $"{away.Id}-{home.Id}";

                    if (playedPairs.Contains(key1) || playedPairs.Contains(key2))
                        continue;

                    matches.Add(new Match
                    {
                        Id = Guid.NewGuid(),
                        CompetitionId = competitionId,
                        HomeTeamId = home.Id,
                        AwayTeamId = away.Id,
                        MatchDay = matchDay,
                        IsPlayed = false
                    });

                    playedPairs.Add(key1);

                    matchesPerTeam[home.Id]++;
                    matchesPerTeam[away.Id]++;

                    break;
                }
            }

            matchDay++;

            // 🛑 Safety to prevent infinite loop
            if (matchDay > 20)
                break;
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