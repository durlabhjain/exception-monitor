using System.Net;
using Dapper;
using ExceptionMonitor.Api.Database;

namespace ExceptionMonitor.Api.Auth;

public interface IApiKeyAuthenticator
{
    Task<ApiKeyContext?> AuthenticateAsync(HttpContext httpContext, CancellationToken cancellationToken);
}

public sealed class ApiKeyAuthenticator(IDbConnectionFactory db, IApiKeyHasher hasher) : IApiKeyAuthenticator
{
    public async Task<ApiKeyContext?> AuthenticateAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var key = ReadApiKey(httpContext.Request);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var parts = key.Split('_', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "exm")
        {
            return null;
        }

        var prefix = parts[1];
        var hash = hasher.Hash(key);

        using var connection = await db.OpenConnectionAsync(cancellationToken);
        var context = await connection.QuerySingleOrDefaultAsync<ApiKeyContext>(new CommandDefinition(
            @"
            select
                k.id as ApiKeyId,
                a.id as ApplicationId,
                c.id as ClientId,
                a.name as ApplicationName,
                c.name as ClientName,
                a.default_environment as DefaultEnvironment,
                k.retention_days as RetentionDays,
                k.rate_limit_per_minute as RateLimitPerMinute,
                k.allow_all_ips as AllowAllIps
            from api_keys k
            inner join applications a on a.id = k.application_id
            inner join clients c on c.id = a.client_id
            where k.key_prefix = @Prefix
              and k.key_hash = @Hash
              and k.is_active = true
              and a.is_active = true
              and c.is_active = true
              and (k.expires_at is null or k.expires_at > now())
              and k.revoked_at is null",
            new { Prefix = prefix, Hash = hash }, cancellationToken: cancellationToken));

        if (context is null)
        {
            return null;
        }

        if (!context.AllowAllIps && !await IsIpAllowedAsync(connection, context.ApiKeyId, httpContext.Connection.RemoteIpAddress, cancellationToken))
        {
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "update api_keys set last_used_at = now() where id = @ApiKeyId",
            new { context.ApiKeyId }, cancellationToken: cancellationToken));

        return context;
    }

    private static string? ReadApiKey(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Authorization", out var authorization))
        {
            var value = authorization.ToString();
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return value[7..].Trim();
            }
        }

        if (request.Headers.TryGetValue("X-Exception-Api-Key", out var apiKey))
        {
            return apiKey.ToString();
        }

        return null;
    }

    private static async Task<bool> IsIpAllowedAsync(System.Data.IDbConnection connection, Guid apiKeyId, IPAddress? remoteIp, CancellationToken cancellationToken)
    {
        if (remoteIp is null)
        {
            return false;
        }

        var remote = remoteIp.MapToIPv4().ToString();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists(select 1 from api_key_ip_allowlists where api_key_id = @ApiKeyId and cast(@RemoteIp as inet) <<= cidr)",
            new { ApiKeyId = apiKeyId, RemoteIp = remote }, cancellationToken: cancellationToken));
    }
}
