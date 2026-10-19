
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.AspNetCore.Mvc;
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

    // GET: api/Teams
    [HttpGet]
    public async Task<IActionResult> GetTeams()
    {
        var teams = await _context.Teams
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync();

        return Ok(teams);
    }

    // POST: api/Teams
    [HttpPost]
    public async Task<IActionResult> CreateTeam(CreateTeamRequest request)
    {
        var name = request.Name.Trim();
        var country = request.Country.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                message = "Team name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            return BadRequest(new
            {
                message = "Country is required."
            });
        }

        var duplicateExists = await _context.Teams
            .AnyAsync(t => t.Name.ToLower() == name.ToLower());

        if (duplicateExists)
        {
            return Conflict(new
            {
                message = "A team with this name already exists."
            });
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name,
            Country = country,
            Coefficient = request.Coefficient
        };

        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        return Created("/api/Teams", team);
    }
}
