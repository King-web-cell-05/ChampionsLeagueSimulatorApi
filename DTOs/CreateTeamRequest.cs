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

    [Range(typeof(decimal), "0", "200")]
    public decimal Coefficient { get; set; }
}