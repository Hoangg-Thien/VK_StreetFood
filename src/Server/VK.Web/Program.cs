using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using VK.Core.Interfaces;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Infrastructure.Seeds;
using VK.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure Data Protection to use a stable application name.
// On Render free tier the container restarts periodically; a new key is generated each time,
// which invalidates old session cookies. We set the application name so at least the
// error is handled gracefully (users are redirected to login cleanly).
var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("VKStreetFood-Web");

// If a persistent key directory is configured (e.g. a mounted volume on paid plans), use it.
var keyStorePath = builder.Configuration["DataProtection:KeyStorePath"];
if (!string.IsNullOrWhiteSpace(keyStorePath))
{
    Directory.CreateDirectory(keyStorePath);
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyStorePath));
}

// Add Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".VKStreetFood.Session";
});

// Add DbContext
builder.Services.AddDbContext<VKStreetFoodDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPoiManagementRepository, PoiManagementRepository>();
builder.Services.AddScoped<ITourManagementRepository, TourManagementRepository>();
builder.Services.Configure<SupabaseStorageOptions>(builder.Configuration.GetSection("SupabaseStorage"));
builder.Services.AddHttpClient<IPoiImageStorageService, SupabasePoiImageStorageService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add HttpClient to call API
builder.Services.AddHttpClient("VKAPI", client =>
{
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5089/api/";
    apiBaseUrl = NormalizeApiBaseUrl(apiBaseUrl);
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ITextTranslationService, GoogleTextTranslationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

var app = builder.Build();

app.UseForwardedHeaders();



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var enableHttpsRedirection = builder.Configuration.GetValue("EnableHttpsRedirection", !app.Environment.IsProduction());
if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

// Serve runtime-uploaded files under wwwroot (e.g. /images/poi/*).
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

if (!app.Environment.IsEnvironment("Testing"))
{
    _ = Task.Run(async () =>
    {
        // Web does NOT run MigrateAsync — schema migrations are owned exclusively by the API service.
        // We wait longer to give the API service time to complete its migrations first.
        await Task.Delay(TimeSpan.FromSeconds(15));
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
            await DatabaseSeeder.SeedAsync(context);
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Background database seeding skipped (DB unreachable or schema not ready).");
        }
    });
}

app.Run();

static string NormalizeApiBaseUrl(string value)
{
    var normalized = value.Trim().TrimEnd('/');
    if (!normalized.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
    {
        normalized += "/api";
    }

    return normalized + "/";
}
