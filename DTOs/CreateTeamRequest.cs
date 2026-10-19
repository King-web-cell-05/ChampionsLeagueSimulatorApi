
using System.ComponentModel.DataAnnotations;

namespace ChampionsLeagueSimulatorAPI.DTOs;

public class CreateTeamRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Country { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int Coefficient { get; set; }
}
