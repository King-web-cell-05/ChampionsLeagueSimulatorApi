namespace ChampionsLeagueSimulatorAPI.Entities;

public class CompetitionTeam
{
    public Guid Id { get; set; }

    public Guid CompetitionId { get; set; }

    public Competition Competition { get; set; }

    public Guid TeamId { get; set; }

    public Team Team { get; set; }
}