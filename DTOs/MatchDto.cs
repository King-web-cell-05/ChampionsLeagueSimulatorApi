namespace ChampionsLeagueSimulatorAPI.DTOs;

public class MatchDto
{
    public Guid Id { get; set; }
    public Guid CompetitionId { get; set; }
    public string CompetitionName { get; set; }
    public Guid HomeTeamId { get; set; }
    public string HomeTeamName { get; set; }
    public Guid AwayTeamId { get; set; }
    public string AwayTeamName { get; set; }
    public int? HomeScore { get; set; }  // <-- nullable
    public int? AwayScore { get; set; }  // <-- nullable
    public bool IsPlayed { get; set; }
    public int MatchDay { get; set; }
}