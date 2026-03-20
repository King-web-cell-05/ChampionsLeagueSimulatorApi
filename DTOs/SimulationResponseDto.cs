using ChampionsLeagueSimulatorAPI.DTOs;

namespace ChampionsLeagueSimulatorApi.DTOs;

public class SimulationResponseDto
{
    // List of matches in this competition
    public List<MatchDto> Matches { get; set; } = new List<MatchDto>();

    // League standings after simulation
    public List<StandingDto> Standings { get; set; } = new List<StandingDto>();
}
