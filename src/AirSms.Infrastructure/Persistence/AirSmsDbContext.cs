using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;
using AirSms.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AirSms.Infrastructure.Persistence;

public class AirSmsDbContext(DbContextOptions<AirSmsDbContext> options)
    : DbContext(options), IAirSmsDbContext
{
    public DbSet<Incident> Incidents => Set<Incident>();

    IQueryable<Incident> IAirSmsDbContext.Incidents => Incidents.AsNoTracking();

    public void AddIncident(Incident incident)
    {
        Incidents.Add(incident);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new IncidentConfiguration());
    }
}
