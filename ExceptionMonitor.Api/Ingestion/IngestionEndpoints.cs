using ExceptionMonitor.Api.Auth;
using Microsoft.Extensions.Options;

namespace ExceptionMonitor.Api.Ingestion;

public static class IngestionEndpoints
{
    public static RouteGroupBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingest").WithTags("Ingestion");

        group.MapPost("", async (HttpContext context, IApiKeyAuthenticator authenticator, IIngestionNormalizer normalizer, IIngestionRepository repository, IOptions<IngestionOptions> options, CancellationToken ct) =>
        {
            var apiKey = await authenticator.AuthenticateAsync(context, ct);
            if (apiKey is null) return Results.Unauthorized();
            if (context.Request.ContentLength > options.Value.MaxPayloadBytes) return Results.Problem("Payload exceeds the configured 1 MB limit.", statusCode: StatusCodes.Status413PayloadTooLarge);

            var normalized = context.Request.HasFormContentType
                ? await normalizer.NormalizeFormAsync(context, apiKey, ct)
                : await normalizer.NormalizeJsonAsync(context, apiKey, ct);

            if (normalized is null || string.IsNullOrWhiteSpace(normalized.Message) || string.IsNullOrWhiteSpace(normalized.StackTrace))
            {
                return Results.BadRequest("Both error/message and stackTrace are required.");
            }

            var response = await repository.SaveAsync(apiKey, normalized, context, ct);
            return Results.Accepted($"/api/events/{response.EventId}", response);
        }).DisableAntiforgery();

        group.MapPost("/form", async (HttpContext context, IApiKeyAuthenticator authenticator, IIngestionNormalizer normalizer, IIngestionRepository repository, IOptions<IngestionOptions> options, CancellationToken ct) =>
        {
            var apiKey = await authenticator.AuthenticateAsync(context, ct);
            if (apiKey is null) return Results.Unauthorized();
            if (context.Request.ContentLength > options.Value.MaxPayloadBytes) return Results.Problem("Payload exceeds the configured 1 MB limit.", statusCode: StatusCodes.Status413PayloadTooLarge);
            if (!context.Request.HasFormContentType) return Results.BadRequest("Expected form content.");

            var normalized = await normalizer.NormalizeFormAsync(context, apiKey, ct);
            if (normalized is null || string.IsNullOrWhiteSpace(normalized.Message) || string.IsNullOrWhiteSpace(normalized.StackTrace))
            {
                return Results.BadRequest("Both error/message and stackTrace are required.");
            }

            var response = await repository.SaveAsync(apiKey, normalized, context, ct);
            return Results.Accepted($"/api/events/{response.EventId}", response);
        }).DisableAntiforgery();

        return group;
    }
}
