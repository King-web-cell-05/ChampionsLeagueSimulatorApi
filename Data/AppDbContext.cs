using Microsoft.EntityFrameworkCore;
using ChampionsLeagueSimulatorAPI.Entities;

namespace ChampionsLeagueSimulatorAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Competition> Competitions => Set<Competition>();

    public DbSet<CompetitionTeam> CompetitionTeams => Set<CompetitionTeam>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<Standing> Standings => Set<Standing>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Match>()
            .HasOne(m => m.HomeTeam)
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.AwayTeam)
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}