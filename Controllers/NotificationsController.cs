using System.Security.Claims;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Page must be positive and pageSize must be between 1 and 100." });

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await query.CountAsync(n => !n.IsRead, cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Type,
                n.IsRead,
                n.CreatedAtUtc,
                n.ReadAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new { items, page, pageSize, totalCount, unreadCount });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var unreadCount = await _context.Notifications
            .CountAsync(n => n.UserId == userId.Value && !n.IsRead, cancellationToken);

        return Ok(new { unreadCount });
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value, cancellationToken);

        if (notification is null)
            return NotFound(new { message = "Notification not found." });

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { notification.Id, notification.IsRead, notification.ReadAtUtc });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var now = DateTime.UtcNow;
        var updatedCount = await _context.Notifications
            .Where(n => n.UserId == userId.Value && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAtUtc, now), cancellationToken);

        return Ok(new { updatedCount, unreadCount = 0 });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var deletedCount = await _context.Notifications
            .Where(n => n.Id == id && n.UserId == userId.Value)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount == 0)
            return NotFound(new { message = "Notification not found." });

        return NoContent();
    }
}
