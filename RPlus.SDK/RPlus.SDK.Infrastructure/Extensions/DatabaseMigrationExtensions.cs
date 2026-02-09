using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RPlus.SDK.Infrastructure.Extensions;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app, bool throwOnFailure, params Type[] contextTypes)
    {
        if (contextTypes == null || contextTypes.Length == 0)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigrations");

        foreach (var contextType in contextTypes)
        {
            try
            {
                if (scope.ServiceProvider.GetService(contextType) is DbContext context)
                {
                    await context.Database.MigrateAsync();
                }
                else
                {
                    logger.LogWarning("Skipping migration for {ContextType} because it is not registered in DI.", contextType.FullName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed for {ContextType}", contextType.Name);
                if (throwOnFailure)
                {
                    throw;
                }
            }
        }
    }
}
