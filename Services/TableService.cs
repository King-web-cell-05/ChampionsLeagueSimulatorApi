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
        // ✅ Get all teams in this competition
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .ToListAsync();

        // ✅ Initialize standings for ALL teams
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
                Points = 0,
                Position = 0
            });

        // ✅ Get all played matches for the competition
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId && m.IsPlayed)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .ToListAsync();

        foreach (var match in matches)
        {
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
        }

        // ✅ Compute goal difference for each team
        foreach (var team in standings.Values)
        {
            team.GoalDifference = team.GoalsFor - team.GoalsAgainst;
        }

        // ✅ Order standings
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