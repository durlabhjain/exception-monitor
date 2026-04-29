namespace ExceptionMonitor.Api.Notifications;

public sealed class NotificationOptions
{
    public int WorkerIntervalSeconds { get; set; } = 30;
    public string FromEmail { get; set; } = "exceptions@example.com";
    public string FromName { get; set; } = "Exception Monitor";
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
