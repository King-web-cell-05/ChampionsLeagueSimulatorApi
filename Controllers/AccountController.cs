
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AccountController(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    // ==========================================
    // GET AUTHENTICATED USER ID
    // ==========================================

    private Guid? GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    // ==========================================
    // GET PROFILE
    // GET: api/Account/profile
    // ==========================================

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication session."
            });
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.IsEmailVerified,
                u.EmailVerifiedAtUtc,
                u.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                message = "Account not found."
            });
        }

        return Ok(user);
    }

    // ==========================================
    // UPDATE PROFILE
    // PUT: api/Account/profile
    // ==========================================

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateAccountProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication session."
            });
        }

        var fullName = request.FullName?.Trim();

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
                message =
                    "Full name cannot exceed 100 characters."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Id == userId.Value,
                cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                message = "Account not found."
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
                user.IsEmailVerified,
                user.CreatedAtUtc
            }
        });
    }

    // ==========================================
    // CHANGE PASSWORD
    // PUT: api/Account/password
    // ==========================================

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangeAccountPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication session."
            });
        }

        if (request.NewPassword !=
            request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "New passwords do not match."
            });
        }

        if (request.CurrentPassword ==
            request.NewPassword)
        {
            return BadRequest(new
            {
                message =
                    "New password must differ from the current password."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Id == userId.Value,
                cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                message = "Account not found."
            });
        }

        var verificationResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.CurrentPassword);

        if (verificationResult ==
            PasswordVerificationResult.Failed)
        {
            return BadRequest(new
            {
                message = "Current password is incorrect."
            });
        }

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                request.NewPassword);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Password updated successfully."
        });
    }
}

// ==========================================
// UPDATE PROFILE REQUEST
// ==========================================

public sealed class UpdateAccountProfileRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;
}

// ==========================================
// CHANGE PASSWORD REQUEST
// ==========================================

public sealed class ChangeAccountPasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
}
