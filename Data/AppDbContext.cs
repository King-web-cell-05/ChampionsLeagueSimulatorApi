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
    public DbSet<Standing> Standings => Set<Standing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Store coefficients with three decimal places.
        modelBuilder.Entity<Team>()
            .Property(t => t.Coefficient)
            .HasPrecision(7, 3);

        // User email must be unique.
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Match -> Home Team.
        modelBuilder.Entity<Match>()
            .HasOne(m => m.HomeTeam)
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Match -> Away Team.
        modelBuilder.Entity<Match>()
            .HasOne(m => m.AwayTeam)
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Match -> Competition.
        modelBuilder.Entity<Match>()
            .HasOne(m => m.Competition)
            .WithMany(c => c.Matches)
            .HasForeignKey(m => m.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // CompetitionTeam composite primary key.
        modelBuilder.Entity<CompetitionTeam>()
            .HasKey(ct => new
            {
                ct.CompetitionId,
                ct.TeamId
            });

        // CompetitionTeam -> Team.
        modelBuilder.Entity<CompetitionTeam>()
            .HasOne(ct => ct.Team)
            .WithMany()
            .HasForeignKey(ct => ct.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // CompetitionTeam -> Competition.
        modelBuilder.Entity<CompetitionTeam>()
            .HasOne(ct => ct.Competition)
            .WithMany()
            .HasForeignKey(ct => ct.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Standing -> Team.
        modelBuilder.Entity<Standing>()
            .HasOne(s => s.Team)
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Standing -> Competition.
        modelBuilder.Entity<Standing>()
            .HasOne<Competition>()
            .WithMany()
            .HasForeignKey(s => s.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}