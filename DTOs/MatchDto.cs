namespace ChampionsLeagueSimulatorAPI.DTOs;

public class MatchDto
{
    public Guid Id { get; set; }

    public Guid CompetitionId { get; set; }
    public string CompetitionName { get; set; } = string.Empty;

    public Guid HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;

    public Guid AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    public bool IsPlayed { get; set; }
    public int MatchDay { get; set; }
}