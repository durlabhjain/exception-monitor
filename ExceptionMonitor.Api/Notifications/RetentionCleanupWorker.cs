using Dapper;
using ExceptionMonitor.Api.Database;

namespace ExceptionMonitor.Api.Notifications;

public sealed class RetentionCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<RetentionCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                using var connection = await db.OpenConnectionAsync(stoppingToken);
                var deleted = await connection.ExecuteAsync(new CommandDefinition(
                    @"delete from exception_events e
                      using api_keys k
                      where e.api_key_id = k.id
                        and e.received_at < now() - make_interval(days => k.retention_days)",
                    cancellationToken: stoppingToken));
                if (deleted > 0) logger.LogInformation("Deleted {DeletedCount} expired exception events", deleted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Retention cleanup failed");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
