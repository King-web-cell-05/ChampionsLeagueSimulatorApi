
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorApi.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminUsersController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET /api/admin/users
    // Supports pagination, searching, and status filtering
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new
            {
                message =
                    "Page must be at least 1 and pageSize must be between 1 and 100."
            });
        }

        var normalizedStatus = status?.Trim().ToLowerInvariant();

        if (normalizedStatus is not (
            null or
            "all" or
            "active" or
            "suspended" or
            "unverified"))
        {
            return BadRequest(new
            {
                message =
                    "Status must be all, active, suspended, or unverified."
            });
        }

        var query = _context.Users.AsNoTracking();

        // Search users by full name or email.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            if (term.Length > 200)
            {
                return BadRequest(new
                {
                    message =
                        "Search must be 200 characters or fewer."
                });
            }

            var pattern = $"%{EscapeLike(term)}%";

            query = query.Where(u =>
                EF.Functions.ILike(
                    u.FullName,
                    pattern,
                    "\\"
                ) ||
                EF.Functions.ILike(
                    u.Email,
                    pattern,
                    "\\"
                ));
        }

        // Filter accounts by status.
        query = normalizedStatus switch
        {
            "active" => query.Where(u =>
                !u.IsSuspended &&
                u.IsEmailVerified),

            "suspended" => query.Where(u =>
                u.IsSuspended),

            "unverified" => query.Where(u =>
                !u.IsEmailVerified),

            _ => query
        };

        var totalCount = await query.CountAsync(
            cancellationToken
        );

        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .ThenBy(u => u.Id)
            .Skip((int)Math.Min(
                (long)(page - 1) * pageSize,
                int.MaxValue
            ))
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Role,
                u.IsEmailVerified,
                u.IsSuspended,
                u.SuspendedAtUtc,
                u.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pageSize
        );

        return Ok(new
        {
            items = users,
            page,
            pageSize,
            totalCount,
            totalPages
        });
    }

    // =====================================================
    // GET /api/admin/users/stats
    // Returns administrator user statistics
    // =====================================================

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var last30DaysUtc = todayUtc.AddDays(-29);

        var users = _context.Users.AsNoTracking();

        var totalUsers = await users.CountAsync(
            cancellationToken
        );

        var activeUsers = await users.CountAsync(
            u => !u.IsSuspended && u.IsEmailVerified,
            cancellationToken
        );

        var suspendedUsers = await users.CountAsync(
            u => u.IsSuspended,
            cancellationToken
        );

        var unverifiedUsers = await users.CountAsync(
            u => !u.IsEmailVerified,
            cancellationToken
        );

        var newUsersLast30Days = await users.CountAsync(
            u => u.CreatedAtUtc >= last30DaysUtc,
            cancellationToken
        );

        return Ok(new
        {
            totalUsers,
            activeUsers,
            suspendedUsers,
            unverifiedUsers,
            newUsersLast30Days
        });
    }

    // =====================================================
    // GET /api/admin/users/{id}
    // Returns detailed information about one user
    // =====================================================

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Role,
                u.IsEmailVerified,
                u.EmailVerifiedAtUtc,
                u.IsSuspended,
                u.SuspendedAtUtc,
                u.CreatedAtUtc,

                competitionCount =
                    _context.Competitions.Count(
                        c => c.OwnerId == u.Id
                    ),

                privateTeamCount =
                    _context.Teams.Count(
                        t => t.OwnerId == u.Id
                    )
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        return Ok(user);
    }

    // =====================================================
    // PATCH /api/admin/users/{id}/suspend
    // Suspends a user and invalidates existing JWTs
    // =====================================================

    [HttpPatch("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Verify administrator identity.
        if (!TryGetAdminId(out var adminId))
        {
            return Unauthorized(new
            {
                message =
                    "Unable to verify administrator identity."
            });
        }

        // Prevent self-suspension.
        if (id == adminId)
        {
            return BadRequest(new
            {
                message =
                    "You cannot suspend your own account."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Id == id,
                cancellationToken
            );

        if (user is null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        // Protect administrator accounts.
        if (user.Role == "Admin")
        {
            return StatusCode(403, new
            {
                message =
                    "Administrator accounts cannot be suspended through this endpoint."
            });
        }

        // Avoid modifying an already suspended account.
        if (user.IsSuspended)
        {
            return Ok(new
            {
                message = "Account is already suspended."
            });
        }

        // Mark account as suspended.
        user.IsSuspended = true;
        user.SuspendedAtUtc = DateTime.UtcNow;

        // SECURITY:
        // Increment SecurityVersion to invalidate
        // previously issued JWTs for this user.
        user.SecurityVersion++;

        await _context.SaveChangesAsync(
            cancellationToken
        );

        return Ok(new
        {
            message = "Account suspended successfully.",
            user.Id,
            user.IsSuspended,
            user.SuspendedAtUtc
        });
    }

    // =====================================================
    // PATCH /api/admin/users/{id}/reactivate
    // Reactivates a suspended user account
    // =====================================================

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Id == id,
                cancellationToken
            );

        if (user is null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        // Administrator accounts are protected.
        if (user.Role == "Admin")
        {
            return StatusCode(403, new
            {
                message =
                    "Administrator accounts cannot be changed through this endpoint."
            });
        }

        if (!user.IsSuspended)
        {
            return Ok(new
            {
                message = "Account is already active."
            });
        }

        // Reactivate account.
        user.IsSuspended = false;
        user.SuspendedAtUtc = null;

        // Do not reset SecurityVersion.
        // Previously revoked JWTs must remain invalid.

        await _context.SaveChangesAsync(
            cancellationToken
        );

        return Ok(new
        {
            message = "Account reactivated successfully.",
            user.Id,
            user.IsSuspended
        });
    }

    // =====================================================
    // HELPER: Extract authenticated administrator ID
    // =====================================================

    private bool TryGetAdminId(out Guid adminId)
    {
        return Guid.TryParse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            ),
            out adminId
        );
    }

    // =====================================================
    // HELPER: Escape PostgreSQL ILIKE wildcards
    // =====================================================

    private static string EscapeLike(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
