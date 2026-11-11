
using System;
using ChampionsLeagueSimulatorAPI.Entities;

namespace ChampionsLeagueSimulatorAPI.Services;

// ==========================================
// MATCH SIMULATION RESULT
// ==========================================

public sealed class MatchEngineResult
{
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }

    public int HomePossession { get; init; }
    public int AwayPossession { get; init; }

    public int HomeShots { get; init; }
    public int AwayShots { get; init; }

    public int HomeShotsOnTarget { get; init; }
    public int AwayShotsOnTarget { get; init; }

    public int HomeCorners { get; init; }
    public int AwayCorners { get; init; }

    public int HomeFouls { get; init; }
    public int AwayFouls { get; init; }

    public int HomeYellowCards { get; init; }
    public int AwayYellowCards { get; init; }

    public int HomeRedCards { get; init; }
    public int AwayRedCards { get; init; }

    public double HomeExpectedGoals { get; init; }
    public double AwayExpectedGoals { get; init; }
}

public class MatchEngineService
{
    // ==========================================
    // PUBLIC SIMULATION METHOD
    // ==========================================

    public MatchEngineResult SimulateMatch(
        Team homeTeam,
        Team awayTeam)
    {
        ArgumentNullException.ThrowIfNull(homeTeam);
        ArgumentNullException.ThrowIfNull(awayTeam);

        // ======================================
        // CLUB STRENGTH
        // ======================================

        double homeStrength =
            NormalizeStrength(homeTeam.Coefficient);

        double awayStrength =
            NormalizeStrength(awayTeam.Coefficient);

        double strengthDifference =
            (homeStrength - awayStrength) / 100.0;

        // ======================================
        // EXPECTED GOALS
        // ======================================

        double homeExpectedGoals =
            1.60 + strengthDifference * 1.20;

        double awayExpectedGoals =
            1.15 - strengthDifference * 1.00;

        homeExpectedGoals = Math.Clamp(
            homeExpectedGoals, 0.15, 3.80);

        awayExpectedGoals = Math.Clamp(
            awayExpectedGoals, 0.15, 3.80);

        // ======================================
        // FINAL SCORE
        // ======================================

        int homeScore =
            GeneratePoissonGoals(homeExpectedGoals);

        int awayScore =
            GeneratePoissonGoals(awayExpectedGoals);

        // ======================================
        // BALL POSSESSION
        // ======================================

        int homePossession = Math.Clamp(
            (int)Math.Round(
                52 +
                strengthDifference * 25 +
                Random.Shared.Next(-9, 10)),
            25,
            75);

        int awayPossession = 100 - homePossession;

        // ======================================
        // TOTAL SHOTS
        // ======================================

        int homeShots = Math.Clamp(
            GeneratePoissonGoals(
                9 + homeExpectedGoals * 3),
            Math.Max(homeScore, 1),
            35);

        int awayShots = Math.Clamp(
            GeneratePoissonGoals(
                9 + awayExpectedGoals * 3),
            Math.Max(awayScore, 1),
            35);

        // ======================================
        // SHOTS ON TARGET
        // ======================================

        int homeShotsOnTarget = Math.Clamp(
            GeneratePoissonGoals(
                2 + homeExpectedGoals * 1.5),
            homeScore,
            homeShots);

        int awayShotsOnTarget = Math.Clamp(
            GeneratePoissonGoals(
                2 + awayExpectedGoals * 1.5),
            awayScore,
            awayShots);

        // ======================================
        // CORNERS
        // ======================================

        int homeCorners = Math.Clamp(
            GeneratePoissonGoals(
                3 + homeShots * 0.15),
            0,
            18);

        int awayCorners = Math.Clamp(
            GeneratePoissonGoals(
                3 + awayShots * 0.15),
            0,
            18);

        // ======================================
        // FOULS
        // ======================================

        int homeFouls = Random.Shared.Next(6, 21);
        int awayFouls = Random.Shared.Next(6, 21);

        // ======================================
        // YELLOW CARDS
        // ======================================

        int homeYellowCards = Math.Clamp(
            GeneratePoissonGoals(
                homeFouls * 0.15),
            0,
            7);

        int awayYellowCards = Math.Clamp(
            GeneratePoissonGoals(
                awayFouls * 0.15),
            0,
            7);

        // ======================================
        // RED CARDS
        // ======================================

        int homeRedCards =
            Random.Shared.NextDouble() < 0.06
                ? 1
                : 0;

        int awayRedCards =
            Random.Shared.NextDouble() < 0.06
                ? 1
                : 0;

        // ======================================
        // RETURN COMPLETE MATCH RESULT
        // ======================================

        return new MatchEngineResult
        {
            HomeScore = homeScore,
            AwayScore = awayScore,

            HomePossession = homePossession,
            AwayPossession = awayPossession,

            HomeShots = homeShots,
            AwayShots = awayShots,

            HomeShotsOnTarget = homeShotsOnTarget,
            AwayShotsOnTarget = awayShotsOnTarget,

            HomeCorners = homeCorners,
            AwayCorners = awayCorners,

            HomeFouls = homeFouls,
            AwayFouls = awayFouls,

            HomeYellowCards = homeYellowCards,
            AwayYellowCards = awayYellowCards,

            HomeRedCards = homeRedCards,
            AwayRedCards = awayRedCards,

            HomeExpectedGoals =
                Math.Round(homeExpectedGoals, 2),

            AwayExpectedGoals =
                Math.Round(awayExpectedGoals, 2)
        };
    }

    // ==========================================
    // NORMALIZE CLUB COEFFICIENT
    // ==========================================

    private static double NormalizeStrength(
        decimal coefficient)
    {
        return Math.Clamp(
            (double)coefficient,
            0.0,
            100.0);
    }

    // ==========================================
    // POISSON GOAL GENERATION
    // ==========================================

    private static int GeneratePoissonGoals(
        double expectedValue)
    {
        double threshold =
            Math.Exp(-expectedValue);

        int count = 0;
        double probability = 1.0;

        do
        {
            count++;

            probability *=
                Random.Shared.NextDouble();
        }
        while (probability > threshold);

        return count - 1;
    }
}
