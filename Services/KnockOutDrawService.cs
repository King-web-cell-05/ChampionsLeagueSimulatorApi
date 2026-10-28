
// ==========================================
// GENERATE ROUND OF 16 DRAW
// ==========================================

[HttpPost("{competitionId:guid}/round-of-16/draw")]
public async Task<IActionResult> GenerateRoundOf16Draw(
    Guid competitionId)
{
    try
    {
        var ties = await _drawService
            .GenerateRoundOf16DrawAsync(competitionId);

        // Query saved ties with their club names.
        var savedTies = await _context.KnockoutTies
            .AsNoTracking()
            .Where(t =>
                t.CompetitionId == competitionId &&
                t.Round == KnockoutRound.RoundOf16)
            .Include(t => t.HomeTeam)
            .Include(t => t.AwayTeam)
            .Include(t => t.Matches)
                .ThenInclude(m => m.HomeTeam)
            .Include(t => t.Matches)
                .ThenInclude(m => m.AwayTeam)
            .OrderBy(t => t.TieNumber)
            .ToListAsync();

        return Ok(new
        {
            Message = "Round of 16 draw generated successfully.",
            CompetitionId = competitionId,
            Round = "RoundOf16",
            TotalTies = ties.Count,
            TotalMatches = savedTies.Sum(t => t.Matches.Count),

            Ties = savedTies.Select(t => new
            {
                t.Id,
                t.TieNumber,

                SeededTeamId = t.HomeTeamId,
                SeededTeam = t.HomeTeam.Name,

                OpponentTeamId = t.AwayTeamId,
                OpponentTeam = t.AwayTeam.Name,

                Matches = t.Matches
                    .OrderBy(m => m.Leg)
                    .Select(m => new
                    {
                        m.Id,
                        m.Leg,
                        HomeTeam = m.HomeTeam.Name,
                        AwayTeam = m.AwayTeam.Name,
                        m.IsPlayed
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
