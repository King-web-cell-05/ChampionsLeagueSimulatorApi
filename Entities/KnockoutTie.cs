
namespace ChampionsLeagueSimulatorAPI.Entities;

public class KnockoutTie
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompetitionId { get; set; }

    public Competition Competition { get; set; } = null!;

    public KnockoutRound Round { get; set; }

    public int TieNumber { get; set; }

    public Guid HomeTeamId { get; set; }

    public Team HomeTeam { get; set; } = null!;

    public Guid AwayTeamId { get; set; }

    public Team AwayTeam { get; set; } = null!;

    public Guid? WinnerTeamId { get; set; }

    public Team? WinnerTeam { get; set; }

    public bool IsCompleted { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<KnockoutMatch> Matches { get; set; }
        = new List<KnockoutMatch>();
}
