using Microsoft.Extensions.Options;

namespace ExceptionMonitor.Api.Auth;

public sealed class AdminApiKeyFilter(IOptions<SecurityOptions> securityOptions, IJwtService jwt) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Accept a valid user JWT
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization[7..].Trim();
            var principal = jwt.Validate(token);
            if (principal is not null)
                return await next(context);
        }

        // Fall back to admin API key (superadmin / bootstrap access)
        var configuredKey = securityOptions.Value.AdminApiKey;
        if (!string.IsNullOrWhiteSpace(configuredKey) &&
            !configuredKey.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase) &&
            httpContext.Request.Headers.TryGetValue("X-Admin-Api-Key", out var supplied) &&
            supplied.Count > 0 && supplied[0] == configuredKey)
        {
            return await next(context);
        }

        return Results.Unauthorized();
    }
}
