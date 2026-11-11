
using System;
using System.ComponentModel.DataAnnotations;

namespace ChampionsLeagueSimulatorAPI.Entities;

public class MatchStatistics
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MatchId { get; set; }

    public Match Match { get; set; } = null!;

    public int HomePossession { get; set; }
    public int AwayPossession { get; set; }

    public int HomeShots { get; set; }
    public int AwayShots { get; set; }

    public int HomeShotsOnTarget { get; set; }
    public int AwayShotsOnTarget { get; set; }

    public int HomeCorners { get; set; }
    public int AwayCorners { get; set; }

    public int HomeFouls { get; set; }
    public int AwayFouls { get; set; }

    public int HomeYellowCards { get; set; }
    public int AwayYellowCards { get; set; }

    public int HomeRedCards { get; set; }
    public int AwayRedCards { get; set; }

    public double HomeExpectedGoals { get; set; }
    public double AwayExpectedGoals { get; set; }
}
