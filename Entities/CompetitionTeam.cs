namespace ChampionsLeagueSimulatorAPI.Entities;

public class CompetitionTeam
{
    public Guid CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;

    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;
}