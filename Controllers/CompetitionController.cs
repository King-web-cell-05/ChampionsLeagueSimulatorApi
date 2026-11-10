
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Authorize]
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

    // ==========================================
    // AUTHENTICATED USER
    // ==========================================

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    // ==========================================
    // GET MY COMPETITIONS
    // GET: api/Competition
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> GetCompetitions()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var competitions = await _context.Competitions
            .AsNoTracking()
            .Where(c => c.OwnerId == userId.Value)
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Season,
                c.CreatedDate
            })
            .ToListAsync();

        return Ok(competitions);
    }

    // ==========================================
    // GET MY COMPETITION BY ID
    // GET: api/Competition/{id}
    // ==========================================

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCompetition(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var competition = await _context.Competitions
            .AsNoTracking()
            .Where(c =>
                c.Id == id &&
                c.OwnerId == userId.Value)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Season,
                c.CreatedDate
            })
            .FirstOrDefaultAsync();

        if (competition is null)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        return Ok(competition);
    }

    // ==========================================
    // CREATE PRIVATE COMPETITION
    // POST: api/Competition
    // ==========================================

    [HttpPost]
    public async Task<IActionResult> CreateCompetition(
        [FromBody] CreateCompetitionRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        if (request is null ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Competition name is required."
            });
        }

        var name = request.Name.Trim();

        if (name.Length > 150)
        {
            return BadRequest(new
            {
                message =
                    "Competition name must not exceed 150 characters."
            });
        }

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Name = name,

            // New competitions are private.
            OwnerId = userId.Value,

            CreatedDate = DateTime.UtcNow
        };

        _context.Competitions.Add(competition);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            competition.Id,
            competition.Name,
            competition.Season,
            competition.CreatedDate
        });
    }

    // ==========================================
    // REMOVE TEAM FROM MY COMPETITION
    // DELETE: api/Competition/{competitionId}/team/{teamId}
    // ==========================================

    [HttpDelete("{competitionId:guid}/team/{teamId:guid}")]
    public async Task<IActionResult> RemoveTeam(
        Guid competitionId,
        Guid teamId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        // Verify competition ownership first.
        var ownsCompetition = await _context.Competitions
            .AsNoTracking()
            .AnyAsync(c =>
                c.Id == competitionId &&
                c.OwnerId == userId.Value);

        if (!ownsCompetition)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        // Verify the team is available to this user.
        var canAccessTeam = await _context.Teams
            .AsNoTracking()
            .AnyAsync(t =>
                t.Id == teamId &&
                (t.IsDefault ||
                 t.OwnerId == userId.Value));

        if (!canAccessTeam)
        {
            return NotFound(new
            {
                message = "Team not found."
            });
        }

        // Keep the existing service behavior.
        await _competitionService.RemoveTeamFromCompetition(
            competitionId,
            teamId);

        return Ok(new
        {
            message =
                "Team removed from competition successfully."
        });
    }
}
