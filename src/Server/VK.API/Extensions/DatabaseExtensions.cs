using VK.Infrastructure.Data;
using VK.Infrastructure.Seeds;
using Microsoft.EntityFrameworkCore;

namespace VK.API.Extensions;

public static class DatabaseExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
            await DatabaseSeeder.InitializeAndSeedAsync(context);
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
            logger.LogWarning(ex, "Database seeding skipped (DB unreachable). Server will still start.");
        }
    }

    /// <summary>
    /// Fire-and-forget seeding — does NOT block startup.
    /// Runs after a short delay so the server is already listening.
    /// </summary>
    public static WebApplication SeedDatabaseInBackground(this WebApplication app)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2)); // let server start first
            try
            {
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
                await DatabaseSeeder.InitializeAndSeedAsync(context);
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
                logger.LogWarning(ex, "Background seeding skipped (DB unreachable).");
            }
        });
        return app;
    }
}