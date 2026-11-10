
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class KnockoutBatchSimulationService
{
    private readonly AppDbContext _context;
    private readonly KnockoutSimulationService _simulationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public KnockoutBatchSimulationService(
        AppDbContext context,
        KnockoutSimulationService simulationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _simulationService = simulationService;
        _httpContextAccessor = httpContextAccessor;
    }

    // ==========================================
    // GET AUTHENTICATED USER
    // ==========================================

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

    // ==========================================
    // VERIFY COMPETITION OWNERSHIP
    // ==========================================

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
    // PLAY-OFF SIMULATION
    // ==========================================

    public Task<object> SimulatePlayoffLegsAsync(
        Guid competitionId,
        int leg)
    {
        return SimulateRoundLegsAsync(
            competitionId,
            KnockoutRound.Playoff,
            leg);
    }

    // ==========================================
    // ROUND OF 16 SIMULATION
    // ==========================================

    public Task<object> SimulateRoundOf16LegsAsync(
        Guid competitionId,
        int leg)
    {
        return SimulateRoundLegsAsync(
            competitionId,
            KnockoutRound.RoundOf16,
            leg);
    }

    // ==========================================
    // REUSABLE KNOCKOUT SIMULATION
    // ==========================================

    public async Task<object> SimulateRoundLegsAsync(
        Guid competitionId,
        KnockoutRound round,
        int leg)
    {
        var userId = GetCurrentUserId();

        await VerifyOwnershipAsync(
            competitionId,
            userId);

        if (leg != 1 && leg != 2)
        {
            throw new ArgumentException(
                "Leg must be 1 or 2.");
        }

        if (round == KnockoutRound.Final)
        {
            throw new InvalidOperationException(
                "The final requires single-match simulation.");
        }

        int expectedTies = round switch
        {
            KnockoutRound.Playoff => 8,
            KnockoutRound.RoundOf16 => 8,
            KnockoutRound.QuarterFinal => 4,
            KnockoutRound.SemiFinal => 2,

            _ => throw new ArgumentException(
                "Unsupported knockout round.")
        };

        // ======================================
        // LOAD OWNED COMPETITION TIES
        // ======================================

        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Competition.OwnerId == userId &&
                t.Round == round)
            .Include(t => t.Matches)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        if (ties.Count != expectedTies)
        {
            throw new InvalidOperationException(
                $"Expected {expectedTies} ties for {round}.");
        }

        // ======================================
        // VALIDATE MATCH LEGS
        // ======================================

        if (ties.Any(t =>
            t.Matches.Count != 2 ||
            t.Matches.Count(m => m.Leg == 1) != 1 ||
            t.Matches.Count(m => m.Leg == 2) != 1))
        {
            throw new InvalidOperationException(
                "Every tie must contain two valid legs.");
        }

        if (leg == 2 && ties.Any(t =>
            !t.Matches.Single(m => m.Leg == 1).IsPlayed))
        {
            throw new InvalidOperationException(
                "Complete all first legs before " +
                "simulating second legs.");
        }

        // ======================================
        // GET PENDING MATCHES
        // ======================================

        var pendingMatches = ties
            .Where(t => !t.IsCompleted)
            .SelectMany(t => t.Matches
                .Where(m =>
                    m.Leg == leg &&
                    !m.IsPlayed)
                .Select(m => new
                {
                    t.TieNumber,
                    MatchId = m.Id
                }))
            .OrderBy(m => m.TieNumber)
            .ToList();

        var results = new List<object>();

        // ======================================
        // SIMULATE MATCHES
        // ======================================

        foreach (var pending in pendingMatches)
        {
            // Revalidate ownership before
            // invoking the match simulation.
            await VerifyOwnershipAsync(
                competitionId,
                userId);

            var result = await _simulationService
                .SimulateMatchAsync(pending.MatchId);

            results.Add(result);
        }

        // ======================================
        // RELOAD UPDATED ROUND STATE
        // ======================================

        var updatedTies = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Competition.OwnerId == userId &&
                t.Round == round)
            .Include(t => t.Matches)
            .ToListAsync();

        return new
        {
            CompetitionId = competitionId,
            Round = round.ToString(),
            Leg = leg,

            SimulatedMatches = results.Count,
            TotalTies = updatedTies.Count,

            CompletedFirstLegs = updatedTies
                .SelectMany(t => t.Matches)
                .Count(m =>
                    m.Leg == 1 &&
                    m.IsPlayed),

            CompletedSecondLegs = updatedTies
                .SelectMany(t => t.Matches)
                .Count(m =>
                    m.Leg == 2 &&
                    m.IsPlayed),

            CompletedTies = updatedTies
                .Count(t => t.IsCompleted),

            IsRoundCompleted =
                updatedTies.Count == expectedTies &&
                updatedTies.All(t =>
                    t.IsCompleted &&
                    t.WinnerTeamId.HasValue),

            Results = results
        };
    }
}
