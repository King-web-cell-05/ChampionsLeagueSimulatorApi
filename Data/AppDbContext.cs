
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Competition> Competitions => Set<Competition>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<User> Users => Set<User>();

    public DbSet<CompetitionTeam> CompetitionTeams =>
        Set<CompetitionTeam>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            .WithMany(c => c.Matches)
            .HasForeignKey(m => m.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CompetitionTeam>()
            .HasKey(ct => new
            {
                ct.CompetitionId,
                ct.TeamId
            });

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
