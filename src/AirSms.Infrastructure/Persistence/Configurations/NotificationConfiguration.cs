using AirSms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirSms.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.UserId).IsRequired();
        builder.Property(notification => notification.Type).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
        builder.Property(notification => notification.RelatedIncidentId);
        builder.Property(notification => notification.CreatedAt).IsRequired();
        builder.Property(notification => notification.ReadAt);
        builder.Property(notification => notification.SourceEventId).IsRequired();

        builder.HasIndex(notification => notification.UserId);
        builder.HasIndex(notification => notification.ReadAt);
        builder.HasIndex(notification => notification.RelatedIncidentId);
        builder.HasIndex(notification => new { notification.SourceEventId, notification.UserId }).IsUnique();
    }
}
