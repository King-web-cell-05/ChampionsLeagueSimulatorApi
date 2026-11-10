
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class SimulationService
{
    private readonly AppDbContext _context;
    private readonly TableService _tableService;
    private readonly DrawService _drawService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SimulationService(
        AppDbContext context,
        TableService tableService,
        DrawService drawService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _tableService = tableService;
        _drawService = drawService;
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

    private async Task VerifyOwnershipAsync(
        Guid competitionId,
        Guid userId)
    {
        if (competitionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Invalid competition ID.",
                nameof(competitionId));
        }

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
    }

    // ==========================================
    // GENERATE LEAGUE FIXTURES
    // ==========================================

    public Task GenerateLeagueFixtures(Guid competitionId)
    {
        return GenerateLeagueFixtures(
            competitionId,
            GetCurrentUserId());
    }

    public async Task GenerateLeagueFixtures(
        Guid competitionId,
        Guid userId)
    {
        await VerifyOwnershipAsync(competitionId, userId);

        // Keep the existing UEFA-style draw.
        await _drawService.GenerateLeagueFixtures(
            competitionId);
    }

    // ==========================================
    // SIMULATE COMPETITION
    // ==========================================

    public Task<SimulationResponseDto> SimulateCompetition(
        Guid competitionId)
    {
        return SimulateCompetition(
            competitionId,
            GetCurrentUserId());
    }

    public async Task<SimulationResponseDto> SimulateCompetition(
        Guid competitionId,
        Guid userId)
    {
        await VerifyOwnershipAsync(competitionId, userId);

        // Only simulate unplayed matches belonging
        // to the authenticated user's competition.
        var matches = await _context.Matches
            .Where(m =>
                m.CompetitionId == competitionId &&
                m.Competition.OwnerId == userId &&
                !m.IsPlayed)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Competition)
            .OrderBy(m => m.MatchDay)
            .ToListAsync();

        if (matches.Count == 0)
        {
            var hasFixtures = await _context.Matches
                .AnyAsync(m =>
                    m.CompetitionId == competitionId &&
                    m.Competition.OwnerId == userId);

            if (!hasFixtures)
            {
                throw new InvalidOperationException(
                    "No fixtures exist for this competition. " +
                    "Generate the draw before simulating matches.");
            }
        }

        var random = Random.Shared;

        foreach (var match in matches)
        {
            match.HomeScore = random.Next(0, 5);
            match.AwayScore = random.Next(0, 5);
            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();

        // Use the ownership-aware standings method.
        var standings = await _tableService.GenerateTable(
            competitionId,
            userId);

        return new SimulationResponseDto
        {
            Matches = matches.Select(m => new MatchDto
            {
                Id = m.Id,
                CompetitionId = m.CompetitionId,
                CompetitionName =
                    m.Competition?.Name ?? string.Empty,

                HomeTeamId = m.HomeTeamId,
                HomeTeamName =
                    m.HomeTeam?.Name ?? string.Empty,

                AwayTeamId = m.AwayTeamId,
                AwayTeamName =
                    m.AwayTeam?.Name ?? string.Empty,

                KickoffUtc = m.KickoffUtc,

                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,

                IsPlayed = m.IsPlayed,
                MatchDay = m.MatchDay
            }).ToList(),

            Standings = standings
        };
    }
}
