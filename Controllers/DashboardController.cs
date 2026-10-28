
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

    // ==========================================
    // GENERAL DASHBOARD SUMMARY
    // GET: api/Dashboard/summary
    // ==========================================

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var totalCompetitions = await _context.Competitions
            .CountAsync();

        var activeCompetition = await _context.Competitions
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedDate)
            .FirstOrDefaultAsync();

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
                (int?)((m.HomeScore ?? 0) +
                       (m.AwayScore ?? 0))) ?? 0;

        var currentMatchday = await matchesQuery
            .Where(m => m.IsPlayed)
            .MaxAsync(m => (int?)m.MatchDay) ?? 0;

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

    // ==========================================
    // COMPETITION OVERVIEW
    // GET: api/Dashboard/competition/{id}/overview
    // ==========================================

    [HttpGet("competition/{competitionId:guid}/overview")]
    public async Task<IActionResult> GetCompetitionOverview(
        Guid competitionId)
    {
        if (competitionId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "A valid competition ID is required."
            });
        }

        // ======================================
        // 1. COMPETITION DETAILS
        // ======================================

        var competition = await _context.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == competitionId);

        if (competition == null)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        // ======================================
        // 2. REGISTERED TEAMS
        // ======================================

        var registeredTeams = await _context.CompetitionTeams
            .AsNoTracking()
            .CountAsync(ct =>
                ct.CompetitionId == competitionId);

        // ======================================
        // 3. LEAGUE-PHASE STATISTICS
        // ======================================

        var matchesQuery = _context.Matches
            .AsNoTracking()
            .Where(m => m.CompetitionId == competitionId);

        var totalMatches = await matchesQuery.CountAsync();

        var matchesPlayed = await matchesQuery
            .CountAsync(m => m.IsPlayed);

        var remainingMatches =
            totalMatches - matchesPlayed;

        var totalGoals = await matchesQuery
            .Where(m => m.IsPlayed)
            .SumAsync(m =>
                (int?)((m.HomeScore ?? 0) +
                       (m.AwayScore ?? 0))) ?? 0;

        var currentMatchday = await matchesQuery
            .Where(m => m.IsPlayed)
            .MaxAsync(m => (int?)m.MatchDay) ?? 0;

        var totalMatchdays = await matchesQuery
            .MaxAsync(m => (int?)m.MatchDay) ?? 0;

        double leagueProgress = totalMatches > 0
            ? Math.Round(
                matchesPlayed * 100.0 / totalMatches,
                2)
            : 0;

        bool leagueCompleted =
            totalMatches > 0 &&
            matchesPlayed == totalMatches;

        // ======================================
        // 4. UPCOMING LEAGUE FIXTURES
        // ======================================

        var upcomingFixtures = await matchesQuery
            .Where(m => !m.IsPlayed)
            .OrderBy(m => m.MatchDay)
            .ThenBy(m => m.Id)
            .Take(5)
            .Select(m => new
            {
                matchId = m.Id,
                matchday = m.MatchDay,
                homeTeamId = m.HomeTeamId,
                homeTeam = m.HomeTeam.Name,
                awayTeamId = m.AwayTeamId,
                awayTeam = m.AwayTeam.Name,
                isPlayed = m.IsPlayed
            })
            .ToListAsync();

        // ======================================
        // 5. RECENT LEAGUE RESULTS
        // ======================================

        var recentResults = await matchesQuery
            .Where(m => m.IsPlayed)
            .OrderByDescending(m => m.MatchDay)
            .ThenByDescending(m => m.Id)
            .Take(5)
            .Select(m => new
            {
                matchId = m.Id,
                matchday = m.MatchDay,
                homeTeam = m.HomeTeam.Name,
                awayTeam = m.AwayTeam.Name,
                homeScore = m.HomeScore,
                awayScore = m.AwayScore,
                isPlayed = m.IsPlayed
            })
            .ToListAsync();

        // ======================================
        // 6. KNOCKOUT ROUND PROGRESS
        // ======================================

        var knockoutTies = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t => t.CompetitionId == competitionId)
            .Select(t => new
            {
                t.Round,
                t.IsCompleted,
                t.WinnerTeamId,
                MatchCount = t.Matches.Count,
                PlayedMatches = t.Matches.Count(m => m.IsPlayed)
            })
            .ToListAsync();

        var knockoutRounds = new[]
        {
            new
            {
                round = KnockoutRound.Playoff,
                name = "Knockout Play-offs",
                expectedTies = 8
            },
            new
            {
                round = KnockoutRound.RoundOf16,
                name = "Round of 16",
                expectedTies = 8
            },
            new
            {
                round = KnockoutRound.QuarterFinal,
                name = "Quarter-finals",
                expectedTies = 4
            },
            new
            {
                round = KnockoutRound.SemiFinal,
                name = "Semi-finals",
                expectedTies = 2
            },
            new
            {
                round = KnockoutRound.Final,
                name = "Championship Final",
                expectedTies = 1
            }
        };

        var knockoutProgress = knockoutRounds
            .Select(r =>
            {
                var ties = knockoutTies
                    .Where(t => t.Round == r.round)
                    .ToList();

                int completedTies = ties.Count(t =>
                    t.IsCompleted &&
                    t.WinnerTeamId.HasValue);

                int totalRoundMatches =
                    ties.Sum(t => t.MatchCount);

                int playedRoundMatches =
                    ties.Sum(t => t.PlayedMatches);

                bool isCompleted =
                    ties.Count == r.expectedTies &&
                    completedTies == r.expectedTies;

                string status = isCompleted
                    ? "Completed"
                    : ties.Count == 0
                        ? "NotStarted"
                        : playedRoundMatches > 0
                            ? "InProgress"
                            : "DrawCompleted";

                return new
                {
                    round = r.round.ToString(),
                    name = r.name,
                    status,

                    expectedTies = r.expectedTies,
                    totalTies = ties.Count,
                    completedTies,

                    totalMatches = totalRoundMatches,
                    playedMatches = playedRoundMatches,

                    isCompleted
                };
            })
            .ToList();

        // ======================================
        // 7. CHAMPIONSHIP FINAL
        // ======================================

        var finalTie = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.Final)
            .Include(t => t.HomeTeam)
            .Include(t => t.AwayTeam)
            .Include(t => t.WinnerTeam)
            .Include(t => t.Matches)
            .SingleOrDefaultAsync();

        bool hasChampion =
            finalTie != null &&
            finalTie.IsCompleted &&
            finalTie.WinnerTeamId.HasValue;

        object? champion = null;
        object? runnerUp = null;
        object? finalResult = null;

        if (finalTie != null)
        {
            var finalMatch = finalTie.Matches
                .OrderBy(m => m.Leg)
                .FirstOrDefault();

            if (hasChampion)
            {
                champion = new
                {
                    teamId = finalTie.WinnerTeamId,
                    teamName = finalTie.WinnerTeam?.Name
                };

                bool homeWon =
                    finalTie.WinnerTeamId ==
                    finalTie.HomeTeamId;

                runnerUp = new
                {
                    teamId = homeWon
                        ? finalTie.AwayTeamId
                        : finalTie.HomeTeamId,

                    teamName = homeWon
                        ? finalTie.AwayTeam.Name
                        : finalTie.HomeTeam.Name
                };
            }

            if (finalMatch != null)
            {
                finalResult = new
                {
                    matchId = finalMatch.Id,

                    homeTeam = finalTie.HomeTeam.Name,
                    awayTeam = finalTie.AwayTeam.Name,

                    homeScore = finalMatch.HomeScore,
                    awayScore = finalMatch.AwayScore,

                    homeTotalGoals =
                        finalMatch.HomeScore.GetValueOrDefault() +
                        finalMatch.HomeExtraTimeScore.GetValueOrDefault(),

                    awayTotalGoals =
                        finalMatch.AwayScore.GetValueOrDefault() +
                        finalMatch.AwayExtraTimeScore.GetValueOrDefault(),

                    wentToExtraTime =
                        finalMatch.WentToExtraTime,

                    wentToPenalties =
                        finalMatch.WentToPenalties,

                    homePenaltyScore =
                        finalMatch.HomePenaltyScore,

                    awayPenaltyScore =
                        finalMatch.AwayPenaltyScore,

                    isPlayed = finalMatch.IsPlayed
                };
            }
        }

        // ======================================
        // 8. COMPETITION STATUS
        // ======================================

        string competitionStatus;

        if (hasChampion)
        {
            competitionStatus = "Completed";
        }
        else if (knockoutTies.Count > 0)
        {
            competitionStatus = "KnockoutStage";
        }
        else if (leagueCompleted)
        {
            competitionStatus = "LeaguePhaseCompleted";
        }
        else if (matchesPlayed > 0)
        {
            competitionStatus = "LeaguePhaseInProgress";
        }
        else if (totalMatches > 0)
        {
            competitionStatus = "FixturesGenerated";
        }
        else
        {
            competitionStatus = "NotStarted";
        }

        // ======================================
        // 9. COMPLETE OVERVIEW RESPONSE
        // ======================================

        return Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,

            competition = new
            {
                id = competition.Id,
                name = competition.Name,
                season = competition.Season,
                createdDate = competition.CreatedDate,
                status = competitionStatus
            },

            teams = new
            {
                registered = registeredTeams,
                expected = 36
            },

            leaguePhase = new
            {
                totalMatches,
                matchesPlayed,
                remainingMatches,
                totalGoals,
                currentMatchday,
                totalMatchdays,
                progressPercentage = leagueProgress,
                isCompleted = leagueCompleted
            },

            upcomingFixtures,
            recentResults,

            knockoutStage = new
            {
                totalTies = knockoutTies.Count,
                completedTies = knockoutTies.Count(t =>
                    t.IsCompleted &&
                    t.WinnerTeamId.HasValue),

                rounds = knockoutProgress
            },

            champion,
            runnerUp,
            finalResult,

            isCompetitionCompleted = hasChampion
        });
    }
}
