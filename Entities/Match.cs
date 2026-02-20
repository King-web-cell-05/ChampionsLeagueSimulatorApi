namespace ChampionsLeagueSimulatorAPI.Entities;

public class Match
{
    public Guid Id { get; set; }

    public Guid CompetitionId { get; set; }

    public Competition Competition { get; set; }

    public Guid HomeTeamId { get; set; }

    public Team HomeTeam { get; set; }

    public Guid AwayTeamId { get; set; }

    public Team AwayTeam { get; set; }

    public int? HomeScore { get; set; }

    public int? AwayScore { get; set; }

    public bool IsPlayed { get; set; } = false;

    public int MatchDay { get; set; }
}