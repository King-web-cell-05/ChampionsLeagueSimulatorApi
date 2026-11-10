
using System.ComponentModel.DataAnnotations;

namespace ChampionsLeagueSimulatorAPI.DTOs;

public class CreateTeamRequest
{
    [Required(ErrorMessage = "Team name is required.")]
    [MaxLength(100, ErrorMessage = "Team name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [MaxLength(80, ErrorMessage = "Country cannot exceed 80 characters.")]
    public string Country { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0",
        "200",
        ErrorMessage = "Coefficient must be between 0 and 200."
    )]
    public decimal Coefficient { get; set; }

    // Optional official football club logo
    [MaxLength(500, ErrorMessage = "Logo URL cannot exceed 500 characters.")]
    [Url(ErrorMessage = "Please provide a valid logo URL.")]
    public string? LogoUrl { get; set; }
}
