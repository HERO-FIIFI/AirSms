using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirSms.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Type)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(message => message.Payload)
            .IsRequired();

        builder.Property(message => message.OccurredAt)
            .IsRequired();

        builder.Property(message => message.ProcessedAt);
        builder.Property(message => message.Error);
        builder.Property(message => message.RetryCount).IsRequired();
        builder.Property(message => message.NextAttemptAt);

        builder.HasIndex(message => message.ProcessedAt);
        builder.HasIndex(message => message.OccurredAt);
        builder.HasIndex(message => message.NextAttemptAt);
    }
}
