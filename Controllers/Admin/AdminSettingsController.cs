
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/settings")]
public sealed class AdminSettingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AdminSettingsController(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    // ==========================================
    // GET ADMIN PROFILE
    // ==========================================

    // GET /api/admin/settings/profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid session."
            });
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(u =>
                u.Id == userId.Value &&
                u.Role == "Admin" &&
                !u.IsSuspended)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Role,
                u.IsEmailVerified,
                u.EmailVerifiedAtUtc,
                u.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return StatusCode(403, new
            {
                message = "Administrator account unavailable."
            });
        }

        return Ok(user);
    }

    // ==========================================
    // UPDATE ADMIN PROFILE
    // ==========================================

    // PUT /api/admin/settings/profile
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateAdminProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid session."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Id == userId.Value &&
                     u.Role == "Admin" &&
                     !u.IsSuspended,
                cancellationToken);

        if (user is null)
        {
            return StatusCode(403, new
            {
                message = "Administrator account unavailable."
            });
        }

        var fullName = request.FullName.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (fullName.Length > 100)
        {
            return BadRequest(new
            {
                message = "Full name cannot exceed 100 characters."
            });
        }

        user.FullName = fullName;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Profile updated successfully.",
            user = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.IsEmailVerified,
                user.EmailVerifiedAtUtc,
                user.CreatedAtUtc
            }
        });
    }

    // ==========================================
    // CHANGE ADMIN PASSWORD
    // ==========================================

    // PUT /api/admin/settings/password
    [HttpPut("password")]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangeAdminPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid session."
            });
        }

        // ======================================
        // BEGIN DATABASE TRANSACTION
        // ======================================

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Lock the administrator row.
        // This prevents concurrent password changes
        // from overwriting each other.

        var user = await _context.Users
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM "Users"
                WHERE "Id" = {userId.Value}
                FOR UPDATE
                """)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null ||
            user.Role != "Admin" ||
            user.IsSuspended ||
            !user.IsEmailVerified)
        {
            return StatusCode(403, new
            {
                message = "Administrator account unavailable."
            });
        }

        // ======================================
        // REVALIDATE SESSION VERSION
        // ======================================

        // Authentication may have completed before
        // another request changed the password.
        // Recheck while holding the row lock.

        var tokenVersionValue =
            User.FindFirstValue("security_version");

        if (!int.TryParse(
                tokenVersionValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var tokenVersion) ||
            tokenVersion != user.SecurityVersion)
        {
            return Unauthorized(new
            {
                message =
                    "Session expired. Please sign in again."
            });
        }

        // ======================================
        // VERIFY CURRENT PASSWORD
        // ======================================

        var verification =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.CurrentPassword);

        if (verification == PasswordVerificationResult.Failed)
        {
            return BadRequest(new
            {
                message = "Current password is incorrect."
            });
        }

        // ======================================
        // VALIDATE NEW PASSWORD
        // ======================================

        if (request.CurrentPassword == request.NewPassword)
        {
            return BadRequest(new
            {
                message =
                    "Your new password must be different from your current password."
            });
        }

        if (user.SecurityVersion == int.MaxValue)
        {
            throw new InvalidOperationException(
                "Security version limit reached.");
        }

        // ======================================
        // UPDATE PASSWORD HASH
        // ======================================

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                request.NewPassword);

        // ======================================
        // REVOKE ALL PREVIOUS JWT SESSIONS
        // ======================================

        user.SecurityVersion++;

        // ======================================
        // SAVE CHANGES ATOMICALLY
        // ======================================

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            message =
                "Password changed successfully. " +
                "All previous sessions have been revoked. " +
                "Please sign in again."
        });
    }

    // ==========================================
    // CURRENT AUTHENTICATED USER
    // ==========================================

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }
}

// ==========================================
// REQUEST MODELS
// ==========================================

public sealed class UpdateAdminProfileRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;
}

public sealed class ChangeAdminPasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(12)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
