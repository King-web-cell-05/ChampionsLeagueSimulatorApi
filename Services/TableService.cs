using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorApi.DTOs;
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
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Include(ct => ct.Team)
            .Select(ct => ct.Team)
            .ToListAsync();

        var standings = teams.ToDictionary(
            t => t.Id,
            t => new StandingDto
            {
                TeamId = t.Id,
                TeamName = t.Name
            });

        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId && m.IsPlayed)
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

        return standings.Values
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ToList();
    }
}