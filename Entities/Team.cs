namespace ChampionsLeagueSimulatorAPI.Entities;

public class Team
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public int Coefficient { get; set; }
}
