using ChampionsLeagueSimulatorAPI.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace ChampionsLeagueSimulatorAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Team> Teams => Set<Team>();
}
