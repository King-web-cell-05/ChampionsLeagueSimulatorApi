namespace ChampionsLeagueSimulatorAPI.DTOs;

public class AddCompetitionTeamRequest
{
    public Guid CompetitionId { get; set; }

    public Guid TeamId { get; set; }
}