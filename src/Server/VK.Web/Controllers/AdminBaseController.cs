using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VK.Web.Controllers;

/// <summary>
/// Base controller for all admin pages.
/// - Checks session authentication on every request.
/// - Adds no-cache headers to prevent back-button access after logout.
/// </summary>
public abstract class AdminBaseController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Prevent browser caching of admin pages
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        // Redirect to login if not authenticated as admin
        var isLoggedIn = HttpContext.Session.GetString("UserLoggedIn") == "true";
        var role = HttpContext.Session.GetString("UserRole");

        if (!isLoggedIn || !string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new RedirectToActionResult("Index", "Home", null);
            return;
        }

        base.OnActionExecuting(context);
    }
}
