
using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public static class TeamLogoSeeder
{
    private static readonly Dictionary<string, string> Logos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Previously configured clubs
            ["Arsenal"] = "https://crests.football-data.org/57.png",
            ["Aston Villa"] = "https://crests.football-data.org/58.png",
            ["Barcelona"] = "https://crests.football-data.org/81.png",
            ["Borussia Dortmund"] = "https://crests.football-data.org/4.png",
            ["Club Brugge"] = "https://crests.football-data.org/851.png",
            ["Feyenoord"] = "https://crests.football-data.org/675.png",
            ["Lille"] = "https://crests.football-data.org/521.png",
            ["Liverpool"] = "https://crests.football-data.org/64.png",
            ["Manchester City"] = "https://crests.football-data.org/65.png",
            ["Napoli"] = "https://crests.football-data.org/113.png",
            ["Paris Saint-Germain"] = "https://crests.football-data.org/524.png",
            ["PSV Eindhoven"] = "https://crests.football-data.org/674.png",
            ["RB Leipzig"] = "https://crests.football-data.org/721.png",
            ["Real Madrid"] = "https://crests.football-data.org/86.png",
            ["Shakhtar Donetsk"] = "https://crests.football-data.org/1887.png",
            ["Sporting CP"] = "https://crests.football-data.org/498.png",

            // Additional clubs with exact database names
            ["Atlético de Madrid"] = "https://crests.football-data.org/78.png",
            ["Bayern München"] = "https://crests.football-data.org/5.png",
            ["Inter"] = "https://crests.football-data.org/108.png",
            ["Manchester United"] = "https://crests.football-data.org/66.png",
            ["Porto"] = "https://crests.football-data.org/503.png",
            ["Real Betis"] = "https://crests.football-data.org/90.png",
            ["Roma"] = "https://crests.football-data.org/100.png",
            ["VfB Stuttgart"] = "https://crests.football-data.org/10.png",
            ["Villarreal"] = "https://crests.football-data.org/94.png",
            ["Lens"] = "https://crests.football-data.org/546.png"
        };

    public static async Task<int> UpdateMissingLogosAsync(
        AppDbContext context)
    {
        var teams = await context.Teams.ToListAsync();

        int updated = 0;

        foreach (var team in teams)
        {
            if (!string.IsNullOrWhiteSpace(team.LogoUrl))
                continue;

            if (!Logos.TryGetValue(
                    team.Name.Trim(),
                    out var logoUrl))
            {
                Console.WriteLine(
                    $"Logo mapping missing: {team.Name}");
                continue;
            }

            team.LogoUrl = logoUrl;
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync();
        }

        Console.WriteLine(
            $"FootballSim: {updated} new logo URLs saved.");

        return updated;
    }
}
