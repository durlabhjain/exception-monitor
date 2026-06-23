using System.Net.Http.Headers;
using System.Text.Json;
using Dapper;
using ExceptionMonitor.Api.Database;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ExceptionMonitor.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Redirect browser to Google's OAuth consent screen
        group.MapGet("/google", (IOptions<GoogleAuthOptions> googleOpts, IMemoryCache cache) =>
        {
            var opts = googleOpts.Value;
            if (string.IsNullOrWhiteSpace(opts.ClientId))
                return Results.Problem("Google OAuth is not configured.", statusCode: 503);

            var state = Guid.NewGuid().ToString("N");
            cache.Set($"oauth_state_{state}", true, TimeSpan.FromMinutes(10));

            var url = "https://accounts.google.com/o/oauth2/v2/auth" +
                      $"?client_id={Uri.EscapeDataString(opts.ClientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(opts.RedirectUri)}" +
                      "&response_type=code" +
                      "&scope=openid%20email%20profile" +
                      $"&state={state}" +
                      "&access_type=online" +
                      "&prompt=select_account";

            return Results.Redirect(url);
        });

        // Google redirects here after consent
        group.MapGet("/callback", async (
            string? code, string? state, string? error,
            IOptions<GoogleAuthOptions> googleOpts,
            IOptions<SecurityOptions> securityOpts,
            IMemoryCache cache,
            IHttpClientFactory httpFactory,
            IJwtService jwt,
            IDbConnectionFactory db,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var frontendUrl = config["FrontendUrl"] ?? "http://localhost:5174";

            if (!string.IsNullOrWhiteSpace(error))
                return Results.Redirect($"{frontendUrl}/login?error={Uri.EscapeDataString(error)}");

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                return Results.Redirect($"{frontendUrl}/login?error=missing_params");

            if (!cache.TryGetValue($"oauth_state_{state}", out _))
                return Results.Redirect($"{frontendUrl}/login?error=invalid_state");

            cache.Remove($"oauth_state_{state}");

            var opts = googleOpts.Value;
            var client = httpFactory.CreateClient();

            // Exchange authorization code for tokens
            var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = opts.ClientId,
                    ["client_secret"] = opts.ClientSecret,
                    ["redirect_uri"] = opts.RedirectUri,
                    ["grant_type"] = "authorization_code"
                }), ct);

            if (!tokenResponse.IsSuccessStatusCode)
                return Results.Redirect($"{frontendUrl}/login?error=token_exchange_failed");

            var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var accessToken = tokenJson.GetProperty("access_token").GetString();

            // Get user info from Google
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var userInfoResponse = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo", ct);

            if (!userInfoResponse.IsSuccessStatusCode)
                return Results.Redirect($"{frontendUrl}/login?error=userinfo_failed");

            var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var googleId = userInfo.GetProperty("id").GetString()!;
            var email = userInfo.GetProperty("email").GetString()!;
            var name = userInfo.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

            // Upsert user and identity
            using var connection = await db.OpenConnectionAsync(ct);

            var userId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                @"insert into users(email, display_name)
                  values(lower(@Email), @Name)
                  on conflict(email) do update set display_name = coalesce(excluded.display_name, users.display_name), updated_at = now()
                  returning id",
                new { Email = email, Name = name }, cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition(
                @"insert into user_identities(user_id, provider, provider_subject, email, email_verified, last_login_at)
                  values(@UserId, 'google', @GoogleId, @Email, true, now())
                  on conflict(provider, provider_subject) do update set last_login_at = now()",
                new { UserId = userId, GoogleId = googleId, Email = email }, cancellationToken: ct));

            // Determine role — first user ever becomes SystemAdmin
            var role = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "select role from user_client_access where user_id = @UserId limit 1",
                new { UserId = userId }, cancellationToken: ct)) ?? "Viewer";

            var userCount = await connection.ExecuteScalarAsync<int>("select count(*) from users");
            if (userCount == 1) role = "SystemAdmin";

            var token = jwt.Generate(userId, email, name, role);
            return Results.Redirect($"{frontendUrl}/auth/callback?token={Uri.EscapeDataString(token)}");
        });

        // Return current user info from JWT
        group.MapGet("/me", (HttpContext ctx, IJwtService jwt) =>
        {
            var bearer = ctx.Request.Headers.Authorization.ToString();
            if (!bearer.StartsWith("Bearer ")) return Results.Unauthorized();
            var principal = jwt.Validate(bearer[7..]);
            if (principal is null) return Results.Unauthorized();

            return Results.Ok(new
            {
                id = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
                email = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value,
                displayName = principal.FindFirst("displayName")?.Value,
                role = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
            });
        });
    }
}
