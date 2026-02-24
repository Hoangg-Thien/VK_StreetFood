using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VK.Web.Models;

namespace VK.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // Redirect to Dashboard instead of Home page
        return RedirectToAction("Index", "Dashboard");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
