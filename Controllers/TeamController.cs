using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _context;

    public TeamsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTeam(CreateTeamRequest request)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = request.Name
        };

        _context.Teams.Add(team);

        await _context.SaveChangesAsync();

        return Ok(team);
    }

    [HttpGet]
    public async Task<IActionResult> GetTeams()
    {
        return Ok(await _context.Teams.ToListAsync());
    }
}