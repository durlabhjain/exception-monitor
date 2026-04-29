using Microsoft.Extensions.Options;

namespace ExceptionMonitor.Api.Auth;

public sealed class AdminApiKeyFilter(IOptions<SecurityOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configuredKey = options.Value.AdminApiKey;
        if (string.IsNullOrWhiteSpace(configuredKey) || configuredKey.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem("Admin API key is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var request = context.HttpContext.Request;
        if (!request.Headers.TryGetValue("X-Admin-Api-Key", out var supplied) || supplied.Count == 0 || supplied[0] != configuredKey)
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
