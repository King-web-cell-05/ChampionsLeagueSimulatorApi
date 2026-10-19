
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Dashboard/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        // Count all competitions in the database.
        var totalCompetitions = await _context.Competitions
            .CountAsync();

        // Use the most recently created competition as the active one.
        var activeCompetition = await _context.Competitions
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedDate)
            .FirstOrDefaultAsync();

        // If a competition exists, show its registered teams.
        // Otherwise, show all teams currently in the database.
        int registeredTeams;

        if (activeCompetition is not null)
        {
            registeredTeams = await _context.CompetitionTeams
                .CountAsync(ct =>
                    ct.CompetitionId == activeCompetition.Id);
        }
        else
        {
            registeredTeams = await _context.Teams.CountAsync();
        }

        // Scope match statistics to the active competition.
        IQueryable<Match> matchesQuery = _context.Matches
            .AsNoTracking();

        if (activeCompetition is not null)
        {
            matchesQuery = matchesQuery.Where(m =>
                m.CompetitionId == activeCompetition.Id);
        }

        var totalMatches = await matchesQuery.CountAsync();

        var matchesPlayed = await matchesQuery
            .CountAsync(m => m.IsPlayed);

        var goalsScored = await matchesQuery
            .Where(m => m.IsPlayed)
            .SumAsync(m =>
                (int?)((m.HomeScore ?? 0) + (m.AwayScore ?? 0))) ?? 0;

        var currentMatchday = await matchesQuery
            .Where(m => m.IsPlayed)
            .MaxAsync(m => (int?)m.MatchDay) ?? 0;

        // Return the next five unplayed fixtures.
        var upcomingFixtures = await matchesQuery
            .Where(m => !m.IsPlayed)
            .OrderBy(m => m.MatchDay)
            .ThenBy(m => m.Id)
            .Take(5)
            .Select(m => new
            {
                id = m.Id,
                matchDay = m.MatchDay,
                homeTeam = m.HomeTeam.Name,
                awayTeam = m.AwayTeam.Name,
                homeScore = m.HomeScore,
                awayScore = m.AwayScore,
                competitionName = m.Competition.Name,
                isPlayed = m.IsPlayed
            })
            .ToListAsync();

        return Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            totalCompetitions,
            registeredTeams,
            totalMatches,
            matchesPlayed,
            goalsScored,
            currentMatchday,

            activeCompetition = activeCompetition is null
                ? null
                : new
                {
                    id = activeCompetition.Id,
                    name = activeCompetition.Name,
                    season = activeCompetition.Season,
                    createdDate = activeCompetition.CreatedDate
                },

            upcomingFixtures
        });
    }
}
