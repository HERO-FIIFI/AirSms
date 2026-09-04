namespace AirSms.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public bool Enabled { get; set; }
    public string BootstrapServers { get; set; } = "";
    public string IncidentEventsTopic { get; set; } = "airsms.incident-events.v1";
    public string ClientId { get; set; } = "airsms-api";
    public string ConsumerGroup { get; set; } = "airsms-core-projections-v1";
}
