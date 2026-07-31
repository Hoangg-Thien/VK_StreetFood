using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using VK.Core.Interfaces;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.API.Extensions;
using VK.API.Services;
using VK.API.Services.AppServices;
using VK.API.Auth;
using VK.API.Middlewares;

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
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    // Render/containers use dynamic proxy IPs; trust forwarded headers from known platform proxy chain.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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
builder.Services.AddScoped<IPaymentAppService, PaymentAppService>();

// Auth services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// ── JWT Bearer Authentication ────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"];
if (!builder.Environment.IsEnvironment("Testing") && string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key must be set via the Jwt__Key environment variable.");

var keyBytes = Encoding.UTF8.GetBytes(
    string.IsNullOrWhiteSpace(jwtKey) ? new string('x', 64) : jwtKey); // testing fallback

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

// ── Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "VK Street Food API",
        Version = "v1",
        Description = "API for Vietnamese Food Street Tour - Multilingual Audio Guide System. " +
                      "Authenticate via POST /api/Tourist/register (tourists) or POST /api/Auth/login (admins). " +
                      "Then pass the returned token as: Authorization: Bearer {token}"
    });
});


var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseForwardedHeaders();

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

var enableHttpsRedirection = builder.Configuration.GetValue("EnableHttpsRedirection", !app.Environment.IsProduction());
if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

// Dedicated health endpoint for container/platform probes.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// ── Auth middleware (must come before MapControllers) ────────────────────────
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed database in background — don't block startup
if (!app.Environment.IsEnvironment("Testing"))
{
    app.SeedDatabaseInBackground();
}

app.Run();

public partial class Program;

