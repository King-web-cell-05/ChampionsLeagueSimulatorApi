
using System.ComponentModel.DataAnnotations;

namespace ChampionsLeagueSimulatorAPI.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    // =====================================
    // EMAIL VERIFICATION
    // =====================================

    public bool IsEmailVerified { get; set; } = false;

    public DateTime? EmailVerifiedAtUtc { get; set; }

    [MaxLength(64)]
    public string? EmailVerificationTokenHash { get; set; }

    public DateTime? EmailVerificationTokenExpiresAtUtc { get; set; }

    // =====================================
    // ACCOUNT INFORMATION
    // =====================================

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
