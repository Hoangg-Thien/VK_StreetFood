using VK.Infrastructure.Data;
using VK.Infrastructure.Seeds;

namespace VK.API.Extensions;

public static class DatabaseExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
            await DatabaseSeeder.SeedAsync(context);
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
            logger.LogWarning(ex, "Database seeding skipped (DB unreachable). Server will still start.");
        }
    }
}