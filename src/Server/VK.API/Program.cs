using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.API.Extensions;
using VK.API.Services;

// Force IPv4 so DNS doesn't resolve Supabase to IPv6 (unreachable on dev machines)
AppContext.SetSwitch("System.Net.preferIPv4Stack", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<VKStreetFoodDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// TTS generation service (Edge TTS — Microsoft Edge Read Aloud, miễn phí, không cần API key)
builder.Services.AddScoped<ITtsGenerationService, TtsGenerationService>();

// AudioTaskManager: singleton — deduplicates concurrent on-demand TTS requests
builder.Services.AddSingleton<IAudioTaskManager, AudioTaskManager>();

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

// Serve static files (audio files)
app.UseStaticFiles();

app.UseHttpsRedirection();
app.MapControllers();

// Seed database in background — don't block startup
app.SeedDatabaseInBackground();

app.Run();
