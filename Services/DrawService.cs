using System.Security.Claims;
using System.Data;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChampionsLeagueSimulatorAPI.Services;

public class DrawService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DrawService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    // ==========================================
    // GET AUTHENTICATED USER ID
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
    // GENERATE FIXTURES - COMPATIBILITY OVERLOAD
    // ==========================================

    public Task<List<Match>> GenerateLeagueFixtures(
        Guid competitionId)
    {
        return GenerateLeagueFixtures(
            competitionId,
            GetCurrentUserId());
    }

    // ==========================================
    // DYNAMIC FIXTURE GENERATION
    // ==========================================

    public async Task<List<Match>> GenerateLeagueFixtures(
        Guid competitionId,
        Guid userId)
    {
        await VerifyOwnershipAsync(
            competitionId,
            userId);

        // Reuse an existing transaction when this
        // method is called during team registration.
        // Otherwise, start a new transaction.

        var ownsTransaction =
            _context.Database.CurrentTransaction == null;

        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
        {
            transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);
        }

        try
        {
            // ==========================================
            // GET REGISTERED TEAMS
            // ==========================================

            var teams = await _context.CompetitionTeams
                .AsNoTracking()
                .Where(ct =>
                    ct.CompetitionId == competitionId)
                .Select(ct => ct.Team)
                .OrderBy(t => t.Name)
                .ToListAsync();

            // A minimum of two teams is required.
            if (teams.Count < 2)
            {
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return new List<Match>();
            }

            // ==========================================
            // GET EXISTING MATCHES
            // ==========================================

            var existingMatches = await _context.Matches
                .AsNoTracking()
                .Where(m =>
                    m.CompetitionId == competitionId)
                .ToListAsync();

            // Store existing pairs in a HashSet.
            // Home/Away order does not matter.
            var existingPairs = new HashSet<
                (Guid First, Guid Second)>();

            foreach (var match in existingMatches)
            {
                existingPairs.Add(
                    NormalizePair(
                        match.HomeTeamId,
                        match.AwayTeamId));
            }

            // ==========================================
            // DETERMINE NEXT MATCHDAY
            // ==========================================

            int nextMatchday = existingMatches.Count == 0
                ? 1
                : existingMatches.Max(m => m.MatchDay) + 1;

            var fixtures = new List<Match>();

            // ==========================================
            // GENERATE ONLY MISSING PAIRINGS
            // ==========================================

            for (int i = 0; i < teams.Count; i++)
            {
                for (int j = i + 1; j < teams.Count; j++)
                {
                    var firstTeam = teams[i];
                    var secondTeam = teams[j];

                    var pair = NormalizePair(
                        firstTeam.Id,
                        secondTeam.Id);

                    // Skip previously scheduled matches.
                    if (existingPairs.Contains(pair))
                    {
                        continue;
                    }

                    // Randomize home and away clubs.
                    bool firstTeamHome =
                        Random.Shared.Next(2) == 0;

                    var homeTeamId = firstTeamHome
                        ? firstTeam.Id
                        : secondTeam.Id;

                    var awayTeamId = firstTeamHome
                        ? secondTeam.Id
                        : firstTeam.Id;

                    fixtures.Add(new Match
                    {
                        Id = Guid.NewGuid(),

                        CompetitionId = competitionId,

                        HomeTeamId = homeTeamId,
                        AwayTeamId = awayTeamId,

                        MatchDay = nextMatchday,

                        KickoffUtc = null,

                        HomeScore = null,
                        AwayScore = null,

                        IsPlayed = false
                    });

                    existingPairs.Add(pair);

                    // Allocate a separate matchday.
                    nextMatchday++;
                }
            }

            // ==========================================
            // SAVE NEW FIXTURES
            // ==========================================

            if (fixtures.Count > 0)
            {
                _context.Matches.AddRange(fixtures);

                await _context.SaveChangesAsync();
            }

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return fixtures;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    // ==========================================
    // NORMALIZE TEAM PAIR
    // ==========================================

    private static (Guid First, Guid Second) NormalizePair(
        Guid teamA,
        Guid teamB)
    {
        return teamA.CompareTo(teamB) < 0
            ? (teamA, teamB)
            : (teamB, teamA);
    }

    // ==========================================
    // BACKWARD COMPATIBILITY
    // ==========================================

    public Task<List<Match>> GenerateDoubleRoundRobin(
        Guid competitionId)
    {
        return GenerateLeagueFixtures(competitionId);
    }
}