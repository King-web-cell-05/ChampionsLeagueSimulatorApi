using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Team> Teams { get; set; }
    public DbSet<Competition> Competitions { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<CompetitionTeam> CompetitionTeams { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Match>()
            .HasOne(m => m.HomeTeam)
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.AwayTeam)
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.Competition)
            .WithMany()
            .HasForeignKey(m => m.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ✅ COMPOSITE KEY CONFIG
        modelBuilder.Entity<CompetitionTeam>()
            .HasKey(ct => new { ct.CompetitionId, ct.TeamId });

        modelBuilder.Entity<CompetitionTeam>()
            .HasOne(ct => ct.Team)
            .WithMany()
            .HasForeignKey(ct => ct.TeamId);

        modelBuilder.Entity<CompetitionTeam>()
            .HasOne(ct => ct.Competition)
            .WithMany()
            .HasForeignKey(ct => ct.CompetitionId);
    }
}