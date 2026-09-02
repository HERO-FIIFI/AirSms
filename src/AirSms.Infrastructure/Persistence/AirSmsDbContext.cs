using AirSms.Application.Common.Interfaces;
using AirSms.Domain.Entities;
using AirSms.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AirSms.Infrastructure.Persistence;

public class AirSmsDbContext(DbContextOptions<AirSmsDbContext> options)
    : DbContext(options), IAirSmsDbContext
{
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<User> Users => Set<User>();

    IQueryable<Incident> IAirSmsDbContext.Incidents => Incidents.AsNoTracking();
    IQueryable<User> IAirSmsDbContext.Users => Users.AsNoTracking();

    public Task<Incident?> FindIncidentForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Incidents.SingleOrDefaultAsync(
            incident => incident.Id == id,
            cancellationToken);
    }

    public void AddIncident(Incident incident)
    {
        Incidents.Add(incident);
    }

    public Task<User?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return Users.AsNoTracking().SingleOrDefaultAsync(
            user => user.Email == normalizedEmail,
            cancellationToken);
    }

    public void AddUser(User user)
    {
        Users.Add(user);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new IncidentConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
    }
}
