using ChampionsLeagueSimulatorAPI.DTOs;

namespace ChampionsLeagueSimulatorApi.DTOs
{
    // SimulationResponseDto.cs
    public class SimulationResponseDto
    {
        public List<MatchDto> Matches { get; set; } = new();
        public List<StandingDto> Standings { get; set; } = new();
    }
}
