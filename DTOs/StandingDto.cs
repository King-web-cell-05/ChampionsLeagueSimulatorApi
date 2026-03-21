namespace ChampionsLeagueSimulatorApi.DTOs
{
    public class StandingDto
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = "";

        public int Played { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference { get; set; }
        public int Points { get; set; }
        public int Position { get; set; }
        public string GroupName { get; set; } = "";
    }
}