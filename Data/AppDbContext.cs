
using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
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

    public DbSet<Notification> Notifications =>
        Set<Notification>();

    public DbSet<CompetitionTeam> CompetitionTeams =>
        Set<CompetitionTeam>();

    public DbSet<Standing> Standings =>
        Set<Standing>();

    // ==========================================
    // MATCH STATISTICS TABLE
    // ==========================================

    public DbSet<MatchStatistics> MatchStatistics =>
        Set<MatchStatistics>();

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

            // ==================================
            // CLUB OWNERSHIP VALIDATION
            // ==================================

            // Default club:
            // IsDefault = true
            // OwnerId = null
            //
            // Private club:
            // IsDefault = false
            // OwnerId != null

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


        // ======================================
        // USER CONFIGURATION
        // ======================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.HasIndex(u => u.Email)
                .IsUnique();

            // JWT session revocation version.
            // New users start at version 1.
            entity.Property(u => u.SecurityVersion)
                .IsRequired()
                .HasDefaultValue(1);
        });


        // ======================================
        // NOTIFICATION CONFIGURATION
        // ======================================

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(n => n.Type)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("system");

            entity.Property(n => n.IsRead)
                .HasDefaultValue(false);

            entity.Property(n => n.CreatedAtUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(n => n.ReadAtUtc)
                .HasColumnType("timestamp with time zone");

            entity.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(n => new
            {
                n.UserId,
                n.CreatedAtUtc
            });

            entity.HasIndex(n => new
            {
                n.UserId,
                n.IsRead
            });
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
            entity.HasKey(m => m.Id);

            entity.Property(m => m.KickoffUtc)
                .HasColumnType(
                    "timestamp with time zone");

            entity.Property(m => m.IsPlayed)
                .HasDefaultValue(false);

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

            // ==================================
            // MATCH -> MATCH STATISTICS
            // ==================================

            // A match may have one statistics
            // record after it is simulated.
            //
            // Existing matches can have null
            // statistics without breaking.

            entity.HasOne(m => m.Statistics)
                .WithOne(s => s.Match)
                .HasForeignKey<MatchStatistics>(
                    s => s.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==================================
            // MATCH LOOKUP INDEXES
            // ==================================

            entity.HasIndex(m => new
            {
                m.CompetitionId,
                m.MatchDay
            });

            entity.HasIndex(m => new
            {
                m.CompetitionId,
                m.IsPlayed
            });
        });

        // ======================================
        // MATCH STATISTICS CONFIGURATION
        // ======================================

        modelBuilder.Entity<MatchStatistics>(entity =>
        {
            entity.HasKey(s => s.Id);

            // ==================================
            // UNIQUE MATCH STATISTICS
            // ==================================

            // Each match must have at most
            // one statistics record.

            entity.HasIndex(s => s.MatchId)
                .IsUnique();

            // ==================================
            // POSSESSION
            // ==================================

            entity.Property(s => s.HomePossession)
                .IsRequired();

            entity.Property(s => s.AwayPossession)
                .IsRequired();

            // ==================================
            // SHOTS
            // ==================================

            entity.Property(s => s.HomeShots)
                .IsRequired();

            entity.Property(s => s.AwayShots)
                .IsRequired();

            entity.Property(s => s.HomeShotsOnTarget)
                .IsRequired();

            entity.Property(s => s.AwayShotsOnTarget)
                .IsRequired();

            // ==================================
            // CORNER KICKS
            // ==================================

            entity.Property(s => s.HomeCorners)
                .IsRequired();

            entity.Property(s => s.AwayCorners)
                .IsRequired();

            // ==================================
            // FOULS
            // ==================================

            entity.Property(s => s.HomeFouls)
                .IsRequired();

            entity.Property(s => s.AwayFouls)
                .IsRequired();

            // ==================================
            // YELLOW CARDS
            // ==================================

            entity.Property(s => s.HomeYellowCards)
                .IsRequired();

            entity.Property(s => s.AwayYellowCards)
                .IsRequired();

            // ==================================
            // RED CARDS
            // ==================================

            entity.Property(s => s.HomeRedCards)
                .IsRequired();

            entity.Property(s => s.AwayRedCards)
                .IsRequired();

            // ==================================
            // EXPECTED GOALS
            // ==================================

            entity.Property(s => s.HomeExpectedGoals)
                .IsRequired();

            entity.Property(s => s.AwayExpectedGoals)
                .IsRequired();

            // ==================================
            // DATA VALIDATION
            // ==================================

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_MatchStatistics_HomePossession",
                    "\"HomePossession\" BETWEEN 0 AND 100");

                table.HasCheckConstraint(
                    "CK_MatchStatistics_AwayPossession",
                    "\"AwayPossession\" BETWEEN 0 AND 100");

                table.HasCheckConstraint(
                    "CK_MatchStatistics_TotalPossession",
                    "\"HomePossession\" + \"AwayPossession\" = 100");

                table.HasCheckConstraint(
                    "CK_MatchStatistics_HomeShots",
                    "\"HomeShots\" >= 0 AND \"HomeShotsOnTarget\" >= 0 AND \"HomeShotsOnTarget\" <= \"HomeShots\"");

                table.HasCheckConstraint(
                    "CK_MatchStatistics_AwayShots",
                    "\"AwayShots\" >= 0 AND \"AwayShotsOnTarget\" >= 0 AND \"AwayShotsOnTarget\" <= \"AwayShots\"");

                table.HasCheckConstraint(
                    "CK_MatchStatistics_ExpectedGoals",
                    "\"HomeExpectedGoals\" >= 0 AND \"AwayExpectedGoals\" >= 0");
            });
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

            // KnockoutMatch -> KnockoutTiemodelBuilder.Entity<User>(entity =>

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
