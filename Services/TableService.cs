using ChampionsLeagueSimulatorApi.DTOs;
using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class TableService
{
    private readonly AppDbContext _context;

    public TableService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<StandingDto>> GenerateTable(Guid competitionId)
    {
        // ✅ FIX: Load teams correctly
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Include(ct => ct.Team)
            .Select(ct => ct.Team)
            .ToListAsync();

        if (!teams.Any())
            return new List<StandingDto>(); // safety

        // ✅ Initialize standings
        var standings = teams.ToDictionary(
            t => t.Id,
            t => new StandingDto
            {
                TeamId = t.Id,
                TeamName = t.Name,
                Played = 0,
                Wins = 0,
                Draws = 0,
                Losses = 0,
                GoalsFor = 0,
                GoalsAgainst = 0,
                GoalDifference = 0,
                Points = 0,
                Position = 0
            });

        // ✅ Get played matches
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId && m.IsPlayed)
            .ToListAsync();

        foreach (var match in matches)
        {
            // 🔒 Safety check (prevents crash if mismatch)
            if (!standings.ContainsKey(match.HomeTeamId) ||
                !standings.ContainsKey(match.AwayTeamId))
                continue;

            var home = standings[match.HomeTeamId];
            var away = standings[match.AwayTeamId];

            var homeScore = match.HomeScore ?? 0;
            var awayScore = match.AwayScore ?? 0;

            home.Played++;
            away.Played++;

            home.GoalsFor += homeScore;
            home.GoalsAgainst += awayScore;

            away.GoalsFor += awayScore;
            away.GoalsAgainst += homeScore;

            if (homeScore > awayScore)
            {
                home.Wins++;
                home.Points += 3;
                away.Losses++;
            }
            else if (homeScore < awayScore)
            {
                away.Wins++;
                away.Points += 3;
                home.Losses++;
            }
            else
            {
                home.Draws++;
                away.Draws++;
                home.Points++;
                away.Points++;
            }

            // ✅ Update goal difference immediately
            home.GoalDifference = home.GoalsFor - home.GoalsAgainst;
            away.GoalDifference = away.GoalsFor - away.GoalsAgainst;
        }

        // ✅ Sort standings
        var ordered = standings.Values
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ToList();

        // ✅ Assign positions
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].Position = i + 1;
        }

        return ordered;
    }
}