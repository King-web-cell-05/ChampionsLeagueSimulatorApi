
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

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
    // AUTHENTICATION
    // ==========================================

    private Guid GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        var value =
            user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.FindFirstValue("sub");

        if (
            user?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(value, out var userId)
        )
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        return userId;
    }

    // ==========================================
    // COMPETITION OWNERSHIP
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
    // PUBLIC GENERATION METHODS
    // ==========================================

    public Task<List<Match>> GenerateLeagueFixtures(
        Guid competitionId)
    {
        return GenerateLeagueFixtures(
            competitionId,
            GetCurrentUserId());
    }

    public async Task<List<Match>> GenerateLeagueFixtures(
        Guid competitionId,
        Guid userId)
    {
        await VerifyOwnershipAsync(
            competitionId,
            userId);

        bool ownsTransaction =
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
            // LOAD REGISTERED CLUBS
            // ==========================================

            var teams = await _context.CompetitionTeams
                .AsNoTracking()
                .Where(ct =>
                    ct.CompetitionId == competitionId)
                .Select(ct => ct.Team)
                .OrderBy(t => t.Name)
                .ToListAsync();

            if (teams.Count < 2)
            {
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return new List<Match>();
            }

            // ==========================================
            // LOAD EXISTING FIXTURES
            // ==========================================

            var existingMatches = await _context.Matches
                .Where(m =>
                    m.CompetitionId == competitionId)
                .OrderBy(m => m.MatchDay)
                .ThenBy(m => m.Id)
                .ToListAsync();

            // Every pair of clubs plays once.
            // Home/away order is ignored when checking
            // whether the pairing already exists.

            var existingPairs =
                new HashSet<(Guid First, Guid Second)>();

            foreach (var match in existingMatches)
            {
                existingPairs.Add(
                    NormalizePair(
                        match.HomeTeamId,
                        match.AwayTeamId));
            }

            // ==========================================
            // GENERATE ONLY MISSING PAIRINGS
            // ==========================================

            var newFixtures = new List<Match>();

            for (int i = 0; i < teams.Count; i++)
            {
                for (int j = i + 1; j < teams.Count; j++)
                {
                    var first = teams[i];
                    var second = teams[j];

                    var pair = NormalizePair(
                        first.Id,
                        second.Id);

                    if (existingPairs.Contains(pair))
                    {
                        continue;
                    }

                    bool firstIsHome =
                        Random.Shared.Next(2) == 0;

                    newFixtures.Add(new Match
                    {
                        Id = Guid.NewGuid(),

                        CompetitionId = competitionId,

                        HomeTeamId = firstIsHome
                            ? first.Id
                            : second.Id,

                        AwayTeamId = firstIsHome
                            ? second.Id
                            : first.Id,

                        MatchDay = 0,

                        KickoffUtc = null,

                        HomeScore = null,
                        AwayScore = null,

                        IsPlayed = false
                    });

                    existingPairs.Add(pair);
                }
            }

            // ==========================================
            // PRESERVE COMPLETED MATCHDAYS
            // ==========================================

            // Completed matches are never changed.
            // Unplayed fixtures can be reorganized
            // into matchdays without scheduling a club
            // more than once on the same day.

            var completedMatches = existingMatches
                .Where(m => m.IsPlayed)
                .ToList();

            var unplayedMatches = existingMatches
                .Where(m => !m.IsPlayed)
                .ToList();

            // Preserve matchday assignments for all
            // completed fixtures.

            var occupiedDays =
                new Dictionary<int, HashSet<Guid>>();

            foreach (var match in completedMatches)
            {
                if (!occupiedDays.TryGetValue(
                    match.MatchDay,
                    out var occupied))
                {
                    occupied = new HashSet<Guid>();

                    occupiedDays[match.MatchDay] =
                        occupied;
                }

                occupied.Add(match.HomeTeamId);
                occupied.Add(match.AwayTeamId);
            }

            // ==========================================
            // SCHEDULE UNPLAYED FIXTURES
            // ==========================================

            // Previously scheduled matches are processed
            // first, followed by newly generated matches.
            // This keeps the ordering relatively stable,
            // although unplayed matchdays may change.

            var matchesToSchedule = unplayedMatches
                .Concat(newFixtures)
                .ToList();

            foreach (var match in matchesToSchedule)
            {
                int day = FindAvailableMatchday(
                    match.HomeTeamId,
                    match.AwayTeamId,
                    occupiedDays);

                match.MatchDay = day;

                if (!occupiedDays.TryGetValue(
                    day,
                    out var occupied))
                {
                    occupied = new HashSet<Guid>();

                    occupiedDays[day] = occupied;
                }

                occupied.Add(match.HomeTeamId);
                occupied.Add(match.AwayTeamId);
            }

            // ==========================================
            // SAVE FIXTURES
            // ==========================================

            if (newFixtures.Count > 0)
            {
                _context.Matches.AddRange(newFixtures);
            }

            if (
                newFixtures.Count > 0 ||
                unplayedMatches.Count > 0
            )
            {
                await _context.SaveChangesAsync();
            }

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return newFixtures;
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
    // FIND AVAILABLE MATCHDAY
    // ==========================================

    private static int FindAvailableMatchday(
        Guid homeTeamId,
        Guid awayTeamId,
        Dictionary<int, HashSet<Guid>> occupiedDays)
    {
        int matchday = 1;

        while (true)
        {
            if (!occupiedDays.TryGetValue(
                matchday,
                out var occupiedTeams))
            {
                return matchday;
            }

            bool homeBusy =
                occupiedTeams.Contains(homeTeamId);

            bool awayBusy =
                occupiedTeams.Contains(awayTeamId);

            if (!homeBusy && !awayBusy)
            {
                return matchday;
            }

            matchday++;
        }
    }

    // ==========================================
    // NORMALIZE CLUB PAIR
    // ==========================================

    private static (Guid First, Guid Second)
        NormalizePair(
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
        // Compatibility with older callers.
        // This currently generates a single
        // round-robin, not a home-and-away league.

        return GenerateLeagueFixtures(competitionId);
    }
}
