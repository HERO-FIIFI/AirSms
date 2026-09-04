using AirSms.Notifications.Models;
using Microsoft.EntityFrameworkCore;

namespace AirSms.Notifications.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(builder =>
        {
            builder.ToTable("notifications");
            builder.HasKey(notification => notification.Id);
            builder.Property(notification => notification.Type).IsRequired().HasMaxLength(200);
            builder.Property(notification => notification.Title).IsRequired().HasMaxLength(200);
            builder.Property(notification => notification.Message).IsRequired().HasMaxLength(1000);
            builder.Property(notification => notification.CreatedAt).IsRequired();
            builder.HasIndex(notification => notification.UserId);
            builder.HasIndex(notification => notification.ReadAt);
            builder.HasIndex(notification => notification.RelatedIncidentId);
            builder.HasIndex(notification => new
            {
                notification.SourceEventId,
                notification.UserId,
                notification.Type
            }).IsUnique();
            builder.HasMany(notification => notification.Deliveries)
                .WithOne()
                .HasForeignKey(delivery => delivery.NotificationId);
        });

        modelBuilder.Entity<NotificationDelivery>(builder =>
        {
            builder.ToTable("notification_deliveries");
            builder.HasKey(delivery => delivery.Id);
            builder.Property(delivery => delivery.Channel).HasConversion<string>().IsRequired();
            builder.Property(delivery => delivery.Status).HasConversion<string>().IsRequired();
            builder.Property(delivery => delivery.Destination).IsRequired().HasMaxLength(320);
            builder.Property(delivery => delivery.LastError).HasMaxLength(1000);
            builder.Property(delivery => delivery.CreatedAt).IsRequired();
            builder.HasIndex(delivery => delivery.Status);
            builder.HasIndex(delivery => delivery.NextAttemptAt);
            builder.HasIndex(delivery => new
            {
                delivery.NotificationId,
                delivery.Channel
            }).IsUnique();
        });
    }
}
