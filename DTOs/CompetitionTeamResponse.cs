namespace ChampionsLeagueSimulatorAPI.DTOs;

public class CompetitionTeamResponse
{
  

    public Guid CompetitionId { get; set; }
    public string CompetitionName { get; set; } = string.Empty;

    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
}