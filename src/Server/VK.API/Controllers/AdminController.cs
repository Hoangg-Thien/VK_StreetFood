using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(VKStreetFoodDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get audio content status and statistics
    /// </summary>
    [HttpGet("audio-status")]
    public async Task<ActionResult> GetAudioStatus()
    {
        var totalAudioContents = await _context.AudioContents
            .Where(a => !a.IsDeleted)
            .CountAsync();

        var byLanguage = await _context.AudioContents
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.LanguageCode)
            .Select(g => new
            {
                languageCode = g.Key,
                count = g.Count()
            })
            .ToListAsync();

        var totalPOIs = await _context.PointsOfInterest
            .Where(p => !p.IsDeleted)
            .CountAsync();

        return Ok(new
        {
            totalPOIs,
            totalAudioContents,
            byLanguage,
            expectedTotal = totalPOIs * 3
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult> Health()
    {
        try
        {
            var poiCount = await _context.PointsOfInterest.CountAsync();
            return Ok(new { status = "healthy", poiCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(500, new { status = "unhealthy", error = ex.Message });
        }
    }
}
