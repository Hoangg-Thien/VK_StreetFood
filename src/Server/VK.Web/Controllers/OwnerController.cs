using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;

namespace VK.Web.Controllers;

public class OwnerController : OwnerBaseController
{
    private readonly VKStreetFoodDbContext _context;

    public OwnerController(VKStreetFoodDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var vendorId = HttpContext.Session.GetInt32("VendorId");
        if (!vendorId.HasValue)
            return RedirectToAction("Index", "Home");

        var vendor = await _context.Vendors
            .Include(v => v.PointOfInterest)
            .FirstOrDefaultAsync(v => v.Id == vendorId.Value && !v.IsDeleted);

        if (vendor == null)
            return RedirectToAction("Index", "Home");

        ViewBag.Vendor = vendor;
        ViewBag.Poi = vendor.PointOfInterest;
        return View("OwnerPage");
    }
}
