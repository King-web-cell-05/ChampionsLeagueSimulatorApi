using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompetitionController : ControllerBase
{
    private readonly AppDbContext _context;

    public CompetitionController(AppDbContext context)
    {
        _context = context;
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
}