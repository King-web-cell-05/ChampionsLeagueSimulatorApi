
namespace ChampionsLeagueSimulatorAPI.Entities;

public class KnockoutMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // ==========================================
    // KNOCKOUT TIE RELATIONSHIP
    // ==========================================

    public Guid KnockoutTieId { get; set; }

    public KnockoutTie KnockoutTie { get; set; } = null!;

    public int Leg { get; set; }

    // ==========================================
    // PARTICIPATING TEAMS
    // ==========================================

    public Guid HomeTeamId { get; set; }

    public Team HomeTeam { get; set; } = null!;

    public Guid AwayTeamId { get; set; }

    public Team AwayTeam { get; set; } = null!;

    // ==========================================
    // REGULATION-TIME SCORE (90 MINUTES)
    // ==========================================

    public int? HomeScore { get; set; }

    public int? AwayScore { get; set; }

    // ==========================================
    // EXTRA TIME
    // ==========================================

    // Whether extra time was played.
    public bool WentToExtraTime { get; set; } = false;

    // Goals scored during extra time only.
    public int? HomeExtraTimeScore { get; set; }

    public int? AwayExtraTimeScore { get; set; }

    // ==========================================
    // PENALTY SHOOTOUT
    // ==========================================

    public bool WentToPenalties { get; set; } = false;

    // Shootout scores, separate from match goals.
    public int? HomePenaltyScore { get; set; }

    public int? AwayPenaltyScore { get; set; }

    // ==========================================
    // MATCH STATUS
    // ==========================================

    public bool IsPlayed { get; set; } = false;

    public DateTime? KickoffUtc { get; set; }
}
