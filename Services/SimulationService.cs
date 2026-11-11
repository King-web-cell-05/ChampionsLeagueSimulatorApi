
using System.Security.Claims;
using System.Data;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class SimulationService
{
    private readonly AppDbContext _context;
    private readonly TableService _tableService;
    private readonly DrawService _drawService;
    private readonly MatchEngineService _matchEngineService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SimulationService(
        AppDbContext context,
        TableService tableService,
        DrawService drawService,
        MatchEngineService matchEngineService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _tableService = tableService;
        _drawService = drawService;
        _matchEngineService = matchEngineService;
        _httpContextAccessor = httpContextAccessor;
    }

    // ==========================================
    // CURRENT AUTHENTICATED USER
    // ==========================================

    private Guid GetCurrentUserId()
    {
        var user = _httpContextAccessor
            .HttpContext?.User;

        var userIdValue =
            user?.FindFirstValue(
                ClaimTypes.NameIdentifier) ??
            user?.FindFirstValue("sub");

        if (!Guid.TryParse(
            userIdValue,
            out var userId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        return userId;
    }

    // ==========================================
    // VERIFY COMPETITION OWNERSHIP
    // ==========================================

    private async Task VerifyOwnershipAsync(
        Guid competitionId,
        Guid userId)
    {
        var exists = await _context.Competitions
            .AnyAsync(c =>
                c.Id == competitionId &&
                c.OwnerId == userId);

        if (!exists)
        {
            throw new KeyNotFoundException(
                "Competition not found or access denied.");
        }
    }

    // ==========================================
    // GENERATE LEAGUE FIXTURES
    // ==========================================

    public Task GenerateLeagueFixtures(
        Guid competitionId)
    {
        return GenerateLeagueFixtures(
            competitionId,
            GetCurrentUserId());
    }

    public async Task GenerateLeagueFixtures(
        Guid competitionId,
        Guid userId)
    {
        await VerifyOwnershipAsync(
            competitionId,
            userId);

        await _drawService.GenerateLeagueFixtures(
            competitionId,
            userId);
    }

    // ==========================================
    // SIMULATE SINGLE MATCH
    // ==========================================

    public Task<SimulationResponseDto> SimulateMatch(
        Guid competitionId,
        Guid matchId)
    {
        return SimulateMatch(
            competitionId,
            matchId,
            GetCurrentUserId());
    }

    public Task<SimulationResponseDto> SimulateMatch(
        Guid competitionId,
        Guid matchId,
        Guid userId)
    {
        if (matchId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid match ID is required.");
        }

        return RunSimulation(
            competitionId,
            userId,
            matchId,
            null);
    }

    // ==========================================
    // SIMULATE MATCHDAY
    // ==========================================

    public Task<SimulationResponseDto> SimulateMatchday(
        Guid competitionId,
        int matchday)
    {
        return SimulateMatchday(
            competitionId,
            matchday,
            GetCurrentUserId());
    }

    public Task<SimulationResponseDto> SimulateMatchday(
        Guid competitionId,
        int matchday,
        Guid userId)
    {
        if (matchday < 1)
        {
            throw new ArgumentException(
                "Matchday must be greater than zero.");
        }

        return RunSimulation(
            competitionId,
            userId,
            null,
            matchday);
    }

    // ==========================================
    // SIMULATE ALL REMAINING MATCHES
    // ==========================================

    public Task<SimulationResponseDto> SimulateCompetition(
        Guid competitionId)
    {
        return SimulateCompetition(
            competitionId,
            GetCurrentUserId());
    }

    public Task<SimulationResponseDto> SimulateCompetition(
        Guid competitionId,
        Guid userId)
    {
        return RunSimulation(
            competitionId,
            userId,
            null,
            null);
    }

    // ==========================================
    // SHARED SIMULATION ENGINE
    // ==========================================

    private async Task<SimulationResponseDto> RunSimulation(
        Guid competitionId,
        Guid userId,
        Guid? matchId,
        int? matchday)
    {
        if (competitionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid competition ID is required.");
        }

        await VerifyOwnershipAsync(
            competitionId,
            userId);

        List<MatchDto> simulatedMatches;

        await using (
            var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable))
        {
            IQueryable<Match> query =
                _context.Matches
                    .Where(m =>
                        m.CompetitionId ==
                            competitionId &&
                        m.Competition.OwnerId ==
                            userId);

            // Filter a specific match.
            if (matchId.HasValue)
            {
                query = query.Where(m =>
                    m.Id == matchId.Value);
            }

            // Filter a specific matchday.
            if (matchday.HasValue)
            {
                query = query.Where(m =>
                    m.MatchDay == matchday.Value);
            }

            // Load the match and both clubs.
            var matches = await query
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Competition)
                .OrderBy(m => m.MatchDay)
                .ThenBy(m => m.Id)
                .ToListAsync();

            // ==================================
            // VALIDATE FIXTURES
            // ==================================

            if (matches.Count == 0)
            {
                if (matchId.HasValue)
                {
                    throw new KeyNotFoundException(
                        "Match not found in this competition.");
                }

                if (matchday.HasValue)
                {
                    throw new KeyNotFoundException(
                        "No fixtures found for this matchday.");
                }

                throw new InvalidOperationException(
                    "No fixtures exist for this competition.");
            }

            // A single match must never be replayed.
            if (
                matchId.HasValue &&
                matches[0].IsPlayed)
            {
                throw new InvalidOperationException(
                    "This match has already been played.");
            }

            var pendingMatches = matches
                .Where(m => !m.IsPlayed)
                .ToList();

            // ==================================
            // REALISTIC MATCH SIMULATION
            // ==================================

            foreach (var match in pendingMatches)
            {
                var result =
                    _matchEngineService.SimulateMatch(
                        match.HomeTeam,
                        match.AwayTeam);

                match.HomeScore =
                    result.HomeScore;

                match.AwayScore =
                    result.AwayScore;

                match.IsPlayed = true;
            }

            if (pendingMatches.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            simulatedMatches = pendingMatches
                .Select(m => new MatchDto
                {
                    Id = m.Id,

                    CompetitionId =
                        m.CompetitionId,

                    CompetitionName =
                        m.Competition?.Name ??
                        string.Empty,

                    HomeTeamId =
                        m.HomeTeamId,

                    HomeTeamName =
                        m.HomeTeam?.Name ??
                        string.Empty,

                    AwayTeamId =
                        m.AwayTeamId,

                    AwayTeamName =
                        m.AwayTeam?.Name ??
                        string.Empty,

                    KickoffUtc =
                        m.KickoffUtc,

                    HomeScore =
                        m.HomeScore,

                    AwayScore =
                        m.AwayScore,

                    IsPlayed =
                        m.IsPlayed,

                    MatchDay =
                        m.MatchDay
                })
                .ToList();

            await transaction.CommitAsync();
        }

        // ======================================
        // RECALCULATE LEAGUE STANDINGS
        // ======================================

        var standings =
            await _tableService.GenerateTable(
                competitionId,
                userId);

        return new SimulationResponseDto
        {
            Matches = simulatedMatches,
            Standings = standings
        };
    }
}
