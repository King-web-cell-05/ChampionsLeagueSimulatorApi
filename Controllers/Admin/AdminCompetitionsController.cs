
using ChampionsLeagueSimulatorAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/competitions")]
public class AdminCompetitionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminCompetitionsController(
        AppDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // GET ALL PLATFORM COMPETITIONS
    // ==========================================

    // GET: api/admin/competitions
    [HttpGet]
    public async Task<IActionResult> GetCompetitions(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Competitions
            .AsNoTracking()
            .AsQueryable();

        // Search by competition name or season.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Season, $"%{term}%"));
        }

        var totalCount = await query.CountAsync();

        // Prevent integer overflow in pagination.
        var skip = (long)(page - 1) * pageSize;

        var items = await query
            .OrderByDescending(c => c.CreatedDate)
            .ThenBy(c => c.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Season,
                c.OwnerId,
                c.CreatedDate,

                OwnerName = c.Owner != null
                    ? c.Owner.FullName
                    : null,

                OwnerEmail = c.Owner != null
                    ? c.Owner.Email
                    : null,

                ClubCount = _context.CompetitionTeams
                    .Count(ct =>
                        ct.CompetitionId == c.Id),

                TotalMatches = _context.Matches
                    .Count(m =>
                        m.CompetitionId == c.Id),

                PlayedMatches = _context.Matches
                    .Count(m =>
                        m.CompetitionId == c.Id &&
                        m.IsPlayed)
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize)
        });
    }

    // ==========================================
    // GET COMPETITION DETAILS
    // ==========================================

    // GET: api/admin/competitions/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCompetition(
        Guid id)
    {
        var competition = await _context.Competitions
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Season,
                c.OwnerId,
                c.CreatedDate,

                OwnerName = c.Owner != null
                    ? c.Owner.FullName
                    : null,

                OwnerEmail = c.Owner != null
                    ? c.Owner.Email
                    : null,

                ClubCount = _context.CompetitionTeams
                    .Count(ct =>
                        ct.CompetitionId == c.Id),

                TotalMatches = _context.Matches
                    .Count(m =>
                        m.CompetitionId == c.Id),

                PlayedMatches = _context.Matches
                    .Count(m =>
                        m.CompetitionId == c.Id &&
                        m.IsPlayed)
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
    // GET COMPETITION CLUBS
    // ==========================================

    // GET: api/admin/competitions/{id}/clubs
    [HttpGet("{id:guid}/clubs")]
    public async Task<IActionResult> GetCompetitionClubs(
        Guid id)
    {
        var exists = await _context.Competitions
            .AsNoTracking()
            .AnyAsync(c => c.Id == id);

        if (!exists)
        {
            return NotFound(new
            {
                message = "Competition not found."
            });
        }

        var clubs = await _context.CompetitionTeams
            .AsNoTracking()
            .Where(ct => ct.CompetitionId == id)
            .OrderBy(ct => ct.Team.Name)
            .Select(ct => new
            {
                ct.Team.Id,
                ct.Team.Name,
                ct.Team.Country,
                ct.Team.IsDefault
            })
            .ToListAsync();

        return Ok(clubs);
    }
}
