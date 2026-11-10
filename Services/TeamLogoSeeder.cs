
using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public static class TeamLogoSeeder
{
    private static readonly Dictionary<string, string> Logos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arsenal"] =
                "https://crests.football-data.org/57.png",

            ["Real Madrid"] =
                "https://crests.football-data.org/86.png",

            ["Bayern Munich"] =
                "https://crests.football-data.org/5.png",

            ["Barcelona"] =
                "https://crests.football-data.org/81.png",

            ["Manchester City"] =
                "https://crests.football-data.org/65.png",

            ["Liverpool"] =
                "https://crests.football-data.org/64.png",

            ["Chelsea"] =
                "https://crests.football-data.org/61.png",

            ["Inter Milan"] =
                "https://crests.football-data.org/108.png",

            ["Juventus"] =
                "https://crests.football-data.org/109.png",

            ["AC Milan"] =
                "https://crests.football-data.org/98.png",

            ["Napoli"] =
                "https://crests.football-data.org/113.png",

            ["Borussia Dortmund"] =
                "https://crests.football-data.org/4.png",

            ["RB Leipzig"] =
                "https://crests.football-data.org/721.png",

            ["Paris Saint-Germain"] =
                "https://crests.football-data.org/524.png",

            ["Atletico Madrid"] =
                "https://crests.football-data.org/78.png",

            ["Bayer Leverkusen"] =
                "https://crests.football-data.org/3.png",

            ["Atalanta"] =
                "https://crests.football-data.org/102.png",

            ["Benfica"] =
                "https://crests.football-data.org/1903.png",

            ["Sporting CP"] =
                "https://crests.football-data.org/498.png",

            ["PSV Eindhoven"] =
                "https://crests.football-data.org/674.png",

            ["Feyenoord"] =
                "https://crests.football-data.org/675.png",

            ["Celtic"] =
                "https://crests.football-data.org/732.png",

            ["Club Brugge"] =
                "https://crests.football-data.org/851.png",

            ["Shakhtar Donetsk"] =
                "https://crests.football-data.org/1887.png",

            ["Red Bull Salzburg"] =
                "https://crests.football-data.org/1877.png",

            ["Dinamo Zagreb"] =
                "https://crests.football-data.org/755.png",

            ["Monaco"] =
                "https://crests.football-data.org/548.png",

            ["Lille"] =
                "https://crests.football-data.org/521.png",

            ["Bologna"] =
                "https://crests.football-data.org/103.png",

            ["Girona"] =
                "https://crests.football-data.org/298.png",

            ["Stuttgart"] =
                "https://crests.football-data.org/10.png",

            ["Aston Villa"] =
                "https://crests.football-data.org/58.png"
        };

    public static async Task<int> UpdateMissingLogosAsync(
        AppDbContext context)
    {
        var teams = await context.Teams.ToListAsync();

        var updated = 0;

        foreach (var team in teams)
        {
            if (!string.IsNullOrWhiteSpace(team.LogoUrl))
                continue;

            if (!Logos.TryGetValue(team.Name.Trim(), out var logoUrl))
                continue;

            team.LogoUrl = logoUrl;
            updated++;
        }

        if (updated > 0)
            await context.SaveChangesAsync();

        return updated;
    }
}
