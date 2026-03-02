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

    [HttpPost]
    public async Task<IActionResult> CreateCompetition(CreateCompetitionRequest request)
    {
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Name = request.Name
        };

        _context.Competitions.Add(competition);
        await _context.SaveChangesAsync();

        return Ok(competition);
    }

    // ✅ DELETE TEAM FROM COMPETITION
    [HttpDelete("{competitionId}/team/{teamId}")]
    public async Task<IActionResult> RemoveTeam(Guid competitionId, Guid teamId)
    {
        await _competitionService.RemoveTeamFromCompetition(competitionId, teamId);
        return Ok("Team removed from competition successfully.");
    }
}