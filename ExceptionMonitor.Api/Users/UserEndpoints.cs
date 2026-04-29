using Dapper;
using ExceptionMonitor.Api.Auth;
using ExceptionMonitor.Api.Database;

namespace ExceptionMonitor.Api.Users;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users").AddEndpointFilter<AdminApiKeyFilter>().WithTags("Users");

        group.MapGet("", async (IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            return Results.Ok(await connection.QueryAsync(new CommandDefinition("select id, email, display_name as DisplayName, is_active as IsActive from users order by email", cancellationToken: ct)));
        });

        group.MapPost("", async (CreateUserRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into users(email, display_name) values(lower(@Email), @DisplayName)
                  on conflict(email) do update set display_name = excluded.display_name, updated_at = now()
                  returning id, email, display_name as DisplayName, is_active as IsActive",
                request, cancellationToken: ct));
            return Results.Ok(row);
        });

        group.MapPost("/client-access", async (GrantClientAccessRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into user_client_access(user_id, client_id, role, all_applications) values(@UserId, @ClientId, @Role, @AllApplications)
                  on conflict(user_id, client_id) do update set role = excluded.role, all_applications = excluded.all_applications
                  returning id, user_id as UserId, client_id as ClientId, role, all_applications as AllApplications",
                request, cancellationToken: ct));
            return Results.Ok(row);
        });

        group.MapPost("/application-access", async (GrantApplicationAccessRequest request, IDbConnectionFactory db, CancellationToken ct) =>
        {
            using var connection = await db.OpenConnectionAsync(ct);
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                @"insert into user_application_access(user_id, application_id, role) values(@UserId, @ApplicationId, @Role)
                  on conflict(user_id, application_id) do update set role = excluded.role
                  returning id, user_id as UserId, application_id as ApplicationId, role",
                request, cancellationToken: ct));
            return Results.Ok(row);
        });

        return group;
    }
}
