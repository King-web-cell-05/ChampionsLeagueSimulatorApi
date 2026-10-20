
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class SimulationService
{
    private readonly AppDbContext _context;
    private readonly TableService _tableService;
    private readonly DrawService _drawService;

    public SimulationService(
        AppDbContext context,
        TableService tableService,
        DrawService drawService)
    {
        _context = context;
        _tableService = tableService;
        _drawService = drawService;
    }

    // Generate fixtures using the randomized UEFA-style draw.
    public async Task GenerateLeagueFixtures(Guid competitionId)
    {
        await _drawService.GenerateLeagueFixtures(competitionId);
    }

    // Simulate all fixtures that have not yet been played.
    public async Task<SimulationResponseDto> SimulateCompetition(
        Guid competitionId)
    {
        if (competitionId == Guid.Empty)
            throw new ArgumentException("Invalid competition ID.");

        var matches = await _context.Matches
            .Where(m =>
                m.CompetitionId == competitionId &&
                !m.IsPlayed)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Competition)
            .OrderBy(m => m.MatchDay)
            .ToListAsync();

        if (matches.Count == 0)
        {
            var hasFixtures = await _context.Matches
                .AnyAsync(m => m.CompetitionId == competitionId);

            if (!hasFixtures)
            {
                throw new InvalidOperationException(
                    "No fixtures exist for this competition. Generate the draw before simulating matches.");
            }
        }

        var random = Random.Shared;

        foreach (var match in matches)
        {
            match.HomeScore = random.Next(0, 5);
            match.AwayScore = random.Next(0, 5);
            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();

        var standings = await _tableService.GenerateTable(competitionId);

        return new SimulationResponseDto
        {
            Matches = matches.Select(m => new MatchDto
            {
                Id = m.Id,
                CompetitionId = m.CompetitionId,
                CompetitionName = m.Competition?.Name ?? string.Empty,
                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam?.Name ?? string.Empty,
                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam?.Name ?? string.Empty,
                KickoffUtc = m.KickoffUtc,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,
                IsPlayed = m.IsPlayed,
                MatchDay = m.MatchDay
            }).ToList(),
            Standings = standings
        };
    }
}
