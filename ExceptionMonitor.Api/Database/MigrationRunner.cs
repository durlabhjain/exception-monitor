using System.Reflection;
using DbUp;

namespace ExceptionMonitor.Api.Database;

public static class MigrationRunner
{
    public static void Run(string connectionString, ILogger logger)
    {
        var result = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed");
            throw result.Error;
        }

        logger.LogInformation("Database migrations completed");
    }
}
