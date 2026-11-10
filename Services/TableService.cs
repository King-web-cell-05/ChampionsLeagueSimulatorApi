
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class TableService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TableService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        var value =
            user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.FindFirstValue("sub");

        if (user?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        return userId;
    }

    // Existing signature preserved.
    public Task<List<StandingDto>> GenerateTable(
        Guid competitionId)
    {
        return GenerateTable(
            competitionId,
            GetCurrentUserId());
    }

    // Ownership-aware overload.
    public async Task<List<StandingDto>> GenerateTable(
        Guid competitionId,
        Guid userId)
    {
        if (competitionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Invalid competition ID.",
                nameof(competitionId));
        }

        // Verify that this competition belongs
        // to the specified authenticated user.
        var ownsCompetition = await _context.Competitions
            .AsNoTracking()
            .AnyAsync(c =>
                c.Id == competitionId &&
                c.OwnerId == userId);

        if (!ownsCompetition)
        {
            throw new KeyNotFoundException(
                "Competition not found.");
        }

        // Get registered clubs in this competition.
        var teams = await _context.CompetitionTeams
            .AsNoTracking()
            .Where(ct =>
                ct.CompetitionId == competitionId &&
                ct.Competition.OwnerId == userId)
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

        // Only completed league matches count.
        var matches = await _context.Matches
            .AsNoTracking()
            .Where(m =>
                m.CompetitionId == competitionId &&
                m.Competition.OwnerId == userId &&
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

        foreach (var standing in standings.Values)
        {
            standing.GoalDifference =
                standing.GoalsFor - standing.GoalsAgainst;
        }

        var rankedStandings = standings.Values
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ThenByDescending(s => s.Wins)
            .ThenBy(s => s.TeamName, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < rankedStandings.Count; i++)
        {
            rankedStandings[i].Position = i + 1;
        }

        return rankedStandings;
    }
}
