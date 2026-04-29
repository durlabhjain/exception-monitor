namespace ExceptionMonitor.Api.Auth;

public sealed record ApiKeyContext(
    Guid ApiKeyId,
    Guid ApplicationId,
    Guid ClientId,
    string ApplicationName,
    string ClientName,
    string DefaultEnvironment,
    int RetentionDays,
    int RateLimitPerMinute,
    bool AllowAllIps);
