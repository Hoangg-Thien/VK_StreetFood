using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VK.Web.Controllers;

public abstract class OwnerBaseController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        var isLoggedIn = HttpContext.Session.GetString("UserLoggedIn") == "true";
        var role = HttpContext.Session.GetString("UserRole");

        if (!isLoggedIn || !string.Equals(role, "poi_owner", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new RedirectToActionResult("Index", "Home", null);
            return;
        }

        base.OnActionExecuting(context);
    }
}
