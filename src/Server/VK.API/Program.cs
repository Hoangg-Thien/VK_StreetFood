using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.API.Extensions;
using VK.Core.Interfaces;
using VK.Infrastructure.ExternalServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<VKStreetFoodDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register TTS Service
builder.Services.AddScoped<ITtsService, GoogleCloudTtsService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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

// Seed database on startup
await app.SeedDatabaseAsync();

app.Run();
