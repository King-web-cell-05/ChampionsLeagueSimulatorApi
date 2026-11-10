
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
    // DATABASE TABLES
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
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(t => t.Country)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(t => t.Coefficient)
                .HasPrecision(7, 3);

            entity.Property(t => t.IsDefault)
                .HasDefaultValue(false);

            // Private club -> Owner
            // Shared default clubs have no owner.
            entity.HasOne(t => t.Owner)
                .WithMany()
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optimize private club lookups.
            entity.HasIndex(t => t.OwnerId);

            // Optimize default/private club filtering.
            entity.HasIndex(t => new
            {
                t.IsDefault,
                t.OwnerId
            });

            // Enforce valid club ownership:
            // Default club: IsDefault = true,
            //               OwnerId = null
            //
            // Private club: IsDefault = false,
            //               OwnerId != null

            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_Teams_Ownership",
                    """
                    ("IsDefault" = TRUE AND "OwnerId" IS NULL)
                    OR
                    ("IsDefault" = FALSE AND "OwnerId" IS NOT NULL)
                    """));
        });

        // ======================================
        // USER CONFIGURATION
        // ======================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.HasIndex(u => u.Email)
                .IsUnique();
        });

        // ======================================
        // COMPETITION CONFIGURATION
        // ======================================

        modelBuilder.Entity<Competition>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(c => c.Season)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(c => c.CreatedDate)
                .HasColumnType(
                    "timestamp with time zone");

            // Competition -> Creator
            entity.HasOne(c => c.Owner)
                .WithMany()
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optimize loading competitions
            // belonging to the current user.
            entity.HasIndex(c => c.OwnerId);

            entity.HasIndex(c => new
            {
                c.OwnerId,
                c.CreatedDate
            });
        });

        // ======================================
        // LEAGUE PHASE MATCH CONFIGURATION
        // ======================================

        modelBuilder.Entity<Match>(entity =>
        {
            entity.Property(m => m.KickoffUtc)
                .HasColumnType(
                    "timestamp with time zone");

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

            entity.Property(t => t.CreatedAtUtc)
                .HasColumnType(
                    "timestamp with time zone");

            entity.Property(t => t.IsCompleted)
                .HasDefaultValue(false);
        });

        // ======================================
        // KNOCKOUT MATCH CONFIGURATION
        // ======================================

        modelBuilder.Entity<KnockoutMatch>(entity =>
        {
            entity.HasKey(m => m.Id);

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

            entity.Property(m => m.KickoffUtc)
                .HasColumnType(
                    "timestamp with time zone");

            entity.Property(m => m.IsPlayed)
                .HasDefaultValue(false);

            // ==================================
            // EXTRA TIME CONFIGURATION
            // ==================================

            entity.Property(m => m.WentToExtraTime)
                .HasDefaultValue(false);

            entity.Property(m => m.HomeExtraTimeScore);

            entity.Property(m => m.AwayExtraTimeScore);

            // ==================================
            // PENALTY SHOOTOUT CONFIGURATION
            // ==================================

            entity.Property(m => m.WentToPenalties)
                .HasDefaultValue(false);

            entity.Property(m => m.HomePenaltyScore);

            entity.Property(m => m.AwayPenaltyScore);
        });
    }
}
