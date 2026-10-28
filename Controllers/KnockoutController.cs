using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnockoutController : ControllerBase
{
    private readonly KnockoutDrawService _drawService;
    private readonly KnockoutSimulationService _simulationService;
    private readonly KnockoutBatchSimulationService _batchSimulationService;
    private readonly AppDbContext _context;

    public KnockoutController(
        KnockoutDrawService drawService,
        KnockoutSimulationService simulationService,
        KnockoutBatchSimulationService batchSimulationService,
        AppDbContext context)
    {
        _drawService = drawService;
        _simulationService = simulationService;
        _batchSimulationService = batchSimulationService;
        _context = context;
    }

    // ==========================================
    // GENERATE PLAY-OFF DRAW
    // ==========================================

    [HttpPost("{competitionId:guid}/playoffs/draw")]
    public async Task<IActionResult> GeneratePlayoffDraw(
        Guid competitionId)
    {
        try
        {
            var ties = await _drawService
                .GeneratePlayoffDraw(competitionId);

            return Ok(new
            {
                Message = "Play-off draw generated successfully.",
                TotalTies = ties.Count,
                TotalMatches = ties.Sum(t => t.Matches.Count),

                Ties = ties.Select(t => new
                {
                    t.Id,
                    Round = t.Round.ToString(),
                    t.TieNumber,
                    SeededTeamId = t.HomeTeamId,
                    UnseededTeamId = t.AwayTeamId,

                    Matches = t.Matches
                        .OrderBy(m => m.Leg)
                        .Select(m => new
                        {
                            m.Id,
                            m.Leg,
                            m.HomeTeamId,
                            m.AwayTeamId
                        })
                })
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                Message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                Message = ex.Message
            });
        }
    }

    // ==========================================
    // GET PLAY-OFF FIXTURES AND RESULTS
    // ==========================================

    [HttpGet("{competitionId:guid}/playoffs")]
    public async Task<IActionResult> GetPlayoffs(
        Guid competitionId)
    {
        var ties = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>