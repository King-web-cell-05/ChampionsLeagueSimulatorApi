namespace ChampionsLeagueSimulatorAPI.Entities;

public class Competition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Season { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<Match> Matches { get; set; } = new List<Match>();
}