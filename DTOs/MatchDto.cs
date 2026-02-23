namespace ChampionsLeagueSimulatorAPI.DTOs;

public class MatchDto
{
    public Guid Id { get; set; }

    public int MatchDay { get; set; }

    public string HomeTeam { get; set; } = string.Empty;

    public string AwayTeam { get; set; } = string.Empty;

    public int? HomeScore { get; set; }

    public int? AwayScore { get; set; }

    public bool IsPlayed { get; set; }
}