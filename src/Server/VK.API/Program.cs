using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using VK.Core.Interfaces;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.API.Extensions;
using VK.API.Services;
using VK.API.Services.AppServices;

// Force IPv4 so DNS doesn't resolve Supabase to IPv6 (unreachable on dev machines)
AppContext.SetSwitch("System.Net.preferIPv4Stack", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<VKStreetFoodDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<AudioStorageOptions>(options =>
{
    options.RootPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "audio");
});

// TTS generation service (Edge TTS — Microsoft Edge Read Aloud, miễn phí, không cần API key)
builder.Services.AddScoped<ITtsGenerationService, TtsGenerationService>();

// AudioTaskManager: singleton — deduplicates concurrent on-demand TTS requests
builder.Services.AddSingleton<IAudioTaskManager, AudioTaskManager>();

// Application services: move business logic out of controllers
builder.Services.AddScoped<IPOIAppService, POIAppService>();
builder.Services.AddScoped<ITourAppService, TourAppService>();
builder.Services.AddScoped<ITouristAppService, TouristAppService>();
builder.Services.AddScoped<IAnalyticsAppService, AnalyticsAppService>();

// Add Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "VK Street Food API",
        Version = "v1",
        Description = "API for Vietnamese Food Street Tour - Multilingual Audio Guide System"
    });
});

var app = builder.Build();

var audioRootPath = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<AudioStorageOptions>>()
    .Value.RootPath;
Directory.CreateDirectory(audioRootPath);

await app.EnsureOwnerAuthSchemaAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VK Street Food API v1");
        c.RoutePrefix = "swagger"; // Swagger UI at /swagger
    });
}

// Serve runtime-generated audio files from a writable folder outside wwwroot.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(audioRootPath),
    RequestPath = "/audio"
});

// Serve static files from wwwroot.
app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

// Seed database in background — don't block startup
if (!app.Environment.IsEnvironment("Testing"))
{
    app.SeedDatabaseInBackground();
}

app.Run();

public partial class Program;
