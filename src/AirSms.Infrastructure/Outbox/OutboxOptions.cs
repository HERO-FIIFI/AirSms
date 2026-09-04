namespace AirSms.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public int MaxRetryCount { get; set; } = 5;
}
