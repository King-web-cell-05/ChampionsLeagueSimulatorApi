using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/competitions")]
public sealed class AdminCompetitionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminCompetitionsController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/admin/competitions?page=1&pageSize=10&search=...
    [HttpGet]
    public async Task<IActionResult> GetCompetitions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Page must be at least 1 and pageSize must be between 1 and 100." });

        var query = _context.Competitions.AsNoTracking();
        var term = search?.Trim();

        if (!string.IsNullOrEmpty(term))
        {
            if (term.Length > 150)
                return BadRequest(new { message = "Search must not exceed 150 characters." });

            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Season, $"%{term}%") ||
                (c.Owner != null &&
                    (EF.Functions.ILike(c.Owner.FullName, $"%{term}%") ||
                     EF.Functions.ILike(c.Owner.Email, $"%{term}%"))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Check overflow before computing the SQL offset.
        var skip = ((long)page - 1) * pageSize;
        if (skip > int.MaxValue)
            return BadRequest(new { message = "Page number is too large." });

        var items = await query
            .OrderByDescending(c => c.CreatedDate)
            .ThenBy(c => c.Id)
            .Skip((int)skip)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Season,
                c.CreatedDate,
                c.OwnerId,
                OwnerName = c.Owner != null ? c.Owner.FullName : null,
                OwnerEmail = c.Owner != null ? c.Owner.Email : null,
                TeamCount = _context.CompetitionTeams.Count(ct => ct.CompetitionId == c.Id),
                MatchCount = _context.Matches.Count(m => m.CompetitionId == c.Id),
                PlayedMatchCount = _context.Matches.Count(m => m.CompetitionId == c.Id && m.IsPlayed)
            })
            .ToListAsync(cancellationToken);

        return Ok(new { items, page, pageSize, totalCount, totalPages });
    }

    // GET /api/admin/competitions/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var totalCompetitions = await _context.Competitions.CountAsync(cancellationToken);
        var totalCreators = await _context.Competitions
            .Where(c => c.OwnerId != null)
            .Select(c => c.OwnerId)
            .Distinct()
            .CountAsync(cancellationToken);
        var totalParticipations = await _context.CompetitionTeams.CountAsync(cancellationToken);
        var totalMatches = await _context.Matches.CountAsync(cancellationToken);
        var playedMatches = await _context.Matches.CountAsync(m => m.IsPlayed, cancellationToken);

        return Ok(new
        {
            totalCompetitions,
            totalCreators,
            totalParticipations,
            totalMatches,
            playedMatches,
            pendingMatches = totalMatches - playedMatches
        });
    }

    // GET /api/admin/competitions/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCompetition(Guid id, CancellationToken cancellationToken)
    {
        var competition = await _context.Competitions
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Season,
                c.CreatedDate,
                c.OwnerId,
                OwnerName = c.Owner != null ? c.Owner.FullName : null,
                OwnerEmail = c.Owner != null ? c.Owner.Email : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (competition is null)
            return NotFound(new { message = "Competition not found." });

        var teams = await _context.CompetitionTeams
            .AsNoTracking()
            .Where(ct => ct.CompetitionId == id)
            .OrderBy(ct => ct.Team.Name)
            .Select(ct => new
            {
                ct.TeamId,
                TeamName = ct.Team.Name,
                ct.GroupName
            })
            .ToListAsync(cancellationToken);

        var matchCount = await _context.Matches.CountAsync(m => m.CompetitionId == id, cancellationToken);
        var playedMatchCount = await _context.Matches.CountAsync(m => m.CompetitionId == id && m.IsPlayed, cancellationToken);
        var knockoutTieCount = await _context.KnockoutTies.CountAsync(t => t.CompetitionId == id, cancellationToken);

        return Ok(new
        {
            competition.Id,
            competition.Name,
            competition.Season,
            competition.CreatedDate,
            competition.OwnerId,
            competition.OwnerName,
            competition.OwnerEmail,
            teamCount = teams.Count,
            teams,
            matchCount,
            playedMatchCount,
            pendingMatchCount = matchCount - playedMatchCount,
            knockoutTieCount
        });
    }
}
