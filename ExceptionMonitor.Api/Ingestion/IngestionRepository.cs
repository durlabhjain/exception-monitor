using System.Text.Json;
using Dapper;
using ExceptionMonitor.Api.Auth;
using ExceptionMonitor.Api.Database;
using NpgsqlTypes;

namespace ExceptionMonitor.Api.Ingestion;

public interface IIngestionRepository
{
    Task<IngestResponse> SaveAsync(ApiKeyContext apiKey, NormalizedExceptionEvent exceptionEvent, HttpContext httpContext, CancellationToken cancellationToken);
}

public sealed class IngestionRepository(IDbConnectionFactory db) : IIngestionRepository
{
    public async Task<IngestResponse> SaveAsync(ApiKeyContext apiKey, NormalizedExceptionEvent exceptionEvent, HttpContext httpContext, CancellationToken cancellationToken)
    {
        using var connection = await db.OpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var groupId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"
            insert into exception_groups(client_id, application_id, environment, fingerprint, exception_type, normalized_message, severity, first_seen_at, last_seen_at, total_count)
            values(@ClientId, @ApplicationId, @Environment, @Fingerprint, @ExceptionType, @Message, @Severity, @OccurredAt, @OccurredAt, 1)
            on conflict (application_id, environment, fingerprint) do update set
                last_seen_at = greatest(exception_groups.last_seen_at, excluded.last_seen_at),
                total_count = exception_groups.total_count + 1,
                updated_at = now(),
                status = case when exception_groups.status = 'Resolved' then 'Open' else exception_groups.status end
            returning id",
            new
            {
                apiKey.ClientId,
                apiKey.ApplicationId,
                exceptionEvent.Environment,
                exceptionEvent.Fingerprint,
                exceptionEvent.ExceptionType,
                exceptionEvent.Message,
                exceptionEvent.Severity,
                exceptionEvent.OccurredAt
            }, tx, cancellationToken: cancellationToken));

        var eventId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"
            insert into exception_events(
                client_id, application_id, api_key_id, group_id, environment, severity, exception_type, message, stack_trace, fingerprint,
                occurred_at, source, release, correlation_id, trace_id, span_id, user_hash, request_method, request_url, request_route,
                request_referrer, request_status_code, remote_ip, user_agent, payload_format, payload_size, tags, metadata, raw_payload)
            values(
                @ClientId, @ApplicationId, @ApiKeyId, @GroupId, @Environment, @Severity, @ExceptionType, @Message, @StackTrace, @Fingerprint,
                @OccurredAt, @Source, @Release, @CorrelationId, @TraceId, @SpanId, @UserHash, @RequestMethod, @RequestUrl, @RequestRoute,
                @RequestReferrer, @RequestStatusCode, cast(@RemoteIp as inet), @UserAgent, @PayloadFormat, @PayloadSize, cast(@Tags as jsonb), cast(@Metadata as jsonb), cast(@RawPayload as jsonb))
            returning id",
            new
            {
                apiKey.ClientId,
                apiKey.ApplicationId,
                apiKey.ApiKeyId,
                GroupId = groupId,
                exceptionEvent.Environment,
                exceptionEvent.Severity,
                exceptionEvent.ExceptionType,
                exceptionEvent.Message,
                exceptionEvent.StackTrace,
                exceptionEvent.Fingerprint,
                exceptionEvent.OccurredAt,
                exceptionEvent.Source,
                exceptionEvent.Release,
                exceptionEvent.CorrelationId,
                exceptionEvent.TraceId,
                exceptionEvent.SpanId,
                exceptionEvent.UserHash,
                exceptionEvent.RequestMethod,
                exceptionEvent.RequestUrl,
                exceptionEvent.RequestRoute,
                exceptionEvent.RequestReferrer,
                exceptionEvent.RequestStatusCode,
                RemoteIp = httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
                exceptionEvent.PayloadFormat,
                exceptionEvent.PayloadSize,
                Tags = JsonSerializer.Serialize(exceptionEvent.Tags),
                Metadata = exceptionEvent.Metadata.GetRawText(),
                RawPayload = exceptionEvent.RawPayload?.GetRawText()
            }, tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "update exception_groups set last_event_id = @EventId where id = @GroupId",
            new { EventId = eventId, GroupId = groupId }, tx, cancellationToken: cancellationToken));

        await QueueFirstSeenNotificationsAsync(connection, tx, apiKey.ApplicationId, groupId, eventId, exceptionEvent, cancellationToken);

        tx.Commit();
        return new IngestResponse(eventId, groupId, exceptionEvent.Fingerprint, "Accepted");
    }

    private static async Task QueueFirstSeenNotificationsAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction tx, Guid applicationId, Guid groupId, Guid eventId, NormalizedExceptionEvent exceptionEvent, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into notification_deliveries(application_id, recipient_id, rule_id, group_id, delivery_type, subject, body, payload)
            select r.application_id,
                   nr.id,
                   r.id,
                   @GroupId,
                   nr.type,
                   concat('[', @Environment, '] ', @ExceptionType, ': ', left(@Message, 120)),
                   concat('First seen error group ', @GroupId, E'\nEvent: ', @EventId, E'\n\n', @Message, E'\n\n', @StackTrace),
                   jsonb_build_object('eventId', @EventId, 'groupId', @GroupId, 'fingerprint', @Fingerprint)
            from notification_rules r
            inner join notification_recipients nr on nr.application_id = r.application_id and nr.is_active = true
            inner join application_environments env on env.application_id = r.application_id and lower(env.name) = @Environment and env.notifications_enabled = true
            inner join exception_groups g on g.id = @GroupId
            where r.application_id = @ApplicationId
              and r.is_active = true
              and r.event_type = 'FirstSeen'
              and (r.environment is null or lower(r.environment) = @Environment)
              and g.total_count = 1",
            new
            {
                ApplicationId = applicationId,
                GroupId = groupId,
                EventId = eventId,
                exceptionEvent.Environment,
                ExceptionType = exceptionEvent.ExceptionType ?? "Exception",
                exceptionEvent.Message,
                exceptionEvent.StackTrace,
                exceptionEvent.Fingerprint
            }, tx, cancellationToken: cancellationToken));
    }
}
