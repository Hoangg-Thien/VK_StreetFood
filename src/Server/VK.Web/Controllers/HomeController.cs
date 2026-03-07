using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Web.Models;

namespace VK.Web.Controllers;

public class HomeController : Controller
{
    private readonly VKStreetFoodDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<HomeController> _logger;

    public HomeController(VKStreetFoodDbContext context, IConfiguration config, ILogger<HomeController> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    // GET / → Login page (no-cache so back button doesn't show it after logout)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") == "true")
            return RedirectToAction("Index", "Dashboard");

        return View();
    }

    // POST /Home/Login → check against Supabase Users table
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        try
        {
            // Admin credentials from appsettings.json
            var adminEmail = _config["AdminAuth:Email"] ?? "admin@vkstreetfood.vn";
            var adminPassword = _config["AdminAuth:Password"] ?? "Admin@2026";

            if (email == adminEmail && password == adminPassword)
            {
                // Look up the user record in the DB (for display info)
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == adminEmail && u.Role == "Admin");

                HttpContext.Session.SetString("AdminLoggedIn", "true");
                HttpContext.Session.SetString("AdminUsername", user?.FullName ?? adminEmail.Split('@')[0]);
                HttpContext.Session.SetString("AdminEmail", adminEmail);

                TempData["InitAdminTab"] = "1";
                return RedirectToAction("Index", "Dashboard");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reach DB during login – falling back to config credentials.");

            // Fallback: allow login with pure config credentials even when DB is down
            var adminEmail = _config["AdminAuth:Email"] ?? "admin@vkstreetfood.vn";
            var adminPassword = _config["AdminAuth:Password"] ?? "Admin@2026";

            if (email == adminEmail && password == adminPassword)
            {
                HttpContext.Session.SetString("AdminLoggedIn", "true");
                HttpContext.Session.SetString("AdminUsername", "Admin");
                HttpContext.Session.SetString("AdminEmail", adminEmail);
                TempData["InitAdminTab"] = "1";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        ViewBag.Error = "Email hoặc mật khẩu không đúng.";
        return View("Index");
    }

    // GET /Home/Logout
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        // No-cache on the response so back button can't go back to admin
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return RedirectToAction("Index", "Home");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
