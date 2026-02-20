using Microsoft.AspNetCore.Mvc;
using ChampionsLeagueSimulatorAPI.Services;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DrawController : ControllerBase
{
    private readonly DrawService _drawService;

    public DrawController(DrawService drawService)
    {
        _drawService = drawService;
    }

    [HttpPost("{competitionId}")]
    public async Task<IActionResult> GenerateDraw(Guid competitionId)
    {
        await _drawService.GenerateDraw(competitionId);

        return Ok("Draw generated successfully");
    }
}