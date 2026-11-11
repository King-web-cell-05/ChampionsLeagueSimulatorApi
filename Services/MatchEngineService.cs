
using System;
using ChampionsLeagueSimulatorAPI.Entities;

namespace ChampionsLeagueSimulatorAPI.Services;

public class MatchEngineService
{
    // ==========================================
    // SIMULATE A REALISTIC FOOTBALL MATCH
    // ==========================================

    public (int HomeScore, int AwayScore) SimulateMatch(
        Team homeTeam,
        Team awayTeam)
    {
        ArgumentNullException.ThrowIfNull(homeTeam);
        ArgumentNullException.ThrowIfNull(awayTeam);

        // Normalize club coefficients to a
        // predictable 0-100 strength scale.
        //
        // Coefficients outside the expected range
        // are clamped to avoid extreme results.

        double homeStrength = NormalizeStrength(
            homeTeam.Coefficient);

        double awayStrength = NormalizeStrength(
            awayTeam.Coefficient);

        // ==========================================
        // EXPECTED GOALS
        // ==========================================

        // Base scoring rates for home and away.
        double homeExpectedGoals = 1.45;
        double awayExpectedGoals = 1.15;

        // Stronger teams create more chances.
        // Positive difference benefits home team.
        double strengthDifference =
            (homeStrength - awayStrength) / 100.0;

        homeExpectedGoals += strengthDifference * 1.2;
        awayExpectedGoals -= strengthDifference * 1.0;

        // Add a small home advantage.
        homeExpectedGoals += 0.15;

        // Keep expected goals within reasonable limits.
        homeExpectedGoals = Math.Clamp(
            homeExpectedGoals,
            0.15,
            3.8);

        awayExpectedGoals = Math.Clamp(
            awayExpectedGoals,
            0.15,
            3.8);

        // ==========================================
        // GENERATE SCORE USING POISSON MODEL
        // ==========================================

        int homeScore = GeneratePoissonGoals(
            homeExpectedGoals);

        int awayScore = GeneratePoissonGoals(
            awayExpectedGoals);

        return (homeScore, awayScore);
    }

    // ==========================================
    // NORMALIZE CLUB STRENGTH
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
        double expectedGoals)
    {
        // Knuth's algorithm for Poisson sampling.
        double threshold = Math.Exp(-expectedGoals);

        int goals = 0;
        double probability = 1.0;

        do
        {
            goals++;

            probability *= Random.Shared.NextDouble();
        }
        while (probability > threshold);

        return goals - 1;
    }
}
