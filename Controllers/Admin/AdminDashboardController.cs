using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminDashboardController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/admin/dashboard/overview
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        var totalUsers = await _context.Users.AsNoTracking()
            .CountAsync(cancellationToken);
        var verifiedUsers = await _context.Users.AsNoTracking()
            .CountAsync(u => u.IsEmailVerified, cancellationToken);
        var suspendedUsers = await _context.Users.AsNoTracking()
            .CountAsync(u => u.IsSuspended, cancellationToken);
        var activeUsers = await _context.Users.AsNoTracking()
            .CountAsync(u => u.IsEmailVerified && !u.IsSuspended, cancellationToken);
        var newUsersLast30Days = await _context.Users.AsNoTracking()
            .CountAsync(u => u.CreatedAtUtc >= thirtyDaysAgo, cancellationToken);

        var totalCompetitions = await _context.Competitions.AsNoTracking()
            .CountAsync(cancellationToken);
        var competitionsCreatedLast30Days = await _context.Competitions.AsNoTracking()
            .CountAsync(c => c.CreatedDate >= thirtyDaysAgo, cancellationToken);

        // A competition is pending if at least one recorded league-phase match is unplayed.
        var competitionsWithPendingMatches = await _context.Competitions.AsNoTracking()
            .CountAsync(c => _context.Matches.Any(m =>
                m.CompetitionId == c.Id && !m.IsPlayed), cancellationToken);

        // This includes competitions with zero recorded matches; it is not a "completed" count.
        var competitionsWithoutPendingMatches = totalCompetitions - competitionsWithPendingMatches;

        var totalClubs = await _context.Teams.AsNoTracking()
            .CountAsync(cancellationToken);
        var defaultClubs = await _context.Teams.AsNoTracking()
            .CountAsync(t => t.IsDefault, cancellationToken);
        var privateClubs = totalClubs - defaultClubs;

        var totalLeagueMatches = await _context.Matches.AsNoTracking()
            .CountAsync(cancellationToken);
        var playedLeagueMatches = await _context.Matches.AsNoTracking()
            .CountAsync(m => m.IsPlayed, cancellationToken);
        var pendingLeagueMatches = totalLeagueMatches - playedLeagueMatches;

        var recentRegistrations = await _context.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAtUtc)
            .ThenBy(u => u.Id)
            .Take(5)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.IsEmailVerified,
                u.IsSuspended,
                u.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var recentCompetitions = await _context.Competitions.AsNoTracking()
            .OrderByDescending(c => c.CreatedDate)
            .ThenBy(c => c.Id)
            .Take(5)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Season,
                c.OwnerId,
                OwnerName = c.Owner != null ? c.Owner.FullName : null,
                c.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            generatedAtUtc = now,
            users = new
            {
                total = totalUsers,
                active = activeUsers,
                verified = verifiedUsers,
                unverified = totalUsers - verifiedUsers,
                suspended = suspendedUsers,
                newLast30Days = newUsersLast30Days
            },
            competitions = new
            {
                total = totalCompetitions,
                newLast30Days = competitionsCreatedLast30Days,
                withPendingLeagueMatches = competitionsWithPendingMatches,
                withoutPendingLeagueMatches = competitionsWithoutPendingMatches
            },
            clubs = new
            {
                total = totalClubs,
                defaultClubs,
                privateClubs
            },
            leagueMatches = new
            {
                total = totalLeagueMatches,
                played = playedLeagueMatches,
                pending = pendingLeagueMatches
            },
            recentRegistrations,
            recentCompetitions
        });
    }
}
