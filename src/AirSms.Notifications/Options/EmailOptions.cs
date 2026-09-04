namespace AirSms.Notifications.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromAddress { get; set; } = "notifications@airsms.local";
    public string FromName { get; set; } = "AirSms Operations";
    public int MaxAttempts { get; set; } = 3;
    public int PollIntervalSeconds { get; set; } = 5;
}
