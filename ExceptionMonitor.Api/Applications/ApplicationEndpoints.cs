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
            var rows = await connection.QueryAsync("select id, name, slug, is_active as IsActive, created_at as CreatedAt from clients order by name");
            return Results.Ok(rows);
        });

        group.MapPost("/clients", async (CreateClientRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Name is required.");
            var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.FromName(request.Name) : Slug.FromName(request.Slug);
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                "insert into clients(name, slug) values(@Name, @Slug) returning id, name, slug, is_active as IsActive, created_at as CreatedAt",
                new { request.Name, Slug = slug }, cancellationToken: ct));
            return Results.Created($"/api/admin/clients/{row.id}", row);
        });

        group.MapGet("/applications", async (Guid? clientId, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var rows = await connection.QueryAsync(new CommandDefinition(
                @"select a.id, a.client_id as ClientId, c.name as ClientName, a.name, a.slug, a.default_environment as DefaultEnvironment,
                         a.default_retention_days as DefaultRetentionDays, a.is_active as IsActive, a.created_at as CreatedAt
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
            return Results.Created($"/api/admin/applications/{id}", new { Id = id });
        });

        group.MapPost("/environments", async (SetEnvironmentRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into application_environments(application_id, name, notifications_enabled)
                  values(@ApplicationId, lower(@Name), @NotificationsEnabled)
                  on conflict (application_id, lower(name)) do update set notifications_enabled = excluded.notifications_enabled
                  returning id, application_id as ApplicationId, name, notifications_enabled as NotificationsEnabled",
                request, cancellationToken: ct));
            return Results.Ok(row);
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

        group.MapPost("/recipients", async (AddRecipientRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into notification_recipients(application_id, type, name, email, webhook_url, webhook_secret)
                  values(@ApplicationId, @Type, @Name, @Email, @WebhookUrl, @WebhookSecret)
                  returning id, application_id as ApplicationId, type, name, email, webhook_url as WebhookUrl, is_active as IsActive",
                request, cancellationToken: ct));
            return Results.Created($"/api/admin/recipients/{row.id}", row);
        });

        group.MapPost("/notification-rules", async (CreateNotificationRuleRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into notification_rules(application_id, environment, name, event_type, severity_minimum, threshold_count, threshold_window_minutes, cooldown_minutes, digest_interval_minutes)
                  values(@ApplicationId, lower(@Environment), @Name, @EventType, @SeverityMinimum, @ThresholdCount, @ThresholdWindowMinutes, @CooldownMinutes, @DigestIntervalMinutes)
                  returning id, application_id as ApplicationId, environment, name, event_type as EventType, is_active as IsActive",
                request, cancellationToken: ct));
            return Results.Created($"/api/admin/notification-rules/{row.id}", row);
        });

        return group;
    }
}
