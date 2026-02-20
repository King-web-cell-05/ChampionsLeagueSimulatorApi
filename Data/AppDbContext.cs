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

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<Standing> Standings => Set<Standing>();
}