using VK.Infrastructure.Data;
using VK.Infrastructure.Seeds;

namespace VK.API.Extensions;

public static class DatabaseExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
        await DatabaseSeeder.SeedAsync(context);
    }
}