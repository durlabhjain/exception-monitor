namespace ExceptionMonitor.Api.Auth;

public sealed class SecurityOptions
{
    public string ApiKeyHashSecret { get; set; } = string.Empty;
    public string AdminApiKey { get; set; } = string.Empty;
}
