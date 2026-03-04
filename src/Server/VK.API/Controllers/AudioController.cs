using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AudioController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<AudioController> _logger;

    public AudioController(VKStreetFoodDbContext context, ILogger<AudioController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lay noi dung van ban de MAUI TTS doc. Fallback ve tieng Viet neu chua co ban dich.
    /// </summary>
    [HttpGet("poi/{poiId}")]
    public async Task<ActionResult> GetAudioByPOI(int poiId, [FromQuery] string languageCode = "vi")
    {
        var audio = await _context.AudioContents
            .FirstOrDefaultAsync(a =>
                a.PointOfInterestId == poiId &&
                a.LanguageCode == languageCode &&
                !a.IsDeleted);

        if (audio == null)
        {
            audio = await _context.AudioContents
                .FirstOrDefaultAsync(a =>
                    a.PointOfInterestId == poiId &&
                    a.LanguageCode == "vi" &&
                    !a.IsDeleted);
        }

        if (audio == null)
            return NotFound(new { message = "Khong co noi dung thuyet minh cho POI nay" });

        _logger.LogInformation("TTS text served for POI {Id}, lang={Lang}", poiId, audio.LanguageCode);

        return Ok(new
        {
            audioId      = audio.Id,
            poiId        = audio.PointOfInterestId,
            languageCode = audio.LanguageCode,
            textContent  = audio.TextContent
        });
    }

    /// <summary>Danh sach ngon ngu co san cho POI.</summary>
    [HttpGet("poi/{poiId}/languages")]
    public async Task<ActionResult> GetAvailableLanguages(int poiId)
    {
        var languages = await _context.AudioContents
            .Where(a => a.PointOfInterestId == poiId && !a.IsDeleted)
            .Select(a => new
            {
                languageCode = a.LanguageCode,
                languageName = a.LanguageCode == "vi" ? "Tieng Viet"
                             : a.LanguageCode == "en" ? "English"
                             : a.LanguageCode == "ko" ? "Korean"
                             : a.LanguageCode
            })
            .ToListAsync();

        return Ok(languages);
    }
}
