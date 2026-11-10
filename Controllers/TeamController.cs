
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

    // GET: api/Teams/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTeam(Guid id)
    {
        var team = await _context.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team is null)
        {
            return NotFound(new
            {
                message = "Team not found."
            });
        }

        return Ok(team);
    }

    // POST: api/Teams
    [HttpPost]
    public async Task<IActionResult> CreateTeam(
        [FromBody] CreateTeamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Country))
        {
            return BadRequest(new
            {
                message = "Team name and country are required."
            });
        }

        if (!IsValidLogoUrl(request.LogoUrl))
        {
            return BadRequest(new
            {
                message = "Logo URL must be a valid HTTPS URL."
            });
        }

        var name = request.Name.Trim();
        var country = request.Country.Trim();

        var duplicateExists = await _context.Teams
            .AnyAsync(t =>
                t.Name.ToLower() == name.ToLower());

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
            Coefficient = request.Coefficient,
            LogoUrl = NormalizeLogoUrl(request.LogoUrl)
        };

        _context.Teams.Add(team);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetTeam),
            new { id = team.Id },
            team
        );
    }

    // PUT: api/Teams/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTeam(
        Guid id,
        [FromBody] CreateTeamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Country))
        {
            return BadRequest(new
            {
                message = "Team name and country are required."
            });
        }

        if (!IsValidLogoUrl(request.LogoUrl))
        {
            return BadRequest(new
            {
                message = "Logo URL must be a valid HTTPS URL."
            });
        }

        var team = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team is null)
        {
            return NotFound(new
            {
                message = "Team not found."
            });
        }

        var name = request.Name.Trim();
        var country = request.Country.Trim();

        var duplicateExists = await _context.Teams
            .AnyAsync(t =>
                t.Id != id &&
                t.Name.ToLower() == name.ToLower());

        if (duplicateExists)
        {
            return Conflict(new
            {
                message = "Another team already has this name."
            });
        }

        team.Name = name;
        team.Country = country;
        team.Coefficient = request.Coefficient;
        team.LogoUrl = NormalizeLogoUrl(request.LogoUrl);

        await _context.SaveChangesAsync();

        return Ok(team);
    }

    // Validates optional logo URLs.
    private static bool IsValidLogoUrl(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return true;
        }

        return Uri.TryCreate(
            logoUrl.Trim(),
            UriKind.Absolute,
            out var uri
        ) && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static string? NormalizeLogoUrl(
        string? logoUrl)
    {
        return string.IsNullOrWhiteSpace(logoUrl)
            ? null
            : logoUrl.Trim();
    }
}
