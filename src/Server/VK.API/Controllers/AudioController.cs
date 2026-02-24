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
    private readonly IWebHostEnvironment _environment;

    public AudioController(
        VKStreetFoodDbContext context, 
        ILogger<AudioController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Get audio content for a POI in specific language
    /// </summary>
    [HttpGet("poi/{poiId}")]
    public async Task<ActionResult> GetAudioByPOI(int poiId, [FromQuery] string languageCode = "vi")
    {
        var audio = await _context.AudioContents
            .FirstOrDefaultAsync(a => 
                a.PointOfInterestId == poiId && 
                a.LanguageCode == languageCode &&
                !a.IsDeleted);

        // Fallback to Vietnamese if not found
        if (audio == null)
        {
            audio = await _context.AudioContents
                .FirstOrDefaultAsync(a => 
                    a.PointOfInterestId == poiId && 
                    a.LanguageCode == "vi" &&
                    !a.IsDeleted);
        }

        if (audio == null)
        {
            return NotFound(new { message = "Audio không tồn tại" });
        }

        return Ok(new
        {
            audioId = audio.Id,
            poiId = audio.PointOfInterestId,
            languageCode = audio.LanguageCode,
            audioFileUrl = audio.AudioFileUrl,
            textContent = audio.TextContent,
            durationInSeconds = audio.DurationInSeconds,
            isGenerated = audio.IsGenerated
        });
    }

    /// <summary>
    /// Stream audio file
    /// </summary>
    [HttpGet("stream/{audioId}")]
    public async Task<ActionResult> StreamAudio(int audioId)
    {
        var audio = await _context.AudioContents
            .FirstOrDefaultAsync(a => a.Id == audioId && !a.IsDeleted);

        if (audio == null)
        {
            return NotFound(new { message = "Audio không tồn tại" });
        }

        // For now, return the URL for client to stream
        // In production, you would implement actual file streaming from storage
        return Ok(new
        {
            streamUrl = audio.AudioFileUrl,
            contentType = "audio/mpeg",
            durationInSeconds = audio.DurationInSeconds
        });
    }

    /// <summary>
    /// Get all available languages for a POI
    /// </summary>
    [HttpGet("poi/{poiId}/languages")]
    public async Task<ActionResult> GetAvailableLanguages(int poiId)
    {
        var languages = await _context.AudioContents
            .Where(a => a.PointOfInterestId == poiId && !a.IsDeleted)
            .Select(a => new
            {
                languageCode = a.LanguageCode,
                languageName = GetLanguageName(a.LanguageCode),
                durationInSeconds = a.DurationInSeconds,
                isGenerated = a.IsGenerated
            })
            .ToListAsync();

        return Ok(languages);
    }

    /// <summary>
    /// Generate audio using TTS (placeholder for Google Cloud TTS integration)
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult> GenerateAudio([FromBody] GenerateAudioRequest request)
    {
        // TODO: Integrate with Google Cloud Text-to-Speech API
        // For now, return a placeholder response

        var audio = await _context.AudioContents
            .FirstOrDefaultAsync(a => 
                a.PointOfInterestId == request.PoiId && 
                a.LanguageCode == request.LanguageCode &&
                !a.IsDeleted);

        if (audio == null)
        {
            return NotFound(new { message = "Audio content không tồn tại" });
        }

        // Mark as generated (will be replaced with actual TTS generation)
        audio.IsGenerated = true;
        audio.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Audio generation queued (TTS integration pending)",
            audioId = audio.Id,
            audioUrl = audio.AudioFileUrl
        });
    }

    private string GetLanguageName(string languageCode)
    {
        return languageCode switch
        {
            "vi" => "Tiếng Việt",
            "en" => "English",
            "ko" => "한국어 (Korean)",
            _ => languageCode
        };
    }
}

public class GenerateAudioRequest
{
    public int PoiId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string? VoiceName { get; set; }
}
