using Backend.Api.Features.PulseRecords;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api.Infrastructure.Persistence;

public sealed class PulseDbContext(DbContextOptions<PulseDbContext> options) : DbContext(options)
{
    public DbSet<PulseEntity> Pulses => Set<PulseEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            return;
        }

        if (optionsBuilder.Options.Extensions.Any(e => e.Info.IsDatabaseProvider && e.GetType().FullName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true))
        {
            optionsBuilder.UseNpgsql(o => o.MigrationsAssembly(typeof(PulseDbContext).Assembly.FullName));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PulseDbContext).Assembly);
    }
}
