using Dapper;
using ExceptionMonitor.Api.Auth;
using ExceptionMonitor.Api.Database;

namespace ExceptionMonitor.Api.Ingestion;

public static class EventQueryEndpoints
{
    public static RouteGroupBuilder MapEventQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").AddEndpointFilter<AdminApiKeyFilter>().WithTags("Events");

        group.MapGet("/error-groups", async (Guid? clientId, Guid? applicationId, string? environment, string? status, string? q, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync(new CommandDefinition(
                @"select g.id, g.client_id as ""clientId"", c.name as ""clientName"", g.application_id as ""applicationId"", a.name as ""applicationName"",
                         g.environment, g.fingerprint, g.exception_type as ""exceptionType"", g.normalized_message as ""message"", g.severity,
                         g.status, g.first_seen_at as ""firstSeenAt"", g.last_seen_at as ""lastSeenAt"", g.total_count as ""totalCount""
                  from exception_groups g
                  inner join clients c on c.id = g.client_id
                  inner join applications a on a.id = g.application_id
                  where (@ClientId is null or g.client_id = @ClientId)
                    and (@ApplicationId is null or g.application_id = @ApplicationId)
                    and (@Environment is null or lower(g.environment) = lower(@Environment))
                    and (@Status is null or g.status = @Status)
                    and (@Q is null or g.normalized_message ilike '%' || @Q || '%' or g.exception_type ilike '%' || @Q || '%')
                  order by g.last_seen_at desc
                  limit 200",
                new { ClientId = clientId, ApplicationId = applicationId, Environment = environment, Status = status, Q = q }, cancellationToken: ct));
            return Results.Ok(rows);
        });

        group.MapGet("/error-groups/{id:guid}", async (Guid id, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var groupRow = (IDictionary<string, object?>)(await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
                @"select g.id, g.client_id as ""clientId"", c.name as ""clientName"", g.application_id as ""applicationId"", a.name as ""applicationName"",
                         g.environment, g.fingerprint, g.exception_type as ""exceptionType"", g.normalized_message as ""message"", g.severity,
                         g.status, g.first_seen_at as ""firstSeenAt"", g.last_seen_at as ""lastSeenAt"", g.total_count as ""totalCount""
                  from exception_groups g
                  inner join clients c on c.id = g.client_id
                  inner join applications a on a.id = g.application_id
                  where g.id = @Id",
                new { Id = id }, cancellationToken: ct)));
            if (groupRow is null) return Results.NotFound();
            var events = await connection.QueryAsync(new CommandDefinition(
                @"select id, occurred_at as ""occurredAt"", received_at as ""receivedAt"", severity,
                         exception_type as ""exceptionType"", message, request_url as ""requestUrl"",
                         request_route as ""requestRoute"", source, release, correlation_id as ""correlationId""
                  from exception_events where group_id = @Id order by received_at desc limit 50",
                new { Id = id }, cancellationToken: ct));
            groupRow["events"] = events;
            return Results.Ok(groupRow);
        });

        group.MapPost("/error-groups/{id:guid}/status", async (Guid id, string status, IDbConnectionFactory db, CancellationToken ct) =>
        {
            if (!new[] { "Open", "Acknowledged", "Resolved", "Ignored" }.Contains(status)) return Results.BadRequest("Invalid status.");
            using var connection = await db.OpenConnectionAsync(ct);
            var count = await connection.ExecuteAsync(new CommandDefinition(
                "update exception_groups set status = @Status, updated_at = now() where id = @Id", new { Id = id, Status = status }, cancellationToken: ct));
            return count == 0 ? Results.NotFound() : Results.NoContent();
        });

        group.MapGet("/events", async (Guid? applicationId, string? environment, string? severity, string? q, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync(new CommandDefinition(
                @"select id, client_id as ""clientId"", application_id as ""applicationId"", group_id as ""groupId"", environment, severity,
                         exception_type as ""exceptionType"", message, occurred_at as ""occurredAt"", received_at as ""receivedAt"",
                         request_url as ""requestUrl"", request_route as ""requestRoute"", correlation_id as ""correlationId""
                  from exception_events
                  where (@ApplicationId is null or application_id = @ApplicationId)
                    and (@Environment is null or lower(environment) = lower(@Environment))
                    and (@Severity is null or severity = @Severity)
                    and (@Q is null or search_vector @@ websearch_to_tsquery('english', @Q))
                  order by received_at desc
                  limit 200",
                new { ApplicationId = applicationId, Environment = environment, Severity = severity, Q = q }, cancellationToken: ct));
            return Results.Ok(rows);
        });

        group.MapGet("/events/{id:guid}", async (Guid id, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
                @"select id, group_id as ""groupId"", client_id as ""clientId"", application_id as ""applicationId"",
                         environment, severity, exception_type as ""exceptionType"", message,
                         stack_trace as ""stackTrace"", fingerprint, occurred_at as ""occurredAt"", received_at as ""receivedAt"",
                         source, release, correlation_id as ""correlationId"", trace_id as ""traceId"", span_id as ""spanId"",
                         user_hash as ""userHash"", request_method as ""requestMethod"",
                         request_url as ""requestUrl"", request_route as ""requestRoute"", request_referrer as ""requestReferrer"",
                         request_status_code as ""requestStatusCode"", host(remote_ip) as ""remoteIp"", user_agent as ""userAgent"",
                         request_headers::text as ""requestHeaders"", request_params::text as ""requestParams"",
                         request_body::text as ""requestBody"", query_string::text as ""queryString"",
                         tags::text as ""tags"", metadata::text as ""metadata"", raw_payload::text as ""rawPayload""
                  from exception_events where id = @Id",
                new { Id = id }, cancellationToken: ct));
            return row is null ? Results.NotFound() : Results.Ok(row);
        });

        return group;
    }
}
