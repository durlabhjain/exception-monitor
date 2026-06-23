using Dapper;
using ExceptionMonitor.Api.Auth;
using ExceptionMonitor.Api.Common;
using ExceptionMonitor.Api.Database;

namespace ExceptionMonitor.Api.Applications;

public static class ApplicationEndpoints
{
    public static RouteGroupBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").AddEndpointFilter<AdminApiKeyFilter>().WithTags("Admin");

        group.MapGet("/clients", async (IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync("select id, name, slug, is_active as \"isActive\", created_at as \"createdAt\" from clients order by name");
            return Results.Ok(rows);
        });

        group.MapPost("/clients", async (CreateClientRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Name is required.");
            var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.FromName(request.Name) : Slug.FromName(request.Slug);
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                "insert into clients(name, slug) values(@Name, @Slug) returning id, name, slug, is_active as \"isActive\", created_at as \"createdAt\"",
                new { request.Name, Slug = slug }, cancellationToken: ct));
            return Results.Created($"/api/admin/clients/{row.id}", row);
        });

        group.MapGet("/applications", async (Guid? clientId, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync(new CommandDefinition(
                @"select a.id, a.client_id as ""clientId"", c.name as ""clientName"", a.name, a.slug,
                         a.default_environment as ""defaultEnvironment"", a.default_retention_days as ""defaultRetentionDays"",
                         a.is_active as ""isActive"", a.created_at as ""createdAt""
                  from applications a inner join clients c on c.id = a.client_id
                  where (@ClientId is null or a.client_id = @ClientId)
                  order by c.name, a.name",
                new { ClientId = clientId }, cancellationToken: ct));
            return Results.Ok(rows);
        });

        group.MapPost("/applications", async (CreateApplicationRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            if (request.ClientId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("ClientId and name are required.");
            var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.FromName(request.Name) : Slug.FromName(request.Slug);
            var environment = string.IsNullOrWhiteSpace(request.DefaultEnvironment) ? "production" : request.DefaultEnvironment.Trim().ToLowerInvariant();
            var retentionDays = request.DefaultRetentionDays.GetValueOrDefault(90);
            using var connection = await db.OpenConnectionAsync(ct);
            var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                @"insert into applications(client_id, name, slug, default_environment, default_retention_days)
                  values(@ClientId, @Name, @Slug, @Environment, @RetentionDays) returning id",
                new { request.ClientId, request.Name, Slug = slug, Environment = environment, RetentionDays = retentionDays }, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition(
                "insert into application_environments(application_id, name, notifications_enabled) values(@Id, @Environment, @Enabled) on conflict do nothing",
                new { Id = id, Environment = environment, Enabled = environment == "production" }, cancellationToken: ct));
            return Results.Created($"/api/admin/applications/{id}", new { id });
        });

        group.MapPost("/environments", async (SetEnvironmentRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into application_environments(application_id, name, notifications_enabled)
                  values(@ApplicationId, lower(@Name), @NotificationsEnabled)
                  on conflict (application_id, lower(name)) do update set notifications_enabled = excluded.notifications_enabled
                  returning id, application_id as ""applicationId"", name, notifications_enabled as ""notificationsEnabled""",
                request, cancellationToken: ct));
            return Results.Ok(row);
        });

        group.MapGet("/applications/{id:guid}/api-keys", async (Guid id, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync(new CommandDefinition(
                @"select id, name, key_prefix as ""keyPrefix"", rate_limit_per_minute as ""rateLimitPerMinute"",
                         retention_days as ""retentionDays"", is_active as ""isActive"",
                         last_used_at as ""lastUsedAt"", created_at as ""createdAt"", revoked_at as ""revokedAt""
                  from api_keys where application_id = @Id order by created_at desc",
                new { Id = id }, cancellationToken: ct));
            return Results.Ok(rows);
        });

        group.MapPost("/api-keys", async (CreateApiKeyRequest request, IDbConnectionFactory db, IApiKeyGenerator generator, IApiKeyHasher hasher, CancellationToken ct) =>
        {
            if (request.ApplicationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("ApplicationId and name are required.");
            var generated = generator.Generate();
            using var connection = await db.OpenConnectionAsync(ct);
            using var tx = connection.BeginTransaction();
            var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                @"insert into api_keys(application_id, name, key_prefix, key_hash, retention_days, rate_limit_per_minute, allow_all_ips)
                  values(@ApplicationId, @Name, @Prefix, @Hash, @RetentionDays, @RateLimit, @AllowAllIps) returning id",
                new
                {
                    request.ApplicationId,
                    request.Name,
                    Prefix = generated.Prefix,
                    Hash = hasher.Hash(generated.Plaintext),
                    RetentionDays = request.RetentionDays.GetValueOrDefault(90),
                    RateLimit = request.RateLimitPerMinute.GetValueOrDefault(1500),
                    request.AllowAllIps
                }, tx, cancellationToken: ct));

            foreach (var cidr in request.IpAllowlist ?? [])
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "insert into api_key_ip_allowlists(api_key_id, cidr) values(@Id, cast(@Cidr as cidr)) on conflict do nothing",
                    new { Id = id, Cidr = cidr }, tx, cancellationToken: ct));
            }

            tx.Commit();
            return Results.Created($"/api/admin/api-keys/{id}", new CreatedApiKeyResponse(id, request.Name, generated.Prefix, generated.Plaintext));
        });

        group.MapPost("/api-keys/{id:guid}/revoke", async (Guid id, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var count = await connection.ExecuteAsync(new CommandDefinition(
                "update api_keys set is_active = false, revoked_at = now() where id = @Id and revoked_at is null", new { Id = id }, cancellationToken: ct));
            return count == 0 ? Results.NotFound() : Results.NoContent();
        });

        group.MapGet("/applications/{id:guid}/recipients", async (Guid id, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync(new CommandDefinition(
                @"select id, application_id as ""applicationId"", type, name, email, webhook_url as ""webhookUrl"", is_active as ""isActive"", created_at as ""createdAt""
                  from notification_recipients where application_id = @Id order by created_at",
                new { Id = id }, cancellationToken: ct));
            return Results.Ok(rows);
        });

        group.MapGet("/applications/{id:guid}/notification-rules", async (Guid id, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync(new CommandDefinition(
                @"select id, application_id as ""applicationId"", environment, name, event_type as ""eventType"",
                         severity_minimum as ""severityMinimum"", threshold_count as ""thresholdCount"",
                         threshold_window_minutes as ""thresholdWindowMinutes"", cooldown_minutes as ""cooldownMinutes"",
                         digest_interval_minutes as ""digestIntervalMinutes"", is_active as ""isActive"", created_at as ""createdAt""
                  from notification_rules where application_id = @Id order by created_at",
                new { Id = id }, cancellationToken: ct));
            return Results.Ok(rows);
        });

        group.MapPost("/recipients", async (AddRecipientRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into notification_recipients(application_id, type, name, email, webhook_url, webhook_secret)
                  values(@ApplicationId, @Type, @Name, @Email, @WebhookUrl, @WebhookSecret)
                  returning id, application_id as ""applicationId"", type, name, email, webhook_url as ""webhookUrl"", is_active as ""isActive"", created_at as ""createdAt""",
                request, cancellationToken: ct));
            return Results.Created($"/api/admin/recipients/{row.id}", row);
        });

        group.MapPut("/recipients/{id:guid}", async (Guid id, UpdateRecipientRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
                @"update notification_recipients
                  set name = @Name, email = @Email, webhook_url = @WebhookUrl, webhook_secret = @WebhookSecret
                  where id = @Id
                  returning id, application_id as ""applicationId"", type, name, email, webhook_url as ""webhookUrl"", is_active as ""isActive"", created_at as ""createdAt""",
                new { Id = id, request.Name, request.Email, request.WebhookUrl, request.WebhookSecret }, cancellationToken: ct));
            return row is null ? Results.NotFound() : Results.Ok(row);
        });

        group.MapDelete("/recipients/{id:guid}", async (Guid id, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var count = await connection.ExecuteAsync(new CommandDefinition(
                "update notification_recipients set is_active = false where id = @Id and is_active = true",
                new { Id = id }, cancellationToken: ct));
            return count == 0 ? Results.NotFound() : Results.NoContent();
        });

        group.MapPost("/notification-rules", async (CreateNotificationRuleRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into notification_rules(application_id, environment, name, event_type, severity_minimum, threshold_count, threshold_window_minutes, cooldown_minutes, digest_interval_minutes)
                  values(@ApplicationId, lower(@Environment), @Name, @EventType, @SeverityMinimum, @ThresholdCount, @ThresholdWindowMinutes, @CooldownMinutes, @DigestIntervalMinutes)
                  returning id, application_id as ""applicationId"", environment, name, event_type as ""eventType"",
                            severity_minimum as ""severityMinimum"", threshold_count as ""thresholdCount"",
                            threshold_window_minutes as ""thresholdWindowMinutes"", cooldown_minutes as ""cooldownMinutes"",
                            digest_interval_minutes as ""digestIntervalMinutes"", is_active as ""isActive"", created_at as ""createdAt""",
                request, cancellationToken: ct));
            return Results.Created($"/api/admin/notification-rules/{row.id}", row);
        });

        group.MapPut("/notification-rules/{id:guid}", async (Guid id, UpdateNotificationRuleRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
                @"update notification_rules
                  set environment = lower(@Environment), name = @Name, event_type = @EventType,
                      severity_minimum = @SeverityMinimum, threshold_count = @ThresholdCount,
                      threshold_window_minutes = @ThresholdWindowMinutes, cooldown_minutes = @CooldownMinutes,
                      digest_interval_minutes = @DigestIntervalMinutes
                  where id = @Id
                  returning id, application_id as ""applicationId"", environment, name, event_type as ""eventType"",
                            severity_minimum as ""severityMinimum"", threshold_count as ""thresholdCount"",
                            threshold_window_minutes as ""thresholdWindowMinutes"", cooldown_minutes as ""cooldownMinutes"",
                            digest_interval_minutes as ""digestIntervalMinutes"", is_active as ""isActive"", created_at as ""createdAt""",
                new { Id = id, request.Environment, request.Name, request.EventType, request.SeverityMinimum,
                      request.ThresholdCount, request.ThresholdWindowMinutes, request.CooldownMinutes, request.DigestIntervalMinutes },
                cancellationToken: ct));
            return row is null ? Results.NotFound() : Results.Ok(row);
        });

        group.MapDelete("/notification-rules/{id:guid}", async (Guid id, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var count = await connection.ExecuteAsync(new CommandDefinition(
                "update notification_rules set is_active = false where id = @Id and is_active = true",
                new { Id = id }, cancellationToken: ct));
            return count == 0 ? Results.NotFound() : Results.NoContent();
        });

        return group;
    }
}
