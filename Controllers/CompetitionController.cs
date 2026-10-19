
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompetitionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CompetitionService _competitionService;

    public CompetitionController(
        AppDbContext context,
        CompetitionService competitionService)
    {
        _context = context;
        _competitionService = competitionService;
    }

    // GET: api/Competition
    [HttpGet]
    public async Task<IActionResult> GetCompetitions()
    {
        var competitions = await _context.Competitions
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();

        return Ok(competitions);
    }

    // POST: api/Competition
    [HttpPost]
    public async Task<IActionResult> CreateCompetition(
        CreateCompetitionRequest request)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                message = "Competition name is required."
            });
        }

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Name = name
        };

        _context.Competitions.Add(competition);
        await _context.SaveChangesAsync();

        return Ok(competition);
    }

    // DELETE: api/Competition/{competitionId}/team/{teamId}
    [HttpDelete("{competitionId}/team/{teamId}")]
    public async Task<IActionResult> RemoveTeam(
        Guid competitionId,
        Guid teamId)
    {
        await _competitionService.RemoveTeamFromCompetition(
            competitionId,
            teamId);

        return Ok("Team removed from competition successfully.");
    }
}
