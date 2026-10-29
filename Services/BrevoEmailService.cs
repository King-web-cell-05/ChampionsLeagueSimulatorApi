
using System.ComponentModel.DataAnnotations;
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

    private const int MaxAttempts = 5;

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
            CreatedAtUtc = DateTime.UtcNow
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
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == email,
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
                message = "Email is already verified."
            });
        }

        if (user.EmailVerificationTokenHash == null ||
            user.EmailVerificationTokenExpiresAtUtc == null ||
            DateTime.UtcNow >
                user.EmailVerificationTokenExpiresAtUtc)
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

            return BadRequest(new
            {
                message =
                    user.EmailVerificationFailedAttempts >= MaxAttempts
                        ? "Too many incorrect attempts. Request a new code."
                        : "Invalid verification code."
            });
        }

        user.IsEmailVerified = true;
        user.EmailVerifiedAtUtc = DateTime.UtcNow;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAtUtc = null;
        user.EmailVerificationFailedAttempts = 0;

        await _context.SaveChangesAsync(cancellationToken);

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

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == email,
                cancellationToken);

        if (user == null || user.IsEmailVerified)
        {
            return Ok(genericResponse);
        }

        if (user.EmailVerificationTokenExpiresAtUtc.HasValue)
        {
            var lastIssuedAt =
                user.EmailVerificationTokenExpiresAtUtc.Value
                    .Subtract(CodeLifetime);

            if (DateTime.UtcNow - lastIssuedAt < ResendCooldown)
            {
                return Ok(genericResponse);
            }
        }

        var code = GenerateCode();

        SetVerificationCode(user, code);

        await _context.SaveChangesAsync(cancellationToken);

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
                "Verification resend failed for user {UserId}",
                user.Id);
        }

        return Ok(genericResponse);
    }

    // ==========================================
    // LOGIN
    // POST api/Auth/login
    // ==========================================

    [AllowAnonymous]
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
    // VERIFICATION HELPERS
    // ==========================================

    private static string GenerateCode()
    {
        var number = RandomNumberGenerator.GetInt32(
            0,
            1_000_000);

        return number.ToString("D6");
    }

    private void SetVerificationCode(User user, string code)
    {
        user.EmailVerificationTokenHash =
            HashCode(user.Id, code);

        user.EmailVerificationTokenExpiresAtUtc =
            DateTime.UtcNow.Add(CodeLifetime);

        user.EmailVerificationFailedAttempts = 0;
    }

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
    [RegularExpression(@"^\d{6}$",
        ErrorMessage = "Enter a valid 6-digit code.")]
    public string Code { get; set; } = string.Empty;
}

public sealed class ResendVerificationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
