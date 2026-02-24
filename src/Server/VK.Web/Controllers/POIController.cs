using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Infrastructure.Data;

namespace VK.Web.Controllers;

public class POIController : Controller
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<POIController> _logger;

    public POIController(
        VKStreetFoodDbContext context,
        ILogger<POIController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: POI
    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var query = _context.PointsOfInterest
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var pois = await query
            .OrderBy(p => p.Name)
            .ToListAsync();

        // Get categories for filter dropdown
        ViewBag.Categories = await _context.Categories
            .Where(c => !c.IsDeleted)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;

        return View(pois);
    }

    // GET: POI/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var poi = await _context.PointsOfInterest
            .Include(p => p.Category)
            .Include(p => p.AudioContents)
            .Include(p => p.Vendors)
                .ThenInclude(v => v.Products)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poi == null)
        {
            return NotFound();
        }

        // Get visit statistics
        var visitCount = await _context.VisitLogs
            .Where(v => v.PointOfInterestId == id && !v.IsDeleted)
            .CountAsync();

        var ratings = await _context.Ratings
            .Where(r => r.PointOfInterestId == id && !r.IsDeleted)
            .ToListAsync();

        ViewBag.VisitCount = visitCount;
        ViewBag.Ratings = ratings;

        return View(poi);
    }

    // GET: POI/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(),
            "Id",
            "Name");

        return View();
    }

    // POST: POI/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description,CategoryId,Latitude,Longitude,Address,ImageUrl,QRCode")] PointOfInterest poi)
    {
        if (ModelState.IsValid)
        {
            poi.CreatedAt = DateTime.UtcNow;
            poi.IsDeleted = false;
            
            _context.Add(poi);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "POI created successfully!";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(),
            "Id",
            "Name",
            poi.CategoryId);

        return View(poi);
    }

    // GET: POI/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var poi = await _context.PointsOfInterest
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poi == null)
        {
            return NotFound();
        }

        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(),
            "Id",
            "Name",
            poi.CategoryId);

        return View(poi);
    }

    // POST: POI/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,CategoryId,Latitude,Longitude,Address,ImageUrl,QRCode,CreatedAt")] PointOfInterest poi)
    {
        if (id != poi.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                poi.UpdatedAt = DateTime.UtcNow;
                _context.Update(poi);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = "POI updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await POIExists(poi.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(),
            "Id",
            "Name",
            poi.CategoryId);

        return View(poi);
    }

    // GET: POI/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var poi = await _context.PointsOfInterest
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poi == null)
        {
            return NotFound();
        }

        return View(poi);
    }

    // POST: POI/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var poi = await _context.PointsOfInterest.FindAsync(id);
        
        if (poi != null)
        {
            // Soft delete
            poi.IsDeleted = true;
            poi.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "POI deleted successfully!";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> POIExists(int id)
    {
        return await _context.PointsOfInterest.AnyAsync(e => e.Id == id && !e.IsDeleted);
    }
}
