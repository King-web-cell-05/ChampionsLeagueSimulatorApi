
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // ==========================================
    // EXISTING DATABASE TABLES
    // ==========================================

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Competition> Competitions =>
        Set<Competition>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<User> Users => Set<User>();

    public DbSet<CompetitionTeam> CompetitionTeams =>
        Set<CompetitionTeam>();

    public DbSet<Standing> Standings =>
        Set<Standing>();

    // ==========================================
    // KNOCKOUT STAGE TABLES
    // ==========================================

    public DbSet<KnockoutTie> KnockoutTies =>
        Set<KnockoutTie>();

    public DbSet<KnockoutMatch> KnockoutMatches =>
        Set<KnockoutMatch>();

    // ==========================================
    // ENTITY CONFIGURATION
    // ==========================================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ======================================
        // TEAM CONFIGURATION
        // ======================================

        modelBuilder.Entity<Team>(entity =>
        {
            entity.Property(t => t.Coefficient)
                .HasPrecision(7, 3);
        });

        // ======================================
        // USER CONFIGURATION
        // ======================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email)
                .IsUnique();
        });

        // ======================================
        // LEAGUE PHASE MATCH CONFIGURATION
        // ======================================

        modelBuilder.Entity<Match>(entity =>
        {
            entity.Property(m => m.KickoffUtc)
                .HasColumnType("timestamp with time zone");

            // Match -> Home Team
            entity.HasOne(m => m.HomeTeam)
                .WithMany()
                .HasForeignKey(m => m.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // Match -> Away Team
            entity.HasOne(m => m.AwayTeam)
                .WithMany()
                .HasForeignKey(m => m.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // Match -> Competition
            entity.HasOne(m => m.Competition)
                .WithMany(c => c.Matches)
                .HasForeignKey(m => m.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ======================================
        // COMPETITION TEAM CONFIGURATION
        // ======================================

        modelBuilder.Entity<CompetitionTeam>(entity =>
        {
            // Composite primary key
            entity.HasKey(ct => new
            {
                ct.CompetitionId,
                ct.TeamId
            });

            // CompetitionTeam -> Team
            entity.HasOne(ct => ct.Team)
                .WithMany()
                .HasForeignKey(ct => ct.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // CompetitionTeam -> Competition
            entity.HasOne(ct => ct.Competition)
                .WithMany()
                .HasForeignKey(ct => ct.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ======================================
        // STANDINGS CONFIGURATION
        // ======================================

        modelBuilder.Entity<Standing>(entity =>
        {
            // Standing -> Team
            entity.HasOne(s => s.Team)
                .WithMany()
                .HasForeignKey(s => s.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // Standing -> Competition
            entity.HasOne<Competition>()
                .WithMany()
                .HasForeignKey(s => s.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ======================================
        // KNOCKOUT TIE CONFIGURATION
        // ======================================

        modelBuilder.Entity<KnockoutTie>(entity =>
        {
            entity.HasKey(t => t.Id);

            // Prevent duplicate ties within a round
            entity.HasIndex(t => new
            {
                t.CompetitionId,
                t.Round,
                t.TieNumber
            })
            .IsUnique();

            // KnockoutTie -> Competition
            entity.HasOne(t => t.Competition)
                .WithMany()
                .HasForeignKey(t => t.CompetitionId)
                .OnDelete(DeleteBehavior.Restrict);

            // KnockoutTie -> Home Team
            entity.HasOne(t => t.HomeTeam)
                .WithMany()
                .HasForeignKey(t => t.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // KnockoutTie -> Away Team
            entity.HasOne(t => t.AwayTeam)
                .WithMany()
                .HasForeignKey(t => t.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // KnockoutTie -> Winner Team
            entity.HasOne(t => t.WinnerTeam)
                .WithMany()
                .HasForeignKey(t => t.WinnerTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // PostgreSQL UTC timestamp
            entity.Property(t => t.CreatedAtUtc)
                .HasColumnType("timestamp with time zone");

            // Tie completion status
            entity.Property(t => t.IsCompleted)
                .HasDefaultValue(false);
        });

        // ======================================
        // KNOCKOUT MATCH CONFIGURATION
        // ======================================

        modelBuilder.Entity<KnockoutMatch>(entity =>
        {
            entity.HasKey(m => m.Id);

            // Prevent duplicate legs within a tie
            entity.HasIndex(m => new
            {
                m.KnockoutTieId,
                m.Leg
            })
            .IsUnique();

            // KnockoutMatch -> KnockoutTie
            entity.HasOne(m => m.KnockoutTie)
                .WithMany(t => t.Matches)
                .HasForeignKey(m => m.KnockoutTieId)
                .OnDelete(DeleteBehavior.Cascade);

            // KnockoutMatch -> Home Team
            entity.HasOne(m => m.HomeTeam)
                .WithMany()
                .HasForeignKey(m => m.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // KnockoutMatch -> Away Team
            entity.HasOne(m => m.AwayTeam)
                .WithMany()
                .HasForeignKey(m => m.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // PostgreSQL UTC timestamp
            entity.Property(m => m.KickoffUtc)
                .HasColumnType("timestamp with time zone");

            // Match completion status
            entity.Property(m => m.IsPlayed)
                .HasDefaultValue(false);

            // ==================================
            // EXTRA TIME CONFIGURATION
            // ==================================

            // Indicates whether extra time was played
            entity.Property(m => m.WentToExtraTime)
                .HasDefaultValue(false);

            // Extra-time goals only (excluding 90-minute goals)
            entity.Property(m => m.HomeExtraTimeScore);

            entity.Property(m => m.AwayExtraTimeScore);

            // ==================================
            // PENALTY SHOOTOUT CONFIGURATION
            // ==================================

            // Indicates whether penalties were required
            entity.Property(m => m.WentToPenalties)
                .HasDefaultValue(false);

            // Penalty shootout scores
            entity.Property(m => m.HomePenaltyScore);

            entity.Property(m => m.AwayPenaltyScore);
        });
    }
}
