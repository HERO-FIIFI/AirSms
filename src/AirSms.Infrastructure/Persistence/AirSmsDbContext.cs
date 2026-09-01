using AirSms.Domain.Entities;
using AirSms.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AirSms.Infrastructure.Persistence;

public class AirSmsDbContext(DbContextOptions<AirSmsDbContext> options) : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new IncidentConfiguration());
    }
}
