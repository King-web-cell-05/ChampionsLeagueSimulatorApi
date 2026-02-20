using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class TableService
{
    private readonly AppDbContext _context;

    public TableService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Standing>> GenerateTable(Guid competitionId)
    {
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId && m.IsPlayed)
            .ToListAsync();

        var standings = new Dictionary<Guid, Standing>();

        foreach (var match in matches)
        {
            if (!standings.ContainsKey(match.HomeTeamId))
                standings[match.HomeTeamId] = new Standing
                {
                    TeamId = match.HomeTeamId,
                    CompetitionId = competitionId
                };

            if (!standings.ContainsKey(match.AwayTeamId))
                standings[match.AwayTeamId] = new Standing
                {
                    TeamId = match.AwayTeamId,
                    CompetitionId = competitionId
                };

            var home = standings[match.HomeTeamId];
            var away = standings[match.AwayTeamId];

            home.Played++;
            away.Played++;

            home.GoalsFor += match.HomeScore.Value;
            home.GoalsAgainst += match.AwayScore.Value;

            away.GoalsFor += match.AwayScore.Value;
            away.GoalsAgainst += match.HomeScore.Value;

            if (match.HomeScore > match.AwayScore)
            {
                home.Wins++;
                home.Points += 3;
                away.Losses++;
            }
            else if (match.HomeScore < match.AwayScore)
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

        return standings.Values
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.GoalDifference)
            .ToList();
    }
}