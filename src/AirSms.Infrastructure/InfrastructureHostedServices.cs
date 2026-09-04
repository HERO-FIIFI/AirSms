namespace AirSms.Infrastructure;

[Flags]
public enum InfrastructureHostedServices
{
    None = 0,
    OutboxPublisher = 1,
    KafkaConsumer = 2,
    All = OutboxPublisher | KafkaConsumer
}
