using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompetitionTeamsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CompetitionTeamsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> AddTeam(AddCompetitionTeamRequest request)
    {
        // Create the join entity
        var entry = new CompetitionTeam
        {
            CompetitionId = request.CompetitionId,
            TeamId = request.TeamId
        };

        _context.CompetitionTeams.Add(entry);
        await _context.SaveChangesAsync();

        // Load related entities
        await _context.Entry(entry).Reference(ct => ct.Competition).LoadAsync();
        await _context.Entry(entry).Reference(ct => ct.Team).LoadAsync();

        // Map to DTO
        var response = new CompetitionTeamResponse
        {
            Id = entry.Id,
            CompetitionId = entry.CompetitionId,
            CompetitionName = entry.Competition?.Name ?? "",
            TeamId = entry.TeamId,
            TeamName = entry.Team?.Name ?? ""
        };

        return Ok(response);
    }

    [HttpGet("{competitionId}")]
    public async Task<IActionResult> GetTeamsByCompetition(Guid competitionId)
    {
        var entries = await _context.CompetitionTeams
            .Where(ct => ct.CompetitionId == competitionId)
            .Include(ct => ct.Team)
            .Include(ct => ct.Competition)
            .ToListAsync();

        var response = entries.Select(ct => new CompetitionTeamResponse
        {
            Id = ct.Id,
            CompetitionId = ct.CompetitionId,
            CompetitionName = ct.Competition?.Name ?? "",
            TeamId = ct.TeamId,
            TeamName = ct.Team?.Name ?? ""
        }).ToList();

        return Ok(response);
    }
}