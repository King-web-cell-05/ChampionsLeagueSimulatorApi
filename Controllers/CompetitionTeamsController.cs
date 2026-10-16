
[HttpPost]
public async Task<IActionResult> AddTeam(AddCompetitionTeamRequest request)
{
    // Check whether the competition exists
    var competitionExists = await _context.Competitions
        .AnyAsync(c => c.Id == request.CompetitionId);

    if (!competitionExists)
    {
        return NotFound(new
        {
            message = "The specified competition does not exist."
        });
    }

    // Check whether the team exists
    var teamExists = await _context.Teams
        .AnyAsync(t => t.Id == request.TeamId);

    if (!teamExists)
    {
        return NotFound(new
        {
            message = "The specified team does not exist."
        });
    }

    // Check whether the team is already in this competition
    var exists = await _context.CompetitionTeams
        .AnyAsync(ct =>
            ct.CompetitionId == request.CompetitionId &&
            ct.TeamId == request.TeamId);

    if (exists)
    {
        return BadRequest(
            "Team already exists in this competition.");
    }

    // Create the relationship
    var entry = new CompetitionTeam
    {
        CompetitionId = request.CompetitionId,
        TeamId = request.TeamId
    };

    _context.CompetitionTeams.Add(entry);
    await _context.SaveChangesAsync();

    // Load related entities
    await _context.Entry(entry)
        .Reference(ct => ct.Competition)
        .LoadAsync();

    await _context.Entry(entry)
        .Reference(ct => ct.Team)
        .LoadAsync();

    // Return the response
    var response = new CompetitionTeamResponse
    {
        CompetitionId = entry.CompetitionId,
        CompetitionName = entry.Competition?.Name ?? "",
        TeamId = entry.TeamId,
        TeamName = entry.Team?.Name ?? ""
    };

    return Ok(response);
}
