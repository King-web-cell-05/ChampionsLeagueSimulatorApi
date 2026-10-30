
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Npgsql;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly BrevoEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    private static readonly TimeSpan CodeLifetime =
        TimeSpan.FromMinutes(10);

    private static readonly TimeSpan ResendCooldown =
        TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ResendWindow =
        TimeSpan.FromHours(1);

    private const int MaxAttempts = 5;
    private const int MaxResendsPerWindow = 5;

    public AuthController(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        BrevoEmailService emailService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _emailService = emailService;
        _logger = logger;
    }

    // ==========================================
    // REGISTER
    // POST api/Auth/register
    // ==========================================

    [AllowAnonymous]
    [EnableRateLimiting("auth-register")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await _context.Users.AnyAsync(
            u => u.Email.ToLower() == email,
            cancellationToken);

        if (exists)
        {
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            IsEmailVerified = false,
            CreatedAtUtc = DateTime.UtcNow,
            EmailVerificationFailedAttempts = 0,
            EmailVerificationResendCount = 0
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        var code = GenerateCode();

        SetVerificationCode(user, code);

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pg &&
                  pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }

        try
        {
            await _emailService.SendVerificationCodeAsync(
                user.Email,
                user.FullName,
                code,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Verification email submission failed for user {UserId}",
                user.Id);

            return StatusCode(503, new
            {
                message =
                    "Your account was created, but we could not " +
                    "send the verification code. Please try " +
                    "resending the code shortly.",
                requiresEmailVerification = true
            });
        }

        return StatusCode(201, new
        {
            message =
                "Registration successful. Check your email " +
                "for a 6-digit verification code.",
            requiresEmailVerification = true,
            user = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.IsEmailVerified
            }
        });
    }

    // ==========================================
    // VERIFY EMAIL
    // POST api/Auth/verify-email
    // ==========================================

    [AllowAnonymous]
    [EnableRateLimiting("auth-verify")]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Lock the matching PostgreSQL row.
        // Concurrent verification and resend requests
        // must wait until this transaction finishes.
        var user = await GetLockedUserAsync(
            email,
            cancellationToken);

        if (user == null)
        {
            return BadRequest(new
            {
                message = "Invalid or expired verification code."
            });
        }

        if (user.IsEmailVerified)
        {
            return Ok(new
            {
                message = "Email is already verified.",
                isEmailVerified = true
            });
        }

        var now = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(
                user.EmailVerificationTokenHash) ||
            !user.EmailVerificationTokenExpiresAtUtc.HasValue ||
            now >= user.EmailVerificationTokenExpiresAtUtc.Value)
        {
            return BadRequest(new
            {
                message =
                    "Verification code expired or unavailable. " +
                    "Request a new code."
            });
        }

        if (user.EmailVerificationFailedAttempts >= MaxAttempts)
        {
            return BadRequest(new
            {
                message =
                    "Too many incorrect attempts. " +
                    "Request a new verification code."
            });
        }

        var submittedHash = HashCode(user.Id, request.Code);

        var submittedBytes =
            Convert.FromHexString(submittedHash);

        var expectedBytes =
            Convert.FromHexString(
                user.EmailVerificationTokenHash);

        var valid = CryptographicOperations.FixedTimeEquals(
            submittedBytes,
            expectedBytes);

        if (!valid)
        {
            user.EmailVerificationFailedAttempts++;

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return BadRequest(new
            {
                message =
                    user.EmailVerificationFailedAttempts >= MaxAttempts
                        ? "Too many incorrect attempts. Request a new code."
                        : "Invalid verification code."
            });
        }

        // Consume the code inside the locked transaction.
        user.IsEmailVerified = true;
        user.EmailVerifiedAtUtc = now;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAtUtc = null;
        user.EmailVerificationFailedAttempts = 0;

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            message =
                "Email verified successfully. You can now log in.",
            isEmailVerified = true
        });
    }

    // ==========================================
    // RESEND VERIFICATION CODE
    // POST api/Auth/resend-verification-code
    // ==========================================

    [AllowAnonymous]
    [EnableRateLimiting("auth-resend")]
    [HttpPost("resend-verification-code")]
    public async Task<IActionResult> ResendVerificationCode(
        [FromBody] ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var genericResponse = new
        {
            message =
                "If this email belongs to an unverified account, " +
                "a verification code will be sent when eligible."
        };

        string? codeToSend = null;
        string? recipientEmail = null;
        string? recipientName = null;
        Guid? recipientUserId = null;

        // Keep the transaction short.
        // Do not send emails while holding database locks.
        await using (var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken))
        {
            var user = await GetLockedUserAsync(
                email,
                cancellationToken);

            if (user == null || user.IsEmailVerified)
            {
                return Ok(genericResponse);
            }

            var now = DateTime.UtcNow;

            // ======================================
            // RESET EXPIRED RESEND QUOTA WINDOW
            // ======================================

            if (!user.EmailVerificationResendWindowStartedAtUtc
                    .HasValue ||
                now - user.EmailVerificationResendWindowStartedAtUtc
                    .Value >= ResendWindow)
            {
                user.EmailVerificationResendWindowStartedAtUtc = now;
                user.EmailVerificationResendCount = 0;
            }

            // ======================================
            // PERSISTENT PER-ACCOUNT RESEND LIMIT
            // ======================================

            if (user.EmailVerificationResendCount >=
                MaxResendsPerWindow)
            {
                return Ok(genericResponse);
            }

            // ======================================
            // 60-SECOND RESEND COOLDOWN
            // ======================================

            if (user.EmailVerificationTokenExpiresAtUtc.HasValue)
            {
                var lastIssuedAt =
                    user.EmailVerificationTokenExpiresAtUtc.Value
                        .Subtract(CodeLifetime);

                if (now - lastIssuedAt < ResendCooldown)
                {
                    return Ok(genericResponse);
                }
            }

            // ======================================
            // GENERATE NEW VERIFICATION CODE
            // ======================================

            var code = GenerateCode();

            SetVerificationCode(user, code);

            user.EmailVerificationResendCount++;

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            codeToSend = code;
            recipientEmail = user.Email;
            recipientName = user.FullName;
            recipientUserId = user.Id;
        }

        // Send only after committing the new code.
        if (codeToSend != null &&
            recipientEmail != null &&
            recipientName != null)
        {
            try
            {
                await _emailService.SendVerificationCodeAsync(
                    recipientEmail,
                    recipientName,
                    codeToSend,
                    cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Verification resend failed for user {UserId}",
                    recipientUserId);
            }
        }

        return Ok(genericResponse);
    }

    // ==========================================
    // LOGIN
    // POST api/Auth/login
    // ==========================================

    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == email,
                cancellationToken);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var passwordResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (!user.IsEmailVerified)
        {
            return StatusCode(403, new
            {
                message =
                    "Please verify your email address before logging in.",
                requiresEmailVerification = true
            });
        }

        if (passwordResult ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    request.Password);

            await _context.SaveChangesAsync(cancellationToken);
        }

        var expiresAtUtc = DateTime.UtcNow.AddHours(1);

        var token = GenerateJwtToken(user, expiresAtUtc);

        return Ok(new
        {
            accessToken = token,
            tokenType = "Bearer",
            expiresAtUtc,
            user = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.IsEmailVerified
            }
        });
    }

    // ==========================================
    // POSTGRESQL ROW-LEVEL LOCKING
    // ==========================================

    private async Task<User?> GetLockedUserAsync(
        string email,
        CancellationToken cancellationToken)
    {
        // Must be called inside an active transaction.
        // FOR UPDATE locks the user row until commit
        // or rollback.
        //
        // FromSqlInterpolated parameterizes the email,
        // preventing SQL injection.

        return await _context.Users
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM "Users"
                WHERE LOWER("Email") = {email}
                FOR UPDATE
                """)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    // ==========================================
    // VERIFICATION CODE GENERATION
    // ==========================================

    private static string GenerateCode()
    {
        var number = RandomNumberGenerator.GetInt32(
            0,
            1_000_000);

        return number.ToString("D6");
    }

    // ==========================================
    // STORE VERIFICATION CODE HASH
    // ==========================================

    private void SetVerificationCode(User user, string code)
    {
        user.EmailVerificationTokenHash =
            HashCode(user.Id, code);

        user.EmailVerificationTokenExpiresAtUtc =
            DateTime.UtcNow.Add(CodeLifetime);

        user.EmailVerificationFailedAttempts = 0;
    }

    // ==========================================
    // HASH VERIFICATION CODE
    // ==========================================

    private string HashCode(Guid userId, string code)
    {
        var keySetting = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is not configured.");

        var key = Convert.FromBase64String(keySetting);

        using var hmac = new HMACSHA256(key);

        var data = Encoding.UTF8.GetBytes(
            $"email-verification:{userId:N}:{code}");

        return Convert.ToHexString(
            hmac.ComputeHash(data));
    }

    // ==========================================
    // JWT GENERATION
    // ==========================================

    private string GenerateJwtToken(
        User user,
        DateTime expiresAtUtc)
    {
        var keySetting = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is not configured.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is not configured.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is not configured.");

        var signingKey = new SymmetricSecurityKey(
            Convert.FromBase64String(keySetting));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new Claim(
                ClaimTypes.Name,
                user.FullName),

            new Claim(
                ClaimTypes.Email,
                user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}

// ==========================================
// REQUEST DTOs
// ==========================================

public sealed class VerifyEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        @"^\d{6}$",
        ErrorMessage = "Enter a valid 6-digit code.")]
    public string Code { get; set; } = string.Empty;
}

public sealed class ResendVerificationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
