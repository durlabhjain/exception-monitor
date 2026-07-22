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
                request_referrer, request_status_code, remote_ip, user_agent, request_headers, request_params, request_body, query_string,
                payload_format, payload_size, tags, metadata, raw_payload)
            values(
                @ClientId, @ApplicationId, @ApiKeyId, @GroupId, @Environment, @Severity, @ExceptionType, @Message, @StackTrace, @Fingerprint,
                @OccurredAt, @Source, @Release, @CorrelationId, @TraceId, @SpanId, @UserHash, @RequestMethod, @RequestUrl, @RequestRoute,
                @RequestReferrer, @RequestStatusCode, cast(@RemoteIp as inet), @UserAgent, cast(@RequestHeaders as jsonb), cast(@RequestParams as jsonb),
                cast(@RequestBody as jsonb), cast(@QueryString as jsonb), @PayloadFormat, @PayloadSize, cast(@Tags as jsonb), cast(@Metadata as jsonb), cast(@RawPayload as jsonb))
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
                RequestHeaders = exceptionEvent.RequestHeaders?.GetRawText(),
                RequestParams = exceptionEvent.RequestParams?.GetRawText(),
                RequestBody = exceptionEvent.RequestBody?.GetRawText(),
                QueryString = exceptionEvent.QueryString?.GetRawText(),
                exceptionEvent.PayloadFormat,
                exceptionEvent.PayloadSize,
                Tags = JsonSerializer.Serialize(exceptionEvent.Tags),
                Metadata = exceptionEvent.Metadata.GetRawText(),
                RawPayload = exceptionEvent.RawPayload?.GetRawText()
            }, tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "update exception_groups set last_event_id = @EventId where id = @GroupId",
            new { EventId = eventId, GroupId = groupId }, tx, cancellationToken: cancellationToken));

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        await QueueFirstSeenNotificationsAsync(connection, tx, apiKey.ApplicationId, apiKey.ApplicationName, groupId, eventId, exceptionEvent, remoteIp, cancellationToken);
        await QueueThresholdNotificationsAsync(connection, tx, apiKey.ApplicationId, apiKey.ApplicationName, groupId, eventId, exceptionEvent, remoteIp, cancellationToken);

        tx.Commit();
        return new IngestResponse(eventId, groupId, exceptionEvent.Fingerprint, "Accepted");
    }

    private static async Task QueueFirstSeenNotificationsAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction tx, Guid applicationId, string applicationName, Guid groupId, Guid eventId, NormalizedExceptionEvent exceptionEvent, string? remoteIp, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into notification_deliveries(application_id, recipient_id, rule_id, group_id, delivery_type, subject, body, payload)
            select r.application_id,
                   nr.id,
                   r.id,
                   @GroupId,
                   nr.type,
                   concat('Error in ', @ApplicationName, ' - ', @Environment, ' - (', @OccurredAtUtc, ' UTC) - ', coalesce(@Source, '—')),
                   @Body,
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
                ApplicationName = applicationName,
                OccurredAtUtc = exceptionEvent.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                GroupId = groupId,
                EventId = eventId,
                exceptionEvent.Environment,
                ExceptionType = exceptionEvent.ExceptionType ?? "Exception",
                exceptionEvent.Message,
                exceptionEvent.StackTrace,
                exceptionEvent.Fingerprint,
                Source = exceptionEvent.Source,
                Body = BuildEmailBody(exceptionEvent, remoteIp)
            }, tx, cancellationToken: cancellationToken));
    }

    private static async Task QueueThresholdNotificationsAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction tx, Guid applicationId, string applicationName, Guid groupId, Guid eventId, NormalizedExceptionEvent exceptionEvent, string? remoteIp, CancellationToken cancellationToken)
    {
        // Find all threshold rules whose count-in-window is now met, then insert deliveries and
        // stamp notification_suppressed_until so the same group doesn't fire again during cooldown.
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            with matched_rules as (
                select r.id          as rule_id,
                       r.application_id,
                       nr.id         as recipient_id,
                       nr.type       as delivery_type,
                       r.cooldown_minutes
                from notification_rules r
                inner join notification_recipients nr
                    on nr.application_id = r.application_id and nr.is_active = true
                inner join application_environments env
                    on env.application_id = r.application_id
                   and lower(env.name) = @Environment
                   and env.notifications_enabled = true
                inner join exception_groups g on g.id = @GroupId
                where r.application_id = @ApplicationId
                  and r.is_active = true
                  and r.event_type = 'Threshold'
                  and (r.environment is null or lower(r.environment) = @Environment)
                  and r.threshold_count is not null
                  and r.threshold_window_minutes is not null
                  and (g.notification_suppressed_until is null or g.notification_suppressed_until < now())
                  and (
                      select count(*)
                      from exception_events e
                      where e.group_id = @GroupId
                        and e.received_at >= now() - make_interval(mins => r.threshold_window_minutes)
                  ) >= r.threshold_count
            ),
            inserted as (
                insert into notification_deliveries(application_id, recipient_id, rule_id, group_id, delivery_type, subject, body, payload)
                select application_id,
                       recipient_id,
                       rule_id,
                       @GroupId,
                       delivery_type,
                       concat('Error in ', @ApplicationName, ' - ', @Environment, ' - (', @OccurredAtUtc, ' UTC) - ', coalesce(@Source, '—')),
                       @Body,
                       jsonb_build_object('eventId', @EventId, 'groupId', @GroupId, 'fingerprint', @Fingerprint)
                from matched_rules
                returning id
            )
            update exception_groups
            set notification_suppressed_until = now() + make_interval(mins => (select min(cooldown_minutes) from matched_rules))
            where id = @GroupId
              and exists (select 1 from inserted)",
            new
            {
                ApplicationId = applicationId,
                ApplicationName = applicationName,
                OccurredAtUtc = exceptionEvent.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                GroupId = groupId,
                EventId = eventId,
                exceptionEvent.Environment,
                ExceptionType = exceptionEvent.ExceptionType ?? "Exception",
                exceptionEvent.Message,
                exceptionEvent.StackTrace,
                exceptionEvent.Fingerprint,
                Source = exceptionEvent.Source,
                Body = BuildEmailBody(exceptionEvent, remoteIp)
            }, tx, cancellationToken: cancellationToken));
    }

    private static string BuildEmailBody(NormalizedExceptionEvent e, string? remoteIp)
    {
        static string Val(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Method   : {Val(e.RequestMethod)}");
        sb.AppendLine($"URL      : {Val(e.RequestUrl)}");
        sb.AppendLine($"IP       : {Val(remoteIp)}");
        sb.AppendLine($"User     : {Val(e.UserName)}");
        sb.AppendLine($"Route    : {Val(e.RequestRoute)}");
        sb.AppendLine($"Referrer : {Val(e.RequestReferrer)}");
        sb.AppendLine();
        sb.AppendLine(e.StackTrace);
        return sb.ToString();
    }
}
