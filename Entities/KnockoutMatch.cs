
namespace ChampionsLeagueSimulatorAPI.Entities;

public class KnockoutMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid KnockoutTieId { get; set; }

    public KnockoutTie KnockoutTie { get; set; } = null!;

    public int Leg { get; set; }

    public Guid HomeTeamId { get; set; }

    public Team HomeTeam { get; set; } = null!;

    public Guid AwayTeamId { get; set; }

    public Team AwayTeam { get; set; } = null!;

    public int? HomeScore { get; set; }

    public int? AwayScore { get; set; }

    public bool IsPlayed { get; set; } = false;

    public DateTime? KickoffUtc { get; set; }
}
