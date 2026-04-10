using Microsoft.EntityFrameworkCore;
using VK.Core.Interfaces;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add DbContext
builder.Services.AddDbContext<VKStreetFoodDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPoiManagementRepository, PoiManagementRepository>();
builder.Services.AddScoped<ITourManagementRepository, TourManagementRepository>();

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

await SchemaBootstrapper.EnsureOwnerAuthSchemaAsync(app.Services, app.Logger);

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
