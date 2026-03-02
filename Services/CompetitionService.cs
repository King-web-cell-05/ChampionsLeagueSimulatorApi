using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class CompetitionService
{
    private readonly AppDbContext _context;

    public CompetitionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task RemoveTeamFromCompetition(Guid competitionId, Guid teamId)
    {
        // Remove from CompetitionTeams
        var entry = await _context.CompetitionTeams
            .FirstOrDefaultAsync(ct =>
                ct.CompetitionId == competitionId &&
                ct.TeamId == teamId);

        if (entry == null)
            throw new Exception("Team not found in this competition.");

        _context.CompetitionTeams.Remove(entry);

        // Remove all matches involving this team
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId &&
                       (m.HomeTeamId == teamId || m.AwayTeamId == teamId))
            .ToListAsync();

        _context.Matches.RemoveRange(matches);

        await _context.SaveChangesAsync();
    }
}