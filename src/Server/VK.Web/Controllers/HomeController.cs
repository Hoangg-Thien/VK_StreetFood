using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VK.Web.Models;

namespace VK.Web.Controllers;

public class HomeController : Controller
{
    // GET / → Login page
    public IActionResult Index()
    {
        // Already logged in → go to dashboard
        if (HttpContext.Session.GetString("AdminLoggedIn") == "true")
            return RedirectToAction("Index", "Dashboard");

        return View();
    }

    // POST /Home/Login → validate credentials
    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        // Simple hardcoded credentials — replace with DB auth later
        if (username == "admin" && password == "admin123")
        {
            HttpContext.Session.SetString("AdminLoggedIn", "true");
            HttpContext.Session.SetString("AdminUsername", username);
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không đúng.";
        return View("Index");
    }

    // GET /Home/Logout
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
