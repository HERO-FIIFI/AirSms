using AirSms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirSms.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");

        builder.HasKey(auditEvent => auditEvent.Id);

        builder.Property(auditEvent => auditEvent.EventId).IsRequired();
        builder.HasIndex(auditEvent => auditEvent.EventId).IsUnique();

        builder.Property(auditEvent => auditEvent.EventType).HasMaxLength(500).IsRequired();
        builder.Property(auditEvent => auditEvent.AggregateId).IsRequired();
        builder.Property(auditEvent => auditEvent.AggregateType).HasMaxLength(100).IsRequired();
        builder.Property(auditEvent => auditEvent.ActorUserId);
        builder.Property(auditEvent => auditEvent.OccurredAt).IsRequired();
        builder.Property(auditEvent => auditEvent.Payload).IsRequired();
        builder.Property(auditEvent => auditEvent.RecordedAt).IsRequired();
        builder.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(100);

        builder.HasIndex(auditEvent => auditEvent.AggregateId);
        builder.HasIndex(auditEvent => auditEvent.OccurredAt);
        builder.HasIndex(auditEvent => auditEvent.ActorUserId);
    }
}
