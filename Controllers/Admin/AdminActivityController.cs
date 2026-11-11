
using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/activity")]
public sealed class AdminActivityController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminActivityController(AppDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // GET PLATFORM ACTIVITY
    // ==========================================
    // GET /api/admin/activity
    // GET /api/admin/activity?type=user
    // GET /api/admin/activity?type=competition
    // GET /api/admin/activity?days=30&page=1&pageSize=10

    [HttpGet]
    public async Task<IActionResult> GetActivity(
        [FromQuery] string type = "all",
        [FromQuery] int days = 30,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        type = (type ?? "all").Trim().ToLowerInvariant();

        if (type != "all" &&
            type != "user" &&
            type != "competition")
        {
            return BadRequest(new
            {
                message = "Invalid activity type."
            });
        }

        if (days < 1 || days > 365)
        {
            return BadRequest(new
            {
                message = "Days must be between 1 and 365."
            });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var now = DateTime.UtcNow;
        var from = now.AddDays(-days);

        // ==========================================
        // USER REGISTRATION EVENTS
        // ==========================================

        var userEvents = _context.Users
            .AsNoTracking()
            .Where(u => u.CreatedAtUtc >= from &&
                        u.CreatedAtUtc <= now)
            .Select(u => new ActivityItem
            {
                Id = u.Id,
                Type = "user",
                Title = "New user registered",
                Description = u.FullName,
                ActorName = u.FullName,
                ActorEmail = u.Email,
                EntityId = u.Id,
                CreatedAtUtc = u.CreatedAtUtc
            });

        // ==========================================
        // COMPETITION CREATION EVENTS
        // ==========================================

        var competitionEvents = _context.Competitions
            .AsNoTracking()
            .Where(c => c.CreatedDate >= from &&
                        c.CreatedDate <= now)
            .Select(c => new ActivityItem
            {
                Id = c.Id,
                Type = "competition",
                Title = "Competition created",
                Description = c.Name,
                ActorName = c.Owner != null
                    ? c.Owner.FullName
                    : null,
                ActorEmail = c.Owner != null
                    ? c.Owner.Email
                    : null,
                EntityId = c.Id,
                CreatedAtUtc = c.CreatedDate
            });

        // ==========================================
        // FILTER AND COMBINE EVENTS
        // ==========================================

        IQueryable<ActivityItem> query;

        if (type == "user")
        {
            query = userEvents;
        }
        else if (type == "competition")
        {
            query = competitionEvents;
        }
        else
        {
            query = userEvents.Concat(competitionEvents);
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var skip = (long)(page - 1) * pageSize;

        if (skip > int.MaxValue)
        {
            return BadRequest(new
            {
                message = "Requested page is too large."
            });
        }

        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenBy(a => a.Type)
            .ThenBy(a => a.Id)
            .Skip((int)skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // ==========================================
        // ACTIVITY STATISTICS
        // ==========================================

        var registrations = await _context.Users
            .AsNoTracking()
            .CountAsync(
                u => u.CreatedAtUtc >= from &&
                     u.CreatedAtUtc <= now,
                cancellationToken);

        var newCompetitions =
            await _context.Competitions
                .AsNoTracking()
                .CountAsync(
                    c => c.CreatedDate >= from &&
                         c.CreatedDate <= now,
                    cancellationToken);

        var playedMatches = await _context.Matches
            .AsNoTracking()
            .CountAsync(
                m => m.IsPlayed,
                cancellationToken);

        var pendingMatches = await _context.Matches
            .AsNoTracking()
            .CountAsync(
                m => !m.IsPlayed,
                cancellationToken);

        return Ok(new
        {
            generatedAtUtc = now,
            filters = new
            {
                type,
                days
            },
            summary = new
            {
                registrations,
                competitionsCreated = newCompetitions,
                recordedEvents = registrations +
                                 newCompetitions,
                playedLeagueMatches = playedMatches,
                pendingLeagueMatches = pendingMatches
            },
            items,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize)
        });
    }

    // ==========================================
    // INTERNAL ACTIVITY PROJECTION
    // ==========================================

    private sealed class ActivityItem
    {
        public Guid Id { get; set; }

        public string Type { get; set; } = "";

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string? ActorName { get; set; }

        public string? ActorEmail { get; set; }

        public Guid EntityId { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
