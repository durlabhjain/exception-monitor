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
                @"select g.id, g.client_id as ClientId, c.name as ClientName, g.application_id as ApplicationId, a.name as ApplicationName,
                         g.environment, g.fingerprint, g.exception_type as ExceptionType, g.normalized_message as Message, g.severity,
                         g.status, g.first_seen_at as FirstSeenAt, g.last_seen_at as LastSeenAt, g.total_count as TotalCount
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
            var groupRow = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
                "select * from exception_groups where id = @Id", new { Id = id }, cancellationToken: ct));
            if (groupRow is null) return Results.NotFound();
            var events = await connection.QueryAsync(new CommandDefinition(
                @"select id, occurred_at as OccurredAt, received_at as ReceivedAt, severity, exception_type as ExceptionType, message,
                         request_url as RequestUrl, request_route as RequestRoute, source, release, correlation_id as CorrelationId
                  from exception_events where group_id = @Id order by received_at desc limit 50",
                new { Id = id }, cancellationToken: ct));
            return Results.Ok(new { Group = groupRow, RecentEvents = events });
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
                @"select id, client_id as ClientId, application_id as ApplicationId, group_id as GroupId, environment, severity,
                         exception_type as ExceptionType, message, occurred_at as OccurredAt, received_at as ReceivedAt,
                         request_url as RequestUrl, request_route as RequestRoute, correlation_id as CorrelationId
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
            var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition("select * from exception_events where id = @Id", new { Id = id }, cancellationToken: ct));
            return row is null ? Results.NotFound() : Results.Ok(row);
        });

        return group;
    }
}
