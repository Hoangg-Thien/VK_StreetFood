using System.Linq;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VK.API.Auth;
using VK.Infrastructure.Data;

namespace VK.API.Tests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Fixed 64-char test secret — must match what Program.cs uses for token validation in Testing env
    internal const string TestJwtKey = "test-secret-key-for-integration-tests-that-is-long-enough-for-hmac";
    internal const string TestJwtIssuer = "VKStreetFoodAPI";
    internal const string TestJwtAudience = "VKStreetFoodClients";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Inject JWT config early via UseSetting so Program.cs picks up the same key for token validation
        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Jwt:ExpiryDays", "365");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<VKStreetFoodDbContext>();
            services.RemoveAll(typeof(DbContextOptions<VKStreetFoodDbContext>));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<VKStreetFoodDbContext>));

            services.AddDbContext<VKStreetFoodDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task ExecuteDbContextAsync(Func<VKStreetFoodDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
        await action(db);
    }

    /// <summary>
    /// Creates an HttpClient with a valid tourist JWT pre-attached for <paramref name="touristId"/>.
    /// </summary>
    public HttpClient CreateAuthenticatedTouristClient(int touristId)
    {
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateTouristToken(touristId);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates an HttpClient with a valid admin JWT pre-attached.
    /// </summary>
    public HttpClient CreateAuthenticatedAdminClient(int userId = 1, string email = "admin@vkstreetfood.local")
    {
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateAdminToken(userId, email);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
