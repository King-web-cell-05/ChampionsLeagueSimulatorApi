using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Services;

public class SimulationService
{
    private readonly AppDbContext _context;
    private readonly Random _random = new();

    private readonly TableService _tableService;

    public SimulationService(AppDbContext context, TableService tableService)
    {
        _context = context;
        _tableService = tableService;
    }

    public async Task<SimulationResult> SimulateCompetition(Guid competitionId)
    {
        // 1️⃣ Load all matches for this competition, including teams
        var matches = await _context.Matches
            .Where(m => m.CompetitionId == competitionId)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.MatchDay)
            .ToListAsync();

        // 2️⃣ Simulate unplayed matches
        foreach (var match in matches.Where(m => !m.IsPlayed))
        {
            match.HomeScore = GenerateGoals();
            match.AwayScore = GenerateGoals();
            match.IsPlayed = true;
        }

        await _context.SaveChangesAsync();

        // 3️⃣ Generate updated standings
        var standings = await _tableService.GenerateTable(competitionId);

        // 4️⃣ Return combined result
        return new SimulationResult
        {
            Matches = matches.Select(m => new MatchResultDto
            {
                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam?.Name ?? "",
                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam?.Name ?? "",
                HomeScore = m.HomeScore ?? 0,
                AwayScore = m.AwayScore ?? 0,
                MatchDay = m.MatchDay
            }).ToList(),
            Standings = standings.Select(s => new StandingDto
            {
                TeamId = s.TeamId,
                TeamName = s.TeamName ?? "",
                Played = s.Played,
                Wins = s.Wins,
                Draws = s.Draws,
                Losses = s.Losses,
                GoalsFor = s.GoalsFor,
                GoalsAgainst = s.GoalsAgainst,
                GoalDifference = s.GoalDifference,
                Points = s.Points
            }).ToList()
        };
    }

    private int GenerateGoals()
    {
        return _random.Next(0, 5); // 0-4 goals per team
    }
}

// DTOs for API response
public class SimulationResult
{
    public List<MatchResultDto> Matches { get; set; } = new();
    public List<StandingDto> Standings { get; set; } = new();
}

public class MatchResultDto
{
    public Guid HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = "";
    public Guid AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = "";
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int MatchDay { get; set; }
}

public class StandingDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public int Played { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public int Points { get; set; }
}