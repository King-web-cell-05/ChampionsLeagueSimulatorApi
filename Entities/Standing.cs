namespace ChampionsLeagueSimulatorAPI.Entities;

public class Standing
{
    public Guid Id { get; set; }

    public Guid CompetitionId { get; set; }

    public Guid TeamId { get; set; }

    public Team Team { get; set; }

    public int Played { get; set; }

    public int Wins { get; set; }

    public int Draws { get; set; }

    public int Losses { get; set; }

    public int GoalsFor { get; set; }

    public int GoalsAgainst { get; set; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    public int Points { get; set; }
}