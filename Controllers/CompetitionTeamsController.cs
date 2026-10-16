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

    // ✅ Add a team to a competition
    [HttpPost]
    public async Task<IActionResult> AddTeam(AddCompetitionTeamRequest request)
    {
        // Check if the team is already in this competition
        var exists = await _context.CompetitionTeams
            .AnyAsync(ct =>
                ct.CompetitionId == request.CompetitionId &&
                ct.TeamId == request.TeamId);

        if (exists)
            return BadRequest("Team already exists in this competition.");

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
            CompetitionId = entry.CompetitionId,
            CompetitionName = entry.Competition?.Name ?? "",
            TeamId = entry.TeamId,
            TeamName = entry.Team?.Name ?? ""
        };

        return Ok(response);
    }

    // ✅ Get all teams in a competition
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
            CompetitionId = ct.CompetitionId,
            CompetitionName = ct.Competition?.Name ?? "",
            TeamId = ct.TeamId,
            TeamName = ct.Team?.Name ?? ""
        }).ToList();

        return Ok(response);
    }

    // ✅ Remove a team from a competition
    [HttpDelete]
    public async Task<IActionResult> RemoveTeam(Guid competitionId, Guid teamId)
    {
        var entry = await _context.CompetitionTeams
            .FirstOrDefaultAsync(ct =>
                ct.CompetitionId == competitionId && ct.TeamId == teamId);

        if (entry == null)
            return NotFound();

        _context.CompetitionTeams.Remove(entry);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}