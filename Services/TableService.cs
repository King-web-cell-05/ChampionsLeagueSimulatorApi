
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

    public async Task<List<StandingDto>> GenerateTable(
        Guid competitionId)
    {
        if (competitionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Invalid competition ID.",
                nameof(competitionId));
        }

        // Get all clubs registered in the competition.
        var teams = await _context.CompetitionTeams
            .AsNoTracking()
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .ToListAsync();

        var standings = teams.ToDictionary(
            team => team.Id,
            team => new StandingDto
            {
                TeamId = team.Id,
                TeamName = team.Name,
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

        // Only completed matches affect league standings.
        var matches = await _context.Matches
            .AsNoTracking()
            .Where(m =>
                m.CompetitionId == competitionId &&
                m.IsPlayed &&
                m.HomeScore.HasValue &&
                m.AwayScore.HasValue)
            .ToListAsync();

        foreach (var match in matches)
        {
            if (!standings.TryGetValue(
                    match.HomeTeamId, out var home) ||
                !standings.TryGetValue(
                    match.AwayTeamId, out var away))
            {
                throw new InvalidOperationException(
                    "A completed match contains a team " +
                    "that is not registered in the competition.");
            }

            int homeScore = match.HomeScore!.Value;
            int awayScore = match.AwayScore!.Value;

            if (homeScore < 0 || awayScore < 0)
            {
                throw new InvalidOperationException(
                    "Match scores cannot be negative.");
            }

            // Matches played.
            home.Played++;
            away.Played++;

            // Goals.
            home.GoalsFor += homeScore;
            home.GoalsAgainst += awayScore;

            away.GoalsFor += awayScore;
            away.GoalsAgainst += homeScore;

            // Match result.
            if (homeScore > awayScore)
            {
                home.Wins++;
                home.Points += 3;

                away.Losses++;
            }
            else if (awayScore > homeScore)
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

        // Calculate final goal differences.
        foreach (var standing in standings.Values)
        {
            standing.GoalDifference =
                standing.GoalsFor - standing.GoalsAgainst;
        }

        // Rank clubs.
        var rankedStandings = standings.Values
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ThenByDescending(s => s.Wins)
            .ThenBy(s => s.TeamName, StringComparer.Ordinal)
            .ToList();

        // Assign positions starting from 1.
        for (int i = 0; i < rankedStandings.Count; i++)
        {
            rankedStandings[i].Position = i + 1;
        }

        return rankedStandings;
    }
}
