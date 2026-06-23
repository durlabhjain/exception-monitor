using System.Threading.RateLimiting;
using ExceptionMonitor.Api.Applications;
using ExceptionMonitor.Api.Auth;
using ExceptionMonitor.Api.Database;
using ExceptionMonitor.Api.Ingestion;
using ExceptionMonitor.Api.Notifications;
using ExceptionMonitor.Api.Users;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<IngestionOptions>(builder.Configuration.GetSection("Ingestion"));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection("Google"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddSingleton(NpgsqlDataSource.Create(postgresConnectionString));
builder.Services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddSingleton<IApiKeyHasher, HmacApiKeyHasher>();
builder.Services.AddSingleton<IApiKeyGenerator, ApiKeyGenerator>();
builder.Services.AddScoped<IApiKeyAuthenticator, ApiKeyAuthenticator>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddScoped<IIngestionNormalizer, IngestionNormalizer>();
builder.Services.AddScoped<IIngestionRepository, IngestionRepository>();
builder.Services.AddSingleton<IFingerprintService, FingerprintService>();
builder.Services.AddScoped<AdminApiKeyFilter>();

builder.Services.AddHostedService<NotificationDeliveryWorker>();
builder.Services.AddHostedService<RetentionCleanupWorker>();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();


builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:5174", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Request.Headers.TryGetValue("X-Exception-Api-Key", out var apiKey)
            ? apiKey.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 2000,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ExceptionMonitor.Api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    MigrationRunner.Run(postgresConnectionString, logger);
}

app.UseSerilogRequestLogging();
app.UseCors("FrontendDev");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { Service = "ExceptionMonitor.Api", Status = "Running" })).ExcludeFromDescription();

app.MapAuthEndpoints();
app.MapIngestionEndpoints();
app.MapApplicationEndpoints();
app.MapEventQueryEndpoints();
app.MapUserEndpoints();

app.Run();
