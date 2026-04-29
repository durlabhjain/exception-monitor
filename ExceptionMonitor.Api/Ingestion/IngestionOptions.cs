namespace ExceptionMonitor.Api.Ingestion;

public sealed class IngestionOptions
{
    public int MaxPayloadBytes { get; set; } = 1_048_576;
    public int DefaultRateLimitPerMinute { get; set; } = 1500;
}
