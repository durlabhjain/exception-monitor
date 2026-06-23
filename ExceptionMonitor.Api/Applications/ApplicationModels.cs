namespace ExceptionMonitor.Api.Applications;

public sealed record CreateClientRequest(string Name, string? Slug);
public sealed record CreateApplicationRequest(Guid ClientId, string Name, string? Slug, string? DefaultEnvironment, int? DefaultRetentionDays);
public sealed record CreateApiKeyRequest(Guid ApplicationId, string Name, int? RetentionDays, int? RateLimitPerMinute, bool AllowAllIps, string[]? IpAllowlist);
public sealed record CreatedApiKeyResponse(Guid Id, string Name, string KeyPrefix, string PlaintextKey);
public sealed record AddRecipientRequest(Guid ApplicationId, string Type, string Name, string? Email, string? WebhookUrl, string? WebhookSecret);
public sealed record UpdateRecipientRequest(string Name, string? Email, string? WebhookUrl, string? WebhookSecret);
public sealed record CreateNotificationRuleRequest(Guid ApplicationId, string? Environment, string Name, string EventType, string SeverityMinimum, int? ThresholdCount, int? ThresholdWindowMinutes, int CooldownMinutes, int? DigestIntervalMinutes);
public sealed record UpdateNotificationRuleRequest(string? Environment, string Name, string EventType, string SeverityMinimum, int? ThresholdCount, int? ThresholdWindowMinutes, int CooldownMinutes, int? DigestIntervalMinutes);
public sealed record SetEnvironmentRequest(Guid ApplicationId, string Name, bool NotificationsEnabled);
