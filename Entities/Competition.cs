
using System.ComponentModel.DataAnnotations;

namespace ChampionsLeagueSimulatorAPI.Entities;

public class Competition
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Season { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // ==========================================
    // COMPETITION OWNERSHIP
    // ==========================================

    // Nullable temporarily for safe migration.
    // New competitions must receive an owner.
    public Guid? OwnerId { get; set; }

    // Navigation property to the creator.
    public User? Owner { get; set; }

    // ==========================================
    // EXISTING RELATIONSHIPS
    // ==========================================

    public ICollection<Match> Matches { get; set; }
        = new List<Match>();
}
