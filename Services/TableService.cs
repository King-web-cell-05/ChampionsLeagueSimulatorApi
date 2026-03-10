using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.EntityFrameworkCore;
using ChampionsLeagueSimulatorApi.DTOs;

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
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId && m.IsPlayed)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .ToListAsync();

        var standings = new Dictionary<Guid, StandingDto>();

        foreach (var match in matches)
        {
            // Initialize home standing
            if (!standings.ContainsKey(match.HomeTeamId))
                standings[match.HomeTeamId] = new StandingDto
                {
                    TeamId = match.HomeTeamId,
                    TeamName = match.HomeTeam?.Name ?? ""
                };

            // Initialize away standing
            if (!standings.ContainsKey(match.AwayTeamId))
                standings[match.AwayTeamId] = new StandingDto
                {
                    TeamId = match.AwayTeamId,
                    TeamName = match.AwayTeam?.Name ?? ""
                };

            var home = standings[match.HomeTeamId];
            var away = standings[match.AwayTeamId];

            home.Played++;
            away.Played++;

            home.GoalsFor += match.HomeScore ?? 0;
            home.GoalsAgainst += match.AwayScore ?? 0;

            away.GoalsFor += match.AwayScore ?? 0;
            away.GoalsAgainst += match.HomeScore ?? 0;

            if ((match.HomeScore ?? 0) > (match.AwayScore ?? 0))
            {
                home.Wins++;
                home.Points += 3;
                away.Losses++;
            }
            else if ((match.HomeScore ?? 0) < (match.AwayScore ?? 0))
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
            .ToList();
    }
}