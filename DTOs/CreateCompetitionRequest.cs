
using System.ComponentModel.DataAnnotations;

namespace ChampionsLeagueSimulatorAPI.DTOs;

public class CreateCompetitionRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
