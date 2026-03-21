using ChampionsLeagueSimulatorApi.DTOs;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class TableService
{
    private readonly AppDbContext _context;

    public TableService(AppDbContext context)
    {
        _context = context;
    }

    // ✅ Return standings per group
    public async Task<Dictionary<string, List<StandingDto>>> GenerateGroupedTable(Guid competitionId)
    {
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Include(ct => ct.Team)
            .ToListAsync();

        // Initialize standings per team
        var standings = teams.ToDictionary(
            t => t.TeamId,
            t => new StandingDto
            {
                TeamId = t.TeamId,
                TeamName = t.Team.Name,
                Played = 0,
                Wins = 0,
                Draws = 0,
                Losses = 0,
                GoalsFor = 0,
                GoalsAgainst = 0,
                Points = 0,
                Position = 0,
                GroupName = t.GroupName
            });

        // Get all played matches
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId && m.IsPlayed)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .ToListAsync();

        foreach (var match in matches)
        {
            var home = standings[match.HomeTeamId];
            var away = standings[match.AwayTeamId];

            int homeScore = match.HomeScore ?? 0;
            int awayScore = match.AwayScore ?? 0;

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

            home.GoalDifference = home.GoalsFor - home.GoalsAgainst;
            away.GoalDifference = away.GoalsFor - away.GoalsAgainst;
        }

        // ✅ Group standings by group
        var grouped = standings.Values
            .GroupBy(s => s.GroupName)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.Points)
                      .ThenByDescending(s => s.GoalDifference)
                      .ThenByDescending(s => s.GoalsFor)
                      .ToList()
            );

        // ✅ Assign position per group
        foreach (var group in grouped)
        {
            for (int i = 0; i < group.Value.Count; i++)
            {
                group.Value[i].Position = i + 1;
            }
        }

        return grouped;
    }
}