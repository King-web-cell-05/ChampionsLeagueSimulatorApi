using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
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
    public async Task<IActionResult> CreateCompetition(Competition competition)
    {
        _context.Competitions.Add(competition);

        await _context.SaveChangesAsync();

        return Ok(competition);
    }

    [HttpGet]
    public async Task<IActionResult> GetCompetitions()
    {
        return Ok(await _context.Competitions.ToListAsync());
    }
}