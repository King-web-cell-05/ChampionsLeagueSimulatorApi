using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class DrawService
{
    private readonly AppDbContext _context;

    public DrawService(AppDbContext context)
    {
        _context = context;
    }

    public async Task GenerateDraw(Guid competitionId)
    {
        // Get all teams for the competition
        var teams = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Select(ct => ct.Team)
            .ToListAsync();

        if (teams.Count < 2)
            throw new Exception("Not enough teams to generate a draw");

        // If odd number of teams, add a dummy "bye" team
        bool hasBye = false;
        if (teams.Count % 2 != 0)
        {
            teams.Add(new Team { Id = Guid.Empty, Name = "BYE" });
            hasBye = true;
        }

        var matches = new List<Match>();
        int totalTeams = teams.Count;
        int totalRounds = totalTeams - 1;
        int matchesPerRound = totalTeams / 2;

        // Create a copy to rotate
        var shuffled = new List<Team>(teams);

        for (int round = 0; round < totalRounds; round++)
        {
            for (int i = 0; i < matchesPerRound; i++)
            {
                var home = shuffled[i];
                var away = shuffled[totalTeams - 1 - i];

                // Skip matches with BYE
                if (home.Id == Guid.Empty || away.Id == Guid.Empty)
                    continue;

                matches.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    HomeTeamId = home.Id,
                    AwayTeamId = away.Id,
                    MatchDay = round + 1,
                    IsPlayed = false
                });
            }

            // Rotate teams except the first one
            var last = shuffled.Last();
            shuffled.RemoveAt(shuffled.Count - 1);
            shuffled.Insert(1, last);
        }

        _context.Matches.AddRange(matches);
        await _context.SaveChangesAsync();
    }
}