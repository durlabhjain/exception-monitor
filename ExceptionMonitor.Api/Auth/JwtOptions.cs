namespace ExceptionMonitor.Api.Auth;

public sealed class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ExceptionMonitor";
    public string Audience { get; set; } = "ExceptionMonitorUI";
    public int ExpiryHours { get; set; } = 24;
}
