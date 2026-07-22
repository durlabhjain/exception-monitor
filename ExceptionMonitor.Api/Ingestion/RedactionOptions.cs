namespace ExceptionMonitor.Api.Ingestion;

public sealed class RedactionOptions
{
    public string[] HeaderNames { get; set; } = [];
    public string[] FieldNames { get; set; } = [];
}
