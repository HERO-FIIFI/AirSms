using AirSms.Application.Common.Interfaces;
using AirSms.Infrastructure.Authentication;
using AirSms.Infrastructure.Kafka;
using AirSms.Infrastructure.Outbox;
using AirSms.Infrastructure.Outbox.Handlers;
using AirSms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AirSms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        InfrastructureHostedServices hostedServices = InfrastructureHostedServices.All)
    {
        var connectionString = configuration.GetConnectionString("AirSmsDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'AirSmsDatabase' was not found.");

        services.AddDbContext<AirSmsDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IAirSmsDbContext>(provider =>
            provider.GetRequiredService<AirSmsDbContext>());
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        var kafkaSection = configuration.GetSection(KafkaOptions.SectionName);
        services.Configure<KafkaOptions>(options =>
        {
            options.Enabled = bool.TryParse(kafkaSection[nameof(KafkaOptions.Enabled)], out var enabled) && enabled;
            options.BootstrapServers = kafkaSection[nameof(KafkaOptions.BootstrapServers)] ?? options.BootstrapServers;
            options.IncidentEventsTopic = kafkaSection[nameof(KafkaOptions.IncidentEventsTopic)] ?? options.IncidentEventsTopic;
            options.ClientId = kafkaSection[nameof(KafkaOptions.ClientId)] ?? options.ClientId;
            options.ConsumerGroup = kafkaSection[nameof(KafkaOptions.ConsumerGroup)] ?? options.ConsumerGroup;
        });
        var outboxSection = configuration.GetSection(OutboxOptions.SectionName);
        services.Configure<OutboxOptions>(options =>
        {
            options.PollIntervalSeconds = ReadInt(
                outboxSection[nameof(OutboxOptions.PollIntervalSeconds)],
                options.PollIntervalSeconds);
            options.BatchSize = ReadInt(
                outboxSection[nameof(OutboxOptions.BatchSize)],
                options.BatchSize);
            options.MaxRetryCount = ReadInt(
                outboxSection[nameof(OutboxOptions.MaxRetryCount)],
                options.MaxRetryCount);
        });
        services.AddSingleton<IntegrationEventMapper>();
        services.AddScoped<NotificationRecipientEnricher>();
        services.AddScoped<IntegrationEventDispatcher>();
        services.AddScoped<AuditProjectionHandler>();

        var kafkaEnabled = bool.TryParse(kafkaSection[nameof(KafkaOptions.Enabled)], out var enabled) && enabled;
        var kafkaBootstrapServers = kafkaSection[nameof(KafkaOptions.BootstrapServers)];
        if (kafkaEnabled && !string.IsNullOrWhiteSpace(kafkaBootstrapServers))
        {
            if (hostedServices.HasFlag(InfrastructureHostedServices.OutboxPublisher))
            {
                services.AddSingleton<IIntegrationEventPublisher, KafkaIntegrationEventPublisher>();
                services.AddHostedService<OutboxProcessor>();
            }

            if (hostedServices.HasFlag(InfrastructureHostedServices.KafkaConsumer))
            {
                services.AddHostedService<KafkaIntegrationEventConsumer>();
            }
        }

        return services;
    }

    private static int ReadInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
