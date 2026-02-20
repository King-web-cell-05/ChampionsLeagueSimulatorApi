using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.DTOs;

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


    // ADD THIS METHOD HERE
    [HttpPost]
    public async Task<IActionResult> AddTeam(AddCompetitionTeamRequest request)
    {
        var entry = new CompetitionTeam
        {
            CompetitionId = request.CompetitionId,
            TeamId = request.TeamId
        };

        _context.CompetitionTeams.Add(entry);

        await _context.SaveChangesAsync();

        return Ok(entry);
    }

}