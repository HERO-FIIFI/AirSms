using AirSms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirSms.Infrastructure.Persistence.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(incident => incident.Id);
        builder.Ignore(incident => incident.DomainEvents);

        builder.Property(incident => incident.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(incident => incident.Description)
            .IsRequired();

        builder.Property(incident => incident.Category)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(incident => incident.Severity)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(incident => incident.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(incident => incident.FlightNumber)
            .HasMaxLength(20);

        builder.Property(incident => incident.AircraftRegistration)
            .HasMaxLength(20);

        builder.Property(incident => incident.ReportedByUserId)
            .IsRequired();

        builder.Property(incident => incident.AssignedToUserId);

        builder.Property(incident => incident.ReportedAt)
            .IsRequired();

        builder.Property(incident => incident.ResolvedAt);

        builder.Property(incident => incident.CreatedAt)
            .IsRequired();

        builder.Property(incident => incident.UpdatedAt);

        builder.HasIndex(incident => incident.Status);
        builder.HasIndex(incident => incident.Severity);
        builder.HasIndex(incident => incident.Category);
        builder.HasIndex(incident => incident.ReportedAt);
        builder.HasIndex(incident => incident.AssignedToUserId);
    }
}
