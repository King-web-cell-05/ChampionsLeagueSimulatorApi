
using System.ComponentModel.DataAnnotations;

namespace ChampionsLeagueSimulatorAPI.Entities;

public class Team
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    public decimal Coefficient { get; set; }

    // ==========================================
    // CLUB CREST
    // ==========================================

    public string? LogoUrl { get; set; }

    // ==========================================
    // DEFAULT CLUB OR PRIVATE CLUB
    // ==========================================

    // True = shared default club.
    // False = user-created private club.
    public bool IsDefault { get; set; } = false;

    // ==========================================
    // CLUB OWNERSHIP
    // ==========================================

    // Null for default clubs.
    // Required for private clubs.
    public Guid? OwnerId { get; set; }

    // Navigation property to the club creator.
    public User? Owner { get; set; }
}
