namespace ExceptionMonitor.Api.Users;

public sealed record CreateUserRequest(string Email, string? DisplayName);
public sealed record GrantClientAccessRequest(Guid UserId, Guid ClientId, string Role, bool AllApplications);
public sealed record GrantApplicationAccessRequest(Guid UserId, Guid ApplicationId, string Role);
