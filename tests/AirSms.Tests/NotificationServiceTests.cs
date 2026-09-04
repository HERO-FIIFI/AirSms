using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AirSms.Contracts.Events;
using AirSms.Notifications;
using AirSms.Notifications.Email;
using AirSms.Notifications.Models;
using AirSms.Notifications.Options;
using AirSms.Notifications.Persistence;
using AirSms.Notifications.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AirSms.Tests;

public class NotificationServiceTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=airsms_notifications;Username=airsms;Password=airsms_dev_password";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AssignedEventCreatesNotificationAndPendingEmailDelivery()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var userId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        try
        {
            await HandleAsync(AssignedEnvelope(incidentId, eventId, userId, "agent@airsms.test"));

            await using var verify = CreateContext();
            var notification = await verify.Notifications.SingleAsync(item =>
                item.SourceEventId == eventId &&
                item.UserId == userId);
            var deliveries = await verify.NotificationDeliveries
                .Where(delivery => delivery.NotificationId == notification.Id)
                .ToListAsync();

            Assert.Equal("IncidentAssigned", notification.Type);
            Assert.Contains(deliveries, delivery =>
                delivery.Channel == NotificationChannel.InApp &&
                delivery.Status == DeliveryStatus.Delivered);
            Assert.Contains(deliveries, delivery =>
                delivery.Channel == NotificationChannel.Email &&
                delivery.Status == DeliveryStatus.Pending);
        }
        finally
        {
            await DeleteNotificationRowsAsync(eventId);
        }
    }

    [Fact]
    public async Task DuplicateKafkaEventDoesNotDuplicateNotificationOrDelivery()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var envelope = AssignedEnvelope(Guid.NewGuid(), eventId, userId, "agent@airsms.test");

        try
        {
            await HandleAsync(envelope);
            await HandleAsync(envelope);

            await using var verify = CreateContext();
            var notification = await verify.Notifications.SingleAsync(item => item.SourceEventId == eventId);
            Assert.Equal(1, await verify.Notifications.CountAsync(item => item.SourceEventId == eventId));
            Assert.Equal(2, await verify.NotificationDeliveries.CountAsync(item =>
                item.NotificationId == notification.Id));
        }
        finally
        {
            await DeleteNotificationRowsAsync(eventId);
        }
    }

    [Fact]
    public async Task UnsupportedNotificationEventIsIgnored()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var eventId = Guid.NewGuid();
        var envelope = AssignedEnvelope(Guid.NewGuid(), eventId, Guid.NewGuid(), "agent@airsms.test") with
        {
            EventType = IncidentIntegrationEventTypes.Started
        };

        await HandleAsync(envelope);

        await using var verify = CreateContext();
        Assert.False(await verify.Notifications.AnyAsync(item => item.SourceEventId == eventId));
    }

    [Fact]
    public async Task EmailWorkerMarksSuccessfulDeliveryDelivered()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var sender = new FakeEmailSender();
        var notification = new AirSms.Notifications.Models.Notification(
            Guid.NewGuid(),
            "IncidentAssigned",
            "Incident assigned",
            "Incident has been assigned to you.",
            Guid.NewGuid(),
            Guid.NewGuid());
        var delivery = new NotificationDelivery(
            notification.Id,
            NotificationChannel.Email,
            "agent@airsms.test");

        await using (var context = CreateContext())
        {
            context.Notifications.Add(notification);
            context.NotificationDeliveries.Add(delivery);
            await context.SaveChangesAsync();
        }

        try
        {
            await CreateEmailWorker(sender).ProcessPendingAsync();

            await using var verify = CreateContext();
            var updated = await verify.NotificationDeliveries.SingleAsync(item => item.Id == delivery.Id);
            Assert.Equal(DeliveryStatus.Delivered, updated.Status);
            Assert.NotNull(updated.DeliveredAt);
            Assert.Single(sender.Sent);
        }
        finally
        {
            await DeleteNotificationRowsAsync(notification.SourceEventId);
        }
    }

    [Fact]
    public async Task EmailFailureSchedulesRetry()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var sender = new FakeEmailSender(shouldFail: true);
        var notification = await AddPendingEmailNotificationAsync();

        try
        {
            await CreateEmailWorker(sender).ProcessPendingAsync();

            await using var verify = CreateContext();
            var delivery = await verify.NotificationDeliveries.SingleAsync(item =>
                item.NotificationId == notification.Id &&
                item.Channel == NotificationChannel.Email);
            Assert.Equal(DeliveryStatus.Pending, delivery.Status);
            Assert.Equal(1, delivery.AttemptCount);
            Assert.NotNull(delivery.NextAttemptAt);
            Assert.NotNull(delivery.LastError);
        }
        finally
        {
            await DeleteNotificationRowsAsync(notification.SourceEventId);
        }
    }

    [Fact]
    public async Task MaxEmailAttemptsMarksFailed()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var sender = new FakeEmailSender(shouldFail: true);
        var notification = await AddPendingEmailNotificationAsync();

        try
        {
            await CreateEmailWorker(sender, maxAttempts: 1).ProcessPendingAsync();

            await using var verify = CreateContext();
            var delivery = await verify.NotificationDeliveries.SingleAsync(item =>
                item.NotificationId == notification.Id &&
                item.Channel == NotificationChannel.Email);
            Assert.Equal(DeliveryStatus.Failed, delivery.Status);
            Assert.Equal(1, delivery.AttemptCount);
            Assert.Null(delivery.NextAttemptAt);
        }
        finally
        {
            await DeleteNotificationRowsAsync(notification.SourceEventId);
        }
    }

    [Fact]
    public async Task NotificationEndpointsRequireJwt()
    {
        using var factory = new NotificationsFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserSeesOnlyOwnNotificationsAndCanMarkOwnRead()
    {
        if (!await DatabaseAvailable())
        {
            return;
        }

        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var current = new AirSms.Notifications.Models.Notification(
            currentUserId,
            "IncidentAssigned",
            "Incident assigned",
            "Incident has been assigned to you.",
            Guid.NewGuid(),
            Guid.NewGuid());
        var other = new AirSms.Notifications.Models.Notification(
            otherUserId,
            "IncidentAssigned",
            "Incident assigned",
            "Incident has been assigned to you.",
            Guid.NewGuid(),
            Guid.NewGuid());

        await using (var context = CreateContext())
        {
            context.Notifications.AddRange(current, other);
            await context.SaveChangesAsync();
        }

        try
        {
            using var factory = new NotificationsFactory();
            using var client = factory.CreateAuthenticatedClient(currentUserId);

            var items = await client.GetFromJsonAsync<List<NotificationResponse>>(
                "/api/notifications",
                WebJson);
            var before = await client.GetFromJsonAsync<UnreadNotificationCountResponse>(
                "/api/notifications/unread-count",
                WebJson);
            var markOther = await client.PostAsync($"/api/notifications/{other.Id}/read", null);
            var markOwn = await client.PostAsync($"/api/notifications/{current.Id}/read", null);
            var after = await client.GetFromJsonAsync<UnreadNotificationCountResponse>(
                "/api/notifications/unread-count",
                WebJson);

            Assert.Single(items!);
            Assert.Equal(current.Id, items![0].Id);
            Assert.Equal(1, before!.Count);
            Assert.Equal(HttpStatusCode.NotFound, markOther.StatusCode);
            Assert.Equal(HttpStatusCode.OK, markOwn.StatusCode);
            Assert.Equal(0, after!.Count);
        }
        finally
        {
            await DeleteNotificationRowsAsync(current.SourceEventId, other.SourceEventId);
        }
    }

    private static async Task HandleAsync(IntegrationEventEnvelope envelope)
    {
        await using var context = CreateContext();
        await new NotificationEventHandler(context).HandleAsync(envelope);
    }

    private static IntegrationEventEnvelope AssignedEnvelope(
        Guid incidentId,
        Guid eventId,
        Guid userId,
        string email)
    {
        var payload = $$"""
            {"IncidentId":"{{incidentId}}","AssignedToUserId":"{{userId}}","ActorUserId":"{{Guid.NewGuid()}}","EventId":"{{eventId}}","OccurredAt":"2026-09-04T00:00:00Z"}
            """;

        return new IntegrationEventEnvelope(
            eventId,
            IncidentIntegrationEventTypes.Assigned,
            1,
            DateTime.Parse("2026-09-04T00:00:00Z").ToUniversalTime(),
            incidentId,
            "Incident",
            Guid.NewGuid(),
            null,
            JsonDocument.Parse(payload).RootElement.Clone(),
            [new IntegrationEventRecipient(userId, email, "AirSms Agent")]);
    }

    private static async Task<AirSms.Notifications.Models.Notification> AddPendingEmailNotificationAsync()
    {
        var notification = new AirSms.Notifications.Models.Notification(
            Guid.NewGuid(),
            "IncidentAssigned",
            "Incident assigned",
            "Incident has been assigned to you.",
            Guid.NewGuid(),
            Guid.NewGuid());
        var delivery = new NotificationDelivery(
            notification.Id,
            NotificationChannel.Email,
            "agent@airsms.test");

        await using var context = CreateContext();
        context.Notifications.Add(notification);
        context.NotificationDeliveries.Add(delivery);
        await context.SaveChangesAsync();
        return notification;
    }

    private static EmailDeliveryWorker CreateEmailWorker(
        IEmailSender sender,
        int maxAttempts = 3)
    {
        var services = new ServiceCollection();
        services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddSingleton(sender);

        var provider = services.BuildServiceProvider();
        return new EmailDeliveryWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new EmailOptions
            {
                Enabled = true,
                MaxAttempts = maxAttempts,
                PollIntervalSeconds = 1
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailDeliveryWorker>.Instance);
    }

    private static NotificationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new NotificationsDbContext(options);
    }

    private static async Task<bool> DatabaseAvailable()
    {
        try
        {
            await using var context = CreateContext();
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static async Task DeleteNotificationRowsAsync(params Guid[] sourceEventIds)
    {
        await using var context = CreateContext();
        var notificationIds = await context.Notifications
            .Where(notification => sourceEventIds.Contains(notification.SourceEventId))
            .Select(notification => notification.Id)
            .ToListAsync();

        await context.NotificationDeliveries
            .Where(delivery => notificationIds.Contains(delivery.NotificationId))
            .ExecuteDeleteAsync();
        await context.Notifications
            .Where(notification => sourceEventIds.Contains(notification.SourceEventId))
            .ExecuteDeleteAsync();
    }

    private sealed class FakeEmailSender(bool shouldFail = false) : IEmailSender
    {
        public List<EmailDeliveryRequest> Sent { get; } = [];

        public Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                throw new InvalidOperationException("SMTP unavailable.");
            }

            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class NotificationsFactory : WebApplicationFactory<AirSms.Notifications.Program>
    {
        public HttpClient CreateAuthenticatedClient(Guid userId)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(userId));
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<NotificationsDbContext>>();
                services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(ConnectionString));
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(new FakeEmailSender());
            });
        }

        private static string CreateToken(Guid userId)
        {
            const string signingKey = "local-development-only-signing-key-replace-outside-development";
            var token = new JwtSecurityToken(
                "AirSms.Api",
                "AirSms.Client",
                [
                    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, "agent@airsms.test"),
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Email, "agent@airsms.test")
                ],
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
