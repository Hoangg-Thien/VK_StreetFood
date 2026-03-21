using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
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

// Add HttpClient to call API
builder.Services.AddHttpClient("VKAPI", client =>
{
    client.BaseAddress = new Uri("http://localhost:5089/api/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

await SchemaBootstrapper.EnsureOwnerAuthSchemaAsync(app.Services, app.Logger);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
